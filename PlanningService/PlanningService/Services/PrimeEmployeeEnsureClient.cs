using System.Net.Http.Headers;
using Kyntus.Messaging.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PlanningService.Data;
using PlanningService.Models;

namespace PlanningService.Services;

public interface IPrimeEmployeeEnsureClient
{
    Task TryEnsureFromPlanningAsync(User user, CancellationToken ct = default);
}

public sealed class PrimeEmployeeEnsureClient(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    AppDbContext context,
    IConfiguration configuration,
    ILogger<PrimeEmployeeEnsureClient> logger) : IPrimeEmployeeEnsureClient
{
    public async Task TryEnsureFromPlanningAsync(User user, CancellationToken ct = default)
    {
        var baseUrl = configuration["Prime:BaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = "http://kyntus_prime_backend:8080/";

        if (!baseUrl.EndsWith('/'))
            baseUrl += "/";

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
            primeServiceId,
        };

        try
        {
            var client = httpClientFactory.CreateClient("PrimeOrgSync");
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(5);

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/prime/org/employees/ensure-from-planning")
            {
                Content = JsonContent.Create(payload),
            };

            var authHeader = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(authHeader))
                request.Headers.Authorization = AuthenticationHeaderValue.Parse(authHeader);

            var response = await client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Prime ensure-from-planning OK pour {Email} guid={Guid}",
                    user.Email,
                    user.Guid);
                return;
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning(
                "Prime ensure-from-planning échec {Status} pour {Email}: {Body}",
                response.StatusCode,
                user.Email,
                body);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Prime ensure-from-planning indisponible pour {Email}", user.Email);
        }
    }
}
