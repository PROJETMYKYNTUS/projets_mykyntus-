using Formation.Domain.Enums;
using Formation.Infrastructure.Persistence;
using Formation.Infrastructure.Services;
using Kyntus.Messaging.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Formation.Infrastructure.Services;

/// <summary>
/// Alerte RH quotidienne : parcours initiaux à ≤ 7 jours de la fin avec documents manquants.
/// </summary>
public sealed class InitialTrainingMissingDocumentsAlertHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<InitialTrainingMissingDocumentsAlertHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("InitialTrainingMissingDocumentsAlert démarré.");
        // Premier passage après un court délai (laisser le démarrage DB se terminer).
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Erreur job alerte documents formation initiale.");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FormationDbContext>();
        var checklist = scope.ServiceProvider.GetRequiredService<FormationDocumentChecklistService>();
        var publish = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var today = DateTime.UtcNow.Date;
        var horizon = today.AddDays(7);

        var paths = await db.InitialTrainingPaths
            .Where(p => p.Status != InitialTrainingStatus.EnProduction
                        && p.Status != InitialTrainingStatus.Rejete
                        && p.DateFinPrevue.Date >= today
                        && p.DateFinPrevue.Date <= horizon)
            .ToListAsync(ct);

        if (paths.Count == 0) return;

        foreach (var path in paths)
            await checklist.MaterializeForPathAsync(path, ct);

        var summaries = await checklist.LoadSummariesAsync(paths.Select(p => p.Id).ToList(), ct);

        foreach (var path in paths)
        {
            if (!summaries.TryGetValue(path.Id, out var summary) || summary.MissingTitles.Count == 0)
                continue;

            await publish.Publish(new InitialTrainingMissingDocumentsAlertMessage
            {
                TrainingPathId = path.Id,
                EmployeeId = path.EmployeeId,
                EmployeeName = path.EmployeeName,
                DateFinPrevue = path.DateFinPrevue,
                MissingDocumentTitles = summary.MissingTitles.ToList(),
                AlertedAt = DateTime.UtcNow,
            }, ct);
        }

        logger.LogInformation(
            "Alerte documents formation : {Candidate} parcours dans la fenêtre J-7.",
            paths.Count);
    }
}
