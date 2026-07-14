using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Parrainage.Application.DTOs;
using Parrainage.Infrastructure.Persistence;
using Parrainage.Infrastructure.Services;

namespace Parrainage.Infrastructure.Messaging;

/// <summary>
/// Clôture les dossiers parrainage liés lorsqu'une formation initiale est rejetée (départ anticipé).
/// </summary>
public sealed class InitialTrainingRejectedReferralConsumer(
    ParrainageDbContext db,
    ReferralWorkflowService workflow,
    ILogger<InitialTrainingRejectedReferralConsumer> logger) : IConsumer<InitialTrainingRejectedMessage>
{
    public async Task Consume(ConsumeContext<InitialTrainingRejectedMessage> context)
    {
        var msg = context.Message;
        if (msg.EmployeeId == Guid.Empty) return;

        var employeeId = msg.EmployeeId.ToString("D");
        var referrals = await db.Referrals
            .Where(r => r.CandidateEmployeeId == employeeId
                        && (r.Status == "IN_TRAINING" || r.Status == "APPROVED"))
            .Select(r => r.Id)
            .ToListAsync(context.CancellationToken);

        if (referrals.Count == 0) return;

        var departureDate = DateOnly.FromDateTime(msg.RejectedAt == default ? DateTime.UtcNow : msg.RejectedAt);
        foreach (var id in referrals)
        {
            try
            {
                await workflow.RejectEarlyDepartureAsync(
                    id,
                    new RejectEarlyDepartureRequest
                    {
                        DepartureDate = departureDate,
                        Comment = $"Rejet formation initiale : {msg.Reason}".Trim(),
                        Actor = new ActorDto
                        {
                            Id = "formation-system",
                            Label = string.IsNullOrWhiteSpace(msg.RejectedBy) ? "Formation" : msg.RejectedBy,
                        },
                    },
                    context.CancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Impossible de clôturer le dossier parrainage {ReferralId} après rejet formation {PathId}.",
                    id,
                    msg.TrainingPathId);
            }
        }
    }
}
