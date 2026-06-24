using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parrainage.Application.Abstractions;
using Parrainage.Application.DTOs;
using Parrainage.Application.Notifications;

namespace Parrainage.API.Controllers;

[ApiController]
[Route("api/parrainage/notifications")]
public sealed class NotificationsController(
    IMediator mediator,
    IParrainageRequestUserResolver userResolver) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ReferralNotificationDto>>> List(
        [FromQuery] string? role,
        [FromQuery] string? userId,
        [FromQuery] string? projectId,
        CancellationToken ct)
    {
        var resolved = userResolver.Resolve(null, null, null, role, userId, projectId);
        var items = await mediator.Send(
            new ListNotificationsQuery(resolved.Role, resolved.UserId, resolved.ProjectId ?? projectId),
            ct);
        return Ok(items);
    }

    [HttpGet("preferences")]
    public async Task<ActionResult<NotificationPreferencesDto>> GetPreferences(CancellationToken ct) =>
        Ok(await mediator.Send(new GetNotificationPreferencesQuery(), ct));

    [HttpPatch("preferences")]
    public async Task<ActionResult<NotificationPreferencesDto>> UpdatePreferences(
        [FromBody] NotificationPreferencesDto body,
        CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateNotificationPreferencesCommand(body), ct));

    [HttpPost("read")]
    public async Task<IActionResult> MarkRead([FromBody] MarkReadRequest body, CancellationToken ct)
    {
        var ok = await mediator.Send(new MarkNotificationReadCommand(body.Id), ct);
        return ok ? NoContent() : NotFound(new { error = $"Notification introuvable : {body.Id}" });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await mediator.Send(new MarkAllNotificationsReadCommand(), ct);
        return NoContent();
    }
}
