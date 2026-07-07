using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Kyntus.Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure.Persistence;
using Planning.Domain.Entities;

namespace Planning.Infrastructure.Services;

public sealed record DirectoryEmployeeCreateResult(
    Guid EmployeeId,
    bool Success,
    string? ErrorMessage = null,
    bool Retryable = true);

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

    Task<bool> IsEmailUsedInDirectoryAsync(string email, Guid? excludeEmployeeId = null, CancellationToken ct = default);

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
                    var errorMessage = TryParseDirectoryError(body);
                    var retryable = IsRetryableStatus(response.StatusCode);
                    logger.LogWarning(
                        "Directory create échec {Status} pour {Email}: {Body}",
                        response.StatusCode,
                        email,
                        string.IsNullOrWhiteSpace(errorMessage) ? body : errorMessage);
                    return new DirectoryEmployeeCreateResult(Guid.Empty, false, errorMessage, retryable);
                }

                var created = await response.Content.ReadFromJsonAsync<DirectoryEmployeeJson>(cancellationToken: ct);
                if (created is null || !Guid.TryParse(created.Id, out var employeeId))
                    return new DirectoryEmployeeCreateResult(Guid.Empty, false, "Réponse Directory invalide.", Retryable: false);

                logger.LogInformation("Directory create OK pour {Email} guid={Guid}", email, employeeId);
                return new DirectoryEmployeeCreateResult(employeeId, true);
            },
            $"create {email}",
            new DirectoryEmployeeCreateResult(Guid.Empty, false),
            ct);

    public Task<bool> IsEmailUsedInDirectoryAsync(string email, Guid? excludeEmployeeId = null, CancellationToken ct = default) =>
        ExecuteWithRetriesAsync(
            async () =>
            {
                if (string.IsNullOrWhiteSpace(email)) return false;

                var client = CreateClient();
                var query = $"api/directory/employees/check-email?email={Uri.EscapeDataString(email.Trim())}";
                if (excludeEmployeeId.HasValue && excludeEmployeeId.Value != Guid.Empty)
                    query += $"&excludeId={excludeEmployeeId.Value:D}";

                using var request = new HttpRequestMessage(HttpMethod.Get, query);
                AttachAuth(request);

                var response = await client.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                    return false;

                var payload = await response.Content.ReadFromJsonAsync<DirectoryEmailCheckJson>(cancellationToken: ct);
                return payload is not null && !payload.IsUnique;
            },
            $"check-email {email}",
            false,
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

                var hr = await context.UserHrProfiles.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);

                var payload = new
                {
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    email = user.Email,
                    role = user.Role?.Name ?? KyntusRoleNames.Employee,
                    serviceId = primeServiceId,
                    parentId = hr?.SuperviseurId,
                    isActive = user.IsActive,
                    hireDate = user.HireDate,
                    chefDeProjetId = hr?.ChefDeProjetId,
                    superviseurId = hr?.SuperviseurId,
                    referentTechniqueId = hr?.ReferentTechniqueId,
                    hrProfile = hr is null ? null : new
                    {
                        hr.DateNaissance,
                        hr.VilleNaissance,
                        hr.Nationalite,
                        hr.Sexe,
                        hr.SituationFamiliale,
                        hr.NombreEnfants,
                        hr.Cin,
                        hr.Adresse,
                        hr.Telephone1,
                        hr.TelephoneUrgence,
                        hr.RelationUrgence,
                        hr.Rib,
                        hr.ImmatriculationInterne,
                        hr.ImmatriculationCnss,
                        hr.DateEntree,
                        hr.DateEmbauche,
                        hr.DateAnciennete,
                        hr.DateSortie,
                        hr.DateEvolutionPoste,
                        hr.AncienPoste,
                        hr.AncienService,
                        hr.NiveauScolaire,
                        hr.IntitulesEtudes,
                        hr.EnFormation,
                        hr.DateDebutFormation,
                        hr.DateFinFormationPrevue,
                        niveauExpertiseMetier = hr.NiveauExpertiseMetier,
                    },
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
                    if (!r.Retryable)
                        return result;
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
        client.Timeout = TimeSpan.FromSeconds(30);
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

    private sealed class DirectoryEmailCheckJson
    {
        public bool IsUnique { get; set; }
    }

    private sealed class DirectoryErrorJson
    {
        public string? Error { get; set; }
        public string? Message { get; set; }
    }

    private static string? TryParseDirectoryError(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<DirectoryErrorJson>(body);
            return parsed?.Error ?? parsed?.Message;
        }
        catch
        {
            return body.Length <= 500 ? body : body[..500];
        }
    }

    private static bool IsRetryableStatus(System.Net.HttpStatusCode statusCode) =>
        statusCode is not (
            System.Net.HttpStatusCode.BadRequest
            or System.Net.HttpStatusCode.Conflict
            or System.Net.HttpStatusCode.NotFound
            or System.Net.HttpStatusCode.UnprocessableEntity);
}
