using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Dto;

namespace PrimeBackend.Services;

public sealed class AllowanceCatalogService(PrimeDbContext db)
{
    public async Task<List<AllowanceTypeDto>> ListTypesAsync(CancellationToken ct) =>
        await db.AllowanceTypes.AsNoTracking()
            .OrderBy(t => t.Category).ThenBy(t => t.Label)
            .Select(t => MapType(t))
            .ToListAsync(ct);

    public async Task<List<AllowanceTypeDto>> ListEligibleTypesAsync(string? businessDepartmentId, CancellationToken ct)
    {
        var q = db.AllowanceTypes.AsNoTracking().Where(t => t.IsActive);
        if (!string.IsNullOrWhiteSpace(businessDepartmentId))
        {
            q = q.Where(t =>
                !t.DepartmentLinks.Any()
                || t.DepartmentLinks.Any(l => l.BusinessDepartmentId == businessDepartmentId));
        }
        return await q.OrderBy(t => t.Category).ThenBy(t => t.Label).Select(t => MapType(t)).ToListAsync(ct);
    }

    public async Task<AllowanceTypeDto> CreateTypeAsync(CreateAllowanceTypeRequest req, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new AllowanceTypeEntity
        {
            Id = Guid.NewGuid(),
            Code = req.Code.Trim().ToUpperInvariant(),
            Label = req.Label.Trim(),
            Category = req.Category.Trim(),
            CalculationMode = req.CalculationMode ?? "Manual",
            DefaultAmount = req.DefaultAmount,
            MinAmount = req.MinAmount,
            MaxAmount = req.MaxAmount,
            RequiresJustification = req.RequiresJustification,
            ApplicableDepartmentKinds = req.ApplicableDepartmentKinds ?? "Support",
            IsActive = true,
            CreatedAt = now,
        };
        db.AllowanceTypes.Add(entity);
        await db.SaveChangesAsync(ct);
        return MapType(entity);
    }

    private static AllowanceTypeDto MapType(AllowanceTypeEntity t) => new(
        t.Id, t.Code, t.Label, t.Category, t.CalculationMode,
        t.DefaultAmount, t.MinAmount, t.MaxAmount, t.RequiresJustification,
        t.ApplicableDepartmentKinds, t.IsActive);
}

public sealed class AllowanceRequestService(
    PrimeDbContext db,
    AllowanceScopeService scope,
    PrimeAuditLogService audit)
{
    public async Task<List<AllowanceRequestDto>> ListAsync(
        string actorUserId, string actorRole, string? departmentId, string? period, CancellationToken ct)
    {
        var q = db.AllowanceRequests.AsNoTracking().Include(r => r.AllowanceType).AsQueryable();
        if (!string.IsNullOrWhiteSpace(period))
            q = q.Where(r => r.Period == period.Trim());
        if (!string.IsNullOrWhiteSpace(departmentId))
            q = q.Where(r => r.BusinessDepartmentId == departmentId.Trim());

        if (actorRole.Equals("Manager", StringComparison.OrdinalIgnoreCase))
        {
            var deptId = await scope.GetManagerDepartmentIdAsync(actorUserId, ct);
            if (deptId is null) return [];
            q = q.Where(r => r.BusinessDepartmentId == deptId);
        }
        else if (actorRole.Equals("Pilote", StringComparison.OrdinalIgnoreCase)
                 || actorRole.Equals("Employee", StringComparison.OrdinalIgnoreCase))
        {
            q = q.Where(r => r.EmployeeId == actorUserId);
        }

        var rows = await q.OrderByDescending(r => r.CreatedAt).Take(500).ToListAsync(ct);
        return rows.Select(MapRequest).ToList();
    }

    public async Task<List<AllowanceRequestDto>> InboxAsync(string actorUserId, string actorRole, CancellationToken ct)
    {
        var status = actorRole switch
        {
            var r when r.Equals("RH", StringComparison.OrdinalIgnoreCase) => AllowanceRequestStatuses.ManagerApproved,
            var r when r.Equals("Comptabilité", StringComparison.OrdinalIgnoreCase)
                      || r.Equals("Comptable", StringComparison.OrdinalIgnoreCase) => AllowanceRequestStatuses.RhApproved,
            _ => null,
        };
        if (status is null) return [];

        var rows = await db.AllowanceRequests.AsNoTracking().Include(r => r.AllowanceType)
            .Where(r => r.Status == status)
            .OrderBy(r => r.CreatedAt)
            .Take(200)
            .ToListAsync(ct);
        return rows.Select(MapRequest).ToList();
    }

    public async Task<AllowanceRequestDto> CreateAsync(
        string actorUserId, CreateAllowanceRequestBody body, CancellationToken ct)
    {
        if (!await scope.IsSupportDepartmentManagerAsync(actorUserId, ct))
            throw new UnauthorizedAccessException("Réservé au manager de département Support.");
        if (!await scope.CanManagerAccessEmployeeAsync(actorUserId, body.EmployeeId, ct))
            throw new UnauthorizedAccessException("Employé hors périmètre manager Support.");

        var emp = await db.Employees.AsNoTracking().FirstAsync(e => e.Id == body.EmployeeId, ct);
        var type = await db.AllowanceTypes.FirstAsync(t => t.Id == body.AllowanceTypeId && t.IsActive, ct);
        ValidateAmount(type, body.Amount, body.Reason);

        var now = DateTimeOffset.UtcNow;
        var entity = new AllowanceRequestEntity
        {
            Id = Guid.NewGuid(),
            EmployeeId = body.EmployeeId,
            BusinessDepartmentId = emp.BusinessDepartmentId ?? "",
            AllowanceTypeId = type.Id,
            Period = body.Period.Trim(),
            Amount = body.Amount,
            Currency = body.Currency ?? "MAD",
            Reason = body.Reason?.Trim() ?? "",
            Source = string.IsNullOrWhiteSpace(body.Source) ? "Manual" : body.Source.Trim(),
            Status = AllowanceRequestStatuses.Draft,
            CreatedByUserId = actorUserId,
            CreatedAt = now,
        };
        db.AllowanceRequests.Add(entity);
        await db.SaveChangesAsync(ct);
        entity = await db.AllowanceRequests.Include(r => r.AllowanceType)
            .FirstAsync(r => r.Id == entity.Id, ct);
        return MapRequest(entity);
    }

    public async Task<AllowanceRequestDto> SubmitAsync(Guid id, string actorUserId, CancellationToken ct)
    {
        if (!await scope.IsSupportDepartmentManagerAsync(actorUserId, ct))
            throw new UnauthorizedAccessException("Réservé au manager de département Support.");

        var entity = await db.AllowanceRequests.Include(r => r.AllowanceType)
            .FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new KeyNotFoundException();
        if (entity.Status != AllowanceRequestStatuses.Draft)
            throw new InvalidOperationException("Seul un brouillon peut être soumis.");
        if (entity.CreatedByUserId != actorUserId)
            throw new UnauthorizedAccessException();

        ValidateAmount(entity.AllowanceType!, entity.Amount, entity.Reason);
        await TransitionAsync(entity, AllowanceRequestStatuses.ManagerApproved, actorUserId, "Manager", "Submitted", null, ct);
        entity.ManagerApprovedByUserId = actorUserId;
        entity.ManagerApprovedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return MapRequest(entity);
    }

    public async Task<AllowanceRequestDto> UpdateDraftAsync(
        Guid id, string actorUserId, UpdateAllowanceRequestBody body, CancellationToken ct)
    {
        if (!await scope.IsSupportDepartmentManagerAsync(actorUserId, ct))
            throw new UnauthorizedAccessException("Réservé au manager de département Support.");

        var entity = await db.AllowanceRequests.Include(r => r.AllowanceType)
            .FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new KeyNotFoundException();
        if (entity.Status != AllowanceRequestStatuses.Draft)
            throw new InvalidOperationException("Seule une demande en brouillon peut être modifiée.");
        if (entity.CreatedByUserId != actorUserId)
            throw new UnauthorizedAccessException();

        if (body.AllowanceTypeId.HasValue && body.AllowanceTypeId.Value != entity.AllowanceTypeId)
        {
            var type = await db.AllowanceTypes.FirstAsync(
                t => t.Id == body.AllowanceTypeId.Value && t.IsActive, ct);
            entity.AllowanceTypeId = type.Id;
            entity.AllowanceType = type;
        }

        if (!string.IsNullOrWhiteSpace(body.Period))
            entity.Period = body.Period.Trim();
        if (body.Amount.HasValue)
            entity.Amount = body.Amount.Value;
        if (body.Reason is not null)
            entity.Reason = body.Reason.Trim();

        ValidateAmount(entity.AllowanceType!, entity.Amount, entity.Reason);
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return MapRequest(entity);
    }

    public async Task<AllowanceRequestDto> ApproveAsync(Guid id, string actorUserId, string actorRole, CancellationToken ct)
    {
        var entity = await db.AllowanceRequests.Include(r => r.AllowanceType)
            .FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new KeyNotFoundException();
        if (!AllowanceValidationRoles.CanActAtStatus(actorRole, entity.Status))
            throw new UnauthorizedAccessException("Rôle non autorisé pour cette étape.");

        var next = AllowanceValidationRoles.NextStatusAfterApproval(entity.Status);
        await TransitionAsync(entity, next, actorUserId, actorRole, "Approved", null, ct);

        if (next == AllowanceRequestStatuses.ManagerApproved)
        {
            entity.ManagerApprovedByUserId = actorUserId;
            entity.ManagerApprovedAt = DateTimeOffset.UtcNow;
        }
        else if (next == AllowanceRequestStatuses.RhApproved)
        {
            entity.RhApprovedByUserId = actorUserId;
            entity.RhApprovedAt = DateTimeOffset.UtcNow;
        }
        else if (next == AllowanceRequestStatuses.ComptaApproved)
        {
            entity.ComptaApprovedByUserId = actorUserId;
            entity.ComptaApprovedAt = DateTimeOffset.UtcNow;
        }
        else if (next == AllowanceRequestStatuses.Paid)
        {
            entity.PaidAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return MapRequest(entity);
    }

    public async Task<AllowanceRequestDto> RejectAsync(
        Guid id, string actorUserId, string actorRole, string reason, CancellationToken ct)
    {
        var entity = await db.AllowanceRequests.Include(r => r.AllowanceType)
            .FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new KeyNotFoundException();
        if (!AllowanceValidationRoles.CanActAtStatus(actorRole, entity.Status))
            throw new UnauthorizedAccessException();

        entity.RejectionReason = reason.Trim();
        await TransitionAsync(entity, AllowanceRequestStatuses.Rejected, actorUserId, actorRole, "Rejected", reason, ct);
        return MapRequest(entity);
    }

    private async Task TransitionAsync(
        AllowanceRequestEntity entity, string toStatus, string actorUserId, string actorRole,
        string action, string? comment, CancellationToken ct)
    {
        var from = entity.Status;
        entity.Status = toStatus;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        db.AllowanceRequestHistories.Add(new AllowanceRequestHistoryEntity
        {
            Id = Guid.NewGuid(),
            AllowanceRequestId = entity.Id,
            Action = action,
            FromStatus = from,
            ToStatus = toStatus,
            ActorUserId = actorUserId,
            ActorRole = actorRole,
            Comment = comment,
            At = DateTimeOffset.UtcNow,
        });
        await audit.RecordAsync(actorUserId, actorUserId, actorRole, action, "AllowanceRequest",
            entity.Id.ToString(), $"{{\"from\":\"{from}\",\"to\":\"{toStatus}\"}}", ct);
        await db.SaveChangesAsync(ct);
    }

    private static void ValidateAmount(AllowanceTypeEntity type, decimal amount, string? reason)
    {
        if (type.MinAmount.HasValue && amount < type.MinAmount.Value)
            throw new InvalidOperationException($"Montant inférieur au minimum ({type.MinAmount}).");
        if (type.MaxAmount.HasValue && amount > type.MaxAmount.Value)
            throw new InvalidOperationException($"Montant supérieur au maximum ({type.MaxAmount}).");
        if (type.RequiresJustification && string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Motif obligatoire pour ce type de prime.");
    }

    private static AllowanceRequestDto MapRequest(AllowanceRequestEntity r) => new(
        r.Id, r.EmployeeId, r.BusinessDepartmentId, r.AllowanceTypeId,
        r.AllowanceType?.Code ?? "", r.AllowanceType?.Label ?? "",
        r.Period, r.Amount, r.Currency, r.Reason, r.Source, r.Status,
        r.CreatedByUserId, r.RejectionReason,
        r.ManagerApprovedAt, r.RhApprovedAt, r.ComptaApprovedAt, r.PaidAt, r.CreatedAt);
}

public sealed class AllowanceRuleEngineService(PrimeDbContext db, AllowanceRequestService requests)
{
    private static readonly HashSet<string> PlanningTypeCodes = ["HOURS_OT", "HOURS_NIGHT"];
    private static readonly HashSet<string> CongesTypeCodes = ["ATTENDANCE"];

    /// <summary>Phase 4 — génère des propositions Draft à partir des règles actives (Planning/Congés).</summary>
    public async Task<int> GenerateProposalsAsync(string period, string businessDepartmentId, string actorUserId, CancellationToken ct)
    {
        var rules = await db.AllowanceRules.AsNoTracking()
            .Include(r => r.AllowanceType)
            .Where(r => r.IsActive && r.BusinessDepartmentId == businessDepartmentId)
            .ToListAsync(ct);
        if (rules.Count == 0) return 0;

        var employees = await db.Employees.AsNoTracking()
            .Where(e => e.BusinessDepartmentId == businessDepartmentId
                        && e.BusinessDepartmentKind == "Support"
                        && e.ParentId == actorUserId)
            .ToListAsync(ct);

        var created = 0;
        foreach (var rule in rules)
        {
            if (!MatchesDataSource(rule)) continue;

            foreach (var emp in employees)
            {
                if (!await EmployeeEligibleForRuleAsync(emp.Id, rule, period, ct)) continue;

                if (await db.AllowanceRequests.AnyAsync(r =>
                        r.EmployeeId == emp.Id && r.Period == period && r.AllowanceTypeId == rule.AllowanceTypeId
                        && r.Status != AllowanceRequestStatuses.Rejected, ct))
                    continue;

                var amount = rule.AllowanceType.DefaultAmount ?? 0m;
                if (amount <= 0) continue;

                await requests.CreateAsync(actorUserId, new CreateAllowanceRequestBody
                {
                    EmployeeId = emp.Id,
                    AllowanceTypeId = rule.AllowanceTypeId,
                    Period = period,
                    Amount = amount,
                    Reason = $"Proposition auto ({rule.DataSource})",
                    Source = "Auto",
                }, ct);
                created++;
            }
        }
        return created;
    }

    private static bool MatchesDataSource(AllowanceRuleEntity rule)
    {
        var code = rule.AllowanceType?.Code ?? "";
        return rule.DataSource switch
        {
            "Planning" => PlanningTypeCodes.Contains(code),
            "Conges" => CongesTypeCodes.Contains(code),
            _ => true,
        };
    }

    /// <summary>Stub Phase 4 — à enrichir avec appels Planning/Congés réels.</summary>
    private Task<bool> EmployeeEligibleForRuleAsync(string employeeId, AllowanceRuleEntity rule, string period, CancellationToken ct) =>
        Task.FromResult(true);
}
