using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Planning.Application.Abstractions;
using Planning.Application.DTOs;
using Planning.Infrastructure.Persistence;

namespace Planning.Infrastructure.Services;

public sealed class OrgReconciliationService(
    AppDbContext db,
    IPlanningOrgMirrorService mirror,
    IServiceProvider serviceProvider) : IOrgReconciliationService
{
    public async Task<int> BackfillFromPrimeAsync(PrimeOrgBackfillRequest request, CancellationToken ct = default)
    {
        var poles = request.Poles.Select(p => new PrimeOrgPoleMirrorDto
        {
            Id = p.Id,
            Name = p.Name,
            Cellules = p.Cellules.Select(c => new PrimeOrgCelluleMirrorDto
            {
                Id = c.Id,
                Name = c.Name,
                Services = c.Services.Select(s => new PrimeOrgLeafServiceMirrorDto { Id = s.Id, Name = s.Name }).ToList(),
            }).ToList(),
        }).ToList();

        return await mirror.SyncFromPrimeTreeAsync(poles, ct);
    }

    public async Task<OrgReconciliationVerifyDto> SyncFromPrimeAsync(CancellationToken ct = default)
    {
        await PlanningOrgBootstrap.SyncFromPrimeAsync(serviceProvider, ct);
        return await VerifyAsync(ct);
    }

    public async Task<OrgReconciliationVerifyDto> SyncFromDirectoryAsync(
        string? authorizationHeader,
        CancellationToken ct = default)
    {
        await mirror.SyncFromDirectoryOverviewAsync(authorizationHeader, ct);
        return await VerifyAsync(ct);
    }

    public async Task<OrgReconciliationVerifyDto> VerifyAsync(CancellationToken ct = default)
    {
        var floorsWithoutPrime = await db.Floors.CountAsync(f => f.PrimePoleId == null, ct);
        var servicesWithoutPrime = await db.Services.CountAsync(s => s.PrimeCelluleId == null, ct);
        var subsWithoutPrime = await db.SubServices.CountAsync(s => s.PrimeServiceId == null, ct);
        var users = await db.Users.CountAsync(u => u.IsActive, ct);

        var duplicateSubNames = await db.SubServices
            .GroupBy(s => new { s.ServiceId, s.Name })
            .Where(g => g.Count() > 1)
            .CountAsync(ct);

        return new OrgReconciliationVerifyDto(
            floorsWithoutPrime,
            servicesWithoutPrime,
            subsWithoutPrime,
            duplicateSubNames,
            users,
            subsWithoutPrime == 0 && floorsWithoutPrime == 0 && servicesWithoutPrime == 0);
    }
}
