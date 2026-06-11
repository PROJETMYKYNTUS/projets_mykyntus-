using Conge.Domain.Enums;
using System;

namespace Conge.Application.DTOs;

public record DemandeCongeDto(
    Guid Id,
    Guid EmployeId,
    Guid ManagerId,
    TypeConge TypeConge,
    TypeCongeExceptionnel? TypeExceptionnel,
    DateTime DateDebut,
    DateTime DateFin,
    double NombreJours,
    StatutDemande Statut,
    string? Motif,
    string? CommentaireManager,
    DateTime DateDemande,
    DateTime? DateDecision
);

public record SoldeCongeDto(
    Guid EmployeId,
    int Annee,
    double SoldeInitial,
    double SoldeUtilise,
    double SoldeRestant
);

public record EmployeSnapshotDto(
    Guid EmployeId,
    string Nom,
    string Prenom,
    string Email,
    Guid ManagerId,
    Guid ServiceId,
    string ServiceNom,
    DateTime DateEmbauche,
    int AncienneteAnnees,
    bool EstEligible
);

public record SuiviEquipeDto(
    Guid EmployeId,
    string NomComplet,
    string ServiceNom,
    int DemandesEnAttente,
    int DemandesValidees,
    double SoldeRestant
);