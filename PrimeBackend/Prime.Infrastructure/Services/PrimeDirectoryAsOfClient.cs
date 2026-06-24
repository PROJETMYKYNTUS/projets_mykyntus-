using System.Net.Http.Json;

namespace Prime.Infrastructure.Services;

public interface IPrimeDirectoryAsOfClient
{
    Task<string?> ResolveChefDeProjetPoleIdAsync(string employeeId, DateTime? asOf, CancellationToken ct = default);
}

/// <summary>Résout l'affectation chef de projet à une date via Directory API (primes rétroactives).</summary>
public sealed class PrimeDirectoryAsOfClient(HttpClient http, IConfiguration configuration) : IPrimeDirectoryAsOfClient
{
    public async Task<string?> ResolveChefDeProjetPoleIdAsync(string employeeId, DateTime? asOf, CancellationToken ct = default)
    {
        if (asOf is null)
            return null;

        var baseUrl = configuration["Directory:BaseUrl"] ?? "http://employee-directory-backend:8080";
        var date = asOf.Value.ToUniversalTime().ToString("O");
        var url = $"{baseUrl.TrimEnd('/')}/api/directory/org/assignments/as-of?date={Uri.EscapeDataString(date)}";

        var dto = await http.GetFromJsonAsync<AsOfResponse>(url, ct);
        if (dto?.Assignments is null) return null;

        var match = dto.Assignments.FirstOrDefault(a =>
            string.Equals(a.Kind, "ChefDeProjet", StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.EmployeeId, employeeId.Trim(), StringComparison.OrdinalIgnoreCase));

        return match?.NodeId;
    }

    private sealed record AsOfResponse(IReadOnlyList<AsOfAssignment> Assignments);
    private sealed record AsOfAssignment(string Kind, string NodeId, string EmployeeId);
}
