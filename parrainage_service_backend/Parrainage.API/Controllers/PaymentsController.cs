using MediatR;
using Microsoft.AspNetCore.Mvc;
using Parrainage.Application.Abstractions;
using Parrainage.Application.Admin;
using Parrainage.Application.Authorization;
using Parrainage.Application.DTOs;
using Parrainage.Application.Payments;

namespace Parrainage.API.Controllers;

[ApiController]
[Route("api/parrainage/payments")]
public sealed class PaymentsController(
    IMediator mediator,
    IParrainageRequestUserResolver userResolver) : ControllerBase
{
    [HttpGet("inbox")]
    public async Task<ActionResult<PaymentInboxDto>> Inbox(CancellationToken ct)
    {
        var user = userResolver.Resolve(null, null, null);
        if (!ParrainageRoleGuard.CanMarkPayment(user.Role) && user.Role != "ADMIN")
            return Forbid();

        return Ok(await mediator.Send(new GetPaymentInboxQuery(), ct));
    }

    [HttpPost("pay-all")]
    public async Task<ActionResult<object>> PayAll([FromBody] MarkReferralPaymentRequest body, CancellationToken ct)
    {
        var user = userResolver.Resolve(null, null, null);
        if (!ParrainageRoleGuard.CanMarkPayment(user.Role))
            return Forbid();

        body.Actor ??= new ActorDto { Id = user.UserId, Label = user.Role, Role = user.Role };

        var result = await mediator.Send(new PayAllReferralsCommand(body), ct);
        return Ok(new { paid = result.Paid, total = result.Total });
    }
}
