using Conge.Domain.Enums;

namespace Conge.Domain.Events;

public record CongeValideEvent(
    Guid DemandeId,
    Guid EmployeId,
    Guid ManagerId,
    double NombreJours,
    TypeConge TypeConge);