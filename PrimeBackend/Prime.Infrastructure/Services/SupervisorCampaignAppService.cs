using Microsoft.EntityFrameworkCore;
using Prime.Application;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;
using Prime.Domain.Entities;
using Prime.Infrastructure.Persistence;
using Prime.Infrastructure.Services;

namespace Prime.Infrastructure.Services;

public sealed class SupervisorCampaignAppService(
    PrimeDbContext db,
    PrimeOrgScopeService org,
    ICommonLinePonderationResolver ponderationResolver) : ISupervisorCampaignAppService
{
    public async Task<IReadOnlyList<SupervisorCelluleCampaignDto>> GetCampaignAsync(
        string supervisorUserId,
        string period,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(supervisorUserId) || string.IsNullOrWhiteSpace(period))
            throw new ArgumentException("supervisorUserId et period sont requis.");

        var supTrim = supervisorUserId.Trim();
        var periodTrim = period.Trim();

        await PrimeSchemaPatches.EnsureCommonLinePonderationTableAsync(db, ct);

        var celluleIds = await org.GetSupervisedCelluleIdsAsync(supTrim, ct);
        if (celluleIds.Count == 0) return [];

        var cellules = await db.Cellules.AsNoTracking()
            .Where(c => celluleIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var drafts = await db.SupervisorCellulePrimeDrafts.AsNoTracking()
            .Where(d => d.SupervisorUserId == supTrim && d.Period == periodTrim && celluleIds.Contains(d.CelluleId))
            .ToListAsync(ct);

        var draftByCellule = drafts
            .GroupBy(d => d.CelluleId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First());

        var prevPeriod = PreviousPeriod(periodTrim);
        var prevDrafts = await db.SupervisorCellulePrimeDrafts.AsNoTracking()
            .Where(d => d.SupervisorUserId == supTrim && d.Period == prevPeriod && celluleIds.Contains(d.CelluleId))
            .ToListAsync(ct);
        var prevByCellule = prevDrafts
            .Where(d => string.Equals(d.Status, "Validated", StringComparison.OrdinalIgnoreCase))
            .GroupBy(d => d.CelluleId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First());

        var employeesByCellule = await org.GetEmployeeCountsByCelluleAsync(celluleIds, ct);

        var result = new List<SupervisorCelluleCampaignDto>();
        foreach (var celluleId in celluleIds.OrderBy(id => cellules.TryGetValue(id, out var n) ? n : id))
        {
            draftByCellule.TryGetValue(celluleId, out var draft);
            prevByCellule.TryGetValue(celluleId, out var prevDraft);
            employeesByCellule.TryGetValue(celluleId, out var totalEmployees);

            var steps = await BuildStepsAsync(celluleId, periodTrim, draft, totalEmployees, ct);
            var next = ResolveNextAction(steps, draft, prevDraft is not null);

            var validationCounts = draft is null
                ? (Complete: 0, InProgress: 0, NotStarted: totalEmployees, Pending: 0, SupervisorApproved: 0, Rejected: 0)
                : await CountFicheStatesAsync(draft.Id, totalEmployees, ct);

            result.Add(new SupervisorCelluleCampaignDto
            {
                CelluleId = celluleId,
                CelluleName = cellules.TryGetValue(celluleId, out var name) ? name : celluleId,
                Period = periodTrim,
                NextActionLabel = next.Label,
                NextActionPath = next.Path,
                DraftId = draft?.Id,
                TemplateId = draft?.TemplateId,
                TemplateDisplayName = draft?.TemplateDisplayName,
                CommonPartStatus = draft?.Status,
                TotalEmployees = totalEmployees,
                CompleteEmployees = validationCounts.Complete,
                InProgressEmployees = validationCounts.InProgress,
                NotStartedEmployees = validationCounts.NotStarted,
                PendingValidationCount = validationCounts.Pending,
                SupervisorApprovedCount = validationCounts.SupervisorApproved,
                RejectedCount = validationCounts.Rejected,
                CanRolloverFromPrevious = draft is null && prevDraft is not null,
                PreviousPeriod = prevDraft is not null ? prevPeriod : null,
                Steps = steps,
            });
        }

        return result;
    }

    private async Task<List<CampaignStepStatusDto>> BuildStepsAsync(
        string celluleId,
        string period,
        SupervisorCellulePrimeDraft? draft,
        int totalEmployees,
        CancellationToken ct)
    {
        var steps = new List<CampaignStepStatusDto>();

        steps.Add(new CampaignStepStatusDto
        {
            Key = "perimeter",
            Label = "Périmètre",
            State = totalEmployees > 0 ? "done" : "blocked",
            Reason = totalEmployees > 0 ? null : "Aucun employé dans cette cellule.",
            ActionPath = "/superviseur/scope",
        });

        var hasTemplate = draft is not null && !string.IsNullOrWhiteSpace(draft.SchemaJson);
        steps.Add(new CampaignStepStatusDto
        {
            Key = "template",
            Label = "Modèle Excel",
            State = hasTemplate ? "done" : "todo",
            Reason = hasTemplate ? null : "Choisissez un modèle Excel.",
            ActionPath = "/template-manager",
        });

        var missingPonds = 0;
        string? ponderationResolveError = null;
        if (draft is not null && hasTemplate)
        {
            try
            {
                var at = CommonLinePonderationPeriod.StartOfUtcDay(ParsePeriodStart(period));
                var hints = TemplateSchemaPonderationHints.FromSchemaJson(draft.SchemaJson);
                var prevHints = await ponderationResolver.BuildPreviousPeriodHintsAsync(celluleId, draft.TemplateId, at, ct);
                var resolved = await ponderationResolver.ResolveAsync(null, celluleId, draft.TemplateId, at, hints, prevHints, ct);
                var schemaIds = ParseSchemaStableIds(draft.SchemaJson);
                missingPonds = schemaIds.Count(sid =>
                    !resolved.Any(r => string.Equals(r.TemplateStableId, sid, StringComparison.OrdinalIgnoreCase) &&
                                       (r.PonderationPrimePct.HasValue || r.PonderationChallengePct.HasValue)));
            }
            catch (Exception ex) when (IsMissingRelation(ex))
            {
                await PrimeSchemaPatches.EnsureCommonLinePonderationTableAsync(db, ct);
                ponderationResolveError =
                    "Table des pondérations en cours d'initialisation — réessayez dans quelques secondes.";
            }
            catch (Exception)
            {
                ponderationResolveError = "Impossible de vérifier les pondérations pour le moment.";
            }
        }

        steps.Add(new CampaignStepStatusDto
        {
            Key = "ponderations",
            Label = "Pondérations",
            State = ponderationResolveError is not null ? "blocked" :
                !hasTemplate ? "todo" : missingPonds == 0 ? "done" : "todo",
            Reason = ponderationResolveError ?? (missingPonds > 0 ? $"{missingPonds} ligne(s) sans pondération." : null),
            ActionPath = "/prime-saisie",
        });

        var commonState = draft switch
        {
            null => "todo",
            { Status: var s } when string.Equals(s, "Validated", StringComparison.OrdinalIgnoreCase) => "done",
            _ => "todo",
        };
        steps.Add(new CampaignStepStatusDto
        {
            Key = "common",
            Label = "Partie commune",
            State = commonState,
            Reason = draft is null ? "Créez ou reconduisez la fiche commune." :
                commonState == "done" ? null : "Saisissez et validez la partie commune.",
            ActionPath = "/prime-saisie",
        });

        var serviceState = totalEmployees == 0 ? "todo" :
            draft is null ? "todo" :
            totalEmployees > 0 && await CountCompleteFichesAsync(draft.Id, totalEmployees, ct) == totalEmployees ? "done" : "todo";
        steps.Add(new CampaignStepStatusDto
        {
            Key = "service",
            Label = "Partie service (pilotes)",
            State = serviceState,
            Reason = draft is null ? "Commencez par la partie commune." : null,
            ActionPath = "/prime-fiches-agents?tab=pilotage",
        });

        steps.Add(new CampaignStepStatusDto
        {
            Key = "validation",
            Label = "Valider les primes",
            State = string.Equals(commonState, "done", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(serviceState, "done", StringComparison.OrdinalIgnoreCase)
                ? "todo"
                : "blocked",
            Reason = commonState != "done"
                ? "Validez d'abord la partie commune."
                : serviceState != "done"
                    ? "Complétez la saisie des pilotes."
                    : null,
            ActionPath = "/prime-validation-hub?tab=validate",
        });

        return steps;
    }

    private static (string? Label, string? Path) ResolveNextAction(
        IReadOnlyList<CampaignStepStatusDto> steps,
        SupervisorCellulePrimeDraft? draft,
        bool canRollover)
    {
        if (canRollover && draft is null)
            return ("Reconduire depuis le mois précédent", "/prime-saisie");

        var blocked = steps.FirstOrDefault(s => s.State == "blocked");
        if (blocked is not null)
            return ($"Débloquer : {blocked.Label}", blocked.ActionPath);

        var todo = steps.FirstOrDefault(s => s.State == "todo");
        if (todo is not null)
            return ($"Continuer : {todo.Label}", todo.ActionPath);

        return ("Suivre la validation", "/prime-validation-hub?tab=validate");
    }

    private async Task<(int Complete, int InProgress, int NotStarted, int Pending, int SupervisorApproved, int Rejected)>
        CountFicheStatesAsync(Guid draftId, int totalEmployees, CancellationToken ct)
    {
        var fiches = await db.EmployeePrimeServiceFiches.AsNoTracking()
            .Where(f => f.CellulePrimeDraftId == draftId)
            .ToListAsync(ct);

        var complete = fiches.Count(f => string.Equals(f.FillingStatus, "Complete", StringComparison.OrdinalIgnoreCase));
        var inProgress = fiches.Count(f => string.Equals(f.FillingStatus, "InProgress", StringComparison.OrdinalIgnoreCase));
        var notStarted = Math.Max(0, totalEmployees - complete - inProgress);
        var pending = fiches.Count(f =>
            string.Equals(f.ValidationStatus, PrimeValidationWorkflowService.Pending, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(f.ValidationStatus, "Référent technique Approved", StringComparison.OrdinalIgnoreCase));
        var supervisorApproved = fiches.Count(f =>
            string.Equals(f.ValidationStatus, "Superviseur Approved", StringComparison.OrdinalIgnoreCase));
        var rejected = fiches.Count(f =>
            string.Equals(f.ValidationStatus, PrimeValidationWorkflowService.Rejected, StringComparison.OrdinalIgnoreCase));

        return (complete, inProgress, notStarted, pending, supervisorApproved, rejected);
    }

    private async Task<int> CountCompleteFichesAsync(Guid draftId, int totalEmployees, CancellationToken ct)
    {
        if (totalEmployees <= 0) return 0;
        var (complete, _, _, _, _, _) = await CountFicheStatesAsync(draftId, totalEmployees, ct);
        return complete;
    }

    private static HashSet<string> ParseSchemaStableIds(string schemaJson)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(schemaJson);
            if (!doc.RootElement.TryGetProperty("lines", out var lines) || lines.ValueKind != System.Text.Json.JsonValueKind.Array)
                return set;
            foreach (var line in lines.EnumerateArray())
            {
                if (line.TryGetProperty("stableId", out var sid))
                {
                    var s = sid.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(s)) set.Add(s);
                }
            }
        }
        catch
        {
            /* ignore */
        }

        return set;
    }

    private static DateTimeOffset ParsePeriodStart(string period)
    {
        var m = System.Text.RegularExpressions.Regex.Match(period, @"^(\d{4})-(\d{2})$");
        if (!m.Success) return DateTimeOffset.UtcNow;
        var y = int.Parse(m.Groups[1].Value);
        var mo = int.Parse(m.Groups[2].Value);
        return new DateTimeOffset(y, mo, 1, 0, 0, 0, TimeSpan.Zero);
    }

    private static string PreviousPeriod(string period)
    {
        var m = System.Text.RegularExpressions.Regex.Match(period, @"^(\d{4})-(\d{2})$");
        if (!m.Success) return period;
        var y = int.Parse(m.Groups[1].Value);
        var mo = int.Parse(m.Groups[2].Value);
        var d = new DateTime(y, mo, 1).AddMonths(-1);
        return $"{d.Year}-{d.Month:D2}";
    }

    private static bool IsMissingRelation(Exception ex)
    {
        for (var cur = ex; cur is not null; cur = cur.InnerException)
        {
            var msg = cur.Message;
            if (msg.Contains("42P01", StringComparison.Ordinal) ||
                msg.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
