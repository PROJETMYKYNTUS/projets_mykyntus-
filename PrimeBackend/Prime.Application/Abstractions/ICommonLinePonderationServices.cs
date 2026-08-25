using Prime.Application.DTOs;

namespace Prime.Application.Abstractions;

public interface ICommonLinePonderationResolver
{
    Task<IReadOnlyList<EffectiveCommonLinePonderationDto>> ResolveAsync(
        string? serviceId,
        string? celluleId,
        string templateId,
        DateTimeOffset at,
        IReadOnlyList<TemplateCommonLineHint>? templateLines = null,
        IReadOnlyList<TemplateCommonLineHint>? previousPeriodLines = null,
        CancellationToken ct = default);

    Task FreezeOntoFicheIfMissingAsync(
        Prime.Domain.Entities.EmployeePrimeServiceFiche fiche,
        string templateId,
        CancellationToken ct = default);

    /// <summary>Défauts pondération lus depuis le schemaJson du dernier draft cellule/template.</summary>
    Task<IReadOnlyList<TemplateCommonLineHint>> LoadHintsFromLatestDraftAsync(
        string? celluleId,
        string templateId,
        CancellationToken ct = default);

    /// <summary>Pondérations cellule actives à la fin du mois précédent (défauts historiques).</summary>
    Task<IReadOnlyList<TemplateCommonLineHint>> BuildPreviousPeriodHintsAsync(
        string? celluleId,
        string templateId,
        DateTimeOffset at,
        CancellationToken ct = default);
}

public interface ICommonLinePonderationsAppService
{
    Task<IReadOnlyList<EffectiveCommonLinePonderationDto>> GetCelluleEffectiveAsync(
        string celluleId,
        string supervisorUserId,
        string? templateId,
        DateTimeOffset? effectiveAt,
        IReadOnlyList<TemplateCommonLineHint>? templateLines = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<CommonLinePonderationDto>> PutCelluleAsync(
        string celluleId,
        string supervisorUserId,
        PutCommonLinePonderationsRequest body,
        CancellationToken ct = default);

    Task<IReadOnlyList<EffectiveCommonLinePonderationDto>> GetServiceEffectiveAsync(
        string serviceId,
        string supervisorUserId,
        string? templateId,
        DateTimeOffset? effectiveAt,
        IReadOnlyList<TemplateCommonLineHint>? templateLines = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<CommonLinePonderationDto>> PutServiceAsync(
        string serviceId,
        string supervisorUserId,
        PutCommonLinePonderationsRequest body,
        bool replaceAll = false,
        CancellationToken ct = default);

    Task DeleteServiceOverrideAsync(
        string serviceId,
        string templateStableId,
        string supervisorUserId,
        string? templateId,
        DateTimeOffset? effectiveAt,
        CancellationToken ct = default);

    Task<int> ConsolidateIdenticalServiceOverridesAsync(
        string celluleId,
        string supervisorUserId,
        string? templateId,
        DateTimeOffset? effectiveAt,
        CancellationToken ct = default);
}
