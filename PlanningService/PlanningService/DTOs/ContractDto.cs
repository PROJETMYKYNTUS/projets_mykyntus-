// DTOs/ContractDto.cs

using System;
using PlanningService.Models;

namespace PlanningService.DTOs
{
    // ── Création d'un contrat ──
    public class CreateContractDto
    {
        public int UserId { get; set; }
        public ContractType Type { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }      // Obligatoire si CDD/Stage/Intérim
        public int? ProbationDays { get; set; }     // Si null → valeur par défaut selon type
        public int AlertThresholdDays { get; set; } = 15;
        public string? Notes { get; set; }
    }

    // ── Mise à jour d'un contrat ──
    public class UpdateContractDto
    {
        public ContractType? Type { get; set; }
        public ContractStatus? Status { get; set; }  // ← enum, pas string
        public DateTime? EndDate { get; set; }
        public int? ProbationDays { get; set; }
        public int? AlertThresholdDays { get; set; }
        public string? Notes { get; set; }
    }

    // ── Réponse contrat (lecture) ──
    public class ContractResponseDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserFullName { get; set; }    // Prénom + Nom de l'employé

        public string Type { get; set; }            // "CDI", "CDD", "Stage", "Intérim"
        public string Status { get; set; }          // "En période d'essai", "Actif", etc.

        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? ProbationEndDate { get; set; }

        public int? JoursRestants { get; set; }           // Jours avant fin contrat
        public int? JoursRestantsPeriodeEssai { get; set; } // Jours avant fin période d'essai

        public bool IsAlertActive { get; set; }    // True si dans la zone d'alerte
        public int AlertThresholdDays { get; set; }

        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ── Notification ──
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