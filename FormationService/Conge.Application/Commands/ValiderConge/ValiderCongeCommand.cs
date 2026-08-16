using MediatR;

namespace Conge.Application.Commands.ValiderConge;

public record ValiderCongeSuperviseurCommand(
    Guid DemandeId,
    Guid SuperviseurId,
    string? Commentaire = null
) : IRequest<bool>;

public record ValiderCongeRhCommand(
    Guid DemandeId,
    Guid RhId,
    string? Commentaire = null
) : IRequest<bool>;

/// <summary>Compat : route historique /valider — oriente selon le statut courant.</summary>
public record ValiderCongeCommand(
    Guid DemandeId,
    Guid ManagerId,
    string? Commentaire = null
) : IRequest<bool>;
