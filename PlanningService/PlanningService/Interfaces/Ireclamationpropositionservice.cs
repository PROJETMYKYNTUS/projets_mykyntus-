using PlanningService.Data.DTOs;

namespace PlanningService.Interfaces
{
    public interface IReclamationService
    {
        // ── Accessible à tous les rôles ──
        Task<ReclamationDto> SoumettreAsync(CreateReclamationDto dto, string auteurId, string auteurNom, string auteurRole);
        Task<PaginatedResult<ReclamationDto>> GetMesDemandesAsync(string auteurId, int page = 1, int pageSize = 20);
        Task<ReclamationDetailDto?> GetByIdAsync(int id, string userId);

        // ── RH, Manager, RP, Admin ──
        Task<PaginatedResult<ReclamationDto>> GetAllAsync(int page = 1, int pageSize = 20, string? status = null, string? priorite = null);
        Task TraiterAsync(int id, UpdateReclamationStatusDto dto, string traiteurId, string traiteurNom);
        Task AssignerAsync(int id, AssignReclamationDto dto, string assigneurId, string assigneurNom);
        Task PrioriserAsync(int id, PrioriserReclamationDto dto, string userId, string userNom);

        // ── RH, Manager, RP, Admin, Audit ──
        Task<SatisfactionReportDto> GetReportSatisfactionAsync(DateTime? from = null, DateTime? to = null);

        // ── RP, Admin, Audit ──
        Task<PaginatedResult<ReclamationDetailDto>> GetHistoriqueAsync(int? reclamationId = null, int page = 1, int pageSize = 20);

        // ── Auteur uniquement ──
        Task NoteSatisfactionAsync(int id, SatisfactionDto dto, string auteurId);
    }

    public interface IPropositionService
    {
        // ── Accessible à tous les rôles ──
        Task<PropositionDto> SoumettreAsync(CreatePropositionDto dto, string auteurId, string auteurNom, string auteurRole);
        Task<PaginatedResult<PropositionDto>> GetMesDemandesAsync(string auteurId, int page = 1, int pageSize = 20);
        Task<PropositionDetailDto?> GetByIdAsync(int id, string userId);

        // ── RH, Manager, RP, Admin ──
        Task<PaginatedResult<PropositionDto>> GetAllAsync(int page = 1, int pageSize = 20, string? status = null);
        Task EvaluerAsync(int id, UpdatePropositionStatusDto dto, string evaluateurId, string evaluateurNom);
        Task AssignerAsync(int id, AssignPropositionDto dto, string assigneurId, string assigneurNom);
        Task PrioriserAsync(int id, PrioriserPropositionDto dto, string userId, string userNom);

        // ── RH, Manager, RP, Admin, Audit ──
        Task<SatisfactionReportDto> GetReportSatisfactionAsync(DateTime? from = null, DateTime? to = null);

        // ── RP, Admin, Audit ──
        Task<PaginatedResult<PropositionDetailDto>> GetHistoriqueAsync(int? propositionId = null, int page = 1, int pageSize = 20);

        // ── Auteur uniquement ──
        Task NoteSatisfactionAsync(int id, SatisfactionDto dto, string auteurId);
    }
}