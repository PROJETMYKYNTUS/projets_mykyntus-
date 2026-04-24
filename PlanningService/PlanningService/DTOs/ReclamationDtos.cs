using PlanningService.Enums;
using System.ComponentModel.DataAnnotations;

namespace PlanningService.Data.DTOs
{
    // ──────────────────────────────────────────
    // RÉCLAMATION — DTOs
    // ──────────────────────────────────────────

    public class CreateReclamationDto
    {
        [Required, MinLength(5), MaxLength(200)]
        public string Titre { get; set; } = string.Empty;

        [Required, MinLength(10), MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public ReclamationType Type { get; set; }
    }

    public class UpdateReclamationStatusDto
    {
        [Required]
        public ReclamationStatus Status { get; set; }

        [MaxLength(1000)]
        public string? Commentaire { get; set; }
    }

    public class AssignReclamationDto
    {
        [Required]
        public string AssigneeId { get; set; } = string.Empty;

        [Required]
        public string AssigneeNom { get; set; } = string.Empty;
    }

    public class PrioriserReclamationDto
    {
        [Required]
        public Priority Priorite { get; set; }
    }

    public class SatisfactionDto
    {
        [Required, Range(1, 5)]
        public int Note { get; set; }

        [MaxLength(500)]
        public string? Commentaire { get; set; }
    }

    public class ReclamationDto
    {
        public int Id { get; set; }
        public string Titre { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Priorite { get; set; } = string.Empty;
        public string AuteurId { get; set; } = string.Empty;
        public string AuteurNom { get; set; } = string.Empty;
        public string AuteurRole { get; set; } = string.Empty;
        public string? AssigneeId { get; set; }
        public string? AssigneeNom { get; set; }
        public string? CommentaireTraitement { get; set; }
        public int? SatisfactionNote { get; set; }
        public string? SatisfactionCommentaire { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? TraiteeAt { get; set; }
        public DateTime? ClotureeAt { get; set; }
    }

    public class ReclamationDetailDto : ReclamationDto
    {
        public List<HistoriqueDto> Historique { get; set; } = new();
    }

    // ──────────────────────────────────────────
    // PROPOSITION — DTOs
    // ──────────────────────────────────────────

    public class CreatePropositionDto
    {
        [Required, MinLength(5), MaxLength(200)]
        public string Titre { get; set; } = string.Empty;

        [Required, MinLength(10), MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? BeneficeAttendu { get; set; }
    }

    public class UpdatePropositionStatusDto
    {
        [Required]
        public PropositionStatus Status { get; set; }

        [MaxLength(1000)]
        public string? Commentaire { get; set; }
    }

    public class AssignPropositionDto
    {
        [Required]
        public string AssigneeId { get; set; } = string.Empty;

        [Required]
        public string AssigneeNom { get; set; } = string.Empty;
    }

    public class PrioriserPropositionDto
    {
        [Required]
        public Priority Priorite { get; set; }
    }

    public class PropositionDto
    {
        public int Id { get; set; }
        public string Titre { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? BeneficeAttendu { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Priorite { get; set; } = string.Empty;
        public string AuteurId { get; set; } = string.Empty;
        public string AuteurNom { get; set; } = string.Empty;
        public string AuteurRole { get; set; } = string.Empty;
        public string? AssigneeId { get; set; }
        public string? AssigneeNom { get; set; }
        public string? CommentaireEvaluation { get; set; }
        public int? SatisfactionNote { get; set; }
        public string? SatisfactionCommentaire { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? EvalueeAt { get; set; }
        public DateTime? ImplementeeAt { get; set; }
    }

    public class PropositionDetailDto : PropositionDto
    {
        public List<HistoriqueDto> Historique { get; set; } = new();
    }

    // ──────────────────────────────────────────
    // COMMUN — DTOs
    // ──────────────────────────────────────────

    public class HistoriqueDto
    {
        public int Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string Valeur { get; set; } = string.Empty;
        public string EffectueParNom { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class SatisfactionReportDto
    {
        public double MoyenneNote { get; set; }
        public int TotalDemandes { get; set; }
        public int TotalAvecNote { get; set; }
        public Dictionary<int, int> RepartitionNotes { get; set; } = new();  // note → count
        public Dictionary<string, int> ParStatut { get; set; } = new();
        public Dictionary<string, int> ParPriorite { get; set; } = new();
    }

    public class PaginatedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}