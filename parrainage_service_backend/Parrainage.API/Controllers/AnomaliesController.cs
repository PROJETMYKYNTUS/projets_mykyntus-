using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parrainage.Application.Anomalies;
using Parrainage.Application.DTOs;

namespace Parrainage.API.Controllers;

[ApiController]
[Route("api/parrainage/anomalies")]
public sealed class AnomaliesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AnomaliesDto>> Get(CancellationToken ct) =>
        Ok(await mediator.Send(new GetAnomaliesQuery(), ct));
}
