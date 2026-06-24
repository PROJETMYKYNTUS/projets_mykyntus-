using Documentation.Application.Abstractions;
using Documentation.Application.Api;
using Microsoft.AspNetCore.Mvc;

namespace Documentation.API.Controllers;

[ApiController]
[Route("api/documentation/data")]
public sealed class DocumentTypesController(IDocumentTypeQueryService documentTypes) : ControllerBase
{
    [HttpGet("document-types")]
    public async Task<ActionResult<IReadOnlyList<DocumentTypeResponse>>> GetDocumentTypes(CancellationToken ct) =>
        Ok(await documentTypes.ListAsync(ct));
}
