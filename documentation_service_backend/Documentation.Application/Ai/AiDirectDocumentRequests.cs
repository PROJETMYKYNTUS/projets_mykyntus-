using MediatR;
using Documentation.Application.Abstractions;
using Documentation.Application.Api;

namespace Documentation.Application.Ai;

public record GenerateDocumentAiCommand(AiDirectDocumentFillRequest Body) : IRequest<AiDirectDocumentFillResponse>;

public sealed class GenerateDocumentAiCommandHandler(IAiDirectDocumentAppService ai)
    : IRequestHandler<GenerateDocumentAiCommand, AiDirectDocumentFillResponse>
{
    public Task<AiDirectDocumentFillResponse> Handle(GenerateDocumentAiCommand request, CancellationToken ct) =>
        ai.GenerateAsync(request.Body, ct);
}

public record PreviewDocumentAiPdfCommand(AiDirectDocumentFillRequest Body) : IRequest<FileExportResultDto>;

public sealed class PreviewDocumentAiPdfCommandHandler(IAiDirectDocumentAppService ai)
    : IRequestHandler<PreviewDocumentAiPdfCommand, FileExportResultDto>
{
    public Task<FileExportResultDto> Handle(PreviewDocumentAiPdfCommand request, CancellationToken ct) =>
        ai.PreviewPdfAsync(request.Body, ct);
}

public record ExportDocumentAiCommand(AiDirectRenderRequest Body) : IRequest<FileExportResultDto>;

public sealed class ExportDocumentAiCommandHandler(IAiDirectDocumentAppService ai)
    : IRequestHandler<ExportDocumentAiCommand, FileExportResultDto>
{
    public Task<FileExportResultDto> Handle(ExportDocumentAiCommand request, CancellationToken ct) =>
        ai.ExportAsync(request.Body, ct);
}
