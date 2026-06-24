using MediatR;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;

namespace Prime.Application.Drafts;

// ---- Cellule prime drafts ----
public record GetSupervisorCellulePrimeDraftQuery(
    string SupervisorUserId,
    string? CelluleId,
    string? PoleId,
    string Period,
    string TemplateId) : IRequest<SupervisorCellulePrimeDraftResponseDto?>;

public sealed class GetSupervisorCellulePrimeDraftQueryHandler(ISupervisorCellulePrimeDraftAppService drafts)
    : IRequestHandler<GetSupervisorCellulePrimeDraftQuery, SupervisorCellulePrimeDraftResponseDto?>
{
    public Task<SupervisorCellulePrimeDraftResponseDto?> Handle(
        GetSupervisorCellulePrimeDraftQuery request,
        CancellationToken ct) =>
        drafts.GetAsync(
            request.SupervisorUserId,
            request.CelluleId,
            request.PoleId,
            request.Period,
            request.TemplateId,
            ct);
}

public record ListActiveSupervisorCellulePrimeDraftsQuery(string SupervisorUserId)
    : IRequest<IReadOnlyList<SupervisorCellulePrimeDraftListItemDto>>;

public sealed class ListActiveSupervisorCellulePrimeDraftsQueryHandler(ISupervisorCellulePrimeDraftAppService drafts)
    : IRequestHandler<ListActiveSupervisorCellulePrimeDraftsQuery, IReadOnlyList<SupervisorCellulePrimeDraftListItemDto>>
{
    public Task<IReadOnlyList<SupervisorCellulePrimeDraftListItemDto>> Handle(
        ListActiveSupervisorCellulePrimeDraftsQuery request,
        CancellationToken ct) =>
        drafts.ListActiveAsync(request.SupervisorUserId, ct);
}

public record UpsertSupervisorCellulePrimeDraftCommand(UpsertSupervisorCellulePrimeDraftRequest Body)
    : IRequest<SupervisorCellulePrimeDraftResponseDto>;

public sealed class UpsertSupervisorCellulePrimeDraftCommandHandler(ISupervisorCellulePrimeDraftAppService drafts)
    : IRequestHandler<UpsertSupervisorCellulePrimeDraftCommand, SupervisorCellulePrimeDraftResponseDto>
{
    public Task<SupervisorCellulePrimeDraftResponseDto> Handle(
        UpsertSupervisorCellulePrimeDraftCommand request,
        CancellationToken ct) =>
        drafts.UpsertAsync(request.Body, ct);
}

public record DeleteSupervisorCellulePrimeDraftCommand(Guid Id, string SupervisorUserId) : IRequest<Unit>;

public sealed class DeleteSupervisorCellulePrimeDraftCommandHandler(ISupervisorCellulePrimeDraftAppService drafts)
    : IRequestHandler<DeleteSupervisorCellulePrimeDraftCommand, Unit>
{
    public async Task<Unit> Handle(DeleteSupervisorCellulePrimeDraftCommand request, CancellationToken ct)
    {
        await drafts.DeleteAsync(request.Id, request.SupervisorUserId, ct);
        return Unit.Value;
    }
}

// ---- Legacy supervisor prime fiches ----
public record CreateSupervisorPrimeFicheCommand(CreateSupervisorPrimeFicheRequest Body)
    : IRequest<SupervisorPrimeFicheResponseDto>;

public sealed class CreateSupervisorPrimeFicheCommandHandler(ISupervisorPrimeFicheAppService fiches)
    : IRequestHandler<CreateSupervisorPrimeFicheCommand, SupervisorPrimeFicheResponseDto>
{
    public Task<SupervisorPrimeFicheResponseDto> Handle(
        CreateSupervisorPrimeFicheCommand request,
        CancellationToken ct) =>
        fiches.CreateAsync(request.Body, ct);
}

public record UpdateSupervisorPrimeFicheSaisieCommand(Guid Id, UpdateSupervisorPrimeFicheSaisieRequest Body)
    : IRequest<SupervisorPrimeFicheResponseDto>;

public sealed class UpdateSupervisorPrimeFicheSaisieCommandHandler(ISupervisorPrimeFicheAppService fiches)
    : IRequestHandler<UpdateSupervisorPrimeFicheSaisieCommand, SupervisorPrimeFicheResponseDto>
{
    public Task<SupervisorPrimeFicheResponseDto> Handle(
        UpdateSupervisorPrimeFicheSaisieCommand request,
        CancellationToken ct) =>
        fiches.UpdateSaisieAsync(request.Id, request.Body, ct);
}

public record ValidateSupervisorPrimeFicheCommand(Guid Id) : IRequest<SupervisorPrimeFicheResponseDto>;

public sealed class ValidateSupervisorPrimeFicheCommandHandler(ISupervisorPrimeFicheAppService fiches)
    : IRequestHandler<ValidateSupervisorPrimeFicheCommand, SupervisorPrimeFicheResponseDto>
{
    public Task<SupervisorPrimeFicheResponseDto> Handle(
        ValidateSupervisorPrimeFicheCommand request,
        CancellationToken ct) =>
        fiches.ValidateAsync(request.Id, ct);
}

public record ListSupervisorPrimeFichesQuery(string SupervisorUserId, string? Period)
    : IRequest<IReadOnlyList<SupervisorPrimeFicheResponseDto>>;

public sealed class ListSupervisorPrimeFichesQueryHandler(ISupervisorPrimeFicheAppService fiches)
    : IRequestHandler<ListSupervisorPrimeFichesQuery, IReadOnlyList<SupervisorPrimeFicheResponseDto>>
{
    public Task<IReadOnlyList<SupervisorPrimeFicheResponseDto>> Handle(
        ListSupervisorPrimeFichesQuery request,
        CancellationToken ct) =>
        fiches.ListAsync(request.SupervisorUserId, request.Period, ct);
}

// ---- Draft global pool ----
public record GetCelluleDraftGlobalPoolStateQuery(Guid DraftId, string SupervisorUserId)
    : IRequest<CelluleDraftGlobalPoolStateDto>;

public sealed class GetCelluleDraftGlobalPoolStateQueryHandler(IPrimeCelluleDraftGlobalPoolAppService pool)
    : IRequestHandler<GetCelluleDraftGlobalPoolStateQuery, CelluleDraftGlobalPoolStateDto>
{
    public Task<CelluleDraftGlobalPoolStateDto> Handle(
        GetCelluleDraftGlobalPoolStateQuery request,
        CancellationToken ct) =>
        pool.GetStateAsync(request.DraftId, request.SupervisorUserId, ct);
}

public record DownloadCelluleDraftGlobalPoolExcelQuery(
    Guid DraftId,
    string SupervisorUserId,
    string? ActingUserId) : IRequest<FileExportResultDto>;

public sealed class DownloadCelluleDraftGlobalPoolExcelQueryHandler(IPrimeCelluleDraftGlobalPoolAppService pool)
    : IRequestHandler<DownloadCelluleDraftGlobalPoolExcelQuery, FileExportResultDto>
{
    public Task<FileExportResultDto> Handle(
        DownloadCelluleDraftGlobalPoolExcelQuery request,
        CancellationToken ct) =>
        pool.DownloadExcelAsync(request.DraftId, request.SupervisorUserId, request.ActingUserId, ct);
}

public record GenerateCelluleDraftGlobalPoolLegacyExcelCommand(Guid DraftId, string SupervisorUserId)
    : IRequest<CelluleDraftGlobalPoolStateDto>;

public sealed class GenerateCelluleDraftGlobalPoolLegacyExcelCommandHandler(IPrimeCelluleDraftGlobalPoolAppService pool)
    : IRequestHandler<GenerateCelluleDraftGlobalPoolLegacyExcelCommand, CelluleDraftGlobalPoolStateDto>
{
    public Task<CelluleDraftGlobalPoolStateDto> Handle(
        GenerateCelluleDraftGlobalPoolLegacyExcelCommand request,
        CancellationToken ct) =>
        pool.GenerateLegacyExcelAsync(request.DraftId, request.SupervisorUserId, ct);
}

public record ApproveCelluleDraftGlobalPoolManagerCommand(
    Guid DraftId,
    string SupervisorUserId,
    GlobalPoolActingUserRequest Body) : IRequest<CelluleDraftGlobalPoolStateDto>;

public sealed class ApproveCelluleDraftGlobalPoolManagerCommandHandler(IPrimeCelluleDraftGlobalPoolAppService pool)
    : IRequestHandler<ApproveCelluleDraftGlobalPoolManagerCommand, CelluleDraftGlobalPoolStateDto>
{
    public Task<CelluleDraftGlobalPoolStateDto> Handle(
        ApproveCelluleDraftGlobalPoolManagerCommand request,
        CancellationToken ct) =>
        pool.ApproveManagerAsync(request.DraftId, request.SupervisorUserId, request.Body, ct);
}

public record ApproveCelluleDraftGlobalPoolRhCommand(
    Guid DraftId,
    string SupervisorUserId,
    GlobalPoolActingUserRequest Body) : IRequest<CelluleDraftGlobalPoolStateDto>;

public sealed class ApproveCelluleDraftGlobalPoolRhCommandHandler(IPrimeCelluleDraftGlobalPoolAppService pool)
    : IRequestHandler<ApproveCelluleDraftGlobalPoolRhCommand, CelluleDraftGlobalPoolStateDto>
{
    public Task<CelluleDraftGlobalPoolStateDto> Handle(
        ApproveCelluleDraftGlobalPoolRhCommand request,
        CancellationToken ct) =>
        pool.ApproveRhAsync(request.DraftId, request.SupervisorUserId, request.Body, ct);
}

public record AckCelluleDraftGlobalPoolComptaCommand(
    Guid DraftId,
    string SupervisorUserId,
    GlobalPoolActingUserRequest Body) : IRequest<CelluleDraftGlobalPoolStateDto>;

public sealed class AckCelluleDraftGlobalPoolComptaCommandHandler(IPrimeCelluleDraftGlobalPoolAppService pool)
    : IRequestHandler<AckCelluleDraftGlobalPoolComptaCommand, CelluleDraftGlobalPoolStateDto>
{
    public Task<CelluleDraftGlobalPoolStateDto> Handle(
        AckCelluleDraftGlobalPoolComptaCommand request,
        CancellationToken ct) =>
        pool.AckComptaAsync(request.DraftId, request.SupervisorUserId, request.Body, ct);
}

// ---- Merged preview ----
public record GetMergedFichePreviewContextQuery(Guid FicheId, string? UserId, string? Role)
    : IRequest<MergedFichePreviewContextDto>;

public sealed class GetMergedFichePreviewContextQueryHandler(IPrimeFichePreviewAppService preview)
    : IRequestHandler<GetMergedFichePreviewContextQuery, MergedFichePreviewContextDto>
{
    public Task<MergedFichePreviewContextDto> Handle(
        GetMergedFichePreviewContextQuery request,
        CancellationToken ct) =>
        preview.GetMergedPreviewContextAsync(request.FicheId, request.UserId, request.Role, ct);
}
