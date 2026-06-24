using MediatR;
using Documentation.Application.Abstractions;
using Documentation.Application.Api;

namespace Documentation.Application.Documents;

public sealed record PreviewDocumentCommand(DocumentWorkflowRequest Body) : IRequest<TemplateFileExportDto>;

public sealed class PreviewDocumentCommandHandler(IDocumentWorkflowGenerationAppService workflow)
    : IRequestHandler<PreviewDocumentCommand, TemplateFileExportDto>
{
    public Task<TemplateFileExportDto> Handle(PreviewDocumentCommand request, CancellationToken ct) =>
        workflow.PreviewDocumentAsync(request.Body, ct);
}

public sealed record GenerateDocumentCommand(DocumentWorkflowRequest Body) : IRequest<DocumentTemplateGenerateResponse>;

public sealed class GenerateDocumentCommandHandler(IDocumentWorkflowGenerationAppService workflow)
    : IRequestHandler<GenerateDocumentCommand, DocumentTemplateGenerateResponse>
{
    public Task<DocumentTemplateGenerateResponse> Handle(GenerateDocumentCommand request, CancellationToken ct) =>
        workflow.GenerateDocumentAsync(request.Body, ct);
}

public sealed record UploadReadyDocumentRequest(
    byte[] FileBytes,
    string FileName,
    string ContentType,
    Guid? DocumentRequestId = null,
    Guid? BeneficiaryUserId = null,
    Guid? DocumentTypeId = null) : IRequest<DocumentTemplateGenerateResponse>;

public sealed class UploadReadyDocumentRequestHandler(IDocumentWorkflowGenerationAppService workflow)
    : IRequestHandler<UploadReadyDocumentRequest, DocumentTemplateGenerateResponse>
{
    public Task<DocumentTemplateGenerateResponse> Handle(UploadReadyDocumentRequest request, CancellationToken ct) =>
        workflow.UploadReadyDocumentAsync(
            new UploadReadyDocumentCommand(
                request.FileBytes,
                request.FileName,
                request.ContentType,
                request.DocumentRequestId,
                request.BeneficiaryUserId,
                request.DocumentTypeId),
            ct);
}

public sealed record AiDirectFillValidatedCommand(AiDirectDocumentFillRequest Body) : IRequest<AiDirectDocumentFillResponse>;

public sealed class AiDirectFillValidatedCommandHandler(IAiDirectDocumentAppService ai)
    : IRequestHandler<AiDirectFillValidatedCommand, AiDirectDocumentFillResponse>
{
    public Task<AiDirectDocumentFillResponse> Handle(AiDirectFillValidatedCommand request, CancellationToken ct) =>
        ai.FillValidatedAsync(request.Body, ct);
}

public sealed record AiDirectPreviewValidatedPdfCommand(AiDirectDocumentFillRequest Body) : IRequest<TemplateFileExportDto>;

public sealed class AiDirectPreviewValidatedPdfCommandHandler(IAiDirectDocumentAppService ai)
    : IRequestHandler<AiDirectPreviewValidatedPdfCommand, TemplateFileExportDto>
{
    public Task<TemplateFileExportDto> Handle(AiDirectPreviewValidatedPdfCommand request, CancellationToken ct) =>
        ai.PreviewValidatedPdfAsync(request.Body, ct);
}
