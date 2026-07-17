using Microsoft.EntityFrameworkCore;
using Planning.Application.Abstractions;
using Planning.Application.DTOs.Planning;
using Planning.Domain.Entities;
using Planning.Domain.Enums;
using Planning.Infrastructure.Persistence;

namespace Planning.Infrastructure.Services;

public class PlanningChangeRequestService : IPlanningChangeRequestService
{
    private readonly AppDbContext _context;
    private const string CasablancaTz = "Africa/Casablanca";

    public PlanningChangeRequestService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PlanningChangeRequestDto> CreateAsync(
        int requesterUserId, CreatePlanningChangeRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new InvalidOperationException("Le motif est obligatoire.");

        var assignment = await _context.ShiftAssignments
            .Include(a => a.WeeklyPlanning).ThenInclude(p => p.SubService)
            .Include(a => a.SubServiceShiftConfig)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == dto.CurrentAssignmentId)
            ?? throw new InvalidOperationException("Assignation introuvable.");

        if (assignment.UserId != requesterUserId)
            throw new InvalidOperationException("Vous ne pouvez demander un changement que pour votre propre créneau.");

        if (assignment.IsOnLeave || assignment.IsHoliday || assignment.SubServiceShiftConfigId == null)
            throw new InvalidOperationException("Ce créneau n'est pas modifiable (congé, férié ou OFF).");

        EnsureCreationDeadline(assignment.WeeklyPlanning.WeekStartDate);

        if (dto.ProposedSwapUserId.HasValue)
        {
            var candidates = await GetSwapCandidatesAsync(assignment.Id, requesterUserId);
            if (candidates.All(c => c.UserId != dto.ProposedSwapUserId.Value))
                throw new InvalidOperationException(
                    "Le collègue proposé n'est pas un candidat valide (même niveau, même sous-service, disponible).");
        }

        var entity = new PlanningChangeRequest
        {
            WeekCode = assignment.WeeklyPlanning.WeekCode,
            RequesterUserId = requesterUserId,
            CurrentAssignmentId = assignment.Id,
            Reason = dto.Reason.Trim(),
            ProposedSwapUserId = dto.ProposedSwapUserId,
            Status = PlanningChangeRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.PlanningChangeRequests.Add(entity);
        await _context.SaveChangesAsync();

        return await MapAsync(entity.Id)
            ?? throw new InvalidOperationException("Erreur création demande.");
    }

    public async Task<List<PlanningChangeRequestDto>> GetMyAsync(int requesterUserId)
    {
        var ids = await _context.PlanningChangeRequests
            .Where(r => r.RequesterUserId == requesterUserId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => r.Id)
            .ToListAsync();

        var result = new List<PlanningChangeRequestDto>();
        foreach (var id in ids)
        {
            var dto = await MapAsync(id);
            if (dto != null) result.Add(dto);
        }
        return result;
    }

    public async Task<List<PlanningChangeRequestDto>> GetAllAsync(string? status, string? weekCode)
    {
        var query = _context.PlanningChangeRequests.AsQueryable();

        if (!string.IsNullOrWhiteSpace(weekCode))
            query = query.Where(r => r.WeekCode == weekCode);

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<PlanningChangeRequestStatus>(status, true, out var st))
            query = query.Where(r => r.Status == st);

        var ids = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => r.Id)
            .ToListAsync();

        var result = new List<PlanningChangeRequestDto>();
        foreach (var id in ids)
        {
            var dto = await MapAsync(id);
            if (dto != null) result.Add(dto);
        }
        return result;
    }

    public async Task<List<ChangeRequestStatsByEmployeeDto>> GetStatsByEmployeeAsync(string? weekCode)
    {
        var query = _context.PlanningChangeRequests.AsQueryable();
        if (!string.IsNullOrWhiteSpace(weekCode))
            query = query.Where(r => r.WeekCode == weekCode);

        var grouped = await query
            .GroupBy(r => r.RequesterUserId)
            .Select(g => new
            {
                UserId = g.Key,
                Total = g.Count(),
                Pending = g.Count(x => x.Status == PlanningChangeRequestStatus.Pending),
                Approved = g.Count(x => x.Status == PlanningChangeRequestStatus.Approved),
                Rejected = g.Count(x => x.Status == PlanningChangeRequestStatus.Rejected)
            })
            .ToListAsync();

        var userIds = grouped.Select(g => g.UserId).ToList();
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        return grouped.Select(g => new ChangeRequestStatsByEmployeeDto
        {
            UserId = g.UserId,
            FullName = users.TryGetValue(g.UserId, out var u)
                ? $"{u.FirstName} {u.LastName}"
                : $"#{g.UserId}",
            TotalRequests = g.Total,
            PendingCount = g.Pending,
            ApprovedCount = g.Approved,
            RejectedCount = g.Rejected
        })
        .OrderByDescending(s => s.TotalRequests)
        .ToList();
    }

    public async Task<List<SwapCandidateDto>> GetSwapCandidatesAsync(int assignmentId, int requesterUserId)
    {
        var assignment = await _context.ShiftAssignments
            .Include(a => a.WeeklyPlanning)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == assignmentId)
            ?? throw new InvalidOperationException("Assignation introuvable.");

        if (assignment.UserId != requesterUserId)
            throw new InvalidOperationException("Assignation non autorisée.");

        var level = assignment.User.Level;
        var subServiceId = assignment.WeeklyPlanning.SubServiceId;
        var date = assignment.AssignedDate;

        var peers = await _context.Users
            .Where(u => u.IsActive
                     && u.SubServiceId == subServiceId
                     && u.Level == level
                     && u.Id != requesterUserId)
            .ToListAsync();

        var peerIds = peers.Select(p => p.Id).ToList();
        var peerAssignments = await _context.ShiftAssignments
            .Include(a => a.SubServiceShiftConfig)
            .Where(a => a.WeeklyPlanningId == assignment.WeeklyPlanningId
                     && a.AssignedDate == date
                     && peerIds.Contains(a.UserId)
                     && !a.IsOnLeave
                     && !a.IsHoliday
                     && a.SubServiceShiftConfigId != null)
            .ToListAsync();

        return peerAssignments
            .Where(a => a.SubServiceShiftConfigId != assignment.SubServiceShiftConfigId)
            .Select(a =>
            {
                var peer = peers.First(p => p.Id == a.UserId);
                return new SwapCandidateDto
                {
                    UserId = peer.Id,
                    FullName = $"{peer.FirstName} {peer.LastName}",
                    Level = peer.Level,
                    AssignmentId = a.Id,
                    ShiftLabel = a.SubServiceShiftConfig?.Label ?? "—"
                };
            })
            .OrderBy(c => c.FullName)
            .ToList();
    }

    public async Task<PlanningChangeRequestDto> ApproveAsync(int id, int processedByUserId)
    {
        var request = await _context.PlanningChangeRequests
            .Include(r => r.CurrentAssignment)
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new InvalidOperationException("Demande introuvable.");

        if (request.Status != PlanningChangeRequestStatus.Pending)
            throw new InvalidOperationException("Seules les demandes en attente peuvent être approuvées.");

        var requesterAssignment = await _context.ShiftAssignments
            .FirstOrDefaultAsync(a => a.Id == request.CurrentAssignmentId)
            ?? throw new InvalidOperationException("Assignation source disparue — demande non applicable.");

        if (!request.ProposedSwapUserId.HasValue)
            throw new InvalidOperationException("Aucune proposition de switch — réaffectation manuelle requise.");

        var swapAssignment = await _context.ShiftAssignments
            .FirstOrDefaultAsync(a =>
                a.WeeklyPlanningId == requesterAssignment.WeeklyPlanningId
                && a.UserId == request.ProposedSwapUserId.Value
                && a.AssignedDate == requesterAssignment.AssignedDate)
            ?? throw new InvalidOperationException("Assignation du collègue proposé introuvable.");

        // Échange des configs + pauses
        var cfgA = requesterAssignment.SubServiceShiftConfigId;
        var breakA = requesterAssignment.BreakTime;
        var halfA = requesterAssignment.IsHalfDaySaturday;
        var slotA = requesterAssignment.SaturdaySlot;

        requesterAssignment.SubServiceShiftConfigId = swapAssignment.SubServiceShiftConfigId;
        requesterAssignment.BreakTime = swapAssignment.BreakTime;
        requesterAssignment.IsHalfDaySaturday = swapAssignment.IsHalfDaySaturday;
        requesterAssignment.SaturdaySlot = swapAssignment.SaturdaySlot;
        requesterAssignment.IsManagerOverride = true;
        requesterAssignment.IsOnLeave = false;
        requesterAssignment.IsHoliday = false;

        swapAssignment.SubServiceShiftConfigId = cfgA;
        swapAssignment.BreakTime = breakA;
        swapAssignment.IsHalfDaySaturday = halfA;
        swapAssignment.SaturdaySlot = slotA;
        swapAssignment.IsManagerOverride = true;
        swapAssignment.IsOnLeave = false;
        swapAssignment.IsHoliday = false;

        request.Status = PlanningChangeRequestStatus.Approved;
        request.ProcessedByUserId = processedByUserId;
        request.ProcessedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return await MapAsync(id) ?? throw new InvalidOperationException("Erreur approve.");
    }

    public async Task<PlanningChangeRequestDto> RejectAsync(
        int id, int processedByUserId, string? reason)
    {
        var request = await _context.PlanningChangeRequests.FindAsync(id)
            ?? throw new InvalidOperationException("Demande introuvable.");

        if (request.Status != PlanningChangeRequestStatus.Pending)
            throw new InvalidOperationException("Seules les demandes en attente peuvent être rejetées.");

        request.Status = PlanningChangeRequestStatus.Rejected;
        request.ProcessedByUserId = processedByUserId;
        request.ProcessedAt = DateTime.UtcNow;
        request.RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        await _context.SaveChangesAsync();
        return await MapAsync(id) ?? throw new InvalidOperationException("Erreur reject.");
    }

    public async Task<PlanningChangeRequestDto> CancelAsync(int id, int requesterUserId)
    {
        var request = await _context.PlanningChangeRequests.FindAsync(id)
            ?? throw new InvalidOperationException("Demande introuvable.");

        if (request.RequesterUserId != requesterUserId)
            throw new InvalidOperationException("Annulation non autorisée.");

        if (request.Status != PlanningChangeRequestStatus.Pending)
            throw new InvalidOperationException("Seules les demandes en attente peuvent être annulées.");

        request.Status = PlanningChangeRequestStatus.Cancelled;
        request.ProcessedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return await MapAsync(id) ?? throw new InvalidOperationException("Erreur cancel.");
    }

    /// <summary>
    /// Deadline : mercredi 23:59 Africa/Casablanca de la semaine précédant la semaine du planning.
    /// </summary>
    public static void EnsureCreationDeadline(DateOnly weekStartDate, DateTime? utcNow = null)
    {
        var tz = ResolveCasablancaTimeZone();
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(utcNow ?? DateTime.UtcNow, tz);

        // Semaine précédente = weekStart - 7 jours ; mercredi = +2 jours depuis lundi
        var prevWeekMonday = weekStartDate.AddDays(-7);
        var deadlineLocal = prevWeekMonday.AddDays(2).ToDateTime(new TimeOnly(23, 59, 59));

        if (nowLocal > deadlineLocal)
            throw new InvalidOperationException(
                "Délai dépassé : les demandes de changement doivent être créées au plus tard " +
                "le mercredi 23:59 (Casablanca) de la semaine précédant le planning.");
    }

    private static TimeZoneInfo ResolveCasablancaTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(CasablancaTz);
        }
        catch (TimeZoneNotFoundException)
        {
            // Windows
            return TimeZoneInfo.FindSystemTimeZoneById("Morocco Standard Time");
        }
    }

    private async Task<PlanningChangeRequestDto?> MapAsync(int id)
    {
        var r = await _context.PlanningChangeRequests
            .AsNoTracking()
            .Include(x => x.Requester)
            .Include(x => x.ProposedSwapUser)
            .Include(x => x.ProcessedBy)
            .Include(x => x.CurrentAssignment)
                .ThenInclude(a => a.SubServiceShiftConfig)
            .Include(x => x.CurrentAssignment)
                .ThenInclude(a => a.WeeklyPlanning)
                    .ThenInclude(p => p.SubService)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (r == null) return null;

        return new PlanningChangeRequestDto
        {
            Id = r.Id,
            WeekCode = r.WeekCode,
            RequesterUserId = r.RequesterUserId,
            RequesterName = $"{r.Requester.FirstName} {r.Requester.LastName}",
            CurrentAssignmentId = r.CurrentAssignmentId,
            AssignmentDay = r.CurrentAssignment.DayOfWeek.ToString(),
            AssignmentDate = r.CurrentAssignment.AssignedDate,
            ShiftLabel = r.CurrentAssignment.SubServiceShiftConfig?.Label ?? "—",
            Reason = r.Reason,
            ProposedSwapUserId = r.ProposedSwapUserId,
            ProposedSwapUserName = r.ProposedSwapUser != null
                ? $"{r.ProposedSwapUser.FirstName} {r.ProposedSwapUser.LastName}"
                : null,
            Status = r.Status.ToString(),
            CreatedAt = r.CreatedAt,
            ProcessedByUserId = r.ProcessedByUserId,
            ProcessedByName = r.ProcessedBy != null
                ? $"{r.ProcessedBy.FirstName} {r.ProcessedBy.LastName}"
                : null,
            ProcessedAt = r.ProcessedAt,
            RejectionReason = r.RejectionReason,
            SubServiceId = r.CurrentAssignment.WeeklyPlanning.SubServiceId,
            SubServiceName = r.CurrentAssignment.WeeklyPlanning.SubService.Name
        };
    }
}
