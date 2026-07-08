using EmployeeDirectory.Application.Abstractions;
using EmployeeDirectory.Application.Dtos;
using EmployeeDirectory.Application.Exceptions;
using EmployeeDirectory.Domain.Entities;
using EmployeeDirectory.Infrastructure.Persistence;
using Kyntus.Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using DomainAssignmentKind = EmployeeDirectory.Domain.Enums.OrgAssignmentKind;
using DomainNodeLevel = EmployeeDirectory.Domain.Enums.OrgNodeLevel;

namespace EmployeeDirectory.Infrastructure.Services;

public sealed class PilotRotationTenureService(DirectoryDbContext db) : IPilotRotationTenureService
{
    public const int MinimumMonthsOnService = 6;
    public const string OverrideReasonPrefix = "[Dérogation]";

    public async Task BootstrapProjectedPilotsAsync(CancellationToken ct = default)
    {
        var pilots = await db.Employees.AsNoTracking()
            .Where(e => e.IsActive
                && e.Role == KyntusRoleNames.Pilote
                && e.ServiceId != null
                && e.ServiceId != "")
            .Select(e => new { e.Id, e.ServiceId, e.HireDate, e.CreatedAt })
            .ToListAsync(ct);

        if (pilots.Count == 0) return;

        var employeeIds = pilots.Select(p => p.Id).ToList();
        var withActive = await db.OrgAssignments.AsNoTracking()
            .Where(a => employeeIds.Contains(a.EmployeeId)
                && a.Kind == DomainAssignmentKind.Pilote
                && a.EffectiveTo == null)
            .Select(a => a.EmployeeId)
            .ToListAsync(ct);
        var hasActive = withActive.ToHashSet();

        var now = DateTime.UtcNow;
        foreach (var pilot in pilots)
        {
            if (hasActive.Contains(pilot.Id)) continue;

            var effectiveFrom = pilot.HireDate == default
                ? pilot.CreatedAt
                : pilot.HireDate;
            if (effectiveFrom > now) effectiveFrom = now;

            db.OrgAssignments.Add(new OrgAssignment
            {
                Id = Guid.NewGuid(),
                Kind = DomainAssignmentKind.Pilote,
                NodeId = pilot.ServiceId!.Trim(),
                NodeLevel = DomainNodeLevel.Service,
                EmployeeId = pilot.Id,
                EffectiveFrom = effectiveFrom,
                ChangeReason = "Bootstrap affectation pilote projetée",
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<PilotRotationEligibilityDto> GetEligibilityAsync(
        Guid employeeId,
        string targetServiceId,
        CancellationToken ct = default)
    {
        var target = targetServiceId.Trim();
        var current = await ResolveCurrentPlacementAsync(employeeId, ct);
        if (current is null || string.IsNullOrWhiteSpace(current.ServiceId))
        {
            return new PilotRotationEligibilityDto(
                Eligible: true,
                IsSameService: false,
                CurrentServiceId: null,
                CurrentServiceName: null,
                CurrentSince: null,
                EligibleAt: null,
                DaysRemaining: 0);
        }

        if (string.Equals(current.ServiceId, target, StringComparison.OrdinalIgnoreCase))
        {
            var sameName = await ResolveServiceNameAsync(current.ServiceId, ct);
            return new PilotRotationEligibilityDto(
                Eligible: true,
                IsSameService: true,
                CurrentServiceId: current.ServiceId,
                CurrentServiceName: sameName,
                CurrentSince: current.EffectiveFrom,
                EligibleAt: null,
                DaysRemaining: 0);
        }

        var eligibleAt = current.EffectiveFrom.AddMonths(MinimumMonthsOnService);
        var now = DateTime.UtcNow;
        var eligible = eligibleAt <= now;
        var daysRemaining = eligible ? 0 : Math.Max(1, (int)Math.Ceiling((eligibleAt - now).TotalDays));
        var serviceName = await ResolveServiceNameAsync(current.ServiceId, ct);

        return new PilotRotationEligibilityDto(
            Eligible: eligible,
            IsSameService: false,
            CurrentServiceId: current.ServiceId,
            CurrentServiceName: serviceName,
            CurrentSince: current.EffectiveFrom,
            EligibleAt: eligible ? null : eligibleAt,
            DaysRemaining: daysRemaining);
    }

    public Task ValidateRotationAsync(
        Guid employeeId,
        string targetServiceId,
        bool forceTenureOverride,
        string? reason,
        CancellationToken ct = default)
    {
        var eligibility = GetEligibilityAsync(employeeId, targetServiceId, ct);
        return ValidateFromEligibilityAsync(eligibility, forceTenureOverride, reason);
    }

    private static async Task ValidateFromEligibilityAsync(
        Task<PilotRotationEligibilityDto> eligibilityTask,
        bool forceTenureOverride,
        string? reason)
    {
        var eligibility = await eligibilityTask;
        if (eligibility.Eligible || eligibility.IsSameService)
            return;

        if (forceTenureOverride)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new InvalidOperationException(
                    "Un motif est obligatoire pour une dérogation à la règle des 6 mois.");
            }
            return;
        }

        var serviceLabel = eligibility.CurrentServiceName ?? eligibility.CurrentServiceId ?? "service actuel";
        throw new PilotRotationTenureException(
            $"Rotation impossible : l'employé doit rester au moins {MinimumMonthsOnService} mois sur « {serviceLabel} » " +
            $"({eligibility.DaysRemaining} jour(s) restant(s)).",
            eligibility.CurrentServiceId,
            eligibility.CurrentSince,
            eligibility.EligibleAt ?? DateTime.UtcNow,
            eligibility.DaysRemaining);
    }

    public async Task<IReadOnlyList<PilotRotationHistoryEntryDto>> GetRotationHistoryAsync(
        Guid employeeId,
        CancellationToken ct = default)
    {
        var segments = await db.OrgAssignments.AsNoTracking()
            .Where(a => a.EmployeeId == employeeId && a.Kind == DomainAssignmentKind.Pilote)
            .OrderByDescending(a => a.EffectiveFrom)
            .Take(100)
            .ToListAsync(ct);

        var serviceIds = segments.Select(s => s.NodeId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var names = await db.OrgServices.AsNoTracking()
            .Where(s => serviceIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, StringComparer.OrdinalIgnoreCase, ct);

        return segments.Select(s =>
        {
            int? duration = null;
            if (s.EffectiveTo is { } end)
                duration = Math.Max(0, (int)Math.Floor((end - s.EffectiveFrom).TotalDays));
            else
                duration = Math.Max(0, (int)Math.Floor((DateTime.UtcNow - s.EffectiveFrom).TotalDays));

            return new PilotRotationHistoryEntryDto(
                s.NodeId,
                names.GetValueOrDefault(s.NodeId) ?? s.NodeId,
                s.EffectiveFrom,
                s.EffectiveTo,
                duration,
                s.ChangeReason,
                IsOverrideReason(s.ChangeReason));
        }).ToList();
    }

    public async Task ApplyRotationHrProfileAsync(
        Guid employeeId,
        string previousServiceId,
        CancellationToken ct = default)
    {
        var previousName = await ResolveServiceNameAsync(previousServiceId, ct) ?? previousServiceId;
        var profile = await db.EmployeeHrProfiles.FirstOrDefaultAsync(p => p.EmployeeId == employeeId, ct);
        if (profile is null)
        {
            profile = new EmployeeHrProfile
            {
                EmployeeId = employeeId,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.EmployeeHrProfiles.Add(profile);
        }

        profile.DateEvolutionPoste = DateOnly.FromDateTime(DateTime.UtcNow);
        profile.AncienPoste = KyntusRoleNames.Pilote;
        profile.AncienService = previousName;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public static string FormatOverrideReason(string reason) =>
        reason.TrimStart().StartsWith(OverrideReasonPrefix, StringComparison.Ordinal)
            ? reason.Trim()
            : $"{OverrideReasonPrefix} {reason.Trim()}";

    public static bool IsOverrideReason(string? reason) =>
        !string.IsNullOrWhiteSpace(reason)
        && reason.TrimStart().StartsWith(OverrideReasonPrefix, StringComparison.Ordinal);

    private async Task<PlacementSegment?> ResolveCurrentPlacementAsync(Guid employeeId, CancellationToken ct)
    {
        var active = await db.OrgAssignments.AsNoTracking()
            .Where(a => a.EmployeeId == employeeId
                && a.Kind == DomainAssignmentKind.Pilote
                && a.EffectiveTo == null)
            .OrderByDescending(a => a.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

        if (active is not null)
        {
            return new PlacementSegment(active.NodeId, active.EffectiveFrom);
        }

        var employee = await db.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null
            || !KyntusRoleNames.IsPilote(employee.Role)
            || string.IsNullOrWhiteSpace(employee.ServiceId))
        {
            return null;
        }

        var effectiveFrom = employee.HireDate == default ? employee.CreatedAt : employee.HireDate;
        return new PlacementSegment(employee.ServiceId.Trim(), effectiveFrom);
    }

    private async Task<string?> ResolveServiceNameAsync(string serviceId, CancellationToken ct)
    {
        var trimmed = serviceId.Trim();
        return await db.OrgServices.AsNoTracking()
            .Where(s => s.Id == trimmed)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(ct);
    }

    private sealed record PlacementSegment(string ServiceId, DateTime EffectiveFrom);
}
