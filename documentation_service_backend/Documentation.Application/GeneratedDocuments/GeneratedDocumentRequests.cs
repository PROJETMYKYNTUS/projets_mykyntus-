using MediatR;
using Documentation.Application.Abstractions;
using Documentation.Application.Api;

namespace Documentation.Application.GeneratedDocuments;

public record DownloadGeneratedDocumentFileQuery(Guid Id) : IRequest<FileExportResultDto>;

public sealed class DownloadGeneratedDocumentFileQueryHandler(IGeneratedDocumentAppService generated)
    : IRequestHandler<DownloadGeneratedDocumentFileQuery, FileExportResultDto>
{
    public Task<FileExportResultDto> Handle(DownloadGeneratedDocumentFileQuery request, CancellationToken ct) =>
        generated.DownloadGeneratedDocumentFileAsync(request.Id, ct);
}

public record GetRhGeneratedDocumentEditorQuery(Guid Id) : IRequest<RhGeneratedDocumentEditorResponse>;

public sealed class GetRhGeneratedDocumentEditorQueryHandler(IGeneratedDocumentAppService generated)
    : IRequestHandler<GetRhGeneratedDocumentEditorQuery, RhGeneratedDocumentEditorResponse>
{
    public Task<RhGeneratedDocumentEditorResponse> Handle(GetRhGeneratedDocumentEditorQuery request, CancellationToken ct) =>
        generated.GetRhGeneratedDocumentEditorAsync(request.Id, ct);
}

public record PutRhGeneratedDocumentEditorCommand(Guid Id, UpdateRhGeneratedDocumentContentRequest Body) : IRequest;

public sealed class PutRhGeneratedDocumentEditorCommandHandler(IGeneratedDocumentAppService generated)
    : IRequestHandler<PutRhGeneratedDocumentEditorCommand>
{
    public Task Handle(PutRhGeneratedDocumentEditorCommand request, CancellationToken ct) =>
        generated.PutRhGeneratedDocumentEditorAsync(request.Id, request.Body, ct);
}

public record FinalizeRhGeneratedDocumentCommand(Guid Id) : IRequest<DocumentTemplateGenerateResponse>;

public sealed class FinalizeRhGeneratedDocumentCommandHandler(IGeneratedDocumentAppService generated)
    : IRequestHandler<FinalizeRhGeneratedDocumentCommand, DocumentTemplateGenerateResponse>
{
    public Task<DocumentTemplateGenerateResponse> Handle(FinalizeRhGeneratedDocumentCommand request, CancellationToken ct) =>
        generated.FinalizeRhGeneratedDocumentAsync(request.Id, ct);
}

public record ExportGeneratedDocumentQuery(Guid Id, string Format, GeneratedDocumentClientContext? ClientContext)
    : IRequest<FileExportResultDto>;

public sealed class ExportGeneratedDocumentQueryHandler(IGeneratedDocumentAppService generated)
    : IRequestHandler<ExportGeneratedDocumentQuery, FileExportResultDto>
{
    public Task<FileExportResultDto> Handle(ExportGeneratedDocumentQuery request, CancellationToken ct) =>
        generated.ExportGeneratedDocumentAsync(request.Id, request.Format, request.ClientContext, ct);
}

public record DownloadDocumentRequestExportQuery(Guid RequestId, string Format, GeneratedDocumentClientContext? ClientContext)
    : IRequest<FileExportResultDto>;

public sealed class DownloadDocumentRequestExportQueryHandler(IGeneratedDocumentAppService generated)
    : IRequestHandler<DownloadDocumentRequestExportQuery, FileExportResultDto>
{
    public Task<FileExportResultDto> Handle(DownloadDocumentRequestExportQuery request, CancellationToken ct) =>
        generated.DownloadDocumentRequestExportAsync(request.RequestId, request.Format, request.ClientContext, ct);
}
