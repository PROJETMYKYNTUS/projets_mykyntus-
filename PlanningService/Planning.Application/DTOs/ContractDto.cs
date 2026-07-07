// DTOs/ContractDto.cs

using System;
using Planning.Domain.Entities;

namespace Planning.Application.DTOs
{
    // -- Cr�ation d'un contrat --
    public class CreateContractDto
    {
        public int UserId { get; set; }
        public ContractType Type { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }      // Obligatoire si CDD/Stage/Int�rim
        public int? ProbationDays { get; set; }     // Si null ? valeur par d�faut selon type
        public int AlertThresholdDays { get; set; } = 15;
        public string? Notes { get; set; }
        public ContractStatus? Status { get; set; }
    }

    // -- Mise � jour d'un contrat --
    public class UpdateContractDto
    {
        public ContractType? Type { get; set; }
        public ContractStatus? Status { get; set; }  // ? enum, pas string
        public DateTime? EndDate { get; set; }
        public int? ProbationDays { get; set; }
        public int? AlertThresholdDays { get; set; }
        public string? Notes { get; set; }
    }

    // -- R�ponse contrat (lecture) --
    public class ContractResponseDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserFullName { get; set; }    // Pr�nom + Nom de l'employ�

        public string Type { get; set; }            // "CDI", "CDD", "Stage", "Int�rim"
        public string Status { get; set; }          // "En p�riode d'essai", "Actif", etc.

        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? ProbationEndDate { get; set; }

        public int? JoursRestants { get; set; }           // Jours avant fin contrat
        public int? JoursRestantsPeriodeEssai { get; set; } // Jours avant fin p�riode d'essai

        public bool IsAlertActive { get; set; }    // True si dans la zone d'alerte
        public int AlertThresholdDays { get; set; }

        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class EmploymentSummaryDto
    {
        public int UserId { get; set; }
        public Guid EmployeeGuid { get; set; }
        public bool IsActive { get; set; }
        public bool HasContract { get; set; }
        public string? ContractStatus { get; set; }
        public DateTime? ProbationEndDate { get; set; }
        public bool IsEligibleForPaymentConfirmation { get; set; }
        public string? BlockReason { get; set; }
    }

    // -- Notification --
    public class NotificationResponseDto
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public int UserId { get; set; }
        public string UserFullName { get; set; }
        public string Type { get; set; }
        public string Message { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}