using Conge.Application.Commands.AnnulerConge;
using Conge.Application.Commands.ConfigConge;
using Conge.Application.Commands.DemanderConge;
using Conge.Application.Commands.RefuserConge;
using Conge.Application.Commands.ValiderConge;
using Conge.Application.Queries.GetDemandesByEmploye;
using Conge.Application.Queries.GetDemandesByManager;
using Conge.Application.Queries.GetHistorique;
using Conge.Application.Queries.GetHistoriqueRh;
using Conge.Application.Queries.GetPendingCongesRhCount;
using Conge.Application.Queries.GetSoldeByEmploye;
using Conge.Domain.Enums;
using Conge.Domain.Interfaces;
using Kyntus.Iam;
using Kyntus.Identity.Jwt;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Conge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CongesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IDemandeCongeRepository _demandeRepo;
    private readonly IRebacClient? _rebac;

    public CongesController(
        IMediator mediator,
        IDemandeCongeRepository demandeRepo,
        IRebacClient? rebac = null)
    {
        _mediator = mediator;
        _demandeRepo = demandeRepo;
        _rebac = rebac;
    }

    // ── Demandes ─────────────────────────────────────────────────────────────

    /// <summary>POST /api/conges — Soumettre une demande de congé</summary>
    [HttpPost]
    public async Task<IActionResult> DemanderConge([FromBody] DemanderCongeCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetDemandesByEmploye), new { employeId = command.EmployeId }, new { id });
    }

    /// <summary>GET /api/conges/employe/{employeId} — Consulter ses congés</summary>
    [HttpGet("employe/{employeId:guid}")]
    public async Task<IActionResult> GetDemandesByEmploye(
        Guid employeId,
        [FromQuery] StatutDemande? statut,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDemandesByEmployeQuery(employeId, statut), ct);
        return Ok(result);
    }

    /// <summary>GET /api/conges/manager/{managerId} — File validation (superviseur ou RH)</summary>
    [HttpGet("manager/{managerId:guid}")]
    public async Task<IActionResult> GetDemandesByManager(Guid managerId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDemandesByManagerQuery(managerId), ct);
        return Ok(result);
    }

    /// <summary>GET /api/conges/historique — Historique global par année (RH)</summary>
    [HttpGet("historique")]
    public async Task<IActionResult> GetHistoriqueRh(
        [FromQuery] int annee,
        CancellationToken ct)
    {
        if (annee < 2000 || annee > 2100)
            return BadRequest(new { message = "Année invalide." });

        var result = await _mediator.Send(new GetHistoriqueRhQuery(annee), ct);
        return Ok(result);
    }

    /// <summary>GET /api/conges/rh/pending-count — Demandes RH en attente (dashboard)</summary>
    [HttpGet("rh/pending-count")]
    public async Task<IActionResult> GetPendingRhCount(CancellationToken ct)
    {
        var count = await _mediator.Send(new GetPendingCongesRhCountQuery(), ct);
        return Ok(new { count });
    }

    /// <summary>GET /api/conges/employe/{employeId}/historique — Historique par année</summary>
    [HttpGet("employe/{employeId:guid}/historique")]
    public async Task<IActionResult> GetHistorique(
        Guid employeId,
        [FromQuery] int annee,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetHistoriqueQuery(employeId, annee), ct);
        return Ok(result);
    }

    /// <summary>GET /api/conges/disponibilite — Contrôle période interdite + quota avant demande</summary>
    [HttpGet("disponibilite")]
    public async Task<IActionResult> GetDisponibilite(
        [FromQuery] Guid employeId,
        [FromQuery] DateTime debut,
        [FromQuery] DateTime fin,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDisponibiliteCongeQuery(employeId, debut, fin), ct);
        return Ok(result);
    }

    // ── Config ───────────────────────────────────────────────────────────────

    [HttpGet("config/periodes-interdites")]
    public async Task<IActionResult> GetPeriodesInterdites(CancellationToken ct)
        => Ok(await _mediator.Send(new GetPeriodesInterditesQuery(), ct));

    [HttpPut("config/periodes-interdites")]
    public async Task<IActionResult> UpdatePeriodesInterdites(
        [FromBody] UpdatePeriodesInterditesRequest body,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdatePeriodesInterditesCommand(body.Mois ?? Array.Empty<int>(), body.UpdatedBy), ct);
        return Ok(result);
    }

    [HttpGet("config/quotas-service")]
    public async Task<IActionResult> GetQuotasService([FromQuery] Guid superviseurId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetQuotasServiceQuery(superviseurId), ct));

    [HttpPut("config/quotas-service")]
    public async Task<IActionResult> UpsertQuotaService(
        [FromBody] UpsertQuotaServiceRequest body,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpsertQuotaServiceCommand(
                body.ServiceId,
                body.MaxAbsentsSimultanes,
                body.SuperviseurId,
                body.ScopeKind), ct);
        return Ok(result);
    }

    [HttpGet("config/quotas-service/{serviceId}")]
    public async Task<IActionResult> GetQuotaService(string serviceId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetQuotaServiceByIdQuery(serviceId), ct));

    // ── Validation / Refus ───────────────────────────────────────────────────

    /// <summary>PUT /api/conges/{id}/valider-superviseur</summary>
    [HttpPut("{id:guid}/valider-superviseur")]
    public async Task<IActionResult> ValiderSuperviseur(
        Guid id,
        [FromBody] ValiderCongeRequest? request,
        CancellationToken ct)
    {
        var actorId = RequireActorId();
        if (actorId is null)
            return Unauthorized(new { message = "Sujet JWT manquant." });

        var forbidden = await EnsureCanActOnDemandeAsync(id, actorId.Value, requireSuperviseurScope: true, ct);
        if (forbidden is not null)
            return forbidden;

        await _mediator.Send(new ValiderCongeSuperviseurCommand(id, actorId.Value, request?.Commentaire), ct);
        return NoContent();
    }

    /// <summary>PUT /api/conges/{id}/valider-rh</summary>
    [HttpPut("{id:guid}/valider-rh")]
    public async Task<IActionResult> ValiderRh(
        Guid id,
        [FromBody] ValiderCongeRequest? request,
        CancellationToken ct)
    {
        var actorId = RequireActorId();
        if (actorId is null)
            return Unauthorized(new { message = "Sujet JWT manquant." });

        await _mediator.Send(new ValiderCongeRhCommand(id, actorId.Value, request?.Commentaire), ct);
        return NoContent();
    }

    /// <summary>PUT /api/conges/{id}/valider — Compat (oriente selon statut)</summary>
    [HttpPut("{id:guid}/valider")]
    public async Task<IActionResult> Valider(
        Guid id,
        [FromBody] ValiderCongeRequest? request,
        CancellationToken ct)
    {
        var actorId = RequireActorId();
        if (actorId is null)
            return Unauthorized(new { message = "Sujet JWT manquant." });

        var demande = await _demandeRepo.GetByIdAsync(id, ct);
        if (demande is null)
            return NotFound();

        if (demande.Statut == StatutDemande.EnAttente)
        {
            var forbidden = await EnsureCanActOnDemandeAsync(id, actorId.Value, requireSuperviseurScope: true, ct);
            if (forbidden is not null)
                return forbidden;
        }

        await _mediator.Send(new ValiderCongeCommand(id, actorId.Value, request?.Commentaire), ct);
        return NoContent();
    }

    /// <summary>PUT /api/conges/{id}/refuser — Refuser une demande</summary>
    [HttpPut("{id:guid}/refuser")]
    public async Task<IActionResult> Refuser(
        Guid id,
        [FromBody] RefuserCongeRequest request,
        CancellationToken ct)
    {
        var actorId = RequireActorId();
        if (actorId is null)
            return Unauthorized(new { message = "Sujet JWT manquant." });

        var demande = await _demandeRepo.GetByIdAsync(id, ct);
        if (demande is null)
            return NotFound();

        if (demande.Statut == StatutDemande.EnAttente)
        {
            var forbidden = await EnsureCanActOnDemandeAsync(id, actorId.Value, requireSuperviseurScope: true, ct);
            if (forbidden is not null)
                return forbidden;
        }

        await _mediator.Send(new RefuserCongeCommand(id, actorId.Value, request.Commentaire), ct);
        return NoContent();
    }

    /// <summary>PUT /api/conges/{id}/annuler — Annuler une demande (employé)</summary>
    [HttpPut("{id:guid}/annuler")]
    public async Task<IActionResult> Annuler(Guid id, [FromQuery] Guid employeId, CancellationToken ct)
    {
        var actorId = RequireActorId();
        if (actorId is not null)
            employeId = actorId.Value;

        await _mediator.Send(new AnnulerCongeCommand(id, employeId), ct);
        return NoContent();
    }

    // ── Solde ────────────────────────────────────────────────────────────────

    /// <summary>GET /api/conges/employe/{employeId}/solde — Consulter le solde</summary>
    [HttpGet("employe/{employeId:guid}/solde")]
    public async Task<IActionResult> GetSolde(
        Guid employeId,
        [FromQuery] int? annee,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSoldeByEmployeQuery(employeId, annee), ct);
        return Ok(result);
    }

    private Guid? RequireActorId() => User.GetSubjectId();

    /// <summary>
    /// Si ReBAC est disponible, exige CanActOn pour le périmètre superviseur.
    /// Sinon on laisse passer (acteur JWT déjà injecté dans la commande).
    /// </summary>
    private async Task<IActionResult?> EnsureCanActOnDemandeAsync(
        Guid demandeId,
        Guid actorId,
        bool requireSuperviseurScope,
        CancellationToken ct)
    {
        if (!requireSuperviseurScope || _rebac is null)
            return null;

        var demande = await _demandeRepo.GetByIdAsync(demandeId, ct);
        if (demande is null)
            return NotFound();

        try
        {
            var allowed = await _rebac.CanActOnAsync(actorId, demande.EmployeId, ct);
            if (!allowed)
                return Forbid();
        }
        catch
        {
            // ReBAC indisponible : ne bloque pas (Phase 3 soft-wire).
        }

        return null;
    }
}

public record ValiderCongeRequest(string? Commentaire);
public record RefuserCongeRequest(string Commentaire);
public record UpdatePeriodesInterditesRequest(IReadOnlyList<int>? Mois, Guid? UpdatedBy = null);
public record UpsertQuotaServiceRequest(
    string ServiceId,
    int MaxAbsentsSimultanes,
    Guid SuperviseurId,
    string? ScopeKind = null);
