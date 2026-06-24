using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parrainage.Application.Config;
using Parrainage.Application.DTOs;

namespace Parrainage.API.Controllers;

[ApiController]
[Route("api/parrainage/config")]
public sealed class ConfigController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SystemConfigDto>> Get(CancellationToken ct) =>
        Ok(await mediator.Send(new GetSystemConfigQuery(), ct));

    [HttpPatch]
    public async Task<ActionResult<SystemConfigDto>> Update([FromBody] UpdateConfigRequest body, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateSystemConfigCommand(body), ct));
}
