using Documentation.Domain.Entities;

namespace Documentation.Application.Abstractions;

/// <summary>Contexte utilisateur résolu pour la requête HTTP courante.</summary>
public interface IDocumentationRequestContext
{
    Guid? UserId { get; }
    AppRole? Role { get; }
    bool IsComplete { get; }
    Guid? ScopeManagerId { get; }
    Guid? ScopeCoachId { get; }
}
