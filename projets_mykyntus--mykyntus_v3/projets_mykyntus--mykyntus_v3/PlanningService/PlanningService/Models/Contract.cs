using PlanningService.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanningService.Models
{
    public enum ContractType
    {
        CDI,
        CDD,
        Stage,
        ANAPEC
    }

    public enum ContractStatus
    {
        EnPeriodeEssai,  // CDI : période d'essai en cours
        Actif,           // Contrat actif / confirmé
        Expire,          // Contrat terminé
        Resilie          // Résilié avant terme
    }

    public class Contract
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }

        [Required]
        public ContractType Type { get; set; }

        [Required]
        public ContractStatus Status { get; set; } = ContractStatus.EnPeriodeEssai;

        [Required]
        public DateTime StartDate { get; set; }

        // Obligatoire pour CDD, Stage, Intérim — null pour CDI
        public DateTime? EndDate { get; set; }

        // Durée période d'essai en jours (CDI = 90j, CDD = selon durée)
        public int? ProbationDays { get; set; }

        // Date de fin de période d'essai (calculée à la création)
        public DateTime? ProbationEndDate { get; set; }

        // Seuil alerte en jours (défaut = 15)
        public int AlertThresholdDays { get; set; } = 15;

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}