using Conge.Domain.Enums;

namespace Conge.Domain.Events;

public record CongeDemandeEvent(
    Guid DemandeId,
    Guid EmployeId,
    Guid ManagerId,
    TypeConge TypeConge,
    double NombreJours);

public record CongeAnnuleEvent(
    Guid DemandeId,
    Guid EmployeId,
    double NombreJours,
    TypeConge TypeConge);

public record SoldeInitialiseEvent(
    Guid EmployeId,
    double SoldeInitial,
    int Annee);