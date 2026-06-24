using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parrainage.Application.Admin;
using Parrainage.Application.DTOs;

namespace Parrainage.API.Controllers;

[ApiController]
[Route("api/parrainage/admin")]
public sealed class AdminExportController(IMediator mediator) : ControllerBase
{
    [HttpGet("export")]
    public async Task<ActionResult<ExportSnapshotDto>> Export(CancellationToken ct) =>
        Ok(await mediator.Send(new ExportAdminSnapshotQuery(), ct));
}
