using Formation.Application.DTOs;
using Formation.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace Formation.API.Controllers;

[ApiController]
[Route("api/formations/document-definitions")]
public sealed class FormationDocumentDefinitionsController(FormationDocumentChecklistService checklist) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<FormationDocumentDefinitionDto>> List(CancellationToken ct) =>
        checklist.ListDefinitionsAsync(ct);

    [HttpPost]
    public async Task<ActionResult<FormationDocumentDefinitionDto>> Create(
        [FromBody] UpsertFormationDocumentDefinitionRequest body,
        CancellationToken ct) =>
        Ok(await checklist.CreateDefinitionAsync(body, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<FormationDocumentDefinitionDto>> Update(
        Guid id,
        [FromBody] UpsertFormationDocumentDefinitionRequest body,
        CancellationToken ct)
    {
        var updated = await checklist.UpdateDefinitionAsync(id, body, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ok = await checklist.DeleteDefinitionAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }
}
