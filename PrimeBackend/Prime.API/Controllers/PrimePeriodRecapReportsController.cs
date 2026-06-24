using MediatR;
using Microsoft.AspNetCore.Mvc;
using Prime.Application;
using Prime.Application.Abstractions;
using Prime.Application.Org;

namespace Prime.API.Controllers;

[ApiController]
[Route("api/prime/reports")]
public sealed class PrimePeriodRecapReportsController(
    IMediator mediator,
    IPrimePeriodRecapReportsAppService? reports) : ControllerBase
{
    [HttpGet("period-primes-recap.xlsx")]
    public async Task<IActionResult> DownloadPeriodRecap(
        [FromQuery] string period, [FromQuery] string actingUserId, CancellationToken ct)
    {
        if (reports is null) return StatusCode(503, new { error = "Base de données non configurée." });
        try
        {
            var file = await mediator.Send(new DownloadPeriodRecapReportQuery(period, actingUserId), ct);
            return File(file.Content, file.ContentType, file.FileName);
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (PrimeApiException ex) { return StatusCode(ex.StatusCode, new { error = ex.Message }); }
    }
}
