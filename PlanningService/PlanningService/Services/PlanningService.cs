using Microsoft.EntityFrameworkCore;
using PlanningService.Data;
using PlanningService.DTOs.Planning;
using PlanningService.Enums;
using PlanningService.Helpers;
using PlanningService.Interfaces;
using PlanningService.Models;

namespace PlanningService.Services;

public class PlanningService : IPlanningService
{
    private readonly AppDbContext _context;

    public PlanningService(AppDbContext context)
    {
        _context = context;
    }

    // ════════════════════════════════════════════════════
    // CRÉER UN PLANNING (vide, en Draft)
    // ════════════════════════════════════════════════════
    public async Task<WeeklyPlanningResponseDto> CreatePlanningAsync(CreateWeeklyPlanningDto dto)
    {
        var existing = await _context.WeeklyPlannings
            .FirstOrDefaultAsync(p => p.WeekCode == dto.WeekCode &&
                                      p.SubServiceId == dto.SubServiceId);
        if (existing != null)
            throw new InvalidOperationException(
                $"Planning {dto.WeekCode} existe déjà pour ce sous-service.");

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
            ?? throw new Exception("Erreur création planning.");
    }

    // ════════════════════════════════════════════════════
    // OVERRIDE BREAK
    // ════════════════════════════════════════════════════
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

    // ════════════════════════════════════════════════════
    // SAUVEGARDER CONFIG SHIFTS DU RESPONSABLE
    // ════════════════════════════════════════════════════
    public async Task<WeekShiftConfigResponseDto> SaveShiftConfigAsync(SaveShiftConfigDto dto)
    {
        var existing = await _context.SubServiceShiftConfigs
            .Where(c => c.SubServiceId == dto.SubServiceId && c.WeekCode == dto.WeekCode)
            .ToListAsync();

        if (existing.Any())
            _context.SubServiceShiftConfigs.RemoveRange(existing);

        var totalEffectif = dto.Shifts.Sum(s => s.RequiredCount);
        var configs = new List<SubServiceShiftConfig>();

        for (int i = 0; i < dto.Shifts.Count; i++)
        {
            var shift = dto.Shifts[i];
            var startTime = TimeOnly.Parse(shift.StartTime);

            var breakStart = shift.BreakRangeStart != null
                ? TimeOnly.Parse(shift.BreakRangeStart)
                : startTime.AddHours(3);

            var breakEnd = shift.BreakRangeEnd != null
                ? TimeOnly.Parse(shift.BreakRangeEnd)
                : startTime.AddHours(shift.WorkHours - 1);

            var percentage = totalEffectif > 0
                ? Math.Round((decimal)shift.RequiredCount / totalEffectif * 100, 1)
                : 0;

            configs.Add(new SubServiceShiftConfig
            {
                SubServiceId = dto.SubServiceId,
                WeekCode = dto.WeekCode,
                WeekStartDate = dto.WeekStartDate,
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

        _context.SubServiceShiftConfigs.AddRange(configs);
        await _context.SaveChangesAsync();

        return await GetShiftConfigAsync(dto.SubServiceId, dto.WeekCode)
            ?? throw new Exception("Erreur sauvegarde config.");
    }

    // ════════════════════════════════════════════════════
    // LIRE LA CONFIG D'UNE SEMAINE
    // ════════════════════════════════════════════════════
    public async Task<WeekShiftConfigResponseDto?> GetShiftConfigAsync(
        int subServiceId, string weekCode)
    {
        var subService = await _context.SubServices.FindAsync(subServiceId);
        if (subService == null) return null;

        var configs = await _context.SubServiceShiftConfigs
            .Where(c => c.SubServiceId == subServiceId && c.WeekCode == weekCode)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();

        return new WeekShiftConfigResponseDto
        {
            SubServiceId = subServiceId,
            SubServiceName = subService.Name,
            WeekCode = weekCode,
            WeekStartDate = configs.FirstOrDefault()?.WeekStartDate ?? DateOnly.MinValue,
            TotalEffectif = configs.Sum(c => c.RequiredCount),
            Shifts = configs.Select(MapToShiftConfigResponseDto).ToList()
        };
    }

    // ════════════════════════════════════════════════════
    // GÉNÉRER DEPUIS LA CONFIG
    // ════════════════════════════════════════════════════
    public async Task<WeeklyPlanningResponseDto> GeneratePlanningFromConfigAsync(
       GeneratePlanningFromConfigDto dto)
    {
        var planning = await _context.WeeklyPlannings
            .Include(p => p.SubService)
            .FirstOrDefaultAsync(p => p.Id == dto.WeeklyPlanningId)
            ?? throw new Exception("Planning introuvable.");

        var shiftConfigs = await _context.SubServiceShiftConfigs
            .Where(c => c.SubServiceId == dto.SubServiceId && c.WeekCode == dto.WeekCode)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();

        if (!shiftConfigs.Any())
            throw new Exception("Aucune config de shifts trouvée.");

        var employees = await _context.Users
            .Where(u => u.SubServiceId == planning.SubServiceId && u.IsActive)
            .OrderBy(u => u.Id)
            .ToListAsync();

        planning.TotalEffectif = employees.Count;

        await AutoAssignSaturdayGroupsAsync(planning.SubServiceId);

        var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(
            planning.WeekStartDate.ToDateTime(TimeOnly.MinValue));
        planning.SaturdayGroupId = weekNumber % 2 == 0 ? 1 : 2;

        _context.ShiftAssignments.RemoveRange(
            _context.ShiftAssignments.Where(a => a.WeeklyPlanningId == planning.Id));
        _context.WeeklyShiftConfigs.RemoveRange(
            _context.WeeklyShiftConfigs.Where(c => c.WeeklyPlanningId == planning.Id));

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

        // ✅ Jours fériés français
        var holidays = FrenchHolidayHelper.GetHolidays(planning.WeekStartDate.Year);

        // ════════════════════════════════════════════════
        // ROTATION — offset par semaine + quotas respectés
        // ════════════════════════════════════════════════
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

        while (cumulative < employees.Count)
        {
            var empStartIdx = (cumulative + currentWeekNumber) % orderedShifts.Count;
            employeeStartShiftIndex[employees[cumulative].Id] = empStartIdx;
            cumulative++;
        }

        // ════════════════════════════════════════════════
        // GÉNÉRATION Lun → Ven
        // ════════════════════════════════════════════════
        int dayIdx = 0;
        foreach (var (day, date) in weekDays)
        {
            // ✅ Jour férié → tous FÉRIÉ
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
                        IsNewEmployee = IsNewEmployee(emp.Id, saturdayGroups)
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
                    IsNewEmployee = IsNewEmployee(emp.Id, saturdayGroups)
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
                    IsNewEmployee = IsNewEmployee(emp.Id, saturdayGroups)
                });
            }

            dayIdx++;
        }

        // ════════════════════════════════════════════════
        // SAMEDI
        // ════════════════════════════════════════════════
        var saturdayDate = planning.WeekStartDate.AddDays(5);
        var saturdayWorkers = new List<int>();

        if (holidays.Contains(saturdayDate))
        {
            // ✅ Samedi férié → tous FÉRIÉ
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
                    IsNewEmployee = IsNewEmployee(emp.Id, saturdayGroups)
                });
            }
        }
        else
        {
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
                        IsNewEmployee = IsNewEmployee(employee.Id, saturdayGroups)
                    });
                }
                else
                {
                    var satAssignment = await GenerateSaturdayAssignmentFromConfigAsync(
                        employee, planning, shiftConfigs, saturdayGroups, empIndex, dto.WeekCode);

                    if (satAssignment != null)
                    {
                        assignments.Add(satAssignment);
                        saturdayWorkers.Add(employee.Id);
                    }
                }
            }
        }

        await SaveSaturdayHistoryAsync(new SetSaturdayHistoryDto(
            dto.SubServiceId,
            dto.WeekCode,
            employees.Select(emp => new SaturdayHistoryEntryDto(
                emp.Id,
                saturdayWorkers.Contains(emp.Id)
            )).ToList()
        ), false);

        _context.ShiftAssignments.AddRange(assignments);

        // ── PAUSES (uniquement jours normaux travaillés) ──
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

        await _context.SaveChangesAsync();

        return await GetPlanningByIdAsync(planning.Id)
            ?? throw new Exception("Erreur génération planning.");
    }

    // ════════════════════════════════════════════════════
    // METTRE SAMEDI OFF (supprimer l'assignation)
    // ════════════════════════════════════════════════════
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

    // ════════════════════════════════════════════════════
    // SATURDAY HISTORY
    // ════════════════════════════════════════════════════
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

    // ════════════════════════════════════════════════════
    // ANCIENNE GÉNÉRATION (gardée pour compatibilité)
    // ════════════════════════════════════════════════════
    public async Task<WeeklyPlanningResponseDto> GeneratePlanningAsync(GeneratePlanningDto dto)
    {
        var planning = await _context.WeeklyPlannings
            .Include(p => p.SubService)
            .Include(p => p.ShiftAssignments)
            .FirstOrDefaultAsync(p => p.Id == dto.WeeklyPlanningId)
            ?? throw new Exception("Planning introuvable.");

        planning.TotalEffectif = dto.TotalEffectif;

        var employees = await _context.Users
            .Where(u => u.SubServiceId == planning.SubServiceId && u.IsActive)
            .OrderBy(u => u.EarlyShiftCount)
            .ToListAsync();

        var shifts = await _context.Shifts
            .OrderBy(s => s.StartTime)
            .ToListAsync();

        if (shifts.Count < 4)
            throw new Exception("Il faut au moins 4 shifts configurés.");

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
                        IsNewEmployee = IsNewEmployee(employee.Id, saturdayGroups)
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
                    IsNewEmployee = IsNewEmployee(employee.Id, saturdayGroups)
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
                var satAssignment = GenerateSaturdayAssignment(
                    employee, planning, shifts, saturdayGroups, empIndex);
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
            ?? throw new Exception("Erreur génération planning.");
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

    // ════════════════════════════════════════════════════
    // OVERRIDE MANAGER
    // ════════════════════════════════════════════════════
    public async Task<DayAssignmentDto> OverrideShiftAsync(OverrideShiftDto dto)
    {
        var assignment = await _context.ShiftAssignments
            .Include(a => a.Shift)
            .Include(a => a.SubServiceShiftConfig)
            .FirstOrDefaultAsync(a => a.Id == dto.ShiftAssignmentId)
            ?? throw new Exception("Assignment introuvable.");

        if (dto.NewSubServiceShiftConfigId > 0)
        {
            var config = await _context.SubServiceShiftConfigs
                .FindAsync(dto.NewSubServiceShiftConfigId)
                ?? throw new Exception("Config shift introuvable.");

            assignment.SubServiceShiftConfigId = dto.NewSubServiceShiftConfigId;
            assignment.ShiftId = null;
            assignment.IsManagerOverride = true;
            await _context.SaveChangesAsync();

            await _context.Entry(assignment)
                .Reference(a => a.SubServiceShiftConfig)
                .LoadAsync();

            return MapToDayDtoNew(assignment);
        }
        else
        {
            var newShift = await _context.Shifts.FindAsync(dto.NewShiftId)
                ?? throw new Exception("Shift introuvable.");

            assignment.ShiftId = dto.NewShiftId;
            assignment.IsManagerOverride = true;
            await _context.SaveChangesAsync();

            return MapToDayDto(assignment, newShift);
        }
    }

    // ════════════════════════════════════════════════════
    // PUBLIER
    // ════════════════════════════════════════════════════
    public async Task<WeeklyPlanningResponseDto> PublishPlanningAsync(int planningId, int validatorId)
    {
        var planning = await _context.WeeklyPlannings.FindAsync(planningId)
            ?? throw new Exception("Planning introuvable.");

        planning.Status = PlanningStatus.Published;
        planning.ValidatedBy = validatorId;
        await _context.SaveChangesAsync();

        return await GetPlanningByIdAsync(planningId)
            ?? throw new Exception("Erreur publication.");
    }

    // ════════════════════════════════════════════════════
    // GROUPES SAMEDI
    // ════════════════════════════════════════════════════
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

    // ════════════════════════════════════════════════════
    // VUE EMPLOYÉ
    // ════════════════════════════════════════════════════
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

    // ════════════════════════════════════════════════════
    // GET PLANNINGS
    // ════════════════════════════════════════════════════
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
            ShiftConfigs = planning.WeeklyShiftConfigs.Select(c => new ShiftConfigResponseDto
            {
                ShiftId = c.ShiftId,
                ShiftLabel = c.Shift.Label,
                StartTime = c.Shift.StartTime.ToString("HH:mm"),
                RequiredCount = c.RequiredCount,
                Percentage = c.Percentage
            }).ToList(),
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
                                       .Select(a => MapToDayDtoNew(a))
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

    // ════════════════════════════════════════════════════
    // OVERRIDE SAMEDI
    // ════════════════════════════════════════════════════
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
                    "WeeklyPlanningId et UserId sont requis pour créer une assignation samedi.");

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

        // FIX — Assigner une pause si elle n existe pas encore
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

    // ════════════════════════════════════════════════════
    // COMMENTAIRES MANAGER
    // ════════════════════════════════════════════════════
    public async Task<PlanningCommentDto> SaveCommentAsync(SavePlanningCommentDto dto)
    {
        var planning = await _context.WeeklyPlannings.FindAsync(dto.WeeklyPlanningId)
            ?? throw new Exception("Planning introuvable.");

        if (planning.Status != PlanningStatus.Draft)
            throw new InvalidOperationException(
                "Impossible d'ajouter un commentaire sur un planning publié.");

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

    // ════════════════════════════════════════════════════
    // HELPERS — NOUVEAU SYSTÈME
    // ════════════════════════════════════════════════════
    private async Task<ShiftAssignment?> GenerateSaturdayAssignmentFromConfigAsync(
        User employee,
        WeeklyPlanning planning,
        List<SubServiceShiftConfig> shiftConfigs,
        List<SaturdayGroup> saturdayGroups,
        int employeeIndex,
        string weekCode)
    {
        var satGroup = saturdayGroups.FirstOrDefault(sg => sg.UserId == employee.Id);

        if (satGroup?.IsNewEmployee == true)
        {
            if (satGroup.NewEmployeeSlot == 0) return null;

            var shiftConfig = satGroup.NewEmployeeSlot == 1
                ? shiftConfigs.OrderBy(s => s.StartTime).First()
                : shiftConfigs.OrderBy(s => s.StartTime).Skip(1).First();

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
                SaturdaySlot = satGroup.NewEmployeeSlot
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
        var shiftIndex = (employeeIndex + weekNumber) % shiftConfigs.Count;

        return new ShiftAssignment
        {
            WeeklyPlanningId = planning.Id,
            UserId = employee.Id,
            SubServiceShiftConfigId = shiftConfigs[shiftIndex].Id,
            AssignedDate = planning.WeekStartDate.AddDays(5),
            DayOfWeek = DayOfWeekEnum.Saturday,
            IsSaturday = true,
            IsNewEmployee = false,
            IsHalfDaySaturday = false,
            SaturdaySlot = 0
        };
    }

    private static string GetPreviousWeekCode(string weekCode)
    {
        var parts = weekCode.Split('-');
        var year = int.Parse(parts[0]);
        var week = int.Parse(parts[1].Replace("W", ""));

        if (week == 1)
            return $"{year - 1}-W52";

        return $"{year}-W{(week - 1):D2}";
    }

    private void AssignBreakTimesFromConfig(
        List<ShiftAssignment> dayAssignments,
        List<SubServiceShiftConfig> shiftConfigs,
        int totalEmployees)
    {
        if (!dayAssignments.Any()) return;

        int maxSimultaneous = Math.Max(1, (int)Math.Floor(totalEmployees * 0.30));
        var breakSlotUsage = new Dictionary<TimeOnly, int>();

        var shiftGroups = dayAssignments
            .GroupBy(a => a.SubServiceShiftConfigId)
            .ToList();

        foreach (var group in shiftGroups)
        {
            var config = shiftConfigs.FirstOrDefault(c => c.Id == group.Key);
            if (config == null) continue;

            var slots = GenerateBreakSlots(config.BreakRangeStart, config.BreakRangeEnd);

            foreach (var assignment in group)
            {
                var bestSlot = slots
                    .OrderBy(s => breakSlotUsage.GetValueOrDefault(s, 0))
                    .FirstOrDefault(s => breakSlotUsage.GetValueOrDefault(s, 0) < maxSimultaneous);

                if (bestSlot == default)
                    bestSlot = slots
                        .OrderBy(s => breakSlotUsage.GetValueOrDefault(s, 0))
                        .First();

                assignment.BreakTime = bestSlot;
                breakSlotUsage[bestSlot] = breakSlotUsage.GetValueOrDefault(bestSlot, 0) + 1;
            }
        }
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

    private static DayAssignmentDto MapToDayDtoNew(ShiftAssignment a)
    {
        var label = a.IsHoliday ? "FÉRIÉ"
     : a.IsOnLeave ? "CONGÉ"
     : a.SubServiceShiftConfig?.Label
       ?? a.Shift?.Label
       ?? "—";


        var startTime = a.SubServiceShiftConfig?.StartTime.ToString("HH:mm")
                        ?? a.Shift?.StartTime.ToString("HH:mm")
                        ?? "";

        var endTime = a.SubServiceShiftConfig?.EndTime.ToString("HH:mm") ?? "";

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

    // ════════════════════════════════════════════════════
    // HELPERS — ANCIEN SYSTÈME
    // ════════════════════════════════════════════════════
    private static DayAssignmentDto MapToDayDto(ShiftAssignment a, Shift? shift) => new()
    {
        AssignmentId = a.Id,
        Day = a.DayOfWeek.ToString(),
        AssignedDate = a.AssignedDate,
        ShiftLabel = shift?.Label ?? "CONGÉ",
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
            DisplayOrder = c.DisplayOrder
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
        List<Shift> shifts, List<SaturdayGroup> saturdayGroups, int employeeIndex)
    {
        var satGroup = saturdayGroups.FirstOrDefault(sg => sg.UserId == employee.Id);
        if (satGroup == null) return null;

        if (satGroup.IsNewEmployee)
        {
            if (satGroup.NewEmployeeSlot == 0) return null;
            var shiftId = satGroup.NewEmployeeSlot == 1
                ? shifts.OrderBy(s => s.StartTime).First().Id
                : shifts.OrderBy(s => s.StartTime).Skip(1).First().Id;

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
                SaturdaySlot = satGroup.NewEmployeeSlot
            };
        }

        if (satGroup.GroupNumber != planning.SaturdayGroupId) return null;

        var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(
            planning.WeekStartDate.ToDateTime(TimeOnly.MinValue));
        var shiftIndex = (employeeIndex + weekNumber) % shifts.Count;

        return new ShiftAssignment
        {
            WeeklyPlanningId = planning.Id,
            UserId = employee.Id,
            ShiftId = shifts[shiftIndex].Id,
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
        var earliest = shiftStart.AddHours(3);
        for (int h = 0; h < 4; h++)
            slots.Add(earliest.AddHours(h));
        return slots;
    }

    private static int GetSaturdayGroupForWeek(DateOnly weekStart)
    {
        var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(
            weekStart.ToDateTime(TimeOnly.MinValue));
        return weekNumber % 2 == 0 ? 1 : 2;
    }

    private static bool IsNewEmployee(int userId, List<SaturdayGroup> groups)
        => groups.Any(g => g.UserId == userId && g.IsNewEmployee);

    private static List<(DayOfWeekEnum day, DateOnly date)> GetWeekDays(DateOnly weekStart)
        => new()
        {
            (DayOfWeekEnum.Monday,    weekStart),
            (DayOfWeekEnum.Tuesday,   weekStart.AddDays(1)),
            (DayOfWeekEnum.Wednesday, weekStart.AddDays(2)),
            (DayOfWeekEnum.Thursday,  weekStart.AddDays(3)),
            (DayOfWeekEnum.Friday,    weekStart.AddDays(4)),
        };
}