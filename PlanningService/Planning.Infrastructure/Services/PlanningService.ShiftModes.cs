using Microsoft.EntityFrameworkCore;
using Planning.Application.DTOs.Planning;
using Planning.Domain.Entities;
using Planning.Domain.Enums;

namespace Planning.Infrastructure.Services;

public partial class PlanningService
{
    private async Task<WeekShiftConfigResponseDto> SaveMultiModeShiftConfigAsync(
        SaveShiftConfigDto dto, bool isTemplate)
    {
        var subService = await _context.SubServices.FindAsync(dto.SubServiceId)
            ?? throw new InvalidOperationException("Sous-service introuvable.");

        if (dto.Modes == null || dto.Modes.Count == 0)
            throw new InvalidOperationException("Au moins un mode actif est requis.");

        foreach (var mode in dto.Modes)
        {
            if (string.IsNullOrWhiteSpace(mode.Title))
                throw new InvalidOperationException("Chaque mode doit avoir un titre.");
            if (mode.Shifts == null || mode.Shifts.Count == 0)
                throw new InvalidOperationException(
                    $"Le mode « {mode.Title} » doit contenir au moins un shift.");
            if (mode.Shifts.Count > 8)
                throw new InvalidOperationException(
                    $"Le mode « {mode.Title} » ne peut pas dépasser 8 shifts.");

            ValidateModePercentages(mode);
        }

        var activeModes = dto.Modes.Where(m => m.IsActive).ToList();
        if (activeModes.Count == 0)
            throw new InvalidOperationException("Au moins un mode actif est requis.");

        subService.MultiShiftModesEnabled = true;

        var existingProfiles = await _context.ShiftModeProfiles
            .Where(p => p.SubServiceId == dto.SubServiceId)
            .ToListAsync();

        var keptProfileIds = new HashSet<int>();
        var modeToProfile = new List<(ShiftModeProfileSaveDto Dto, ShiftModeProfile Profile)>();

        for (var i = 0; i < dto.Modes.Count; i++)
        {
            var modeDto = dto.Modes[i];
            ShiftModeProfile? profile = null;
            if (modeDto.Id is int id && id > 0)
                profile = existingProfiles.FirstOrDefault(p => p.Id == id);

            if (profile == null)
            {
                profile = new ShiftModeProfile
                {
                    SubServiceId = dto.SubServiceId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.ShiftModeProfiles.Add(profile);
                existingProfiles.Add(profile);
            }

            profile.Title = modeDto.Title.Trim();
            profile.DisplayOrder = modeDto.DisplayOrder > 0 ? modeDto.DisplayOrder : i + 1;
            profile.IsDefault = modeDto.IsDefault;
            profile.IsActive = modeDto.IsActive;
            profile.MinPresencePercent = modeDto.MinPresencePercent <= 0
                ? 0
                : Math.Clamp(modeDto.MinPresencePercent, 50, 100);
            profile.IsCriticalCell = modeDto.IsCriticalCell;
            profile.UpdatedAt = DateTime.UtcNow;
            if (profile.IsActive)
                profile.ArchivedAt = null;

            if (profile.Id > 0)
                keptProfileIds.Add(profile.Id);

            modeToProfile.Add((modeDto, profile));
        }

        // Un seul défaut parmi les actifs
        var defaults = modeToProfile.Where(x => x.Profile.IsActive && x.Profile.IsDefault).ToList();
        if (defaults.Count == 0)
        {
            var firstActive = modeToProfile.First(x => x.Profile.IsActive);
            firstActive.Profile.IsDefault = true;
        }
        else if (defaults.Count > 1)
        {
            for (var i = 1; i < defaults.Count; i++)
                defaults[i].Profile.IsDefault = false;
        }

        var removedProfiles = existingProfiles
            .Where(p => p.Id > 0 && !keptProfileIds.Contains(p.Id))
            .ToList();

        if (removedProfiles.Count > 0)
        {
            var removedIds = removedProfiles.Select(p => p.Id).ToList();
            var usedIds = await _context.WeeklyEmployeeShiftModes
                .AsNoTracking()
                .Where(e => removedIds.Contains(e.ShiftModeProfileId))
                .Select(e => e.ShiftModeProfileId)
                .Distinct()
                .ToListAsync();

            var usedSet = usedIds.ToHashSet();
            foreach (var profile in removedProfiles)
            {
                profile.IsActive = false;
                profile.IsDefault = false;
                profile.ArchivedAt ??= DateTime.UtcNow;
                profile.UpdatedAt = DateTime.UtcNow;

                if (!usedSet.Contains(profile.Id))
                {
                    var orphanConfigs = await _context.SubServiceShiftConfigs
                        .Where(c => c.ShiftModeProfileId == profile.Id)
                        .ToListAsync();
                    if (orphanConfigs.Count > 0)
                        _context.SubServiceShiftConfigs.RemoveRange(orphanConfigs);
                    _context.ShiftModeProfiles.Remove(profile);
                }
            }
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            var detail = ex.InnerException?.Message ?? ex.Message;
            throw new InvalidOperationException(
                "Échec enregistrement profils de modes : " + detail, ex);
        }

        foreach (var (modeDto, profile) in modeToProfile)
        {
            await UpsertModeShiftConfigsAsync(
                dto,
                isTemplate,
                profile.Id,
                modeDto.Shifts,
                profile.MinPresencePercent,
                profile.IsCriticalCell);
        }

        if (isTemplate)
            await SyncTemplateCellSettingsToAllWeekSnapshotsAsync(dto.SubServiceId);

        return await BuildWeekShiftConfigResponseAsync(
                   dto.SubServiceId, isTemplate, isTemplate ? null : dto.WeekCode)
               ?? throw new Exception("Erreur sauvegarde config multi-modes.");
    }

    private static void ValidateModePercentages(ShiftModeProfileSaveDto mode)
    {
        if (mode.Shifts.Count == 0) return;

        var missing = mode.Shifts.Where(s => !s.Percentage.HasValue).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Le mode « {mode.Title} » : indiquer le pourcentage pour tous les shifts.");

        var sum = mode.Shifts.Sum(s => s.Percentage!.Value);
        if (Math.Abs(sum - 100m) > 0.6m)
            throw new InvalidOperationException(
                $"Les pourcentages du mode « {mode.Title} » doivent totaliser ~100 % (actuellement {sum}).");
    }

    private async Task UpsertModeShiftConfigsAsync(
        SaveShiftConfigDto dto,
        bool isTemplate,
        int modeProfileId,
        List<ShiftConfigItemDto> shifts,
        int modeMinPresence,
        bool isCriticalCell)
    {
        List<SubServiceShiftConfig> existing;
        if (isTemplate)
        {
            existing = await _context.SubServiceShiftConfigs
                .Where(c => c.SubServiceId == dto.SubServiceId
                            && c.IsTemplate
                            && c.ShiftModeProfileId == modeProfileId)
                .ToListAsync();
        }
        else
        {
            existing = await _context.SubServiceShiftConfigs
                .Where(c => c.SubServiceId == dto.SubServiceId
                            && !c.IsTemplate
                            && c.WeekCode == dto.WeekCode
                            && c.ShiftModeProfileId == modeProfileId)
                .ToListAsync();
        }

        var cellMinPresence = modeMinPresence <= 0
            ? 0
            : Math.Clamp(modeMinPresence, 50, 100);

        var incoming = new List<(ShiftConfigItemDto Shift, int Index, SubServiceShiftConfig Built)>();
        for (var i = 0; i < shifts.Count; i++)
        {
            var shift = shifts[i];
            var startTime = TimeOnly.Parse(shift.StartTime);
            var breakDuration = shift.BreakDurationMinutes > 0
                ? shift.BreakDurationMinutes
                : BreakSlotPlanner.BreakDurationMinutes;

            var breakSlots = BreakSlotPlanner.NormalizeSlots(
                startTime, isCriticalCell, shift.BreakSlots);
            var (breakStart, breakEnd) = BreakSlotPlanner.SyncRange(breakSlots, breakDuration);

            var percentage = Math.Round(shift.Percentage ?? 0m, 1);

            incoming.Add((shift, i, new SubServiceShiftConfig
            {
                SubServiceId = dto.SubServiceId,
                ShiftModeProfileId = modeProfileId,
                WeekCode = isTemplate ? null : dto.WeekCode,
                WeekStartDate = isTemplate ? null : dto.WeekStartDate,
                IsTemplate = isTemplate,
                Label = shift.Label,
                StartTime = startTime,
                WorkHours = shift.WorkHours,
                BreakDurationMinutes = breakDuration,
                BreakRangeStart = breakStart,
                BreakRangeEnd = breakEnd,
                BreakSlotsJson = BreakSlotPlanner.SerializeSlots(breakSlots),
                IsCriticalCell = isCriticalCell,
                // Multi-mode template : quotas en % ; RequiredCount calculé à la génération.
                RequiredCount = 0,
                Percentage = percentage,
                MinPresencePercent = cellMinPresence,
                DisplayOrder = shift.DisplayOrder > 0 ? shift.DisplayOrder : i + 1,
                CreatedAt = DateTime.UtcNow
            }));
        }

        var builtList = incoming.Select(x => x.Built).ToList();
        LevelBalanceEvaluator.ApplyShiftKindsFromStartTimes(builtList);

        for (var i = 0; i < incoming.Count; i++)
        {
            var kindRaw = shifts[i].ShiftKind;
            if (!string.IsNullOrWhiteSpace(kindRaw)
                && Enum.TryParse<ShiftKind>(kindRaw, ignoreCase: true, out var parsed))
            {
                incoming[i].Built.ShiftKind = parsed;
            }
        }

        var unmatchedExisting = existing.ToList();
        var pairs = new List<(SubServiceShiftConfig Existing, SubServiceShiftConfig Desired)>();
        var pairedIncoming = new HashSet<int>();
        var toAdd = new List<SubServiceShiftConfig>();

        foreach (var item in incoming)
        {
            var label = item.Built.Label?.Trim() ?? string.Empty;
            var match = unmatchedExisting.FirstOrDefault(e =>
                string.Equals(e.Label?.Trim(), label, StringComparison.OrdinalIgnoreCase));
            if (match == null) continue;
            pairs.Add((match, item.Built));
            unmatchedExisting.Remove(match);
            pairedIncoming.Add(item.Index);
        }

        foreach (var item in incoming.Where(x => !pairedIncoming.Contains(x.Index)))
        {
            var match = unmatchedExisting.FirstOrDefault(e => e.DisplayOrder == item.Built.DisplayOrder);
            if (match == null) continue;
            pairs.Add((match, item.Built));
            unmatchedExisting.Remove(match);
            pairedIncoming.Add(item.Index);
        }

        var remainingIncoming = incoming.Where(x => !pairedIncoming.Contains(x.Index)).ToList();
        var byPos = Math.Min(remainingIncoming.Count, unmatchedExisting.Count);
        for (var i = 0; i < byPos; i++)
        {
            pairs.Add((unmatchedExisting[i], remainingIncoming[i].Built));
            pairedIncoming.Add(remainingIncoming[i].Index);
        }
        unmatchedExisting.RemoveRange(0, byPos);

        foreach (var item in remainingIncoming.Skip(byPos))
            toAdd.Add(item.Built);

        if (isTemplate && unmatchedExisting.Count > 0)
        {
            var removeIds = unmatchedExisting.Select(e => e.Id).ToList();
            var blocked = await _context.PlanningExceptionalRequests
                .AsNoTracking()
                .Where(r => removeIds.Contains(r.RequestedShiftTemplateId))
                .Select(r => r.RequestedShiftTemplateId)
                .Distinct()
                .ToListAsync();

            if (blocked.Count > 0)
            {
                var labels = unmatchedExisting
                    .Where(e => blocked.Contains(e.Id))
                    .Select(e => e.Label)
                    .Distinct()
                    .ToList();
                throw new InvalidOperationException(
                    "Impossible de supprimer le(s) shift(s) « "
                    + string.Join(" », « ", labels)
                    + " » : des demandes exceptionnelles y font encore référence. "
                    + "Modifiez ces shifts ou traitez les demandes avant de les retirer.");
            }
        }

        if (unmatchedExisting.Count > 0)
            _context.SubServiceShiftConfigs.RemoveRange(unmatchedExisting);

        foreach (var (row, desired) in pairs)
        {
            row.ShiftModeProfileId = desired.ShiftModeProfileId;
            row.WeekCode = desired.WeekCode;
            row.WeekStartDate = desired.WeekStartDate;
            row.IsTemplate = desired.IsTemplate;
            row.Label = desired.Label;
            row.StartTime = desired.StartTime;
            row.WorkHours = desired.WorkHours;
            row.BreakDurationMinutes = desired.BreakDurationMinutes;
            row.BreakRangeStart = desired.BreakRangeStart;
            row.BreakRangeEnd = desired.BreakRangeEnd;
            row.BreakSlotsJson = desired.BreakSlotsJson;
            row.IsCriticalCell = desired.IsCriticalCell;
            row.RequiredCount = desired.RequiredCount;
            row.Percentage = desired.Percentage;
            row.MinPresencePercent = desired.MinPresencePercent;
            row.DisplayOrder = desired.DisplayOrder;
            row.ShiftKind = desired.ShiftKind;
            row.UpdatedAt = DateTime.UtcNow;
        }

        if (toAdd.Count > 0)
            _context.SubServiceShiftConfigs.AddRange(toAdd);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            var detail = ex.InnerException?.Message ?? ex.Message;
            throw new InvalidOperationException(
                "Échec enregistrement configuration shifts multi-modes : " + detail, ex);
        }
    }

    private async Task<WeekShiftConfigResponseDto?> BuildWeekShiftConfigResponseAsync(
        int subServiceId, bool isTemplate, string? weekCode)
    {
        var subService = await _context.SubServices.FindAsync(subServiceId);
        if (subService == null) return null;

        List<SubServiceShiftConfig> configs;
        if (isTemplate)
        {
            configs = await _context.SubServiceShiftConfigs
                .Where(c => c.SubServiceId == subServiceId && c.IsTemplate)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();
        }
        else
        {
            configs = await _context.SubServiceShiftConfigs
                .Where(c => c.SubServiceId == subServiceId
                            && !c.IsTemplate
                            && c.WeekCode == weekCode)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();
        }

        var profiles = await _context.ShiftModeProfiles
            .AsNoTracking()
            .Where(p => p.SubServiceId == subServiceId)
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.Id)
            .ToListAsync();

        var multi = subService.MultiShiftModesEnabled;
        if (configs.Count == 0 && !(multi && profiles.Any(p => p.IsActive)))
            return null;

        var dto = new WeekShiftConfigResponseDto
        {
            SubServiceId = subServiceId,
            SubServiceName = subService.Name,
            WeekCode = isTemplate ? string.Empty : (weekCode ?? string.Empty),
            WeekStartDate = isTemplate
                ? default
                : configs.FirstOrDefault()?.WeekStartDate ?? DateOnly.MinValue,
            IsTemplate = isTemplate,
            IsCriticalCell = multi
                ? profiles.Any(p => p.IsActive && p.IsCriticalCell) || configs.Any(c => c.IsCriticalCell)
                : configs.Any(c => c.IsCriticalCell),
            MinPresencePercent = configs.FirstOrDefault()?.MinPresencePercent is int mp && mp > 0
                ? mp
                : (multi ? 0 : 70),
            MultiShiftModesEnabled = multi,
            TotalEffectif = configs.Sum(c => c.RequiredCount),
            Shifts = configs.Select(MapToShiftConfigResponseDto).ToList()
        };

        if (multi)
        {
            var activeProfiles = profiles.Where(p => p.IsActive || configs.Any(c => c.ShiftModeProfileId == p.Id))
                .ToList();
            dto.Modes = activeProfiles.Select(p => new ShiftModeProfileDto
            {
                Id = p.Id,
                Title = p.Title,
                DisplayOrder = p.DisplayOrder,
                IsDefault = p.IsDefault,
                IsActive = p.IsActive,
                MinPresencePercent = p.MinPresencePercent,
                IsCriticalCell = p.IsCriticalCell,
                Shifts = configs
                    .Where(c => c.ShiftModeProfileId == p.Id)
                    .OrderBy(c => c.DisplayOrder)
                    .Select(MapToShiftConfigResponseDto)
                    .ToList()
            }).ToList();

            if (dto.Modes.Count > 0)
            {
                dto.MinPresencePercent = dto.Modes.First().MinPresencePercent;
                dto.IsCriticalCell = dto.Modes.Any(m => m.IsCriticalCell);
            }
        }

        return dto;
    }

    public async Task<WeeklyShiftModePlanDto> GetWeeklyShiftModePlanAsync(
        int subServiceId, string weekCode, DateOnly weekStartDate)
    {
        var subService = await _context.SubServices.FindAsync(subServiceId)
            ?? throw new InvalidOperationException("Sous-service introuvable.");

        var employees = await _context.Users
            .AsNoTracking()
            .Where(u => u.SubServiceId == subServiceId && u.IsActive)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync();

        var availableModes = await _context.ShiftModeProfiles
            .AsNoTracking()
            .Where(p => p.SubServiceId == subServiceId && p.IsActive)
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Id)
            .Select(p => new ShiftModeProfileDto
            {
                Id = p.Id,
                Title = p.Title,
                DisplayOrder = p.DisplayOrder,
                IsDefault = p.IsDefault,
                IsActive = p.IsActive,
                MinPresencePercent = p.MinPresencePercent
            })
            .ToListAsync();

        var plan = await _context.WeeklyCellShiftModePlans
            .Include(p => p.EmployeeModes)
            .FirstOrDefaultAsync(p => p.SubServiceId == subServiceId && p.WeekCode == weekCode);

        Dictionary<int, int> assignmentByUser = new();
        if (plan != null)
        {
            assignmentByUser = plan.EmployeeModes
                .ToDictionary(e => e.UserId, e => e.ShiftModeProfileId);
        }
        else
        {
            var previous = await _context.WeeklyCellShiftModePlans
                .Include(p => p.EmployeeModes)
                .Where(p => p.SubServiceId == subServiceId
                            && p.IsValidated
                            && p.WeekStartDate < weekStartDate)
                .OrderByDescending(p => p.WeekStartDate)
                .FirstOrDefaultAsync();

            if (previous != null)
            {
                var activeModeIds = availableModes.Select(m => m.Id).ToHashSet();
                foreach (var line in previous.EmployeeModes)
                {
                    if (activeModeIds.Contains(line.ShiftModeProfileId))
                        assignmentByUser[line.UserId] = line.ShiftModeProfileId;
                }
            }
        }

        var modeTitles = availableModes.ToDictionary(m => m.Id, m => m.Title);
        var today = DateOnly.FromDateTime(DateTime.Today);

        return new WeeklyShiftModePlanDto
        {
            Id = plan?.Id ?? 0,
            SubServiceId = subServiceId,
            SubServiceName = subService.Name,
            WeekCode = weekCode,
            WeekStartDate = weekStartDate,
            IsValidated = plan?.IsValidated ?? false,
            IsLocked = weekStartDate <= today,
            ValidatedAt = plan?.ValidatedAt,
            AvailableModes = availableModes,
            Employees = employees.Select(u =>
            {
                assignmentByUser.TryGetValue(u.Id, out var modeId);
                var hasMode = modeId > 0;
                return new WeeklyEmployeeShiftModeDto
                {
                    UserId = u.Id,
                    FullName = $"{u.FirstName} {u.LastName}".Trim(),
                    Level = u.Level,
                    SaturdayWorkMode = u.SaturdayWorkMode,
                    ShiftModeProfileId = hasMode ? modeId : null,
                    ShiftModeTitle = hasMode && modeTitles.TryGetValue(modeId, out var title)
                        ? title
                        : null
                };
            }).ToList()
        };
    }

    public async Task<WeeklyShiftModePlanDto> SaveWeeklyShiftModePlanAsync(
        SaveWeeklyShiftModePlanDto dto)
    {
        var subService = await _context.SubServices.FindAsync(dto.SubServiceId)
            ?? throw new InvalidOperationException("Sous-service introuvable.");

        if (!subService.MultiShiftModesEnabled)
            throw new InvalidOperationException(
                "Les modes de shifts ne sont pas activés pour cette cellule.");

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (dto.WeekStartDate <= today)
            throw new InvalidOperationException(
                "Impossible de modifier les modes : la semaine est commencée (lecture seule).");

        var activeEmployees = await _context.Users
            .AsNoTracking()
            .Where(u => u.SubServiceId == dto.SubServiceId && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync();

        var activeEmployeeSet = activeEmployees.ToHashSet();
        var activeModes = await _context.ShiftModeProfiles
            .AsNoTracking()
            .Where(p => p.SubServiceId == dto.SubServiceId && p.IsActive)
            .Select(p => p.Id)
            .ToListAsync();
        var activeModeSet = activeModes.ToHashSet();

        if (dto.Employees == null || dto.Employees.Count != activeEmployeeSet.Count)
            throw new InvalidOperationException(
                "Chaque employé actif doit avoir exactement un mode.");

        var seenUsers = new HashSet<int>();
        foreach (var item in dto.Employees)
        {
            if (!activeEmployeeSet.Contains(item.UserId))
                throw new InvalidOperationException(
                    $"Employé {item.UserId} hors périmètre ou inactif.");
            if (!seenUsers.Add(item.UserId))
                throw new InvalidOperationException(
                    $"Employé {item.UserId} affecté plus d'une fois.");
            if (!activeModeSet.Contains(item.ShiftModeProfileId))
                throw new InvalidOperationException(
                    $"Mode {item.ShiftModeProfileId} invalide ou inactif.");
        }

        if (seenUsers.Count != activeEmployeeSet.Count
            || !activeEmployeeSet.SetEquals(seenUsers))
            throw new InvalidOperationException(
                "Chaque employé actif doit avoir exactement un mode.");

        var plan = await _context.WeeklyCellShiftModePlans
            .Include(p => p.EmployeeModes)
            .FirstOrDefaultAsync(p => p.SubServiceId == dto.SubServiceId && p.WeekCode == dto.WeekCode);

        if (plan == null)
        {
            plan = new WeeklyCellShiftModePlan
            {
                SubServiceId = dto.SubServiceId,
                WeekCode = dto.WeekCode,
                WeekStartDate = dto.WeekStartDate,
                CreatedAt = DateTime.UtcNow
            };
            _context.WeeklyCellShiftModePlans.Add(plan);
        }
        else
        {
            plan.WeekStartDate = dto.WeekStartDate;
            plan.UpdatedAt = DateTime.UtcNow;
            if (plan.EmployeeModes.Count > 0)
                _context.WeeklyEmployeeShiftModes.RemoveRange(plan.EmployeeModes);
        }

        plan.IsValidated = true;
        plan.ValidatedAt = DateTime.UtcNow;
        plan.ValidatedByUserId = dto.ActorUserId;

        await _context.SaveChangesAsync();

        foreach (var item in dto.Employees)
        {
            _context.WeeklyEmployeeShiftModes.Add(new WeeklyEmployeeShiftMode
            {
                WeeklyCellShiftModePlanId = plan.Id,
                UserId = item.UserId,
                ShiftModeProfileId = item.ShiftModeProfileId
            });
        }

        await _context.SaveChangesAsync();

        return await GetWeeklyShiftModePlanAsync(dto.SubServiceId, dto.WeekCode, dto.WeekStartDate);
    }

    private async Task<Dictionary<int, int>> ResolveEmployeeModeMapAsync(
        int subServiceId, string weekCode, IReadOnlyList<int> userIds)
    {
        var subService = await _context.SubServices.FindAsync(subServiceId)
            ?? throw new InvalidOperationException("Sous-service introuvable.");

        if (!subService.MultiShiftModesEnabled)
            return new Dictionary<int, int>();

        var distinctIds = userIds.Distinct().ToList();
        if (distinctIds.Count == 0)
            return new Dictionary<int, int>();

        var plan = await _context.WeeklyCellShiftModePlans
            .AsNoTracking()
            .Include(p => p.EmployeeModes)
            .FirstOrDefaultAsync(p => p.SubServiceId == subServiceId
                                      && p.WeekCode == weekCode
                                      && p.IsValidated);

        if (plan == null)
            throw new InvalidOperationException(
                "Plan de modes hebdomadaires manquant ou non validé pour cette semaine.");

        var map = plan.EmployeeModes
            .Where(e => distinctIds.Contains(e.UserId))
            .ToDictionary(e => e.UserId, e => e.ShiftModeProfileId);

        var missing = distinctIds.Where(id => !map.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                "Plan de modes incomplet : employés sans mode — "
                + string.Join(", ", missing) + ".");

        return map;
    }

    private static void ApplyModeRequiredCounts(
        List<SubServiceShiftConfig> snapshotConfigs,
        IReadOnlyDictionary<int, int> headcountByMode)
    {
        foreach (var group in snapshotConfigs
                     .Where(c => c.ShiftModeProfileId.HasValue)
                     .GroupBy(c => c.ShiftModeProfileId!.Value))
        {
            headcountByMode.TryGetValue(group.Key, out var headcount);
            var ordered = group.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id).ToList();
            var percentages = ordered.Select(c => c.Percentage).ToList();
            var counts = ShiftModeQuotaAllocator.AllocateCounts(percentages, headcount);
            for (var i = 0; i < ordered.Count; i++)
                ordered[i].RequiredCount = counts[i];
        }
    }
}
