using System.Net.Http.Headers;
using Kyntus.Messaging.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PlanningService.Data;
using PlanningService.Models;

namespace PlanningService.Services;

public interface IDirectoryEmployeeEnsureClient
{
    Task<bool> TryEnsureFromPlanningAsync(User user, CancellationToken ct = default);
    Task<bool> TryDeleteFromPlanningAsync(Guid employeeGuid, CancellationToken ct = default);
}

public sealed class DirectoryEmployeeEnsureClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    AppDbContext context,
    IConfiguration configuration,
    ILogger<DirectoryEmployeeEnsureClient> logger) : IDirectoryEmployeeEnsureClient
{
    private const int MaxRetries = 3;

    public Task<bool> TryEnsureFromPlanningAsync(User user, CancellationToken ct = default) =>
        ExecuteWithRetriesAsync(
            () => SendEnsureAsync(user, ct),
            $"ensure {user.Email}",
            ct);

    public Task<bool> TryDeleteFromPlanningAsync(Guid employeeGuid, CancellationToken ct = default)
    {
        if (employeeGuid == Guid.Empty) return Task.FromResult(true);
        return ExecuteWithRetriesAsync(
            () => SendDeleteAsync(employeeGuid, ct),
            $"delete guid={employeeGuid}",
            ct);
    }

    private async Task<bool> ExecuteWithRetriesAsync(
        Func<Task<bool>> action,
        string label,
        CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                if (await action())
                    return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Directory {Label} tentative {Attempt}/{Max}", label, attempt, MaxRetries);
            }

            if (attempt < MaxRetries)
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
        }

        logger.LogError("Directory {Label} échoué après {Max} tentatives", label, MaxRetries);
        return false;
    }

    private async Task<bool> SendEnsureAsync(User user, CancellationToken ct)
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
            employeeId = user.Guid,
            firstName = user.FirstName,
            lastName = user.LastName,
            email = user.Email,
            role = user.Role?.Name ?? KyntusRoleNames.Employee,
            serviceId = primeServiceId,
        };

        var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/directory/employees")
        {
            Content = JsonContent.Create(payload),
        };
        AttachAuth(request);

        var response = await client.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Directory ensure OK pour {Email} guid={Guid}", user.Email, user.Guid);
            return true;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        logger.LogWarning("Directory ensure échec {Status} pour {Email}: {Body}", response.StatusCode, user.Email, body);
        return false;
    }

    private async Task<bool> SendDeleteAsync(Guid employeeGuid, CancellationToken ct)
    {
        var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/directory/employees/{employeeGuid:D}");
        AttachAuth(request);

        var response = await client.SendAsync(request, ct);
        if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            logger.LogInformation("Directory delete OK pour guid={Guid}", employeeGuid);
            return true;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        logger.LogWarning("Directory delete échec {Status} guid={Guid}: {Body}", response.StatusCode, employeeGuid, body);
        return false;
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
}
