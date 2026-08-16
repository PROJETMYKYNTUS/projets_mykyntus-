using Conge.Application.DTOs;
using Conge.Domain.Entities;
using Conge.Domain.Interfaces;
using MediatR;

namespace Conge.Application.Commands.ConfigConge;

public record GetPeriodesInterditesQuery : IRequest<PeriodesInterditesDto>;

public record UpdatePeriodesInterditesCommand(IReadOnlyList<int> Mois, Guid? UpdatedBy = null)
    : IRequest<PeriodesInterditesDto>;

public record GetQuotasServiceQuery(Guid SuperviseurId) : IRequest<IReadOnlyList<QuotaCongeServiceDto>>;

public record UpsertQuotaServiceCommand(Guid ServiceId, int MaxAbsentsSimultanes, Guid SuperviseurId)
    : IRequest<QuotaCongeServiceDto>;

public record GetQuotaServiceByIdQuery(Guid ServiceId) : IRequest<QuotaCongeServiceDto?>;

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

    public GetQuotasServiceHandler(
        IEmployeSnapshotRepository employeRepo,
        IQuotaCongeServiceRepository quotaRepo)
    {
        _employeRepo = employeRepo;
        _quotaRepo = quotaRepo;
    }

    public async Task<IReadOnlyList<QuotaCongeServiceDto>> Handle(GetQuotasServiceQuery request, CancellationToken ct)
    {
        var team = await _employeRepo.GetByManagerIdAsync(request.SuperviseurId, ct);
        var services = team
            .Where(e => e.ServiceId != Guid.Empty)
            .GroupBy(e => e.ServiceId)
            .Select(g => new { ServiceId = g.Key, ServiceNom = g.First().ServiceNom, Effectif = g.Count() })
            .ToList();

        var quotas = await _quotaRepo.GetByServiceIdsAsync(services.Select(s => s.ServiceId), ct);
        var byId = quotas.ToDictionary(q => q.ServiceId);

        return services.Select(s =>
        {
            byId.TryGetValue(s.ServiceId, out var q);
            return new QuotaCongeServiceDto(
                s.ServiceId,
                string.IsNullOrWhiteSpace(s.ServiceNom) ? s.ServiceId.ToString() : s.ServiceNom,
                q?.MaxAbsentsSimultanes,
                s.Effectif);
        }).OrderBy(x => x.ServiceNom).ToList();
    }
}

public class UpsertQuotaServiceHandler : IRequestHandler<UpsertQuotaServiceCommand, QuotaCongeServiceDto>
{
    private readonly IEmployeSnapshotRepository _employeRepo;
    private readonly IQuotaCongeServiceRepository _quotaRepo;
    private readonly IUnitOfWork _uow;

    public UpsertQuotaServiceHandler(
        IEmployeSnapshotRepository employeRepo,
        IQuotaCongeServiceRepository quotaRepo,
        IUnitOfWork uow)
    {
        _employeRepo = employeRepo;
        _quotaRepo = quotaRepo;
        _uow = uow;
    }

    public async Task<QuotaCongeServiceDto> Handle(UpsertQuotaServiceCommand request, CancellationToken ct)
    {
        var team = await _employeRepo.GetByManagerIdAsync(request.SuperviseurId, ct);
        var inScope = team.FirstOrDefault(e => e.ServiceId == request.ServiceId)
            ?? throw new InvalidOperationException("Ce service n'appartient pas à votre périmètre.");

        var existing = await _quotaRepo.GetByServiceIdAsync(request.ServiceId, ct);
        if (existing is null)
        {
            existing = QuotaCongeService.Creer(request.ServiceId, request.MaxAbsentsSimultanes, request.SuperviseurId);
            await _quotaRepo.AddAsync(existing, ct);
        }
        else
        {
            existing.MettreAJour(request.MaxAbsentsSimultanes, request.SuperviseurId);
            _quotaRepo.Update(existing);
        }

        await _uow.SaveChangesAsync(ct);
        var effectif = team.Count(e => e.ServiceId == request.ServiceId);
        return new QuotaCongeServiceDto(
            request.ServiceId,
            string.IsNullOrWhiteSpace(inScope.ServiceNom) ? request.ServiceId.ToString() : inScope.ServiceNom,
            existing.MaxAbsentsSimultanes,
            effectif);
    }
}

public class GetQuotaServiceByIdHandler : IRequestHandler<GetQuotaServiceByIdQuery, QuotaCongeServiceDto?>
{
    private readonly IQuotaCongeServiceRepository _quotaRepo;
    private readonly IEmployeSnapshotRepository _employeRepo;

    public GetQuotaServiceByIdHandler(
        IQuotaCongeServiceRepository quotaRepo,
        IEmployeSnapshotRepository employeRepo)
    {
        _quotaRepo = quotaRepo;
        _employeRepo = employeRepo;
    }

    public async Task<QuotaCongeServiceDto?> Handle(GetQuotaServiceByIdQuery request, CancellationToken ct)
    {
        var q = await _quotaRepo.GetByServiceIdAsync(request.ServiceId, ct);
        var peers = await _employeRepo.GetByServiceIdAsync(request.ServiceId, ct);
        var nom = peers.FirstOrDefault()?.ServiceNom ?? request.ServiceId.ToString();
        if (q is null)
            return new QuotaCongeServiceDto(request.ServiceId, nom, null, peers.Count);
        return new QuotaCongeServiceDto(request.ServiceId, nom, q.MaxAbsentsSimultanes, peers.Count);
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
