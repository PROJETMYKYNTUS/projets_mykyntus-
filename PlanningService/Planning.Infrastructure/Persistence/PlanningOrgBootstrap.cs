using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Planning.Application.Abstractions;
using Planning.Application.DTOs;

namespace Planning.Infrastructure.Persistence;

/// <summary>
/// Aligne le miroir Planning (Floors / Services / SubServices) avec Organisation RH (Prime).
/// </summary>
public static class PlanningOrgBootstrap
{
    public static async Task SyncFromPrimeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var mirror = scope.ServiceProvider.GetRequiredService<IPlanningOrgMirrorService>();
        var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("PlanningOrgBootstrap");

        var baseUrl = (config["Prime:BaseUrl"] ?? "http://prime-backend:8080/").TrimEnd('/') + "/";
        using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(45) };

        const int maxAttempts = 30;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var departments = await http.GetFromJsonAsync<List<PrimeDepartmentJson>>("api/prime/departments", ct)
                    ?? [];
                if (departments.Count == 0)
                {
                    log.LogInformation("Planning org mirror : aucune structure Prime — skip.");
                    return;
                }

                var poles = MapDepartmentsToMirrorPoles(departments);
                var actions = await mirror.SyncFromPrimeTreeAsync(poles, ct);
                log.LogInformation(
                    "Planning org mirror : {DeptCount} pôle(s) Prime, {Actions} action(s).",
                    departments.Count,
                    actions);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                log.LogWarning(ex, "Planning org mirror attempt {Attempt}/{Max} — retry in 3s.", attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Planning org mirror abandoned after {Max} attempts.", maxAttempts);
                return;
            }
        }
    }

    private static List<PrimeOrgPoleMirrorDto> MapDepartmentsToMirrorPoles(IEnumerable<PrimeDepartmentJson> departments)
    {
        var poles = new List<PrimeOrgPoleMirrorDto>();
        foreach (var dept in departments)
        {
            poles.Add(new PrimeOrgPoleMirrorDto
            {
                Id = dept.Id,
                Name = dept.Name,
                Cellules = (dept.Poles ?? []).Select(cellule => new PrimeOrgCelluleMirrorDto
                {
                    Id = cellule.Id,
                    Name = cellule.Name,
                    Services = ExtractLeafServices(cellule),
                }).ToList(),
            });
        }
        return poles;
    }

    private static List<PrimeOrgLeafServiceMirrorDto> ExtractLeafServices(PrimeCelluleJson cellule)
    {
        var leaves = cellule.Cells ?? cellule.Cellules ?? [];
        return leaves.Select(l => new PrimeOrgLeafServiceMirrorDto { Id = l.Id, Name = l.Name }).ToList();
    }

    private sealed class PrimeDepartmentJson
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public List<PrimeCelluleJson>? Poles { get; set; }
    }

    private sealed class PrimeCelluleJson
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        [JsonPropertyName("cells")]
        public List<PrimeLeafJson>? Cells { get; set; }
        public List<PrimeLeafJson>? Cellules { get; set; }
    }

    private sealed class PrimeLeafJson
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }
}
