using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Kyntus.Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using PlanningService.Data;
using PlanningService.Models;

namespace PlanningService.Services;

public sealed record DirectoryEmployeeCreateResult(Guid EmployeeId, bool Success);

public interface IDirectoryEmployeeWriteClient
{
    Task<DirectoryEmployeeCreateResult> TryCreateEmployeeAsync(
        string firstName,
        string lastName,
        string email,
        string role,
        string? primeServiceId,
        DateTime hireDate,
        CancellationToken ct = default);

    Task<bool> TryUpdateEmployeeAsync(User user, CancellationToken ct = default);
    Task<bool> TryLinkAuthSubjectAsync(Guid employeeId, Guid authSubjectId, CancellationToken ct = default);
    Task<bool> TryDeleteEmployeeAsync(Guid employeeGuid, CancellationToken ct = default);
}

public sealed class DirectoryEmployeeWriteClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    AppDbContext context,
    IConfiguration configuration,
    ILogger<DirectoryEmployeeWriteClient> logger) : IDirectoryEmployeeWriteClient
{
    private const int MaxRetries = 3;

    public Task<DirectoryEmployeeCreateResult> TryCreateEmployeeAsync(
        string firstName,
        string lastName,
        string email,
        string role,
        string? primeServiceId,
        DateTime hireDate,
        CancellationToken ct = default) =>
        ExecuteWithRetriesAsync<DirectoryEmployeeCreateResult>(
            async () =>
            {
                var payload = new
                {
                    employeeId = (Guid?)null,
                    firstName,
                    lastName,
                    email,
                    role,
                    serviceId = primeServiceId,
                    parentId = (Guid?)null,
                    hireDate,
                };

                var client = CreateClient();
                using var request = new HttpRequestMessage(HttpMethod.Post, "api/directory/employees")
                {
                    Content = JsonContent.Create(payload),
                };
                AttachAuth(request);

                var response = await client.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    logger.LogWarning("Directory create échec {Status} pour {Email}: {Body}", response.StatusCode, email, body);
                    return new DirectoryEmployeeCreateResult(Guid.Empty, false);
                }

                var created = await response.Content.ReadFromJsonAsync<DirectoryEmployeeJson>(cancellationToken: ct);
                if (created is null || !Guid.TryParse(created.Id, out var employeeId))
                    return new DirectoryEmployeeCreateResult(Guid.Empty, false);

                logger.LogInformation("Directory create OK pour {Email} guid={Guid}", email, employeeId);
                return new DirectoryEmployeeCreateResult(employeeId, true);
            },
            $"create {email}",
            new DirectoryEmployeeCreateResult(Guid.Empty, false),
            ct);

    public Task<bool> TryUpdateEmployeeAsync(User user, CancellationToken ct = default) =>
        ExecuteWithRetriesAsync(
            async () =>
            {
                string? primeServiceId = null;
                if (user.SubServiceId.HasValue)
                {
                    primeServiceId = await context.SubServices.AsNoTracking()
                        .Where(ss => ss.Id == user.SubServiceId.Value)
                        .Select(ss => ss.PrimeServiceId)
                        .FirstOrDefaultAsync(ct);
                }

                var payload = new
                {
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    role = user.Role?.Name ?? KyntusRoleNames.Employee,
                    serviceId = primeServiceId,
                    parentId = (Guid?)null,
                    isActive = user.IsActive,
                    hireDate = user.HireDate,
                };

                var client = CreateClient();
                using var request = new HttpRequestMessage(HttpMethod.Put, $"api/directory/employees/{user.Guid:D}")
                {
                    Content = JsonContent.Create(payload),
                };
                AttachAuth(request);

                var response = await client.SendAsync(request, ct);
                if (response.IsSuccessStatusCode)
                    return true;

                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Directory update échec {Status} pour {Email}: {Body}", response.StatusCode, user.Email, body);
                return false;
            },
            $"update {user.Email}",
            false,
            ct);

    public Task<bool> TryLinkAuthSubjectAsync(Guid employeeId, Guid authSubjectId, CancellationToken ct = default)
    {
        if (employeeId == Guid.Empty || authSubjectId == Guid.Empty)
            return Task.FromResult(true);

        return ExecuteWithRetriesAsync(
            async () =>
            {
                var client = CreateClient();
                using var request = new HttpRequestMessage(HttpMethod.Patch, $"api/directory/employees/{employeeId:D}/auth-subject")
                {
                    Content = JsonContent.Create(new { authSubjectId }),
                };
                AttachAuth(request);

                var response = await client.SendAsync(request, ct);
                return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound;
            },
            $"link-auth guid={employeeId}",
            false,
            ct);
    }

    public Task<bool> TryDeleteEmployeeAsync(Guid employeeGuid, CancellationToken ct = default)
    {
        if (employeeGuid == Guid.Empty) return Task.FromResult(true);
        return ExecuteWithRetriesAsync(
            async () =>
            {
                var client = CreateClient();
                using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/directory/employees/{employeeGuid:D}");
                AttachAuth(request);
                var response = await client.SendAsync(request, ct);
                return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound;
            },
            $"delete guid={employeeGuid}",
            false,
            ct);
    }

    private async Task<T> ExecuteWithRetriesAsync<T>(
        Func<Task<T>> action,
        string label,
        T failureValue,
        CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var result = await action();
                if (result is bool b && !b)
                {
                    // continue retry
                }
                else if (result is DirectoryEmployeeCreateResult r && !r.Success)
                {
                    // continue retry
                }
                else
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Directory {Label} tentative {Attempt}/{Max}", label, attempt, MaxRetries);
            }

            if (attempt < MaxRetries)
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
        }

        logger.LogError("Directory {Label} échoué après {Max} tentatives", label, MaxRetries);
        return failureValue;
    }

    private HttpClient CreateClient()
    {
        var baseUrl = configuration["Directory:BaseUrl"]?.Trim()
            ?? "http://employee-directory-backend:8080/";
        if (!baseUrl.EndsWith('/')) baseUrl += "/";

        var client = httpClientFactory.CreateClient("DirectorySync");
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(10);
        return client;
    }

    private void AttachAuth(HttpRequestMessage request)
    {
        var authHeader = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authHeader))
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);
    }

    private sealed class DirectoryEmployeeJson
    {
        public string Id { get; set; } = "";
    }
}
