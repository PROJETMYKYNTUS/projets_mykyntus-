using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Planning.Application.Abstractions;
using Planning.Application.DTOs.Planning;
using Planning.Domain.Entities;
using Planning.Domain.Enums;
using Planning.Infrastructure.Hubs;
using Planning.Infrastructure.Persistence;

namespace Planning.Infrastructure.Services;

public class PlanningChangeRequestService : IPlanningChangeRequestService
{
    private readonly AppDbContext _context;
    private readonly IHubContext<PlanningHub> _hubContext;
    private readonly ILogger<PlanningChangeRequestService> _logger;
    private const string CasablancaTz = "Africa/Casablanca";
    private const string ChangeRequestSubService = "Demande de changement";

    public PlanningChangeRequestService(
        AppDbContext context,
        IHubContext<PlanningHub> hubContext,
        ILogger<PlanningChangeRequestService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
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

        var requesterName = $"{assignment.User.FirstName} {assignment.User.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(requesterName))
            requesterName = assignment.User.Email ?? $"#{requesterUserId}";

        try
        {
            await NotifyRhAdminsAsync(
                entity.WeekCode,
                $"Nouvelle demande de changement — {requesterName} ({entity.WeekCode})",
                "/planning/change-requests");
        }
        catch (Exception ex)
        {
            // La demande est déjà persistée : ne pas faire échouer la création si la notif échoue.
            _logger.LogError(ex,
                "Échec notification RH/Admin pour la demande de changement {RequestId} ({WeekCode})",
                entity.Id, entity.WeekCode);
        }

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
        {
            // Pas de switch proposé : le RH traite manuellement sur la grille.
            // Approuver = confirmer que la réaffectation a été faite (ou clôturer la demande).
            request.Status = PlanningChangeRequestStatus.Approved;
            request.ProcessedByUserId = processedByUserId;
            request.ProcessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await NotifyUserAsync(
                request.RequesterUserId,
                request.WeekCode,
                $"Votre demande de changement ({request.WeekCode}) a été traitée par le RH.",
                "/mes-plannings");

            return await MapAsync(id) ?? throw new InvalidOperationException("Erreur approve.");
        }

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

        await NotifyUserAsync(
            request.RequesterUserId,
            request.WeekCode,
            $"Votre demande de changement ({request.WeekCode}) a été approuvée.",
            "/mes-plannings");

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

        var rejectMsg = string.IsNullOrWhiteSpace(request.RejectionReason)
            ? $"Votre demande de changement ({request.WeekCode}) a été refusée."
            : $"Votre demande de changement ({request.WeekCode}) a été refusée : {request.RejectionReason}";

        await NotifyUserAsync(
            request.RequesterUserId,
            request.WeekCode,
            rejectMsg,
            "/mes-plannings");

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
    /// Deadline : mercredi 23:59 Africa/Casablanca de la semaine du planning (lundi = weekStart).
    /// Permet à l’employé de demander un changement après publication, jusqu’au mercredi inclus.
    /// </summary>
    public static void EnsureCreationDeadline(DateOnly weekStartDate, DateTime? utcNow = null)
    {
        var tz = ResolveCasablancaTimeZone();
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(utcNow ?? DateTime.UtcNow, tz);

        // Mercredi de la semaine du planning = weekStart (lundi) + 2 jours
        var deadlineLocal = weekStartDate.AddDays(2).ToDateTime(new TimeOnly(23, 59, 59));

        if (nowLocal > deadlineLocal)
            throw new InvalidOperationException(
                "Délai dépassé : les demandes de changement doivent être créées au plus tard " +
                "le mercredi 23:59 (Casablanca) de la semaine du planning.");
    }

    private async Task NotifyRhAdminsAsync(string weekCode, string message, string deepLink)
    {
        var recipients = await _context.Users
            .AsNoTracking()
            .Where(u => u.IsActive
                        && u.AuthUserId != null
                        && u.Role != null
                        && (u.Role.Name.ToLower() == "rh" || u.Role.Name.ToLower() == "admin"))
            .Select(u => new { u.Id, AuthUserId = u.AuthUserId!.Value })
            .ToListAsync();

        if (recipients.Count == 0)
        {
            _logger.LogWarning(
                "Aucune fiche Planning RH/Admin avec AuthUserId — push SignalR groupe {Group} uniquement. Message: {Message}",
                PlanningHub.RhAdminsGroup, message);
        }

        // Persistance individuelle (historique cloche après refresh).
        var created = new List<PlanningNotification>();
        foreach (var r in recipients)
        {
            var notif = new PlanningNotification
            {
                UserId = r.Id,
                AuthUserId = r.AuthUserId,
                WeeklyPlanningId = null,
                WeekCode = weekCode,
                SubServiceName = ChangeRequestSubService,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            _context.PlanningNotifications.Add(notif);
            created.Add(notif);
        }

        if (created.Count > 0)
            await _context.SaveChangesAsync();

        // Push temps réel : groupe partagé RH/Admin (même si 0 destinataire en base).
        var payload = new
        {
            id = created.FirstOrDefault()?.Id,
            weekCode,
            subServiceName = ChangeRequestSubService,
            message,
            weeklyPlanningId = (int?)null,
            deepLink,
            createdAt = created.FirstOrDefault()?.CreatedAt ?? DateTime.UtcNow,
            isRead = false
        };

        await _hubContext.Clients
            .Group(PlanningHub.RhAdminsGroup)
            .SendAsync("PlanningPublished", payload);

        _logger.LogInformation(
            "Notification demande de changement poussée vers {Group} ({RecipientCount} persistée(s))",
            PlanningHub.RhAdminsGroup, created.Count);
    }

    private async Task NotifyUserAsync(int planningUserId, string weekCode, string message, string deepLink)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == planningUserId && u.AuthUserId != null)
            .Select(u => new { u.Id, AuthUserId = u.AuthUserId!.Value })
            .FirstOrDefaultAsync();

        if (user is null) return;
        await PersistAndPushAsync(user.Id, user.AuthUserId, weekCode, message, deepLink);
    }

    private async Task PersistAndPushAsync(
        int userId,
        int authUserId,
        string weekCode,
        string message,
        string deepLink)
    {
        var notif = new PlanningNotification
        {
            UserId = userId,
            AuthUserId = authUserId,
            WeeklyPlanningId = null,
            WeekCode = weekCode,
            SubServiceName = ChangeRequestSubService,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.PlanningNotifications.Add(notif);
        await _context.SaveChangesAsync();

        await _hubContext.Clients
            .Group($"user_{authUserId}")
            .SendAsync("PlanningPublished", new
            {
                id = notif.Id,
                weekCode = notif.WeekCode,
                subServiceName = notif.SubServiceName,
                message = notif.Message,
                weeklyPlanningId = (int?)null,
                deepLink,
                createdAt = notif.CreatedAt,
                isRead = false
            });
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
            SubServiceName = r.CurrentAssignment.WeeklyPlanning.SubService.Name,
            WeeklyPlanningId = r.CurrentAssignment.WeeklyPlanningId
        };
    }
}
