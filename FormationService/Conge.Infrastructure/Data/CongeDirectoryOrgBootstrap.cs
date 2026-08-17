using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Conge.Domain.Entities;
using Conge.Domain.Interfaces;
using Conge.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Conge.Infrastructure.Data;

/// <summary>Bootstrap one-shot : seed miroir org + enrichit ServiceNom des snapshots depuis Directory overview.</summary>
public sealed class CongeDirectoryOrgBootstrap(
    IServiceScopeFactory scopeFactory,
    ILogger<CongeDirectoryOrgBootstrap> logger) : IHostedService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var httpFactory = scope.ServiceProvider.GetService<IHttpClientFactory>();
            if (httpFactory is null)
            {
                logger.LogDebug("CONGE org bootstrap skipped (no HttpClientFactory)");
                return;
            }

            var http = httpFactory.CreateClient("directory-org");
            using var resp = await http.GetAsync("api/directory/org/overview", cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("CONGE org bootstrap overview HTTP {Status}", (int)resp.StatusCode);
                return;
            }

            var overview = await resp.Content.ReadFromJsonAsync<BootstrapOverview>(JsonOptions, cancellationToken);
            if (overview is null) return;

            var orgNodes = scope.ServiceProvider.GetRequiredService<IOrgNodeCongeRepository>();
            var employeRepo = scope.ServiceProvider.GetRequiredService<IEmployeSnapshotRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var catalog = scope.ServiceProvider.GetService<DirectoryOrgCatalog>();

            var seeded = 0;
            foreach (var c in overview.Services ?? [])
            {
                if (string.IsNullOrWhiteSpace(c.Id) || string.IsNullOrWhiteSpace(c.Name)) continue;
                await orgNodes.UpsertAsync(c.Id, c.Name, "Cellule", c.EtageId, cancellationToken);
                seeded++;
            }

            foreach (var s in overview.SousServices ?? [])
            {
                if (string.IsNullOrWhiteSpace(s.Id) || string.IsNullOrWhiteSpace(s.Name)) continue;
                await orgNodes.UpsertAsync(s.Id, s.Name, "Service", s.ServiceId, cancellationToken);
                seeded++;
            }

            foreach (var dept in overview.OperationalDepartments ?? [])
            {
                foreach (var pole in dept.Poles ?? [])
                {
                    if (!string.IsNullOrWhiteSpace(pole.Id) && !string.IsNullOrWhiteSpace(pole.Name))
                    {
                        await orgNodes.UpsertAsync(pole.Id, pole.Name, "Pole", null, cancellationToken);
                        seeded++;
                    }

                    foreach (var cell in pole.Cellules ?? [])
                    {
                        if (!string.IsNullOrWhiteSpace(cell.Id) && !string.IsNullOrWhiteSpace(cell.Name))
                        {
                            await orgNodes.UpsertAsync(cell.Id, cell.Name, "Cellule", pole.Id, cancellationToken);
                            seeded++;
                        }

                        foreach (var svc in cell.Services ?? [])
                        {
                            if (string.IsNullOrWhiteSpace(svc.Id) || string.IsNullOrWhiteSpace(svc.Name)) continue;
                            await orgNodes.UpsertAsync(svc.Id, svc.Name, "Service", cell.Id, cancellationToken);
                            seeded++;
                        }
                    }
                }
            }

            // Enrich ServiceNom when it is empty or equals an org id.
            var nameById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in await orgNodes.GetAllActiveAsync(cancellationToken))
                nameById[n.Id] = n.Name;

            var employees = await employeRepo.GetAllAsync(cancellationToken);
            var enriched = 0;
            foreach (var e in employees)
            {
                var orgSvc = QuotaCongeService.NormalizeNodeId(e.OrgServiceId);
                if (orgSvc is null) continue;
                if (!nameById.TryGetValue(orgSvc, out var niceName) || string.IsNullOrWhiteSpace(niceName))
                    continue;
                if (!string.IsNullOrWhiteSpace(e.ServiceNom)
                    && !string.Equals(e.ServiceNom, orgSvc, StringComparison.OrdinalIgnoreCase)
                    && !e.ServiceNom.StartsWith("svc-", StringComparison.OrdinalIgnoreCase)
                    && !e.ServiceNom.StartsWith("cell-", StringComparison.OrdinalIgnoreCase))
                    continue;

                e.MettreAJour(
                    e.Nom, e.Prenom, e.Email, e.ManagerId, e.ServiceId, niceName, e.Role,
                    e.DateEmbauche, e.PoleId, e.CelluleId, e.OrgServiceId, e.BusinessDepartmentId);
                employeRepo.Update(e);
                enriched++;
            }

            await uow.SaveChangesAsync(cancellationToken);
            catalog?.InvalidateCache();
            logger.LogInformation(
                "CONGE org bootstrap : {Seeded} nœuds, {Enriched} snapshots enrichis",
                seeded, enriched);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CONGE org bootstrap ignoré");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private sealed class BootstrapOverview
    {
        public List<FlatCellule>? Services { get; set; }
        public List<FlatService>? SousServices { get; set; }
        public List<OpDept>? OperationalDepartments { get; set; }
    }

    private sealed class FlatCellule
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? EtageId { get; set; }
    }

    private sealed class FlatService
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? ServiceId { get; set; }
    }

    private sealed class OpDept
    {
        public List<PoleNode>? Poles { get; set; }
    }

    private sealed class PoleNode
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public List<CellNode>? Cellules { get; set; }
    }

    private sealed class CellNode
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public List<SvcNode>? Services { get; set; }
    }

    private sealed class SvcNode
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }
}
