using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Parrainage.Application.DTOs;
using Parrainage.Infrastructure.Persistence;
using Parrainage.Infrastructure.Services;

namespace Parrainage.Infrastructure.Messaging;

/// <summary>
/// Confirme automatiquement le passage en production des dossiers parrainage liés
/// lorsque Formation RH valide le parcours initiale.
/// </summary>
public sealed class InitialTrainingCompletedReferralConsumer(
    ParrainageDbContext db,
    ReferralWorkflowService workflow,
    ILogger<InitialTrainingCompletedReferralConsumer> logger) : IConsumer<InitialTrainingCompletedMessage>
{
    public async Task Consume(ConsumeContext<InitialTrainingCompletedMessage> context)
    {
        var msg = context.Message;
        if (msg.EmployeeId == Guid.Empty) return;

        var employeeId = msg.EmployeeId.ToString("D");
        var referrals = await db.Referrals
            .Where(r => r.CandidateEmployeeId == employeeId && r.Status == "IN_TRAINING")
            .ToListAsync(context.CancellationToken);

        if (referrals.Count == 0) return;

        var productionDate = msg.ProductionStartDate == default
            ? DateOnly.FromDateTime(msg.CompletedAt == default ? DateTime.UtcNow : msg.CompletedAt)
            : msg.ProductionStartDate;

        foreach (var referral in referrals)
        {
            try
            {
                var startDate = productionDate;

                // Formation peut valider avant la date de fin prévue : aligner pour ConfirmProduction.
                if (referral.TrainingEndDate.HasValue && startDate < referral.TrainingEndDate.Value)
                {
                    referral.TrainingEndDate = startDate;
                    await db.SaveChangesAsync(context.CancellationToken);
                }

                if (referral.CandidateStartDate.HasValue && startDate < referral.CandidateStartDate.Value)
                    startDate = referral.CandidateStartDate.Value;

                await workflow.ConfirmProductionStartAsync(
                    referral.Id,
                    new ConfirmProductionStartRequest
                    {
                        ProductionStartDate = startDate,
                        Comment = "Passage en production confirmé automatiquement (validation RH Formation).",
                        Actor = new ActorDto
                        {
                            Id = "formation-system",
                            Label = "Formation",
                        },
                    },
                    context.CancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Impossible de confirmer la production du dossier parrainage {ReferralId} après formation {PathId}.",
                    referral.Id,
                    msg.TrainingPathId);
            }
        }
    }
}
