using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PlanningService.Models
{
    public enum NotificationType
    {
        MiParcoursCDD,           // Mi-parcours CDD
        MiParcoursPeriodeEssai,  // Mi-parcours période d'essai CDI
        AvantFinCDD,             // X jours avant fin CDD
        AvantFinPeriodeEssai,    // X jours avant fin période d'essai
        AvantFinStage,           // X jours avant fin Stage
        AvantFinInterim          // X jours avant fin Intérim
    }

    public class ContractNotification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ContractId { get; set; }

        [ForeignKey("ContractId")]
        public Contract Contract { get; set; }

        [Required]
        public NotificationType Type { get; set; }

        [Required]
        public string Message { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReadAt { get; set; }
    }
}