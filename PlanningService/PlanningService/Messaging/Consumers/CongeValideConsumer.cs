using MassTransit;
using Planning.Messaging.Messages;


namespace PlanningService.Messaging.Consumers;

/// <summary>
/// Reçu depuis Conge Service quand un congé est validé.
/// Met à jour le planning des absences.
/// </summary>
public class CongeValideConsumer : IConsumer<CongeValideMessage>
{
    private readonly ILogger<CongeValideConsumer> _logger;
    // Injecte ton AppDbContext si tu veux persister les absences
    // private readonly AppDbContext _context;

    public CongeValideConsumer(ILogger<CongeValideConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CongeValideMessage> context)
    {
        var msg = context.Message;

        _logger.LogInformation(
            "📥 Congé validé reçu → EmployeId: {EmployeId} | Du {Debut} au {Fin} ({Jours} jours)",
            msg.EmployeId, msg.DateDebut.ToShortDateString(),
            msg.DateFin.ToShortDateString(), msg.NombreJours);

        // TODO : mettre à jour ton planning / absences ici
        // Exemple :
        // var absence = new Absence
        // {
        //     EmployeId  = msg.EmployeId,
        //     DateDebut  = msg.DateDebut,
        //     DateFin    = msg.DateFin,
        //     NombreJours = msg.NombreJours,
        //     Type       = "Congé"
        // };
        // _context.Absences.Add(absence);
        // await _context.SaveChangesAsync(context.CancellationToken);

        await Task.CompletedTask;
    }
}
