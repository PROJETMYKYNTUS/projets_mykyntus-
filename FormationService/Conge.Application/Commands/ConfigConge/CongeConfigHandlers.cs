using Conge.Application.Abstractions;
using Conge.Application.DTOs;
using Conge.Domain.Entities;
using Conge.Domain.Interfaces;
using Kyntus.Iam;
using MediatR;

namespace Conge.Application.Commands.ConfigConge;

public record GetPeriodesInterditesQuery : IRequest<PeriodesInterditesDto>;

public record UpdatePeriodesInterditesCommand(IReadOnlyList<int> Mois, Guid? UpdatedBy = null)
    : IRequest<PeriodesInterditesDto>;

public record GetQuotasServiceQuery(Guid SuperviseurId) : IRequest<IReadOnlyList<QuotaCongeServiceDto>>;

public record UpsertQuotaServiceCommand(
    string ServiceId,
    int MaxAbsentsSimultanes,
    Guid SuperviseurId,
    string? ScopeKind = null)
    : IRequest<QuotaCongeServiceDto>;

public record GetQuotaServiceByIdQuery(string ServiceId) : IRequest<QuotaCongeServiceDto?>;

public record GetDisponibiliteCongeQuery(Guid EmployeId, DateTime Debut, DateTime Fin)
    : IRequest<CongeDisponibiliteDto>;

public class GetPeriodesInterditesHandler : IRequestHandler<GetPeriodesInterditesQuery, PeriodesInterditesDto>
{
    private readonly IPeriodeInterditeRepository _repo;

    public GetPeriodesInterditesHandler(IPeriodeInterditeRepository repo) => _repo = repo;

    public async Task<PeriodesInterditesDto> Handle(GetPeriodesInterditesQuery request, CancellationToken ct)
    {
        var row = await _repo.GetOrCreateAsync(ct);
        return new PeriodesInterditesDto(row.GetMois().ToList(), row.UpdatedAt);
    }
}

public class UpdatePeriodesInterditesHandler : IRequestHandler<UpdatePeriodesInterditesCommand, PeriodesInterditesDto>
{
    private readonly IPeriodeInterditeRepository _repo;
    private readonly IUnitOfWork _uow;

    public UpdatePeriodesInterditesHandler(IPeriodeInterditeRepository repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    public async Task<PeriodesInterditesDto> Handle(UpdatePeriodesInterditesCommand request, CancellationToken ct)
    {
        var row = await _repo.GetOrCreateAsync(ct);
        row.MettreAJour(request.Mois, request.UpdatedBy);
        _repo.Update(row);
        await _uow.SaveChangesAsync(ct);
        return new PeriodesInterditesDto(row.GetMois().ToList(), row.UpdatedAt);
    }
}

public class GetQuotasServiceHandler : IRequestHandler<GetQuotasServiceQuery, IReadOnlyList<QuotaCongeServiceDto>>
{
    private readonly IEmployeSnapshotRepository _employeRepo;
    private readonly IQuotaCongeServiceRepository _quotaRepo;
    private readonly IDirectoryOrgCatalog? _orgCatalog;
    private readonly IRebacClient? _rebac;

    public GetQuotasServiceHandler(
        IEmployeSnapshotRepository employeRepo,
        IQuotaCongeServiceRepository quotaRepo,
        IDirectoryOrgCatalog? orgCatalog = null,
        IRebacClient? rebac = null)
    {
        _employeRepo = employeRepo;
        _quotaRepo = quotaRepo;
        _orgCatalog = orgCatalog;
        _rebac = rebac;
    }

    public async Task<IReadOnlyList<QuotaCongeServiceDto>> Handle(GetQuotasServiceQuery request, CancellationToken ct)
    {
        var catalog = _orgCatalog is null
            ? DirectoryOrgCatalogSnapshot.Empty
            : await _orgCatalog.GetSnapshotAsync(ct);

        var team = await CongeQuotaPerimeter.ResolveTeamAsync(
            _employeRepo, _rebac, request.SuperviseurId, ct);
        var nodes = await CongeQuotaPerimeter.ResolveQuotaNodesAsync(
            _employeRepo, _rebac, request.SuperviseurId, team, catalog, ct);

        var quotas = await _quotaRepo.GetByServiceIdsAsync(nodes.Select(n => n.NodeId), ct);
        var byId = quotas.ToDictionary(q => q.ServiceId, StringComparer.OrdinalIgnoreCase);

        return nodes.Select(n =>
        {
            byId.TryGetValue(n.NodeId, out var q);
            var scope = q is not null
                ? QuotaScopeKinds.Normalize(q.ScopeKind)
                : n.ScopeKind;
            return new QuotaCongeServiceDto(
                n.NodeId,
                n.Label,
                q?.MaxAbsentsSimultanes,
                n.Effectif,
                scope);
        }).OrderBy(x => x.ScopeKind).ThenBy(x => x.ServiceNom).ToList();
    }
}

public class UpsertQuotaServiceHandler : IRequestHandler<UpsertQuotaServiceCommand, QuotaCongeServiceDto>
{
    private readonly IEmployeSnapshotRepository _employeRepo;
    private readonly IQuotaCongeServiceRepository _quotaRepo;
    private readonly IUnitOfWork _uow;
    private readonly IDirectoryOrgCatalog? _orgCatalog;
    private readonly IRebacClient? _rebac;

    public UpsertQuotaServiceHandler(
        IEmployeSnapshotRepository employeRepo,
        IQuotaCongeServiceRepository quotaRepo,
        IUnitOfWork uow,
        IDirectoryOrgCatalog? orgCatalog = null,
        IRebacClient? rebac = null)
    {
        _employeRepo = employeRepo;
        _quotaRepo = quotaRepo;
        _uow = uow;
        _orgCatalog = orgCatalog;
        _rebac = rebac;
    }

    public async Task<QuotaCongeServiceDto> Handle(UpsertQuotaServiceCommand request, CancellationToken ct)
    {
        var nodeId = QuotaCongeService.NormalizeNodeId(request.ServiceId)
            ?? throw new ArgumentException("ServiceId requis.");

        var catalog = _orgCatalog is null
            ? DirectoryOrgCatalogSnapshot.Empty
            : await _orgCatalog.GetSnapshotAsync(ct);

        var team = await CongeQuotaPerimeter.ResolveTeamAsync(
            _employeRepo, _rebac, request.SuperviseurId, ct);
        var nodes = await CongeQuotaPerimeter.ResolveQuotaNodesAsync(
            _employeRepo, _rebac, request.SuperviseurId, team, catalog, ct);
        var inScope = nodes.FirstOrDefault(n =>
                string.Equals(n.NodeId, nodeId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Ce périmètre n'appartient pas à votre scope.");

        var scopeKind = QuotaScopeKinds.Normalize(request.ScopeKind ?? inScope.ScopeKind);
        var existing = await _quotaRepo.GetByServiceIdAsync(nodeId, ct);
        if (existing is null)
        {
            existing = QuotaCongeService.Creer(
                nodeId, request.MaxAbsentsSimultanes, request.SuperviseurId, scopeKind);
            await _quotaRepo.AddAsync(existing, ct);
        }
        else
        {
            existing.MettreAJour(request.MaxAbsentsSimultanes, request.SuperviseurId, scopeKind);
            _quotaRepo.Update(existing);
        }

        await _uow.SaveChangesAsync(ct);
        return new QuotaCongeServiceDto(
            nodeId,
            inScope.Label,
            existing.MaxAbsentsSimultanes,
            inScope.Effectif,
            existing.ScopeKind);
    }
}

public class GetQuotaServiceByIdHandler : IRequestHandler<GetQuotaServiceByIdQuery, QuotaCongeServiceDto?>
{
    private readonly IQuotaCongeServiceRepository _quotaRepo;
    private readonly IEmployeSnapshotRepository _employeRepo;
    private readonly IDirectoryOrgCatalog? _orgCatalog;

    public GetQuotaServiceByIdHandler(
        IQuotaCongeServiceRepository quotaRepo,
        IEmployeSnapshotRepository employeRepo,
        IDirectoryOrgCatalog? orgCatalog = null)
    {
        _quotaRepo = quotaRepo;
        _employeRepo = employeRepo;
        _orgCatalog = orgCatalog;
    }

    public async Task<QuotaCongeServiceDto?> Handle(GetQuotaServiceByIdQuery request, CancellationToken ct)
    {
        var nodeId = QuotaCongeService.NormalizeNodeId(request.ServiceId);
        if (nodeId is null) return null;

        var catalog = _orgCatalog is null
            ? DirectoryOrgCatalogSnapshot.Empty
            : await _orgCatalog.GetSnapshotAsync(ct);

        var q = await _quotaRepo.GetByServiceIdAsync(nodeId, ct);
        var peers = await _employeRepo.GetByOrgNodeIdAsync(nodeId, ct);
        var scope = q is not null
            ? QuotaScopeKinds.Normalize(q.ScopeKind)
            : CongeQuotaPerimeter.InferScopeKind(peers.FirstOrDefault(), nodeId);
        var nom = CongeQuotaPerimeter.ResolveNodeLabel(peers.FirstOrDefault(), nodeId, scope, catalog);
        var effectif = catalog.GetHeadcount(nodeId, scope);
        if (effectif == 0)
            effectif = peers.Count;
        if (q is null)
            return new QuotaCongeServiceDto(nodeId, nom, null, effectif, scope);
        return new QuotaCongeServiceDto(nodeId, nom, q.MaxAbsentsSimultanes, effectif, scope);
    }
}

public class GetDisponibiliteCongeHandler : IRequestHandler<GetDisponibiliteCongeQuery, CongeDisponibiliteDto>
{
    private readonly Services.CongeReglesService _regles;

    public GetDisponibiliteCongeHandler(Services.CongeReglesService regles) => _regles = regles;

    public async Task<CongeDisponibiliteDto> Handle(GetDisponibiliteCongeQuery request, CancellationToken ct)
    {
        var r = await _regles.EvaluerDisponibiliteAsync(request.EmployeId, request.Debut, request.Fin, ct);
        return new CongeDisponibiliteDto(r.Ok, r.Motif, r.MoisInterdits.ToList(), r.JoursSatures.ToList());
    }
}

/// <summary>Résolution du périmètre quotas (ReBAC org + ManagerId legacy + catalogue Directory).</summary>
internal static class CongeQuotaPerimeter
{
    internal sealed record QuotaNode(string NodeId, string ScopeKind, string Label, int Effectif);

    internal static async Task<IReadOnlyList<EmployeSnapshot>> ResolveTeamAsync(
        IEmployeSnapshotRepository employeRepo,
        IRebacClient? rebac,
        Guid actorId,
        CancellationToken ct)
    {
        var actor = await employeRepo.GetByEmployeIdAsync(actorId, ct);
        if (IsRhOrAdmin(actor?.Role))
            return await employeRepo.GetAllAsync(ct);

        var managed = await TryGetManagedNodeIdsAsync(rebac, actorId, ct);
        return await employeRepo.GetByPerimeterAsync(
            actorId,
            managed is { Count: > 0 } ? managed : null,
            ct);
    }

    internal static async Task<IReadOnlyList<QuotaNode>> ResolveQuotaNodesAsync(
        IEmployeSnapshotRepository employeRepo,
        IRebacClient? rebac,
        Guid actorId,
        IReadOnlyList<EmployeSnapshot> team,
        DirectoryOrgCatalogSnapshot catalog,
        CancellationToken ct)
    {
        var actor = await employeRepo.GetByEmployeIdAsync(actorId, ct);
        var map = new Dictionary<string, QuotaNode>(StringComparer.OrdinalIgnoreCase);

        if (IsRhOrAdmin(actor?.Role))
        {
            foreach (var e in team)
                AddEmployeeNodes(map, e, catalog);
            return map.Values.ToList();
        }

        if (rebac is not null)
        {
            try
            {
                var cellules = await rebac.GetManagedNodeIdsAsync(actorId, "Superviseur", ct);
                foreach (var raw in cellules)
                    TryAddManagedNode(map, raw, QuotaScopeKinds.Cellule, team, catalog);

                var services = await rebac.GetManagedNodeIdsAsync(actorId, "ReferentTechnique", ct);
                foreach (var raw in services)
                    TryAddManagedNode(map, raw, QuotaScopeKinds.Service, team, catalog);
            }
            catch
            {
                /* soft-wire */
            }
        }

        if (map.Count == 0)
        {
            foreach (var e in team)
                AddEmployeeNodes(map, e, catalog);
        }

        return map.Values.ToList();
    }

    private static void TryAddManagedNode(
        Dictionary<string, QuotaNode> map,
        string raw,
        string scopeKind,
        IReadOnlyList<EmployeSnapshot> team,
        DirectoryOrgCatalogSnapshot catalog)
    {
        var id = QuotaCongeService.NormalizeNodeId(raw);
        if (id is null) return;

        var peers = team.Where(e => MatchesNode(e, id)).ToList();
        var label = ResolveNodeLabel(peers.FirstOrDefault(), id, scopeKind, catalog);
        var effectif = catalog.GetHeadcount(id, scopeKind);
        if (effectif == 0)
            effectif = peers.Count;
        map[id] = new QuotaNode(id, scopeKind, label, effectif);
    }

    private static void AddEmployeeNodes(
        Dictionary<string, QuotaNode> map,
        EmployeSnapshot e,
        DirectoryOrgCatalogSnapshot catalog)
    {
        var celluleId = QuotaCongeService.NormalizeNodeId(e.CelluleId);
        if (celluleId is not null)
            UpsertBucket(map, celluleId, QuotaScopeKinds.Cellule, e, catalog);

        if (TryResolveServiceId(e, out var serviceId))
            UpsertBucket(map, serviceId, QuotaScopeKinds.Service, e, catalog);
    }

    private static void UpsertBucket(
        Dictionary<string, QuotaNode> map,
        string id,
        string scopeKind,
        EmployeSnapshot sample,
        DirectoryOrgCatalogSnapshot catalog)
    {
        var label = ResolveNodeLabel(sample, id, scopeKind, catalog);
        var catalogCount = catalog.GetHeadcount(id, scopeKind);
        if (map.TryGetValue(id, out var cur))
        {
            var effectif = catalogCount > 0 ? catalogCount : cur.Effectif + 1;
            map[id] = cur with
            {
                Effectif = effectif,
                Label = PreferNom(cur.Label, label)
            };
        }
        else
        {
            map[id] = new QuotaNode(id, scopeKind, label, catalogCount > 0 ? catalogCount : 1);
        }
    }

    internal static bool MatchesNode(EmployeSnapshot e, string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return false;
        return string.Equals(e.OrgServiceId, nodeId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(e.CelluleId, nodeId, StringComparison.OrdinalIgnoreCase)
            || (e.ServiceId != Guid.Empty
                && string.Equals(e.ServiceId.ToString(), nodeId, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool TryResolveServiceId(EmployeSnapshot e, out string serviceId)
    {
        var org = QuotaCongeService.NormalizeNodeId(e.OrgServiceId);
        if (org is not null)
        {
            serviceId = org;
            return true;
        }

        if (e.ServiceId != Guid.Empty)
        {
            serviceId = e.ServiceId.ToString();
            return true;
        }

        serviceId = string.Empty;
        return false;
    }

    internal static string InferScopeKind(EmployeSnapshot? e, string nodeId)
    {
        if (e is null) return QuotaScopeKinds.Service;
        if (string.Equals(e.CelluleId, nodeId, StringComparison.OrdinalIgnoreCase))
            return QuotaScopeKinds.Cellule;
        return QuotaScopeKinds.Service;
    }

    internal static string ResolveNodeLabel(
        EmployeSnapshot? e,
        string nodeId,
        string scopeKind,
        DirectoryOrgCatalogSnapshot? catalog = null)
    {
        var fromCatalog = catalog?.GetName(nodeId);
        if (!string.IsNullOrWhiteSpace(fromCatalog))
            return fromCatalog;

        var shortId = nodeId.Length <= 12 ? nodeId : nodeId[..12];
        var fallback = scopeKind == QuotaScopeKinds.Cellule
            ? $"Cellule {shortId}"
            : $"Service {shortId}";

        if (e is null)
            return fallback;

        // ServiceNom n'est un vrai libellé que s'il ne ressemble pas à un id org.
        var nom = (e.ServiceNom ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nom)
            || LooksLikeOrgId(nom)
            || string.Equals(nom, e.OrgServiceId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(nom, e.CelluleId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(nom, nodeId, StringComparison.OrdinalIgnoreCase))
        {
            return fallback;
        }

        // Ne pas réutiliser le nom de service pour une cellule.
        if (scopeKind == QuotaScopeKinds.Cellule
            && !string.Equals(e.CelluleId, nodeId, StringComparison.OrdinalIgnoreCase))
            return fallback;

        return nom;
    }

    private static bool LooksLikeOrgId(string value)
    {
        if (Guid.TryParse(value, out _)) return true;
        var v = value.Trim();
        return v.StartsWith("cell-", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("svc-", StringComparison.OrdinalIgnoreCase)
            || v.StartsWith("pole-", StringComparison.OrdinalIgnoreCase);
    }

    private static string PreferNom(string current, string candidate)
    {
        if ((current.StartsWith("Service ", StringComparison.Ordinal)
             || current.StartsWith("Cellule ", StringComparison.Ordinal))
            && !(candidate.StartsWith("Service ", StringComparison.Ordinal)
                 || candidate.StartsWith("Cellule ", StringComparison.Ordinal)))
            return candidate;
        return current;
    }

    private static async Task<IReadOnlyList<string>?> TryGetManagedNodeIdsAsync(
        IRebacClient? rebac,
        Guid actorId,
        CancellationToken ct)
    {
        if (rebac is null) return null;
        try
        {
            var superviseurNodes = await rebac.GetManagedNodeIdsAsync(actorId, "Superviseur", ct);
            var referentNodes = await rebac.GetManagedNodeIdsAsync(actorId, "ReferentTechnique", ct);
            var list = superviseurNodes.Concat(referentNodes)
                .Select(QuotaCongeService.NormalizeNodeId)
                .Where(n => n is not null)
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return list.Count > 0 ? list : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsRhOrAdmin(string? role)
    {
        var r = role?.Trim() ?? string.Empty;
        return r.Equals("RH", StringComparison.OrdinalIgnoreCase)
            || r.Equals("Admin", StringComparison.OrdinalIgnoreCase);
    }
}
