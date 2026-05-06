using PlanningService.Enums;

namespace PlanningService.Models
{
    public class Reclamation
    {
        public int Id { get; set; }
        public string Titre { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ReclamationType Type { get; set; }
        public ReclamationStatus Status { get; set; } = ReclamationStatus.Soumise;
        public Priority Priorite { get; set; } = Priority.Normale;

        // Auteur
        public string AuteurId { get; set; } = string.Empty;
        public string AuteurNom { get; set; } = string.Empty;
        public string AuteurRole { get; set; } = string.Empty;

        // Assignation
        public string? AssigneeId { get; set; }
        public string? AssigneeNom { get; set; }

        // Traitement
        public string? CommentaireTraitement { get; set; }
        public int? SatisfactionNote { get; set; }   // 1–5
        public string? SatisfactionCommentaire { get; set; }

        // Dates
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? TraiteeAt { get; set; }
        public DateTime? ClotureeAt { get; set; }

        // Audit
        public List<ReclamationHistorique> Historique { get; set; } = new();
    }

    public class ReclamationHistorique
    {
        public int Id { get; set; }
        public int ReclamationId { get; set; }
        public Reclamation Reclamation { get; set; } = null!;

        public string Action { get; set; } = string.Empty;       // ex: "Statut changé", "Assigné à"
        public string Valeur { get; set; } = string.Empty;        // ex: "EnCours", "Jean Dupont"
        public string EffectueParId { get; set; } = string.Empty;
        public string EffectueParNom { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Proposition
    {
        public int Id { get; set; }
        public string Titre { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? BeneficeAttendu { get; set; }
        public PropositionStatus Status { get; set; } = PropositionStatus.Soumise;
        public Priority Priorite { get; set; } = Priority.Normale;

        // Auteur
        public string AuteurId { get; set; } = string.Empty;
        public string AuteurNom { get; set; } = string.Empty;
        public string AuteurRole { get; set; } = string.Empty;

        // Assignation
        public string? AssigneeId { get; set; }
        public string? AssigneeNom { get; set; }

        // Évaluation
        public string? CommentaireEvaluation { get; set; }
        public int? SatisfactionNote { get; set; }
        public string? SatisfactionCommentaire { get; set; }

        // Dates
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? EvalueeAt { get; set; }
        public DateTime? ImplementeeAt { get; set; }

        // Audit
        public List<PropositionHistorique> Historique { get; set; } = new();
    }

    public class PropositionHistorique
    {
        public int Id { get; set; }
        public int PropositionId { get; set; }
        public Proposition Proposition { get; set; } = null!;

        public string Action { get; set; } = string.Empty;
        public string Valeur { get; set; } = string.Empty;
        public string EffectueParId { get; set; } = string.Empty;
        public string EffectueParNom { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}