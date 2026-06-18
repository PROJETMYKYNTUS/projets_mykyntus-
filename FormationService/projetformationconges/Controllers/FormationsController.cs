using System;
using System.Threading.Tasks;
using Formation.Application.Commands.CreateFormation;
using Formation.Application.Commands.DeleteFormationn;
using Formation.Application.Commands.InscrireFormationn;
using Formation.Application.Commands.UpdateFormationn;
using Formation.Application.Commands.ValidnerFormation;
using Formation.Application.Queriies.GetFormationById;
using Formation.Application.Queriies.GetFormations;
using Formation.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Formation.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FormationsController : ControllerBase
{
    private readonly IMediator _mediator;
    public FormationsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] StatutFormation? statut)
        => Ok(await _mediator.Send(new GetFormationsQuery(statut)));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
        => Ok(await _mediator.Send(new GetFormationByIdQuery(id)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFormationCommand cmd)
    {
        var id = await _mediator.Send(cmd);
        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFormationCommand cmd)
    {
        await _mediator.Send(cmd with { Id = id });
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteFormationCommand(id));
        return NoContent();
    }

    [HttpPost("{id:guid}/valider")]
    public async Task<IActionResult> Valider(Guid id)
    {
        await _mediator.Send(new ValiderFormationCommand(id));
        return NoContent();
    }

    [HttpPost("{id:guid}/inscrire")]
    public async Task<IActionResult> Inscrire(Guid id, [FromBody] InscrireFormationCommand cmd)
    {
        var inscriptionId = await _mediator.Send(cmd with { FormationId = id });
        return Ok(new { inscriptionId });
    }
}