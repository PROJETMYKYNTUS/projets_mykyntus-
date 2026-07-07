using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Parrainage.Application.Abstractions;

namespace Parrainage.Infrastructure.Services;

public sealed class PlanningEmploymentCheckClient(
    HttpClient http,
    IConfiguration configuration,
    ILogger<PlanningEmploymentCheckClient> logger) : IPlanningEmploymentCheckClient
{
    public async Task<PlanningEmploymentSummary?> GetEmploymentSummaryAsync(
        string candidateEmployeeId,
        CancellationToken ct = default)
    {
        var guid = candidateEmployeeId.Trim();
        if (string.IsNullOrEmpty(guid) || !Guid.TryParse(guid, out var employeeGuid))
            return null;

        var baseUrl = configuration["Planning:BaseUrl"] ?? "http://planning-backend:8080/";
        var url = $"{baseUrl.TrimEnd('/')}/api/contract/employee/{employeeGuid}/employment-summary";

        try
        {
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Planning employment summary {Status} for {EmployeeGuid}",
                    (int)response.StatusCode,
                    employeeGuid);
                return null;
            }

            var dto = await response.Content.ReadFromJsonAsync<PlanningEmploymentSummaryDto>(cancellationToken: ct);
            if (dto is null) return null;

            return new PlanningEmploymentSummary
            {
                IsActive = dto.IsActive,
                HasContract = dto.HasContract,
                ContractStatus = dto.ContractStatus,
                ProbationEndDate = dto.ProbationEndDate,
                IsEligibleForPaymentConfirmation = dto.IsEligibleForPaymentConfirmation,
                BlockReason = dto.BlockReason,
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Planning employment summary unavailable for {EmployeeGuid}", employeeGuid);
            return null;
        }
    }

    private sealed class PlanningEmploymentSummaryDto
    {
        public bool IsActive { get; set; }
        public bool HasContract { get; set; }
        public string? ContractStatus { get; set; }
        public DateTime? ProbationEndDate { get; set; }
        public bool IsEligibleForPaymentConfirmation { get; set; }
        public string? BlockReason { get; set; }
    }
}
