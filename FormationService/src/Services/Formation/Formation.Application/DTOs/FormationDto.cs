using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Formation.Domain.Enums;

namespace Formation.Application.DTOs;

public record FormationDto(
    Guid Id,
    string Titre,
    string Description,
    string Formateur,
    DateTime DateDebut,
    DateTime DateFin,
    int CapaciteMax,
    int NombreInscrits,
    decimal Prix,
    StatutFormation Statut,
    DateTime CreatedAt
);

public record InscriptionDto(
    Guid Id,
    Guid FormationId,
    string TitreFormation,
    Guid EmployeId,
    string NomEmploye,
    StatutInscription Statut,
    int Progression,
    DateTime? DateValidation,
    DateTime CreatedAt
);
