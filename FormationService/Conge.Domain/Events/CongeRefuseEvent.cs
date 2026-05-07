using Conge.Domain.Enums;

namespace Conge.Domain.Events;

public record CongeRefuseEvent(
    Guid DemandeId,
    Guid EmployeId,
    Guid ManagerId,
    string Motif);