using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure.Persistence;
using Planning.Application.DTOs.Planning;
using Planning.Domain.Enums;
using Planning.Infrastructure.Helpers;
using Planning.Infrastructure.Hubs;
using Planning.Application.Abstractions;
using Planning.Domain.Entities;

namespace Planning.Infrastructure.Services;

public class PlanningService : IPlanningService
{
    private readonly AppDbContext _context;
    private readonly IHubContext<PlanningHub> _hubContext;

    public PlanningService(AppDbContext context, IHubContext<PlanningHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    // ----------------------------------------------------
    // CR�ER UN PLANNING (vide, en Draft)
    // ----------------------------------------------------
    public async Task<WeeklyPlanningResponseDto> CreatePlanningAsync(CreateWeeklyPlanningDto dto)
    {
        await ValidatePlanningInputsAsync(dto.SubServiceId, dto.WeekStartDate, dto.TotalEffectif);

        var existing = await _context.WeeklyPlannings
            .FirstOrDefaultAsync(p => p.WeekCode == dto.WeekCode &&
                                      p.SubServiceId == dto.SubServiceId);
        if (existing != null)
            throw new InvalidOperationException(
                $"Planning {dto.WeekCode} existe d�j� pour ce sous-service.");

        var planning = new WeeklyPlanning
        {
            SubServiceId = dto.SubServiceId,
            WeekCode = dto.WeekCode,
            WeekStartDate = dto.WeekStartDate,
            TotalEffectif = dto.TotalEffectif,
            SaturdayGroupId = GetSaturdayGroupForWeek(dto.WeekStartDate),
            Status = PlanningStatus.Draft,
            CreatedAt = DateTime.UtcNow
        };

        _context.WeeklyPlannings.Add(planning);
        await _context.SaveChangesAsync();

        return await GetPlanningByIdAsync(planning.Id)
            ?? throw new Exception("Erreur cr�ation planning.");
    }

    // ----------------------------------------------------
    // OVERRIDE BREAK
    // ----------------------------------------------------
    public async Task<DayAssignmentDto> OverrideBreakAsync(OverrideBreakDto dto)
    {
        var assignment = await _context.ShiftAssignments
            .Include(a => a.Shift)
            .Include(a => a.SubServiceShiftConfig)
            .FirstOrDefaultAsync(a => a.Id == dto.ShiftAssignmentId)
            ?? throw new Exception("Assignment introuvable.");

        assignment.BreakTime = TimeOnly.Parse(dto.NewBreakTime);
        assignment.IsManagerOverride = true;
        await _context.SaveChangesAsync();

        return MapToDayDtoNew(assignment);
    }

    // ----------------------------------------------------
    // CONFIG SHIFTS — TEMPLATE + SNAPSHOT
    // ----------------------------------------------------
    public async Task<WeekShiftConfigResponseDto> SaveShiftTemplateAsync(SaveShiftConfigDto dto)
    {
        dto.WeekCode = null;
        dto.WeekStartDate = null;
        return await SaveShiftConfigInternalAsync(dto, isTemplate: true);
    }

    public async Task<WeekShiftConfigResponseDto> SaveShiftConfigAsync(SaveShiftConfigDto dto)
    {
        // WeekCode vide → modèle permanent (rétro-compat + nouvelle UI)
        var isTemplate = string.IsNullOrWhiteSpace(dto.WeekCode);
        return await SaveShiftConfigInternalAsync(dto, isTemplate);
    }

    private async Task<WeekShiftConfigResponseDto> SaveShiftConfigInternalAsync(
        SaveShiftConfigDto dto, bool isTemplate)
    {
        List<SubServiceShiftConfig> existing;
        if (isTemplate)
        {
            existing = await _context.SubServiceShiftConfigs
                .Where(c => c.SubServiceId == dto.SubServiceId && c.IsTemplate)
                .ToListAsync();
        }
        else
        {
            existing = await _context.SubServiceShiftConfigs
                .Where(c => c.SubServiceId == dto.SubServiceId
                         && !c.IsTemplate
                         && c.WeekCode == dto.WeekCode)
                .ToListAsync();
        }

        if (existing.Count > 0)
            _context.SubServiceShiftConfigs.RemoveRange(existing);

        var totalEffectif = dto.Shifts.Sum(s => s.RequiredCount);
        var configs = new List<SubServiceShiftConfig>();

        for (int i = 0; i < dto.Shifts.Count; i++)
        {
            var shift = dto.Shifts[i];
            var startTime = TimeOnly.Parse(shift.StartTime);

            var breakStart = shift.BreakRangeStart != null
                ? TimeOnly.Parse(shift.BreakRangeStart)
                : startTime.AddHours(4);

            var breakDuration = shift.BreakDurationMinutes > 0 ? shift.BreakDurationMinutes : 60;
            var breakEnd = shift.BreakRangeEnd != null
                ? TimeOnly.Parse(shift.BreakRangeEnd)
                : breakStart.AddMinutes(breakDuration);

            var percentage = totalEffectif > 0
                ? Math.Round((decimal)shift.RequiredCount / totalEffectif * 100, 1)
                : 0;

            configs.Add(new SubServiceShiftConfig
            {
                SubServiceId = dto.SubServiceId,
                WeekCode = isTemplate ? null : dto.WeekCode,
                WeekStartDate = isTemplate ? null : dto.WeekStartDate,
                IsTemplate = isTemplate,
                Label = shift.Label,
                StartTime = startTime,
                WorkHours = shift.WorkHours,
                BreakDurationMinutes = shift.BreakDurationMinutes,
                BreakRangeStart = breakStart,
                BreakRangeEnd = breakEnd,
                RequiredCount = shift.RequiredCount,
                Percentage = percentage,
                MinPresencePercent = shift.MinPresencePercent,
                DisplayOrder = shift.DisplayOrder > 0 ? shift.DisplayOrder : i + 1,
                CreatedAt = DateTime.UtcNow
            });
        }

        LevelBalanceEvaluator.ApplyShiftKindsFromStartTimes(configs);

        // Override explicite si fourni dans le DTO
        for (int i = 0; i < configs.Count; i++)
        {
            var kindRaw = dto.Shifts[i].ShiftKind;
            if (!string.IsNullOrWhiteSpace(kindRaw)
                && Enum.TryParse<ShiftKind>(kindRaw, ignoreCase: true, out var parsed))
            {
                configs[i].ShiftKind = parsed;
            }
        }

        _context.SubServiceShiftConfigs.AddRange(configs);
        await _context.SaveChangesAsync();

        if (isTemplate)
            return await GetShiftTemplateAsync(dto.SubServiceId)
                ?? throw new Exception("Erreur sauvegarde template.");

        return await GetShiftConfigAsync(dto.SubServiceId, dto.WeekCode!)
            ?? throw new Exception("Erreur sauvegarde config.");
    }

    public async Task<WeekShiftConfigResponseDto?> GetShiftTemplateAsync(int subServiceId)
    {
        var subService = await _context.SubServices.FindAsync(subServiceId);
        if (subService == null) return null;

        var configs = await _context.SubServiceShiftConfigs
            .Where(c => c.SubServiceId == subServiceId && c.IsTemplate)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();

        if (configs.Count == 0) return null;

        return new WeekShiftConfigResponseDto
        {
            SubServiceId = subServiceId,
            SubServiceName = subService.Name,
            WeekCode = string.Empty,
            WeekStartDate = default,
            IsTemplate = true,
            TotalEffectif = configs.Sum(c => c.RequiredCount),
            Shifts = configs.Select(MapToShiftConfigResponseDto).ToList()
        };
    }

    public async Task<ShiftConfigStatusResponseDto> GetShiftConfigStatusAsync()
    {
        var subs = await _context.SubServices
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.Name, s.PrimeServiceId })
            .ToListAsync();

        var templateAgg = await _context.SubServiceShiftConfigs
            .AsNoTracking()
            .Where(c => c.IsTemplate)
            .GroupBy(c => c.SubServiceId)
            .Select(g => new
            {
                SubServiceId = g.Key,
                ShiftCount = g.Count(),
                TemplateEffectif = g.Sum(x => x.RequiredCount)
            })
            .ToListAsync();

        var templateBySub = templateAgg.ToDictionary(x => x.SubServiceId);

        var activeCounts = await _context.Users
            .AsNoTracking()
            .Where(u => u.IsActive && u.SubServiceId != null)
            .GroupBy(u => u.SubServiceId!.Value)
            .Select(g => new { SubServiceId = g.Key, Count = g.Count() })
            .ToListAsync();

        var activeBySub = activeCounts.ToDictionary(x => x.SubServiceId, x => x.Count);

        var items = subs.Select(s =>
        {
            templateBySub.TryGetValue(s.Id, out var tpl);
            activeBySub.TryGetValue(s.Id, out var active);
            return new ShiftConfigStatusItemDto
            {
                SubServiceId = s.Id,
                SubServiceName = s.Name,
                PrimeServiceId = s.PrimeServiceId,
                HasTemplate = tpl != null && tpl.ShiftCount > 0,
                ShiftCount = tpl?.ShiftCount ?? 0,
                TemplateEffectif = tpl?.TemplateEffectif ?? 0,
                ActiveEmployeeCount = active
            };
        }).ToList();

        return new ShiftConfigStatusResponseDto
        {
            Items = items,
            ConfiguredCount = items.Count(i => i.HasTemplate),
            TotalCount = items.Count
        };
    }

    public async Task<WeekShiftConfigResponseDto?> GetShiftConfigAsync(
        int subServiceId, string weekCode)
    {
        var subService = await _context.SubServices.FindAsync(subServiceId);
        if (subService == null) return null;

        var configs = await _context.SubServiceShiftConfigs
            .Where(c => c.SubServiceId == subServiceId
                     && !c.IsTemplate
                     && c.WeekCode == weekCode)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();

        // Fallback template si pas encore de snapshot
        if (configs.Count == 0)
        {
            var template = await GetShiftTemplateAsync(subServiceId);
            if (template != null)
            {
                template.WeekCode = weekCode;
                template.IsTemplate = true;
                return template;
            }
            return null;
        }

        return new WeekShiftConfigResponseDto
        {
            SubServiceId = subServiceId,
            SubServiceName = subService.Name,
            WeekCode = weekCode,
            WeekStartDate = configs.FirstOrDefault()?.WeekStartDate ?? DateOnly.MinValue,
            IsTemplate = false,
            TotalEffectif = configs.Sum(c => c.RequiredCount),
            Shifts = configs.Select(MapToShiftConfigResponseDto).ToList()
        };
    }

    public async Task EnsureWeekSnapshotAsync(
        int subServiceId, string weekCode, DateOnly weekStartDate, bool forceRefresh = false)
    {
        var existing = await _context.SubServiceShiftConfigs
            .Where(c => c.SubServiceId == subServiceId
                     && !c.IsTemplate
                     && c.WeekCode == weekCode)
            .ToListAsync();

        if (existing.Count > 0 && !forceRefresh)
            return;

        var template = await _context.SubServiceShiftConfigs
            .Where(c => c.SubServiceId == subServiceId && c.IsTemplate)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();

        if (template.Count == 0)
            throw new InvalidOperationException(
                "Aucune configuration shifts (modèle) pour ce sous-service. Configurez d'abord les shifts.");

        if (existing.Count > 0)
            _context.SubServiceShiftConfigs.RemoveRange(existing);

        foreach (var t in template)
        {
            _context.SubServiceShiftConfigs.Add(new SubServiceShiftConfig
            {
                SubServiceId = subServiceId,
                WeekCode = weekCode,
                WeekStartDate = weekStartDate,
                IsTemplate = false,
                Label = t.Label,
                StartTime = t.StartTime,
                WorkHours = t.WorkHours,
                BreakDurationMinutes = t.BreakDurationMinutes,
                BreakRangeStart = t.BreakRangeStart,
                BreakRangeEnd = t.BreakRangeEnd,
                RequiredCount = t.RequiredCount,
                Percentage = t.Percentage,
                MinPresencePercent = t.MinPresencePercent,
                DisplayOrder = t.DisplayOrder,
                ShiftKind = t.ShiftKind,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        // Rétro-compat : templates sans ShiftKind → déduire depuis StartTime
        var snapshot = await _context.SubServiceShiftConfigs
            .Where(c => c.SubServiceId == subServiceId && !c.IsTemplate && c.WeekCode == weekCode)
            .ToListAsync();
        if (snapshot.All(c => c.ShiftKind == ShiftKind.Standard) && snapshot.Count > 0)
        {
            LevelBalanceEvaluator.ApplyShiftKindsFromStartTimes(snapshot);
            await _context.SaveChangesAsync();
        }
    }

    // ----------------------------------------------------
    // GÉNÉRER DEPUIS LA CONFIG
    // ----------------------------------------------------
    public async Task<WeeklyPlanningResponseDto> GeneratePlanningFromConfigAsync(
       GeneratePlanningFromConfigDto dto)
    {
        var planning = await _context.WeeklyPlannings
            .Include(p => p.SubService)
            .FirstOrDefaultAsync(p => p.Id == dto.WeeklyPlanningId)
            ?? throw new Exception("Planning introuvable.");

        var weekCode = string.IsNullOrWhiteSpace(dto.WeekCode) ? planning.WeekCode : dto.WeekCode!;
        var subServiceId = dto.SubServiceId > 0 ? dto.SubServiceId : planning.SubServiceId;

        // Régénération brouillon : retirer les assignments avant refresh snapshot (FK)
        var forceRefresh = planning.Status == PlanningStatus.Draft;
        if (forceRefresh)
        {
            _context.ShiftAssignments.RemoveRange(
                _context.ShiftAssignments.Where(a => a.WeeklyPlanningId == planning.Id));
            _context.WeeklyShiftConfigs.RemoveRange(
                _context.WeeklyShiftConfigs.Where(c => c.WeeklyPlanningId == planning.Id));
            await _context.SaveChangesAsync();
        }

        await EnsureWeekSnapshotAsync(subServiceId, weekCode, planning.WeekStartDate, forceRefresh);

        var shiftConfigs = await _context.SubServiceShiftConfigs
            .Where(c => c.SubServiceId == subServiceId
                     && !c.IsTemplate
                     && c.WeekCode == weekCode)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();

        if (!shiftConfigs.Any())
            throw new Exception("Aucune config de shifts trouvée.");

        var employees = await _context.Users
            .Where(u => u.SubServiceId == planning.SubServiceId && u.IsActive)
            .OrderBy(u => u.Id)
            .ToListAsync();

        planning.TotalEffectif = employees.Count;

        // Aligne les quotas snapshot sur l'effectif réel (conserve les proportions du modèle)
        RescaleShiftQuotasToEffectif(shiftConfigs, employees.Count);

        await AutoAssignSaturdayGroupsAsync(planning.SubServiceId);

        var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(
            planning.WeekStartDate.ToDateTime(TimeOnly.MinValue));
        planning.SaturdayGroupId = weekNumber % 2 == 0 ? 1 : 2;

        if (!forceRefresh)
        {
            _context.ShiftAssignments.RemoveRange(
                _context.ShiftAssignments.Where(a => a.WeeklyPlanningId == planning.Id));
            _context.WeeklyShiftConfigs.RemoveRange(
                _context.WeeklyShiftConfigs.Where(c => c.WeeklyPlanningId == planning.Id));
        }

        await _context.SaveChangesAsync();

        var userIds = employees.Select(e => e.Id).ToList();

        var saturdayGroups = await _context.SaturdayGroups
            .Where(sg => userIds.Contains(sg.UserId))
            .ToListAsync();

        var weekEnd = planning.WeekStartDate.AddDays(6);
        var conges = await _context.Conges
            .Where(c => userIds.Contains(c.UserId)
                     && c.Status == CongeStatus.Approved
                     && c.StartDate <= weekEnd
                     && c.EndDate >= planning.WeekStartDate)
            .ToListAsync();

        var assignments = new List<ShiftAssignment>();
        var weekDays = GetWeekDays(planning.WeekStartDate);

        // ? Jours f�ri�s fran�ais
        var holidays = FrenchHolidayHelper.GetHolidays(planning.WeekStartDate.Year);

        // ------------------------------------------------
        // ROTATION � offset par semaine + quotas respect�s
        // ------------------------------------------------
        var currentWeekNumber = System.Globalization.ISOWeek.GetWeekOfYear(
            planning.WeekStartDate.ToDateTime(TimeOnly.MinValue));

        var orderedShifts = shiftConfigs.OrderBy(sc => sc.DisplayOrder).ToList();

        var employeeStartShiftIndex = new Dictionary<int, int>();
        int cumulative = 0;
        for (int shiftIdx = 0; shiftIdx < orderedShifts.Count; shiftIdx++)
        {
            for (int q = 0; q < orderedShifts[shiftIdx].RequiredCount; q++)
            {
                if (cumulative < employees.Count)
                {
                    var empStartIdx = (shiftIdx + currentWeekNumber) % orderedShifts.Count;
                    employeeStartShiftIndex[employees[cumulative].Id] = empStartIdx;
                    cumulative++;
                }
            }
        }

        // Après rescale, le surplus ne devrait plus arriver ; filet de sécurité
        while (cumulative < employees.Count)
        {
            var empStartIdx = (cumulative + currentWeekNumber) % orderedShifts.Count;
            employeeStartShiftIndex[employees[cumulative].Id] = empStartIdx;
            cumulative++;
        }

        // ------------------------------------------------
        // G�N�RATION Lun ? Ven
        // ------------------------------------------------
        int dayIdx = 0;
        foreach (var (day, date) in weekDays)
        {
            // ? Jour f�ri� ? tous F�RI�
            if (holidays.Contains(date))
            {
                foreach (var emp in employees)
                {
                    assignments.Add(new ShiftAssignment
                    {
                        WeeklyPlanningId = planning.Id,
                        UserId = emp.Id,
                        SubServiceShiftConfigId = null,
                        AssignedDate = date,
                        DayOfWeek = day,
                        IsSaturday = false,
                        IsOnLeave = false,
                        IsHoliday = true,
                        IsNewEmployee = IsBeginnerLevel(emp)
                    });
                }
                dayIdx++;
                continue;
            }

            var availableEmployees = employees.Where(e =>
                !conges.Any(c =>
                    c.UserId == e.Id &&
                    c.StartDate <= date &&
                    c.EndDate >= date))
                .ToList();

            var onLeaveEmployees = employees.Where(e =>
                conges.Any(c =>
                    c.UserId == e.Id &&
                    c.StartDate <= date &&
                    c.EndDate >= date))
                .ToList();

            foreach (var emp in onLeaveEmployees)
            {
                assignments.Add(new ShiftAssignment
                {
                    WeeklyPlanningId = planning.Id,
                    UserId = emp.Id,
                    SubServiceShiftConfigId = null,
                    AssignedDate = date,
                    DayOfWeek = day,
                    IsSaturday = false,
                    IsOnLeave = true,
                    IsHoliday = false,
                    IsNewEmployee = IsBeginnerLevel(emp)
                });
            }

            var shiftCountToday = orderedShifts.ToDictionary(s => s.Id, s => 0);

            foreach (var emp in availableEmployees)
            {
                var startIdx = employeeStartShiftIndex.ContainsKey(emp.Id)
                    ? employeeStartShiftIndex[emp.Id]
                    : 0;
                var todayShiftIdx = (startIdx + dayIdx) % orderedShifts.Count;
                var todayShift = orderedShifts[todayShiftIdx];

                var finalShift = todayShift;
                int attempts = 0;
                while (shiftCountToday[finalShift.Id] >= finalShift.RequiredCount
                       && attempts < orderedShifts.Count)
                {
                    todayShiftIdx = (todayShiftIdx + 1) % orderedShifts.Count;
                    finalShift = orderedShifts[todayShiftIdx];
                    attempts++;
                }

                shiftCountToday[finalShift.Id]++;

                assignments.Add(new ShiftAssignment
                {
                    WeeklyPlanningId = planning.Id,
                    UserId = emp.Id,
                    SubServiceShiftConfigId = finalShift.Id,
                    AssignedDate = date,
                    DayOfWeek = day,
                    IsSaturday = false,
                    IsOnLeave = false,
                    IsHoliday = false,
                    IsNewEmployee = IsBeginnerLevel(emp)
                });
            }

            dayIdx++;
        }

        // ------------------------------------------------
        // SAMEDI
        // ------------------------------------------------
        var saturdayDate = planning.WeekStartDate.AddDays(5);
        var saturdayWorkers = new List<int>();

        if (holidays.Contains(saturdayDate))
        {
            // ? Samedi f�ri� ? tous F�RI�
            foreach (var emp in employees)
            {
                assignments.Add(new ShiftAssignment
                {
                    WeeklyPlanningId = planning.Id,
                    UserId = emp.Id,
                    SubServiceShiftConfigId = null,
                    AssignedDate = saturdayDate,
                    DayOfWeek = DayOfWeekEnum.Saturday,
                    IsSaturday = true,
                    IsOnLeave = false,
                    IsHoliday = true,
                    IsNewEmployee = IsBeginnerLevel(emp)
                });
            }
        }
        else
        {
            // Compteurs d'équité demi-journée Débutant (créneau 1 = plus tôt, 2 = suivant)
            var beginnerHalfDaySlotCounts = new Dictionary<int, int> { [1] = 0, [2] = 0 };

            for (int empIndex = 0; empIndex < employees.Count; empIndex++)
            {
                var employee = employees[empIndex];

                bool isOnLeaveSaturday = conges.Any(c =>
                    c.UserId == employee.Id &&
                    c.StartDate <= saturdayDate &&
                    c.EndDate >= saturdayDate);

                if (isOnLeaveSaturday)
                {
                    assignments.Add(new ShiftAssignment
                    {
                        WeeklyPlanningId = planning.Id,
                        UserId = employee.Id,
                        SubServiceShiftConfigId = null,
                        AssignedDate = saturdayDate,
                        DayOfWeek = DayOfWeekEnum.Saturday,
                        IsSaturday = true,
                        IsOnLeave = true,
                        IsHoliday = false,
                        IsNewEmployee = IsBeginnerLevel(employee)
                    });
                }
                else
                {
                    var satAssignment = await GenerateSaturdayAssignmentFromConfigAsync(
                        employee, planning, shiftConfigs, saturdayGroups, empIndex,
                        string.IsNullOrWhiteSpace(dto.WeekCode) ? planning.WeekCode : dto.WeekCode!,
                        beginnerHalfDaySlotCounts);

                    if (satAssignment != null)
                    {
                        assignments.Add(satAssignment);
                        saturdayWorkers.Add(employee.Id);
                    }
                }
            }
        }

        var usersById = employees.ToDictionary(e => e.Id);
        LevelBalanceRepairer.Repair(assignments, shiftConfigs, usersById, employees, planning);

        saturdayWorkers = assignments
            .Where(a => a.IsSaturday && a.SubServiceShiftConfigId != null && !a.IsOnLeave && !a.IsHoliday)
            .Select(a => a.UserId)
            .Distinct()
            .ToList();

        await SaveSaturdayHistoryAsync(new SetSaturdayHistoryDto(
            dto.SubServiceId,
            dto.WeekCode,
            employees.Select(emp => new SaturdayHistoryEntryDto(
                emp.Id,
                saturdayWorkers.Contains(emp.Id)
            )).ToList()
        ), false);

        _context.ShiftAssignments.AddRange(assignments);

        // -- PAUSES (uniquement jours normaux travaillés) --
        var workDayAssignments = assignments
            .Where(a => !a.IsSaturday && !a.IsOnLeave && !a.IsHoliday
                     && a.SubServiceShiftConfigId != null)
            .GroupBy(a => a.AssignedDate)
            .ToList();

        foreach (var dayGroup in workDayAssignments)
            AssignBreakTimesFromConfig(dayGroup.ToList(), shiftConfigs, employees.Count);

        var saturdayWorkAssignments = assignments
            .Where(a => a.IsSaturday && !a.IsOnLeave && !a.IsHoliday
                     && a.SubServiceShiftConfigId != null)
            .ToList();
        if (saturdayWorkAssignments.Any())
            AssignBreakTimesFromConfig(saturdayWorkAssignments, shiftConfigs, employees.Count);

        // Anomalies éventuelles (cas forcés) exposées via CoverageReport — pas de blocage.

        await _context.SaveChangesAsync();

        return await GetPlanningByIdAsync(planning.Id)
            ?? throw new Exception("Erreur g�n�ration planning.");
    }

    // ----------------------------------------------------
    // METTRE SAMEDI OFF (supprimer l'assignation)
    // ----------------------------------------------------
    public async Task SetSaturdayOffAsync(int weeklyPlanningId, int userId)
    {
        var assignment = await _context.ShiftAssignments
            .FirstOrDefaultAsync(a =>
                a.WeeklyPlanningId == weeklyPlanningId &&
                a.UserId == userId &&
                a.IsSaturday);

        if (assignment != null)
        {
            _context.ShiftAssignments.Remove(assignment);
            await _context.SaveChangesAsync();
        }
    }

    // ----------------------------------------------------
    // SATURDAY HISTORY
    // ----------------------------------------------------
    public async Task<List<SaturdayHistoryResponseDto>> GetSaturdayHistoryAsync(
        int subServiceId, string weekCode)
    {
        var employees = await _context.Users
            .Where(u => u.SubServiceId == subServiceId && u.IsActive)
            .ToListAsync();

        var histories = await _context.SaturdayHistories
            .Where(h => h.SubServiceId == subServiceId && h.WeekCode == weekCode)
            .ToListAsync();

        return employees.Select(emp =>
        {
            var history = histories.FirstOrDefault(h => h.UserId == emp.Id);
            return new SaturdayHistoryResponseDto(
                emp.Id,
                $"{emp.FirstName} {emp.LastName}",
                weekCode,
                history?.WorkedSaturday ?? false,
                history?.IsManualEntry ?? false
            );
        }).ToList();
    }

    public async Task<List<SaturdayYtdDto>> GetSaturdayYtdAsync(int subServiceId, int year)
    {
        var prefix = $"{year}-";
        var employees = await _context.Users
            .Where(u => u.SubServiceId == subServiceId && u.IsActive)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync();

        var histories = await _context.SaturdayHistories
            .Where(h => h.SubServiceId == subServiceId && h.WeekCode.StartsWith(prefix))
            .ToListAsync();

        return employees.Select(emp =>
        {
            var rows = histories.Where(h => h.UserId == emp.Id).ToList();
            var worked = rows.Count(h => h.WorkedSaturday);
            var total = rows.Count;
            var off = total - worked;
            var pct = total > 0
                ? Math.Round((decimal)worked / total * 100, 1)
                : 0m;
            return new SaturdayYtdDto(
                emp.Id,
                $"{emp.FirstName} {emp.LastName}",
                worked,
                off,
                total,
                pct);
        }).ToList();
    }

    public async Task SaveSaturdayHistoryAsync(SetSaturdayHistoryDto dto, bool isManual)
    {
        foreach (var entry in dto.Entries)
        {
            var existing = await _context.SaturdayHistories
                .FirstOrDefaultAsync(h =>
                    h.UserId == entry.UserId &&
                    h.WeekCode == dto.WeekCode &&
                    h.SubServiceId == dto.SubServiceId);

            if (existing != null)
            {
                existing.WorkedSaturday = entry.WorkedSaturday;
                existing.IsManualEntry = isManual;
            }
            else
            {
                _context.SaturdayHistories.Add(new SaturdayHistory
                {
                    UserId = entry.UserId,
                    SubServiceId = dto.SubServiceId,
                    WeekCode = dto.WeekCode,
                    WorkedSaturday = entry.WorkedSaturday,
                    IsManualEntry = isManual,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        await _context.SaveChangesAsync();
    }

    // ----------------------------------------------------
    // ANCIENNE G�N�RATION (gard�e pour compatibilit�)
    // ----------------------------------------------------
    public async Task<WeeklyPlanningResponseDto> GeneratePlanningAsync(GeneratePlanningDto dto)
    {
        var planning = await _context.WeeklyPlannings
            .Include(p => p.SubService)
            .Include(p => p.ShiftAssignments)
            .FirstOrDefaultAsync(p => p.Id == dto.WeeklyPlanningId)
            ?? throw new Exception("Planning introuvable.");

        await ValidatePlanningInputsAsync(planning.SubServiceId, planning.WeekStartDate, dto.TotalEffectif);
        planning.TotalEffectif = dto.TotalEffectif;

        var employees = await _context.Users
            .Where(u => u.SubServiceId == planning.SubServiceId && u.IsActive)
            .OrderBy(u => u.EarlyShiftCount)
            .ToListAsync();

        if (employees.Count == 0)
            throw new InvalidOperationException("Ce service n'a aucun employ� actif.");

        var shifts = await _context.Shifts
            .OrderBy(s => s.StartTime)
            .ToListAsync();

        if (shifts.Count < 4)
            throw new InvalidOperationException(
                "Il faut au moins 4 shifts configur�s. Contactez l'administrateur.");

        await AutoAssignSaturdayGroupsAsync(planning.SubServiceId);

        var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(
            planning.WeekStartDate.ToDateTime(TimeOnly.MinValue));
        planning.SaturdayGroupId = weekNumber % 2 == 0 ? 1 : 2;
        await _context.SaveChangesAsync();

        _context.WeeklyShiftConfigs.RemoveRange(
            _context.WeeklyShiftConfigs.Where(c => c.WeeklyPlanningId == planning.Id));
        _context.ShiftAssignments.RemoveRange(
            _context.ShiftAssignments.Where(a => a.WeeklyPlanningId == planning.Id));

        var shiftConfigs = CalculateShiftQuotas(shifts, dto.TotalEffectif, planning.Id);
        _context.WeeklyShiftConfigs.AddRange(shiftConfigs);

        var userIds = employees.Select(e => e.Id).ToList();
        var saturdayGroups = await _context.SaturdayGroups
            .Where(sg => userIds.Contains(sg.UserId))
            .ToListAsync();

        var weekEnd = planning.WeekStartDate.AddDays(6);
        var conges = await _context.Conges
            .Where(c => userIds.Contains(c.UserId)
                     && c.Status == CongeStatus.Approved
                     && c.StartDate <= weekEnd
                     && c.EndDate >= planning.WeekStartDate)
            .ToListAsync();

        var assignments = new List<ShiftAssignment>();
        var weekDays = GetWeekDays(planning.WeekStartDate);

        for (int empIndex = 0; empIndex < employees.Count; empIndex++)
        {
            var employee = employees[empIndex];

            var recentShiftIds = await _context.ShiftAssignments
                .Where(a => a.UserId == employee.Id &&
                            a.ShiftId != null &&
                            a.WeeklyPlanning.WeekStartDate < planning.WeekStartDate)
                .OrderByDescending(a => a.AssignedDate)
                .Take(20)
                .Select(a => a.ShiftId!.Value)
                .ToListAsync();

            var employeeShiftRotation = GetEmployeeWeekRotation(shifts, empIndex, recentShiftIds);

            for (int dayIndex = 0; dayIndex < weekDays.Count; dayIndex++)
            {
                var (day, date) = weekDays[dayIndex];

                bool isOnLeave = conges.Any(c =>
                    c.UserId == employee.Id &&
                    c.StartDate <= date &&
                    c.EndDate >= date);

                if (isOnLeave)
                {
                    assignments.Add(new ShiftAssignment
                    {
                        WeeklyPlanningId = planning.Id,
                        UserId = employee.Id,
                        ShiftId = null,
                        AssignedDate = date,
                        DayOfWeek = day,
                        IsSaturday = false,
                        IsOnLeave = true,
                        IsNewEmployee = IsBeginnerLevel(employee)
                    });
                    continue;
                }

                var shiftId = employeeShiftRotation[dayIndex % shifts.Count];
                assignments.Add(new ShiftAssignment
                {
                    WeeklyPlanningId = planning.Id,
                    UserId = employee.Id,
                    ShiftId = shiftId,
                    AssignedDate = date,
                    DayOfWeek = day,
                    IsSaturday = false,
                    IsOnLeave = false,
                    IsNewEmployee = IsBeginnerLevel(employee)
                });

                if (shiftId == shifts.First().Id)
                    employee.EarlyShiftCount++;
            }

            var saturdayDate2 = planning.WeekStartDate.AddDays(5);
            bool isOnLeaveSat = conges.Any(c =>
                c.UserId == employee.Id &&
                c.StartDate <= saturdayDate2 &&
                c.EndDate >= saturdayDate2);

            if (!isOnLeaveSat)
            {
                var beginnerSlots = new Dictionary<int, int> { [1] = 0, [2] = 0 };
                // Compter les débutants déjà placés dans ce batch (boucle par employé)
                foreach (var a in assignments.Where(x => x.IsSaturday && x.IsHalfDaySaturday && x.SaturdaySlot is 1 or 2))
                    beginnerSlots[a.SaturdaySlot] = beginnerSlots.GetValueOrDefault(a.SaturdaySlot, 0) + 1;

                var satAssignment = GenerateSaturdayAssignment(
                    employee, planning, shifts, saturdayGroups, empIndex, beginnerSlots);
                if (satAssignment != null)
                    assignments.Add(satAssignment);
            }
        }

        _context.ShiftAssignments.AddRange(assignments);

        var allDays = assignments
            .Where(a => !a.IsSaturday && !a.IsOnLeave && a.ShiftId != null)
            .GroupBy(a => a.AssignedDate)
            .ToList();

        foreach (var dayGroup in allDays)
            AssignBreakTimes(dayGroup.ToList(), shifts, employees.Count);

        var saturdayAssignments = assignments
            .Where(a => a.IsSaturday && !a.IsOnLeave && a.ShiftId != null)
            .ToList();
        if (saturdayAssignments.Any())
            AssignBreakTimes(saturdayAssignments, shifts, employees.Count);

        await _context.SaveChangesAsync();

        return await GetPlanningByIdAsync(planning.Id)
            ?? throw new Exception("Erreur g�n�ration planning.");
    }

    public async Task SyncNewEmployeesAsync()
    {
        var threeMonthsAgo = DateTime.UtcNow.AddMonths(-3);
        var employees = await _context.Users.Where(u => u.IsActive).ToListAsync();

        foreach (var emp in employees)
        {
            if (emp.HireDate >= threeMonthsAgo && !emp.IsNewEmployee)
                emp.IsNewEmployee = true;
        }

        await _context.SaveChangesAsync();
    }

    public async Task AutoAssignSaturdayGroupsAsync(int subServiceId)
    {
        var employees = await _context.Users
            .Where(u => u.SubServiceId == subServiceId && u.IsActive)
            .OrderBy(u => u.Id)
            .ToListAsync();

        if (!employees.Any()) return;

        var existingUserIds = await _context.SaturdayGroups
            .Where(sg => employees.Select(e => e.Id).Contains(sg.UserId))
            .Select(sg => sg.UserId)
            .ToListAsync();

        var employeesWithoutGroup = employees
            .Where(e => !existingUserIds.Contains(e.Id))
            .ToList();

        if (!employeesWithoutGroup.Any()) return;

        var group1Count = await _context.SaturdayGroups
            .CountAsync(sg => existingUserIds.Contains(sg.UserId) && sg.GroupNumber == 1);
        var group2Count = await _context.SaturdayGroups
            .CountAsync(sg => existingUserIds.Contains(sg.UserId) && sg.GroupNumber == 2);

        foreach (var emp in employeesWithoutGroup)
        {
            var groupNumber = group1Count <= group2Count ? 1 : 2;
            _context.SaturdayGroups.Add(new SaturdayGroup
            {
                UserId = emp.Id,
                GroupNumber = groupNumber,
                IsNewEmployee = false,
                ManagerOverride = false,
                AssignedAt = DateTime.UtcNow,
                AssignedBy = 0
            });
            if (groupNumber == 1) group1Count++;
            else group2Count++;
        }

        await _context.SaveChangesAsync();
    }
    public async Task<MyPlanningDto?> GetMyCurrentPlanningAsync(int userId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Console.WriteLine($"TODAY: {today}, userId: {userId}");

        // Test 1 : tous les plannings publi�s
        var allPublished = await _context.WeeklyPlannings
            .Where(p => p.Status == PlanningStatus.Published)
            .ToListAsync();
        Console.WriteLine($"Plannings publi�s: {allPublished.Count}");
        foreach (var p in allPublished)
            Console.WriteLine($"  -> Id={p.Id} WeekCode={p.WeekCode} WeekStart={p.WeekStartDate}");

        // Test 2 : assignments pour cet userId
        var assignments = await _context.ShiftAssignments
            .Where(a => a.UserId == userId)
            .ToListAsync();
        Console.WriteLine($"Assignments pour userId={userId}: {assignments.Count}");

        var planning = await _context.WeeklyPlannings
            .Include(p => p.SubService)
            .Include(p => p.ShiftAssignments)
                .ThenInclude(a => a.Shift)
            .Include(p => p.ShiftAssignments)
                .ThenInclude(a => a.SubServiceShiftConfig)
            .Where(p => p.Status == PlanningStatus.Published
                     && p.ShiftAssignments.Any(a => a.UserId == userId))
            .OrderByDescending(p => p.WeekStartDate)
            .FirstOrDefaultAsync();

        Console.WriteLine($"Planning trouv�: {planning?.Id.ToString() ?? "NULL"}");

        if (planning == null) return null;

        return new MyPlanningDto
        {
            WeekCode = planning.WeekCode,
            WeekStartDate = planning.WeekStartDate,
            SubServiceName = planning.SubService.Name,
            Days = planning.ShiftAssignments
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.AssignedDate)
                .Select(a => MapToDayDtoNew(a))
                .ToList()
        };
    }
    // ----------------------------------------------------
    // OVERRIDE MANAGER
    // ----------------------------------------------------
    public async Task<DayAssignmentDto> OverrideShiftAsync(OverrideShiftDto dto)
    {
        var assignment = await _context.ShiftAssignments
            .Include(a => a.Shift)
            .Include(a => a.SubServiceShiftConfig)
            .FirstOrDefaultAsync(a => a.Id == dto.ShiftAssignmentId)
            ?? throw new Exception("Assignment introuvable.");

        // ? TOUJOURS remettre IsHoliday � false lors d'un override
        assignment.IsHoliday = false;
        assignment.IsManagerOverride = true;

        if (dto.NewSubServiceShiftConfigId > 0)
        {
            var config = await _context.SubServiceShiftConfigs
                .FindAsync(dto.NewSubServiceShiftConfigId)
                ?? throw new Exception("Config shift introuvable.");

            assignment.SubServiceShiftConfigId = dto.NewSubServiceShiftConfigId;
            assignment.ShiftId = null;
            assignment.IsOnLeave = false;
            assignment.IsHoliday = false;
            assignment.IsManagerOverride = true;

            // ? Assigner une pause automatiquement
            if (assignment.BreakTime == null)
            {
                var slots = GenerateBreakSlots(config.BreakRangeStart, config.BreakRangeEnd);
                if (slots.Any())
                    assignment.BreakTime = slots.First();
            }

            await _context.SaveChangesAsync();

            await _context.Entry(assignment)
                .Reference(a => a.SubServiceShiftConfig)
                .LoadAsync();

            return MapToDayDtoNew(assignment);
        }
        else
        {
            // ? action = 'off' ? repos (config null, shift null)
            assignment.SubServiceShiftConfigId = null;
            assignment.ShiftId = null;
            assignment.IsOnLeave = false;
            await _context.SaveChangesAsync();

            return MapToDayDtoNew(assignment);
        }
    }
    // ----------------------------------------------------
    // PUBLIER (validation Admin/RH — consultation obligatoire)
    // ----------------------------------------------------
    public async Task RecordConsultationAsync(int planningId, int userId)
    {
        var exists = await _context.WeeklyPlannings.AnyAsync(p => p.Id == planningId);
        if (!exists) throw new InvalidOperationException("Planning introuvable.");

        var existing = await _context.PlanningConsultations
            .FirstOrDefaultAsync(c => c.PlanningId == planningId && c.UserId == userId);

        if (existing != null)
        {
            existing.ConsultedAt = DateTime.UtcNow;
        }
        else
        {
            _context.PlanningConsultations.Add(new PlanningConsultation
            {
                PlanningId = planningId,
                UserId = userId,
                ConsultedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
    }

    public async Task<bool> HasConsultedAsync(int planningId, int userId) =>
        await _context.PlanningConsultations
            .AnyAsync(c => c.PlanningId == planningId && c.UserId == userId);

    public async Task<WeeklyPlanningResponseDto> PublishPlanningAsync(int planningId, int validatorId)
    {
        var planning = await _context.WeeklyPlannings
            .Include(p => p.ShiftAssignments)
            .Include(p => p.SubService)
            .FirstOrDefaultAsync(p => p.Id == planningId);

        if (planning == null) throw new Exception("Planning introuvable");

        if (planning.Status != PlanningStatus.Draft)
            throw new InvalidOperationException("Seuls les brouillons peuvent être validés.");

        var consulted = await HasConsultedAsync(planningId, validatorId);
        if (!consulted)
            throw new InvalidOperationException(
                "Consultation obligatoire avant validation. Ouvrez d'abord le planning.");

        // Anomalies niveau : warning coverage uniquement — ne bloque pas la publication.

        planning.Status = PlanningStatus.Published;
        planning.ValidatedBy = validatorId;
        await _context.SaveChangesAsync();

        var planningUserIds = planning.ShiftAssignments
            .Select(a => a.UserId)
            .Distinct()
            .ToList();

        var users = await _context.Users
            .Where(u => planningUserIds.Contains(u.Id) && u.AuthUserId != null)
            .Select(u => new { u.Id, u.AuthUserId })
            .ToListAsync();

        var message = $"Votre planning {planning.WeekCode} est disponible !";
        var subServiceName = planning.SubService.Name;
        const string deepLink = "/mes-plannings";
        var created = new List<PlanningNotification>();

        foreach (var user in users)
        {
            var notif = new PlanningNotification
            {
                UserId = user.Id,
                AuthUserId = user.AuthUserId!.Value,
                WeeklyPlanningId = planning.Id,
                WeekCode = planning.WeekCode,
                SubServiceName = subServiceName,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.PlanningNotifications.Add(notif);
            created.Add(notif);
        }

        // Persister d'abord pour disposer des Id avant le push SignalR
        await _context.SaveChangesAsync();

        foreach (var notif in created)
        {
            await _hubContext.Clients
                .Group($"user_{notif.AuthUserId}")
                .SendAsync("PlanningPublished", new
                {
                    id = notif.Id,
                    weekCode = notif.WeekCode,
                    subServiceName = notif.SubServiceName,
                    message = notif.Message,
                    weeklyPlanningId = notif.WeeklyPlanningId,
                    deepLink,
                    createdAt = notif.CreatedAt,
                    isRead = false
                });
        }

        return await GetPlanningByIdAsync(planning.Id)
            ?? throw new Exception("Erreur publication planning.");
    }

    // ----------------------------------------------------
    // GROUPES SAMEDI
    // ----------------------------------------------------
    public async Task SetSaturdayGroupAsync(SetSaturdayGroupDto dto)
    {
        var existing = await _context.SaturdayGroups
            .FirstOrDefaultAsync(sg => sg.UserId == dto.UserId);

        if (existing != null)
        {
            existing.GroupNumber = dto.GroupNumber;
            existing.IsNewEmployee = dto.IsNewEmployee;
        }
        else
        {
            _context.SaturdayGroups.Add(new SaturdayGroup
            {
                UserId = dto.UserId,
                GroupNumber = dto.GroupNumber,
                IsNewEmployee = dto.IsNewEmployee
            });
        }
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<object>> GetSaturdayGroupsAsync(int subServiceId)
    {
        var users = await _context.Users
            .Where(u => u.SubServiceId == subServiceId && u.IsActive)
            .ToListAsync();

        var userIds = users.Select(u => u.Id).ToList();
        var groups = await _context.SaturdayGroups
            .Where(sg => userIds.Contains(sg.UserId))
            .ToListAsync();

        return users.Select(u =>
        {
            var g = groups.FirstOrDefault(sg => sg.UserId == u.Id);
            return (object)new
            {
                userId = u.Id,
                fullName = $"{u.FirstName} {u.LastName}",
                groupNumber = g?.GroupNumber ?? 0,
                isNewEmployee = g?.IsNewEmployee ?? false
            };
        });
    }

    // ----------------------------------------------------
    // VUE EMPLOY�
    // ----------------------------------------------------
    public async Task<MyPlanningDto?> GetMyPlanningAsync(int userId, string weekCode)
    {
        var planning = await _context.WeeklyPlannings
            .Include(p => p.SubService)
            .Include(p => p.ShiftAssignments)
                .ThenInclude(a => a.Shift)
            .Include(p => p.ShiftAssignments)
                .ThenInclude(a => a.SubServiceShiftConfig)
            .FirstOrDefaultAsync(p => p.WeekCode == weekCode &&
                                      p.ShiftAssignments.Any(a => a.UserId == userId) &&
                                      p.Status == PlanningStatus.Published);

        if (planning == null) return null;

        return new MyPlanningDto
        {
            WeekCode = planning.WeekCode,
            WeekStartDate = planning.WeekStartDate,
            SubServiceName = planning.SubService.Name,
            Days = planning.ShiftAssignments
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.AssignedDate)
                .Select(a => MapToDayDtoNew(a))
                .ToList()
        };
    }

    public async Task<IEnumerable<MyPlanningDto>> GetMyPlanningHistoryAsync(int userId)
    {
        var plannings = await _context.WeeklyPlannings
            .Include(p => p.SubService)
            .Include(p => p.ShiftAssignments.Where(a => a.UserId == userId))
                .ThenInclude(a => a.Shift)
            .Include(p => p.ShiftAssignments.Where(a => a.UserId == userId))
                .ThenInclude(a => a.SubServiceShiftConfig)
            .Where(p => p.ShiftAssignments.Any(a => a.UserId == userId) &&
                        p.Status == PlanningStatus.Published)
            .OrderByDescending(p => p.WeekStartDate)
            .Take(10)
            .ToListAsync();

        return plannings.Select(p => new MyPlanningDto
        {
            WeekCode = p.WeekCode,
            WeekStartDate = p.WeekStartDate,
            SubServiceName = p.SubService.Name,
            Days = p.ShiftAssignments
                .OrderBy(a => a.AssignedDate)
                .Select(a => MapToDayDtoNew(a))
                .ToList()
        });
    }

    // ----------------------------------------------------
    // NOTIFICATIONS DE PUBLICATION (persist�es)
    // ----------------------------------------------------
    public async Task<IEnumerable<PlanningNotificationDto>> GetMyNotificationsAsync(int authUserId)
    {
        var rows = await _context.PlanningNotifications
            .Where(n => n.AuthUserId == authUserId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync();

        return rows.Select(n => new PlanningNotificationDto
        {
            Id = n.Id,
            WeeklyPlanningId = n.WeeklyPlanningId,
            WeekCode = n.WeekCode,
            SubServiceName = n.SubServiceName,
            Message = n.Message,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt,
            DeepLink = ResolveNotificationDeepLink(n.WeekCode, n.SubServiceName, n.Message)
        });
    }

    /// <summary>Route SPA dérivée du type de notification (planning / formation / demande).</summary>
    public static string ResolveNotificationDeepLink(string weekCode, string subServiceName, string message)
    {
        var sub = (subServiceName ?? string.Empty).Trim();
        var code = (weekCode ?? string.Empty).ToUpperInvariant();

        if (sub.Contains("Demande", StringComparison.OrdinalIgnoreCase))
        {
            if ((message ?? string.Empty).Contains("Nouvelle demande", StringComparison.OrdinalIgnoreCase))
                return "/planning/change-requests";
            return "/mes-plannings";
        }

        if (code.StartsWith("TRAINING-ANIM-", StringComparison.Ordinal)
            || code.StartsWith("TRAINING-START-ANIM-", StringComparison.Ordinal))
            return "/mes-sessions";

        if (code.StartsWith("TRAIN-", StringComparison.Ordinal)
            || code.StartsWith("TRAINING-", StringComparison.Ordinal)
            || sub.Contains("Formation", StringComparison.OrdinalIgnoreCase))
            return "/mes-formations";

        return "/mes-plannings";
    }

    public async Task MarkNotificationReadAsync(int id, int authUserId)
    {
        var notif = await _context.PlanningNotifications
            .FirstOrDefaultAsync(n => n.Id == id && n.AuthUserId == authUserId);
        if (notif is null || notif.IsRead) return;

        notif.IsRead = true;
        notif.ReadAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task MarkAllNotificationsReadAsync(int authUserId)
    {
        var unread = await _context.PlanningNotifications
            .Where(n => n.AuthUserId == authUserId && !n.IsRead)
            .ToListAsync();
        if (unread.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = now;
        }
        await _context.SaveChangesAsync();
    }

    // ----------------------------------------------------
    // GET PLANNINGS
    // ----------------------------------------------------
    public async Task<WeeklyPlanningResponseDto?> GetPlanningByIdAsync(int id)
    {
        var planning = await _context.WeeklyPlannings
            .Include(p => p.SubService)
            .Include(p => p.WeeklyShiftConfigs).ThenInclude(c => c.Shift)
            .Include(p => p.ShiftAssignments).ThenInclude(a => a.Shift)
            .Include(p => p.ShiftAssignments).ThenInclude(a => a.SubServiceShiftConfig)
            .Include(p => p.ShiftAssignments).ThenInclude(a => a.User)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (planning == null) return null;

        var comments = await _context.PlanningComments
            .Where(c => c.WeeklyPlanningId == id)
            .ToListAsync();

        // ? AJOUTER � charger les cong�s de la semaine
        var userIds = planning.ShiftAssignments.Select(a => a.UserId).Distinct().ToList();
        var weekEnd = planning.WeekStartDate.AddDays(6);
        var conges = await _context.Conges
            .Where(c => userIds.Contains(c.UserId)
                     && c.Status == CongeStatus.Approved
                     && c.StartDate <= weekEnd
                     && c.EndDate >= planning.WeekStartDate)
            .ToListAsync();

        var subConfigs = await _context.SubServiceShiftConfigs
            .Where(c => c.SubServiceId == planning.SubServiceId && c.WeekCode == planning.WeekCode)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();

        List<ShiftConfigResponseDto> shiftConfigs;
        if (planning.WeeklyShiftConfigs.Count > 0)
        {
            shiftConfigs = planning.WeeklyShiftConfigs.Select(c => new ShiftConfigResponseDto
            {
                ShiftId = c.ShiftId,
                ShiftLabel = c.Shift.Label,
                StartTime = c.Shift.StartTime.ToString("HH:mm"),
                RequiredCount = c.RequiredCount,
                Percentage = c.Percentage
            }).ToList();
        }
        else
        {
            var totalRequired = subConfigs.Sum(c => c.RequiredCount);
            shiftConfigs = subConfigs.Select(c => new ShiftConfigResponseDto
            {
                ShiftId = c.Id,
                ShiftLabel = c.Label,
                StartTime = c.StartTime.ToString("HH:mm"),
                ShiftKind = c.ShiftKind.ToString(),
                RequiredCount = c.RequiredCount,
                Percentage = totalRequired > 0
                    ? Math.Round((decimal)c.RequiredCount / totalRequired * 100, 1)
                    : 0
            }).ToList();
        }

        var usersForCoverage = planning.ShiftAssignments
            .Select(a => a.User)
            .Where(u => u != null)
            .GroupBy(u => u!.Id)
            .ToDictionary(g => g.Key, g => g.First()!);
        var coverage = BuildCoverageReport(planning, subConfigs, usersForCoverage);

        return new WeeklyPlanningResponseDto
        {
            Id = planning.Id,
            SubServiceId = planning.SubServiceId,
            WeekCode = planning.WeekCode,
            WeekStartDate = planning.WeekStartDate,
            Status = planning.Status.ToString(),
            TotalEffectif = planning.TotalEffectif,
            SaturdayGroupId = planning.SaturdayGroupId,
            SubServiceName = planning.SubService.Name,
            ShiftConfigs = shiftConfigs,
            CoverageReport = coverage,
            Assignments = planning.ShiftAssignments
                .GroupBy(a => a.UserId)
                .Select(g => new EmployeePlanningDto
                {
                    UserId = g.Key,
                    FullName = $"{g.First().User.FirstName} {g.First().User.LastName}",
                    IsNewEmployee = g.First().IsNewEmployee,
                    Level = g.First().User.Level,
                    ManagerComment = comments.FirstOrDefault(c => c.UserId == g.Key)?.Comment,
                    Days = g.OrderBy(a => a.AssignedDate)
                             .Select(a => MapToDayDtoNew(a, conges))
                             .ToList()
                }).ToList()
        };
    }

    public async Task<IEnumerable<WeeklyPlanningResponseDto>> GetPlanningsBySubServiceAsync(
        int subServiceId)
    {
        var ids = await _context.WeeklyPlannings
            .Where(p => p.SubServiceId == subServiceId)
            .OrderByDescending(p => p.WeekStartDate)
            .Select(p => p.Id)
            .ToListAsync();

        var result = new List<WeeklyPlanningResponseDto>();
        foreach (var id in ids)
        {
            var dto = await GetPlanningByIdAsync(id);
            if (dto != null) result.Add(dto);
        }
        return result;
    }

    public async Task DeletePlanningAsync(int id)
    {
        var planning = await _context.WeeklyPlannings
            .Include(p => p.ShiftAssignments)
            .Include(p => p.WeeklyShiftConfigs)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new Exception("Planning introuvable.");

        _context.ShiftAssignments.RemoveRange(planning.ShiftAssignments);
        _context.WeeklyShiftConfigs.RemoveRange(planning.WeeklyShiftConfigs);
        _context.WeeklyPlannings.Remove(planning);

        await _context.SaveChangesAsync();
    }

    // ----------------------------------------------------
    // OVERRIDE SAMEDI
    // ----------------------------------------------------
    public async Task<DayAssignmentDto> OverrideSaturdayShiftAsync(OverrideSaturdayDto dto)
    {
        ShiftAssignment assignment;

        if (dto.ShiftAssignmentId > 0)
        {
            assignment = await _context.ShiftAssignments
                .Include(a => a.SubServiceShiftConfig)
                .FirstOrDefaultAsync(a => a.Id == dto.ShiftAssignmentId)
                ?? throw new Exception("Assignment introuvable.");

            assignment.SubServiceShiftConfigId = dto.NewSubServiceShiftConfigId;
            assignment.IsManagerOverride = true;
        }
        else
        {
            if (dto.WeeklyPlanningId == 0 || dto.UserId == 0)
                throw new Exception(
                    "WeeklyPlanningId et UserId sont requis pour cr�er une assignation samedi.");

            var planning = await _context.WeeklyPlannings
                .FirstOrDefaultAsync(p => p.Id == dto.WeeklyPlanningId)
                ?? throw new Exception("Planning introuvable.");

            var existing = await _context.ShiftAssignments
                .FirstOrDefaultAsync(a =>
                    a.WeeklyPlanningId == dto.WeeklyPlanningId &&
                    a.UserId == dto.UserId &&
                    a.IsSaturday);

            if (existing != null)
            {
                existing.SubServiceShiftConfigId = dto.NewSubServiceShiftConfigId;
                existing.IsOnLeave = false;
                existing.IsManagerOverride = true;
                assignment = existing;
            }
            else
            {
                var saturdayDate = planning.WeekStartDate.AddDays(5);

                assignment = new ShiftAssignment
                {
                    WeeklyPlanningId = dto.WeeklyPlanningId,
                    UserId = dto.UserId,
                    SubServiceShiftConfigId = dto.NewSubServiceShiftConfigId,
                    AssignedDate = saturdayDate,
                    DayOfWeek = DayOfWeekEnum.Saturday,
                    IsSaturday = true,
                    IsOnLeave = false,
                    IsManagerOverride = true,
                    IsNewEmployee = false,
                    IsHalfDaySaturday = false,
                    SaturdaySlot = 0
                };
                _context.ShiftAssignments.Add(assignment);
            }
        }

        await _context.SaveChangesAsync();

        // Recharger la config pour avoir label/heure/plage pause
        await _context.Entry(assignment)
            .Reference(a => a.SubServiceShiftConfig)
            .LoadAsync();

        // FIX � Assigner une pause si elle n existe pas encore
        if (assignment.SubServiceShiftConfig != null && assignment.BreakTime == null)
        {
            var slots = GenerateBreakSlots(
                assignment.SubServiceShiftConfig.BreakRangeStart,
                assignment.SubServiceShiftConfig.BreakRangeEnd);

            if (slots.Any())
            {
                assignment.BreakTime = slots.First();
                await _context.SaveChangesAsync();
            }
        }

        return MapToDayDtoNew(assignment);
    }

    // ----------------------------------------------------
    // COMMENTAIRES MANAGER
    // ----------------------------------------------------
    public async Task<PlanningCommentDto> SaveCommentAsync(SavePlanningCommentDto dto)
    {
        var planning = await _context.WeeklyPlannings.FindAsync(dto.WeeklyPlanningId)
            ?? throw new Exception("Planning introuvable.");

        if (planning.Status != PlanningStatus.Draft)
            throw new InvalidOperationException(
                "Impossible d'ajouter un commentaire sur un planning publi�.");

        var existing = await _context.PlanningComments
            .FirstOrDefaultAsync(c =>
                c.WeeklyPlanningId == dto.WeeklyPlanningId &&
                c.UserId == dto.UserId);

        if (existing != null)
        {
            existing.Comment = dto.Comment;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new PlanningComment
            {
                WeeklyPlanningId = dto.WeeklyPlanningId,
                UserId = dto.UserId,
                Comment = dto.Comment,
                CreatedBy = dto.CreatedBy,
                CreatedAt = DateTime.UtcNow
            };
            _context.PlanningComments.Add(existing);
        }

        await _context.SaveChangesAsync();

        var user = await _context.Users.FindAsync(dto.UserId);
        return new PlanningCommentDto
        {
            Id = existing.Id,
            UserId = existing.UserId,
            FullName = user != null ? $"{user.FirstName} {user.LastName}" : "",
            Comment = existing.Comment,
            CreatedAt = existing.CreatedAt,
            UpdatedAt = existing.UpdatedAt
        };
    }

    public async Task DeleteCommentAsync(int planningId, int userId)
    {
        var comment = await _context.PlanningComments
            .FirstOrDefaultAsync(c =>
                c.WeeklyPlanningId == planningId &&
                c.UserId == userId);

        if (comment != null)
        {
            _context.PlanningComments.Remove(comment);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<PlanningCommentDto>> GetCommentsAsync(int planningId)
    {
        return await _context.PlanningComments
            .Include(c => c.User)
            .Where(c => c.WeeklyPlanningId == planningId)
            .Select(c => new PlanningCommentDto
            {
                Id = c.Id,
                UserId = c.UserId,
                FullName = $"{c.User.FirstName} {c.User.LastName}",
                Comment = c.Comment,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync();
    }

    // ----------------------------------------------------
    // HELPERS — génération samedi (niveau contractuel)
    // ----------------------------------------------------
    private async Task<ShiftAssignment?> GenerateSaturdayAssignmentFromConfigAsync(
        User employee,
        WeeklyPlanning planning,
        List<SubServiceShiftConfig> shiftConfigs,
        List<SaturdayGroup> saturdayGroups,
        int employeeIndex,
        string weekCode,
        Dictionary<int, int> beginnerHalfDaySlotCounts)
    {
        var satGroup = saturdayGroups.FirstOrDefault(sg => sg.UserId == employee.Id);
        var orderedConfigs = shiftConfigs.OrderBy(s => s.StartTime).ThenBy(s => s.DisplayOrder).ToList();

        // Débutant (Level 1) : tous les samedis, demi-journée, créneau auto équitable
        if (IsBeginnerLevel(employee))
        {
            if (orderedConfigs.Count == 0) return null;

            var slot = PickBalancedHalfDaySlot(beginnerHalfDaySlotCounts);
            var shiftConfig = slot == 1 || orderedConfigs.Count == 1
                ? orderedConfigs[0]
                : orderedConfigs[Math.Min(1, orderedConfigs.Count - 1)];

            beginnerHalfDaySlotCounts[slot] = beginnerHalfDaySlotCounts.GetValueOrDefault(slot, 0) + 1;

            return new ShiftAssignment
            {
                WeeklyPlanningId = planning.Id,
                UserId = employee.Id,
                SubServiceShiftConfigId = shiftConfig.Id,
                AssignedDate = planning.WeekStartDate.AddDays(5),
                DayOfWeek = DayOfWeekEnum.Saturday,
                IsSaturday = true,
                IsNewEmployee = true,
                IsHalfDaySaturday = true,
                SaturdaySlot = slot
            };
        }

        var previousWeekCode = GetPreviousWeekCode(weekCode);

        var previousHistory = await _context.SaturdayHistories
            .FirstOrDefaultAsync(h =>
                h.UserId == employee.Id &&
                h.WeekCode == previousWeekCode &&
                h.SubServiceId == planning.SubServiceId);

        bool workedLastSaturday = previousHistory?.WorkedSaturday ?? false;
        bool worksThisSaturday = !workedLastSaturday;

        if (previousHistory == null && satGroup != null)
            worksThisSaturday = satGroup.GroupNumber == planning.SaturdayGroupId;

        if (!worksThisSaturday) return null;

        var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(
            planning.WeekStartDate.ToDateTime(TimeOnly.MinValue));
        var shiftIndex = (employeeIndex + weekNumber) % orderedConfigs.Count;

        return new ShiftAssignment
        {
            WeeklyPlanningId = planning.Id,
            UserId = employee.Id,
            SubServiceShiftConfigId = orderedConfigs[shiftIndex].Id,
            AssignedDate = planning.WeekStartDate.AddDays(5),
            DayOfWeek = DayOfWeekEnum.Saturday,
            IsSaturday = true,
            IsNewEmployee = false,
            IsHalfDaySaturday = false,
            SaturdaySlot = 0
        };
    }

    /// <summary>Choisit le créneau demi-journée le moins chargé (égalité → 1).</summary>
    private static int PickBalancedHalfDaySlot(Dictionary<int, int> counts)
    {
        var c1 = counts.GetValueOrDefault(1, 0);
        var c2 = counts.GetValueOrDefault(2, 0);
        return c1 <= c2 ? 1 : 2;
    }

    private static bool IsBeginnerLevel(User employee) => employee.Level == 1;

    private static string GetPreviousWeekCode(string weekCode)
    {
        var parts = weekCode.Split('-');
        var year = int.Parse(parts[0]);
        var week = int.Parse(parts[1].Replace("W", ""));

        if (week == 1)
            return $"{year - 1}-W52";

        return $"{year}-W{(week - 1):D2}";
    }

    /// <summary>
    /// Ajuste RequiredCount des configs pour que la somme = effectif réel,
    /// en conservant les proportions (et Percentage).
    /// </summary>
    private static void RescaleShiftQuotasToEffectif(
        List<SubServiceShiftConfig> shiftConfigs, int employeeCount)
    {
        if (shiftConfigs.Count == 0 || employeeCount <= 0) return;

        var totalRequired = shiftConfigs.Sum(c => c.RequiredCount);
        if (totalRequired == employeeCount) return;

        if (totalRequired <= 0)
        {
            var baseCount = employeeCount / shiftConfigs.Count;
            var rest = employeeCount % shiftConfigs.Count;
            for (var i = 0; i < shiftConfigs.Count; i++)
            {
                shiftConfigs[i].RequiredCount = baseCount + (i < rest ? 1 : 0);
                shiftConfigs[i].Percentage = employeeCount > 0
                    ? Math.Round((decimal)shiftConfigs[i].RequiredCount / employeeCount * 100, 1)
                    : 0;
            }
            return;
        }

        var allocated = 0;
        for (var i = 0; i < shiftConfigs.Count; i++)
        {
            if (i == shiftConfigs.Count - 1)
            {
                shiftConfigs[i].RequiredCount = Math.Max(0, employeeCount - allocated);
            }
            else
            {
                var share = (int)Math.Floor(
                    (decimal)shiftConfigs[i].RequiredCount / totalRequired * employeeCount);
                shiftConfigs[i].RequiredCount = share;
                allocated += share;
            }
        }

        // Corriger si arrondis laissent un reste non placé sur le dernier (déjà fait)
        // ou trop alloué avant le dernier
        var sum = shiftConfigs.Sum(c => c.RequiredCount);
        if (sum != employeeCount && shiftConfigs.Count > 0)
        {
            shiftConfigs[^1].RequiredCount = Math.Max(
                0,
                shiftConfigs[^1].RequiredCount + (employeeCount - sum));
        }

        foreach (var c in shiftConfigs)
        {
            c.Percentage = employeeCount > 0
                ? Math.Round((decimal)c.RequiredCount / employeeCount * 100, 1)
                : 0;
        }
    }

    private void AssignBreakTimesFromConfig(
        List<ShiftAssignment> dayAssignments,
        List<SubServiceShiftConfig> shiftConfigs,
        int totalEmployees)
    {
        if (!dayAssignments.Any()) return;

        // Usage global par créneau (tous shifts confondus) + limite par shift
        var breakSlotUsage = new Dictionary<TimeOnly, int>();
        // Max pauses simultanées au niveau service : basé sur la présence min la plus stricte
        var serviceMinPresence = shiftConfigs.Count > 0
            ? shiftConfigs.Min(c => c.MinPresencePercent <= 0 ? 70 : c.MinPresencePercent)
            : 70;
        serviceMinPresence = Math.Clamp(serviceMinPresence, 50, 95);
        var serviceMaxBreak = Math.Max(1, (int)Math.Floor(totalEmployees * (100 - serviceMinPresence) / 100.0));

        var shiftGroups = dayAssignments
            .GroupBy(a => a.SubServiceShiftConfigId)
            .ToList();

        foreach (var group in shiftGroups)
        {
            var config = shiftConfigs.FirstOrDefault(c => c.Id == group.Key);
            if (config == null) continue;

            var groupSize = group.Count();
            var minPresence = config.MinPresencePercent <= 0 ? 70 : config.MinPresencePercent;
            minPresence = Math.Clamp(minPresence, 50, 95);
            // Présence min PAR SHIFT : max en pause = floor(effectifs_shift * (100 - min) / 100)
            var shiftMaxBreak = Math.Max(0, (int)Math.Floor(groupSize * (100 - minPresence) / 100.0));
            if (shiftMaxBreak == 0 && groupSize > 0)
                shiftMaxBreak = 1; // au moins 1 possible si le groupe est petit

            var slots = GenerateBreakSlots(config.BreakRangeStart, config.BreakRangeEnd);
            var shiftSlotUsage = new Dictionary<TimeOnly, int>();

            foreach (var assignment in group)
            {
                var bestSlot = slots
                    .OrderBy(s => breakSlotUsage.GetValueOrDefault(s, 0))
                    .ThenBy(s => shiftSlotUsage.GetValueOrDefault(s, 0))
                    .FirstOrDefault(s =>
                        shiftSlotUsage.GetValueOrDefault(s, 0) < shiftMaxBreak
                        && breakSlotUsage.GetValueOrDefault(s, 0) < serviceMaxBreak);

                if (bestSlot == default)
                {
                    bestSlot = slots
                        .OrderBy(s => breakSlotUsage.GetValueOrDefault(s, 0))
                        .ThenBy(s => shiftSlotUsage.GetValueOrDefault(s, 0))
                        .First();
                }

                assignment.BreakTime = bestSlot;
                shiftSlotUsage[bestSlot] = shiftSlotUsage.GetValueOrDefault(bestSlot, 0) + 1;
                breakSlotUsage[bestSlot] = breakSlotUsage.GetValueOrDefault(bestSlot, 0) + 1;
            }
        }
    }

    private static CoverageReportDto BuildCoverageReport(
        WeeklyPlanning planning,
        List<SubServiceShiftConfig> shiftConfigs,
        IReadOnlyDictionary<int, User>? usersById = null)
    {
        var report = new CoverageReportDto();
        if (shiftConfigs.Count == 0)
            return report;

        usersById ??= planning.ShiftAssignments
            .Where(a => a.User != null)
            .GroupBy(a => a.UserId)
            .ToDictionary(g => g.Key, g => g.First().User);

        var levelAnomalies = LevelBalanceEvaluator.Evaluate(
            planning.ShiftAssignments, shiftConfigs, usersById, usersById?.Values.ToList());
        report.LevelBalanceAnomalies = levelAnomalies;
        report.HasLevelBalanceAnomaly = levelAnomalies.Count > 0;
        foreach (var a in levelAnomalies)
            report.Warnings.Add(a.Message);

        var anomalyKeys = levelAnomalies
            .Select(a => (a.Date, a.ShiftConfigId))
            .ToHashSet();
        var anomalyDates = levelAnomalies.Select(a => a.Date).ToHashSet();

        var dayNames = new[]
        {
            "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"
        };

        for (var i = 0; i < 6; i++)
        {
            var date = planning.WeekStartDate.AddDays(i);
            var dayName = dayNames[i];
            var daySynth = new DaySynthesisDto { Date = date, Day = dayName };

            daySynth.LeaveCount = planning.ShiftAssignments.Count(a =>
                a.AssignedDate == date && a.IsOnLeave);
            daySynth.HolidayCount = planning.ShiftAssignments.Count(a =>
                a.AssignedDate == date && a.IsHoliday);

            var dayPresent = planning.ShiftAssignments
                .Where(a =>
                    a.AssignedDate == date
                    && a.SubServiceShiftConfigId != null
                    && !a.IsOnLeave
                    && !a.IsHoliday)
                .ToList();
            daySynth.PresentCount = dayPresent.Count;

            if (i == 5)
            {
                daySynth.SaturdayBeginners = dayPresent.Count(a =>
                    usersById != null
                    && usersById.TryGetValue(a.UserId, out var u)
                    && u.Level == 1);
                daySynth.SaturdaySeniors = dayPresent.Count(a =>
                    usersById != null
                    && usersById.TryGetValue(a.UserId, out var u)
                    && u.Level >= 2);
            }

            foreach (var cfg in shiftConfigs.OrderBy(c => c.DisplayOrder))
            {
                var dayAssignments = planning.ShiftAssignments
                    .Where(a =>
                        a.AssignedDate == date
                        && a.SubServiceShiftConfigId == cfg.Id
                        && !a.IsOnLeave
                        && !a.IsHoliday)
                    .ToList();

                var assigned = dayAssignments.Count;
                var beginnerCount = dayAssignments.Count(a =>
                    usersById != null
                    && usersById.TryGetValue(a.UserId, out var u)
                    && u.Level == 1);
                var seniorCount = dayAssignments.Count(a =>
                    usersById != null
                    && usersById.TryGetValue(a.UserId, out var u)
                    && u.Level >= 2);

                var staffingPct = cfg.RequiredCount > 0
                    ? Math.Round((decimal)assigned / cfg.RequiredCount * 100, 1)
                    : 100m;

                var understaffed = i < 5 && cfg.RequiredCount > 0 && assigned < cfg.RequiredCount;

                var minPresence = cfg.MinPresencePercent <= 0 ? 70 : Math.Clamp(cfg.MinPresencePercent, 50, 100);
                var presenceIssue = false;
                if (assigned > 1 && i < 5)
                {
                    var maxBreakAllowed = (int)Math.Floor(assigned * (100 - minPresence) / 100.0);
                    if (maxBreakAllowed == 0)
                        maxBreakAllowed = 1;
                    presenceIssue = dayAssignments
                        .Where(a => a.BreakTime.HasValue)
                        .GroupBy(a => a.BreakTime!.Value)
                        .Any(g => g.Count() > maxBreakAllowed);
                }

                var hasLevel = anomalyKeys.Contains((date, cfg.Id))
                               || (i == 5 && anomalyDates.Contains(date));
                var isUnder = understaffed || presenceIssue;

                report.Items.Add(new CoverageDayShiftDto
                {
                    Date = date,
                    Day = dayName,
                    ShiftConfigId = cfg.Id,
                    ShiftLabel = cfg.Label,
                    ShiftKind = cfg.ShiftKind.ToString(),
                    RequiredCount = cfg.RequiredCount,
                    AssignedCount = assigned,
                    MinPresencePercent = minPresence,
                    PresencePercent = staffingPct,
                    IsUnderstaffed = isUnder,
                    HasLevelBalanceAnomaly = hasLevel,
                });

                daySynth.Shifts.Add(new DaySynthesisShiftDto
                {
                    ShiftConfigId = cfg.Id,
                    ShiftLabel = cfg.Label,
                    ShiftKind = cfg.ShiftKind.ToString(),
                    AssignedCount = assigned,
                    RequiredCount = cfg.RequiredCount,
                    Delta = assigned - cfg.RequiredCount,
                    BeginnerCount = beginnerCount,
                    SeniorCount = seniorCount,
                    IsUnderstaffed = isUnder,
                    HasLevelBalanceAnomaly = hasLevel
                });

                if (isUnder || hasLevel)
                    daySynth.HasAnyAnomaly = true;

                if (understaffed)
                {
                    report.HasUnderstaffing = true;
                    report.Warnings.Add(
                        $"{dayName} {date:dd/MM} — {cfg.Label}: {assigned}/{cfg.RequiredCount} affectés (quota)");
                }
                else if (presenceIssue)
                {
                    report.HasUnderstaffing = true;
                    report.Warnings.Add(
                        $"{dayName} {date:dd/MM} — {cfg.Label}: trop de pauses simultanées (présence min {minPresence} %)");
                }
            }

            report.DaySynthesis.Add(daySynth);
        }

        return report;
    }

    private static List<TimeOnly> GenerateBreakSlots(TimeOnly rangeStart, TimeOnly rangeEnd)
    {
        var slots = new List<TimeOnly>();
        var current = rangeStart;

        while (current < rangeEnd)
        {
            slots.Add(current);
            current = current.AddMinutes(30);
        }

        if (!slots.Any()) slots.Add(rangeStart);
        return slots;
    }

    private static DayAssignmentDto MapToDayDtoNew(
     ShiftAssignment a,
     List<Conge>? conges = null) // ? param�tre optionnel
    {
        var label = a.IsHoliday ? "F�RI�"
                  : a.IsOnLeave ? "CONG�"
                  : a.SubServiceShiftConfig?.Label
                    ?? a.Shift?.Label
                    ?? "�";

        var startTime = a.SubServiceShiftConfig?.StartTime.ToString("HH:mm")
                        ?? a.Shift?.StartTime.ToString("HH:mm")
                        ?? "";

        var endTime = a.SubServiceShiftConfig?.EndTime.ToString("HH:mm") ?? "";

        // ? Trouver le type d'absence
        string? absenceType = null;
        if (a.IsOnLeave && conges != null)
        {
            var conge = conges.FirstOrDefault(c =>
                c.UserId == a.UserId &&
                c.StartDate <= a.AssignedDate &&
                c.EndDate >= a.AssignedDate);
            absenceType = conge?.AbsenceType.ToString();
        }

        return new DayAssignmentDto
        {
            AssignmentId = a.Id,
            Day = a.DayOfWeek.ToString(),
            AssignedDate = a.AssignedDate,
            ShiftLabel = label,
            StartTime = startTime,
            EndTime = endTime,
            IsSaturday = a.IsSaturday,
            IsManagerOverride = a.IsManagerOverride,
            BreakTime = a.BreakTime?.ToString("HH:mm"),
            IsOnLeave = a.IsOnLeave,
            AbsenceType = absenceType, // ? NOUVEAU
            IsHalfDaySaturday = a.IsHalfDaySaturday,
            SaturdaySlot = a.SaturdaySlot,
            SlotLabel = a.SaturdaySlot == 1 ? "8h00-12h00"
                              : a.SaturdaySlot == 2 ? "12h00-16h00"
                              : string.Empty,
            IsHoliday = a.IsHoliday,
            HolidayName = a.IsHoliday
                ? FrenchHolidayHelper.GetHolidayName(a.AssignedDate)
                : string.Empty
        };
    }

    // ----------------------------------------------------
    // HELPERS � ANCIEN SYST�ME
    // ----------------------------------------------------
    private static DayAssignmentDto MapToDayDto(ShiftAssignment a, Shift? shift) => new()
    {
        AssignmentId = a.Id,
        Day = a.DayOfWeek.ToString(),
        AssignedDate = a.AssignedDate,
        ShiftLabel = shift?.Label ?? "CONG�",
        StartTime = shift?.StartTime.ToString("HH:mm") ?? "",
        IsSaturday = a.IsSaturday,
        IsManagerOverride = a.IsManagerOverride,
        BreakTime = a.BreakTime?.ToString("HH:mm"),
        IsOnLeave = a.IsOnLeave,
        IsHalfDaySaturday = a.IsHalfDaySaturday,
        SaturdaySlot = a.SaturdaySlot,
        SlotLabel = a.SaturdaySlot == 1 ? "8h00-12h00"
                          : a.SaturdaySlot == 2 ? "12h00-16h00"
                          : string.Empty
    };

    private static ShiftConfigResponseNewDto MapToShiftConfigResponseDto(
        SubServiceShiftConfig c) => new()
        {
            Id = c.Id,
            Label = c.Label,
            StartTime = c.StartTime.ToString("HH:mm"),
            EndTime = c.EndTime.ToString("HH:mm"),
            WorkHours = c.WorkHours,
            BreakRangeStart = c.BreakRangeStart.ToString("HH:mm"),
            BreakRangeEnd = c.BreakRangeEnd.ToString("HH:mm"),
            BreakDurationMinutes = c.BreakDurationMinutes,
            RequiredCount = c.RequiredCount,
            Percentage = c.Percentage,
            MinPresencePercent = c.MinPresencePercent,
            DisplayOrder = c.DisplayOrder,
            ShiftKind = c.ShiftKind.ToString()
        };

    private List<int> GetEmployeeWeekRotation(
        List<Shift> shifts, int employeeIndex, List<int> recentShiftIds)
    {
        var shiftUsageCount = shifts.ToDictionary(
            s => s.Id,
            s => recentShiftIds.Count(r => r == s.Id));

        var sortedShifts = shifts
            .OrderBy(s => shiftUsageCount[s.Id])
            .ThenBy(s => s.StartTime)
            .ToList();

        var rotation = new List<int>();
        var shiftCount = sortedShifts.Count;
        var offset = employeeIndex % shiftCount;

        for (int day = 0; day < 5; day++)
            rotation.Add(sortedShifts[(day + offset) % shiftCount].Id);

        return rotation;
    }

    private List<WeeklyShiftConfig> CalculateShiftQuotas(
        List<Shift> shifts, int totalEffectif, int planningId)
    {
        var configs = new List<WeeklyShiftConfig>();
        var baseCount = totalEffectif / shifts.Count;
        var remainder = totalEffectif % shifts.Count;

        for (int i = 0; i < shifts.Count; i++)
        {
            var count = baseCount + (i < remainder ? 1 : 0);
            configs.Add(new WeeklyShiftConfig
            {
                WeeklyPlanningId = planningId,
                ShiftId = shifts[i].Id,
                RequiredCount = count,
                Percentage = totalEffectif > 0
                    ? Math.Round((decimal)count / totalEffectif * 100, 1) : 0
            });
        }
        return configs;
    }

    private ShiftAssignment? GenerateSaturdayAssignment(
        User employee, WeeklyPlanning planning,
        List<Shift> shifts, List<SaturdayGroup> saturdayGroups, int employeeIndex,
        Dictionary<int, int> beginnerHalfDaySlotCounts)
    {
        var satGroup = saturdayGroups.FirstOrDefault(sg => sg.UserId == employee.Id);
        var orderedShifts = shifts.OrderBy(s => s.StartTime).ToList();

        if (IsBeginnerLevel(employee))
        {
            if (orderedShifts.Count == 0) return null;

            var slot = PickBalancedHalfDaySlot(beginnerHalfDaySlotCounts);
            var shiftId = slot == 1 || orderedShifts.Count == 1
                ? orderedShifts[0].Id
                : orderedShifts[Math.Min(1, orderedShifts.Count - 1)].Id;

            beginnerHalfDaySlotCounts[slot] = beginnerHalfDaySlotCounts.GetValueOrDefault(slot, 0) + 1;

            return new ShiftAssignment
            {
                WeeklyPlanningId = planning.Id,
                UserId = employee.Id,
                ShiftId = shiftId,
                AssignedDate = planning.WeekStartDate.AddDays(5),
                DayOfWeek = DayOfWeekEnum.Saturday,
                IsSaturday = true,
                IsNewEmployee = true,
                IsHalfDaySaturday = true,
                SaturdaySlot = slot
            };
        }

        if (satGroup == null) return null;
        if (satGroup.GroupNumber != planning.SaturdayGroupId) return null;

        var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(
            planning.WeekStartDate.ToDateTime(TimeOnly.MinValue));
        var shiftIndex = (employeeIndex + weekNumber) % orderedShifts.Count;

        return new ShiftAssignment
        {
            WeeklyPlanningId = planning.Id,
            UserId = employee.Id,
            ShiftId = orderedShifts[shiftIndex].Id,
            AssignedDate = planning.WeekStartDate.AddDays(5),
            DayOfWeek = DayOfWeekEnum.Saturday,
            IsSaturday = true,
            IsNewEmployee = false,
            IsHalfDaySaturday = false,
            SaturdaySlot = 0
        };
    }

    private void AssignBreakTimes(
        List<ShiftAssignment> dayAssignments, List<Shift> shifts, int totalEmployees)
    {
        if (!dayAssignments.Any()) return;
        int maxSimultaneous = Math.Max(1, (int)Math.Floor(totalEmployees * 0.30));
        var breakSlotUsage = new Dictionary<TimeOnly, int>();

        foreach (var group in dayAssignments.GroupBy(a => a.ShiftId))
        {
            var shift = shifts.First(s => s.Id == group.Key);
            var slots = GetBreakSlots(shift.StartTime);

            foreach (var assignment in group)
            {
                var bestSlot = slots
                    .OrderBy(slot => breakSlotUsage.GetValueOrDefault(slot, 0))
                    .First(slot => breakSlotUsage.GetValueOrDefault(slot, 0) < maxSimultaneous);

                assignment.BreakTime = bestSlot;
                breakSlotUsage[bestSlot] = breakSlotUsage.GetValueOrDefault(bestSlot, 0) + 1;
            }
        }
    }

    private static List<TimeOnly> GetBreakSlots(TimeOnly shiftStart)
    {
        var slots = new List<TimeOnly>();
        var earliest = shiftStart.AddHours(4);
        for (int h = 0; h < 4; h++)
            slots.Add(earliest.AddHours(h));
        return slots;
    }

    private async Task ValidatePlanningInputsAsync(int subServiceId, DateOnly weekStartDate, int totalEffectif)
    {
        if (weekStartDate.DayOfWeek != DayOfWeek.Monday)
            throw new InvalidOperationException("La semaine doit commencer un lundi.");

        if (totalEffectif <= 0)
            throw new InvalidOperationException("L'effectif total doit �tre sup�rieur � 0.");

        var subServiceExists = await _context.SubServices.AnyAsync(s => s.Id == subServiceId);
        if (!subServiceExists)
            throw new InvalidOperationException("Service introuvable.");

        var employeeCount = await _context.Users
            .CountAsync(u => u.SubServiceId == subServiceId && u.IsActive);

        if (employeeCount == 0)
            throw new InvalidOperationException("Ce service n'a aucun employ� actif.");

        if (totalEffectif > employeeCount)
            throw new InvalidOperationException(
                $"L'effectif ne peut pas d�passer {employeeCount} (employ�s actifs du service).");
    }

    private static int GetSaturdayGroupForWeek(DateOnly weekStart)
    {
        var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(
            weekStart.ToDateTime(TimeOnly.MinValue));
        return weekNumber % 2 == 0 ? 1 : 2;
    }

    private static List<(DayOfWeekEnum day, DateOnly date)> GetWeekDays(DateOnly weekStart)
        => new()
        {
            (DayOfWeekEnum.Monday,    weekStart),
            (DayOfWeekEnum.Tuesday,   weekStart.AddDays(1)),
            (DayOfWeekEnum.Wednesday, weekStart.AddDays(2)),
            (DayOfWeekEnum.Thursday,  weekStart.AddDays(3)),
            (DayOfWeekEnum.Friday,    weekStart.AddDays(4)),
        };

    public async Task<IReadOnlyList<WeeklyPlanningResponseDto>> GetEquipePlanningsByAuthUserIdAsync(int authUserId)
    {
        var manager = await _context.Users
            .Include(u => u.ManagedSubServices)
            .Include(u => u.ManagedServices)
            .FirstOrDefaultAsync(u => u.AuthUserId == authUserId);

        if (manager is null)
            return [];

        var subServiceIds = manager.ManagedSubServices
            .Select(s => s.SubServiceId)
            .ToList();

        var serviceIds = manager.ManagedServices
            .Select(s => s.ServiceId)
            .ToList();

        var subServicesFromServices = await _context.SubServices
            .Where(ss => serviceIds.Contains(ss.ServiceId))
            .Select(ss => ss.Id)
            .ToListAsync();

        subServiceIds = subServiceIds
            .Union(subServicesFromServices)
            .Distinct()
            .ToList();

        if (subServiceIds.Count == 0)
            return [];

        var planningIds = await _context.WeeklyPlannings
            .Where(p => subServiceIds.Contains(p.SubServiceId)
                        && p.Status == PlanningStatus.Published)
            .OrderByDescending(p => p.WeekStartDate)
            .Take(10)
            .Select(p => p.Id)
            .ToListAsync();

        var result = new List<WeeklyPlanningResponseDto>();
        foreach (var id in planningIds)
        {
            var dto = await GetPlanningByIdAsync(id);
            if (dto is not null)
                result.Add(dto);
        }

        return result;
    }

    // ----------------------------------------------------
    // VUE SEMAINE + AUTO-GÉNÉRATION
    // ----------------------------------------------------
    public async Task<PlanningWeekListDto> GetWeekOverviewAsync(string weekCode, int? viewerUserId = null)
    {
        var weekStart = ParseWeekCodeToMonday(weekCode);
        var subServices = await _context.SubServices
            .Include(s => s.Service)
            .OrderBy(s => s.Name)
            .ToListAsync();

        var plannings = await _context.WeeklyPlannings
            .Where(p => p.WeekCode == weekCode)
            .ToListAsync();

        var templateSubIds = await _context.SubServiceShiftConfigs
            .Where(c => c.IsTemplate)
            .Select(c => c.SubServiceId)
            .Distinct()
            .ToListAsync();

        var consultedIds = new HashSet<int>();
        if (viewerUserId.HasValue)
        {
            var planningIds = plannings.Select(p => p.Id).ToList();
            consultedIds = (await _context.PlanningConsultations
                .Where(c => c.UserId == viewerUserId.Value && planningIds.Contains(c.PlanningId))
                .Select(c => c.PlanningId)
                .ToListAsync()).ToHashSet();
        }

        var items = new List<PlanningWeekItemDto>();
        foreach (var ss in subServices)
        {
            var planning = plannings.FirstOrDefault(p => p.SubServiceId == ss.Id);
            var hasTemplate = templateSubIds.Contains(ss.Id);
            // SubService = plus petite entité orga (= « service » métier pour le planning)
            var orgLabel = ss.Service != null
                ? $"{ss.Name} · {ss.Service.Name}"
                : ss.Name;

            items.Add(new PlanningWeekItemDto
            {
                SubServiceId = ss.Id,
                SubServiceName = ss.Name,
                OrgLabel = orgLabel,
                PlanningId = planning?.Id,
                Status = planning?.Status.ToString(),
                TotalEffectif = planning?.TotalEffectif ?? 0,
                HasTemplate = hasTemplate,
                CoverageOk = true,
                HasConsulted = planning != null && consultedIds.Contains(planning.Id)
            });
        }

        return new PlanningWeekListDto
        {
            WeekCode = weekCode,
            WeekStartDate = weekStart,
            Items = items
        };
    }

    public async Task<AutoGenerateSettingsDto> GetAutoGenerateSettingsAsync()
    {
        var settings = await EnsureAutoGenerateSettingsEntityAsync();
        return MapAutoGenerateSettings(settings);
    }

    public async Task<AutoGenerateSettingsDto> SaveAutoGenerateSettingsAsync(
        AutoGenerateSettingsDto dto, int? updatedByUserId)
    {
        var settings = await EnsureAutoGenerateSettingsEntityAsync();
        settings.Enabled = dto.Enabled;
        settings.DayOfWeek = dto.DayOfWeek;
        settings.HourLocal = Math.Clamp(dto.HourLocal, 0, 23);
        settings.MinuteLocal = Math.Clamp(dto.MinuteLocal, 0, 59);
        settings.TimeZone = string.IsNullOrWhiteSpace(dto.TimeZone) ? "Africa/Casablanca" : dto.TimeZone.Trim();
        settings.Target = string.Equals(dto.Target, "CurrentWeek", StringComparison.OrdinalIgnoreCase)
            ? "CurrentWeek"
            : "NextWeek";
        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedByUserId = updatedByUserId;
        await _context.SaveChangesAsync();
        return MapAutoGenerateSettings(settings);
    }

    public async Task<AutoGenerateWeekResultDto> AutoGenerateWeekAsync(
        string? weekCode = null, bool forceDraftRefresh = false)
    {
        DateOnly weekStart;
        string code;
        if (!string.IsNullOrWhiteSpace(weekCode))
        {
            code = weekCode!;
            weekStart = ParseWeekCodeToMonday(code);
        }
        else
        {
            var settings = await EnsureAutoGenerateSettingsEntityAsync();
            weekStart = GetTargetWeekMonday(settings.Target);
            code = FormatWeekCode(weekStart);
        }

        var result = new AutoGenerateWeekResultDto { WeekCode = code };

        var templateSubIds = await _context.SubServiceShiftConfigs
            .Where(c => c.IsTemplate)
            .Select(c => c.SubServiceId)
            .Distinct()
            .ToListAsync();

        var allSubs = await _context.SubServices.Select(s => s.Id).ToListAsync();
        foreach (var subId in allSubs.Where(id => !templateSubIds.Contains(id)))
        {
            result.Skipped++;
            result.Messages.Add($"SubService {subId}: config shifts manquante (template).");
        }

        foreach (var subId in templateSubIds)
        {
            try
            {
                var existing = await _context.WeeklyPlannings
                    .FirstOrDefaultAsync(p => p.SubServiceId == subId && p.WeekCode == code);

                if (existing != null)
                {
                    if (existing.Status == PlanningStatus.Published)
                    {
                        result.Skipped++;
                        result.Messages.Add($"SubService {subId}: déjà publié — ignoré.");
                        continue;
                    }

                    if (!forceDraftRefresh)
                    {
                        result.Skipped++;
                        result.Messages.Add($"SubService {subId}: brouillon existant — ignoré.");
                        continue;
                    }

                    await GeneratePlanningFromConfigAsync(new GeneratePlanningFromConfigDto
                    {
                        SubServiceId = subId,
                        WeekCode = code,
                        WeeklyPlanningId = existing.Id
                    });
                    result.Created++;
                    result.Messages.Add($"SubService {subId}: brouillon régénéré.");
                    continue;
                }

                var employeesCount = await _context.Users
                    .CountAsync(u => u.SubServiceId == subId && u.IsActive);

                var created = await CreatePlanningAsync(new CreateWeeklyPlanningDto
                {
                    SubServiceId = subId,
                    WeekCode = code,
                    WeekStartDate = weekStart,
                    TotalEffectif = Math.Max(1, employeesCount)
                });

                await GeneratePlanningFromConfigAsync(new GeneratePlanningFromConfigDto
                {
                    SubServiceId = subId,
                    WeekCode = code,
                    WeeklyPlanningId = created.Id
                });

                result.Created++;
                result.Messages.Add($"SubService {subId}: brouillon créé.");
            }
            catch (Exception ex)
            {
                result.Errors++;
                result.Messages.Add($"SubService {subId}: {ex.Message}");
            }
        }

        var settingsEntity = await EnsureAutoGenerateSettingsEntityAsync();
        settingsEntity.LastRunAt = DateTime.UtcNow;
        settingsEntity.LastRunWeekCode = code;
        await _context.SaveChangesAsync();

        return result;
    }

    private async Task<PlanningAutoGenerateSettings> EnsureAutoGenerateSettingsEntityAsync()
    {
        var settings = await _context.PlanningAutoGenerateSettings
            .FirstOrDefaultAsync(s => s.Id == PlanningAutoGenerateSettings.SingletonId);

        if (settings != null) return settings;

        settings = new PlanningAutoGenerateSettings();
        _context.PlanningAutoGenerateSettings.Add(settings);
        await _context.SaveChangesAsync();
        return settings;
    }

    private static AutoGenerateSettingsDto MapAutoGenerateSettings(PlanningAutoGenerateSettings s) => new()
    {
        Enabled = s.Enabled,
        DayOfWeek = s.DayOfWeek,
        HourLocal = s.HourLocal,
        MinuteLocal = s.MinuteLocal,
        TimeZone = s.TimeZone,
        Target = s.Target,
        LastRunAt = s.LastRunAt,
        LastRunWeekCode = s.LastRunWeekCode
    };

    private static DateOnly GetTargetWeekMonday(string target)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var currentMonday = GetMonday(today);
        return string.Equals(target, "CurrentWeek", StringComparison.OrdinalIgnoreCase)
            ? currentMonday
            : currentMonday.AddDays(7);
    }

    private static DateOnly GetMonday(DateOnly date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7; // Monday=0
        return date.AddDays(-diff);
    }

    private static string FormatWeekCode(DateOnly monday)
    {
        var dt = monday.ToDateTime(TimeOnly.MinValue);
        var week = System.Globalization.ISOWeek.GetWeekOfYear(dt);
        var year = System.Globalization.ISOWeek.GetYear(dt);
        return $"{year}-W{week:D2}";
    }

    private static DateOnly ParseWeekCodeToMonday(string weekCode)
    {
        var parts = weekCode.Split('-');
        var year = int.Parse(parts[0]);
        var week = int.Parse(parts[1].Replace("W", "", StringComparison.OrdinalIgnoreCase));
        return DateOnly.FromDateTime(System.Globalization.ISOWeek.ToDateTime(year, week, DayOfWeek.Monday));
    }

}
