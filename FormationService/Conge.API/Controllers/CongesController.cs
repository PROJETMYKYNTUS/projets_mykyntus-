using Conge.Application.Commands.AnnulerConge;
using Conge.Application.Commands.DemanderConge;
using Conge.Application.Commands.RefuserConge;
using Conge.Application.Commands.ValiderConge;
using Conge.Application.Queries.GetDemandesByEmploye;
using Conge.Application.Queries.GetDemandesByManager;
using Conge.Application.Queries.GetHistorique;
using Conge.Application.Queries.GetHistoriqueRh;
using Conge.Application.Queries.GetSoldeByEmploye;
using Conge.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Conge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CongesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CongesController(IMediator mediator) => _mediator = mediator;

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

    /// <summary>GET /api/conges/manager/{managerId} — Suivi équipe (manager)</summary>
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

    // ── Validation / Refus ───────────────────────────────────────────────────

    /// <summary>PUT /api/conges/{id}/valider — Valider une demande</summary>
    [HttpPut("{id:guid}/valider")]
    public async Task<IActionResult> Valider(Guid id, [FromBody] ValiderCongeRequest request, CancellationToken ct)
    {
        await _mediator.Send(new ValiderCongeCommand(id, request.ManagerId, request.Commentaire), ct);
        return NoContent();
    }

    /// <summary>PUT /api/conges/{id}/refuser — Refuser une demande</summary>
    [HttpPut("{id:guid}/refuser")]
    public async Task<IActionResult> Refuser(Guid id, [FromBody] RefuserCongeRequest request, CancellationToken ct)
    {
        await _mediator.Send(new RefuserCongeCommand(id, request.ManagerId, request.Commentaire), ct);
        return NoContent();
    }

    /// <summary>PUT /api/conges/{id}/annuler — Annuler une demande (employé)</summary>
    [HttpPut("{id:guid}/annuler")]
    public async Task<IActionResult> Annuler(Guid id, [FromQuery] Guid employeId, CancellationToken ct)
    {
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
}

// ── Request bodies ────────────────────────────────────────────────────────────

public record ValiderCongeRequest(Guid ManagerId, string? Commentaire);
public record RefuserCongeRequest(Guid ManagerId, string Commentaire);