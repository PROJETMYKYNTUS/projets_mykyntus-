using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parrainage.Application.DTOs;
using Parrainage.Application.Rules;

namespace Parrainage.API.Controllers;

[ApiController]
[Route("api/parrainage/rules")]
public sealed class RulesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ReferralRuleDto>>> List(CancellationToken ct) =>
        Ok(await mediator.Send(new ListReferralRulesQuery(), ct));

    [HttpGet("catalog")]
    public async Task<ActionResult<List<ReferralRuleCatalogDto>>> Catalog(CancellationToken ct) =>
        Ok(await mediator.Send(new GetReferralRulesCatalogQuery(), ct));

    [HttpPut("{id}")]
    public async Task<ActionResult<ReferralRuleDto>> Upsert(string id, [FromBody] UpsertRuleRequest body, CancellationToken ct)
    {
        try
        {
            return Ok(await mediator.Send(new UpsertReferralRuleCommand(id, body), ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        var deleted = await mediator.Send(new DeleteReferralRuleCommand(id), ct);
        return deleted ? NoContent() : NotFound(new { error = $"Règle introuvable : {id}" });
    }
}
