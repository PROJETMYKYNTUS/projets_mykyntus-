using MediatR;
using Documentation.Application.Abstractions;
using Documentation.Application.Api;

namespace Documentation.Application.DocumentRequests;

public record ListDocumentRequestsQuery(DocumentRequestListQuery Query) : IRequest<PagedResponse<DocumentRequestResponse>>;

public sealed class ListDocumentRequestsQueryHandler(IDocumentRequestAppService requests)
    : IRequestHandler<ListDocumentRequestsQuery, PagedResponse<DocumentRequestResponse>>
{
    public Task<PagedResponse<DocumentRequestResponse>> Handle(ListDocumentRequestsQuery request, CancellationToken ct) =>
        requests.ListAsync(request.Query, ct);
}

public record GetDocumentRequestQuery(Guid Id) : IRequest<DocumentRequestResponse?>;

public sealed class GetDocumentRequestQueryHandler(IDocumentRequestAppService requests)
    : IRequestHandler<GetDocumentRequestQuery, DocumentRequestResponse?>
{
    public Task<DocumentRequestResponse?> Handle(GetDocumentRequestQuery request, CancellationToken ct) =>
        requests.GetByIdAsync(request.Id, ct);
}

public record GetDocumentRequestFieldValuesQuery(Guid Id) : IRequest<DocumentRequestFieldValuesResponse?>;

public sealed class GetDocumentRequestFieldValuesQueryHandler(IDocumentRequestAppService requests)
    : IRequestHandler<GetDocumentRequestFieldValuesQuery, DocumentRequestFieldValuesResponse?>
{
    public Task<DocumentRequestFieldValuesResponse?> Handle(GetDocumentRequestFieldValuesQuery request, CancellationToken ct) =>
        requests.GetFieldValuesAsync(request.Id, ct);
}

public record PutDocumentRequestFieldValuesCommand(Guid Id, PutDocumentRequestFieldValuesRequest Body)
    : IRequest<DocumentRequestFieldValuesResponse>;

public sealed class PutDocumentRequestFieldValuesCommandHandler(IDocumentRequestAppService requests)
    : IRequestHandler<PutDocumentRequestFieldValuesCommand, DocumentRequestFieldValuesResponse>
{
    public Task<DocumentRequestFieldValuesResponse> Handle(PutDocumentRequestFieldValuesCommand request, CancellationToken ct) =>
        requests.PutFieldValuesAsync(request.Id, request.Body, ct);
}

public record CreateDocumentRequestCommand(CreateDocumentRequestBody Body) : IRequest<DocumentRequestResponse>;

public sealed class CreateDocumentRequestCommandHandler(IDocumentRequestAppService requests)
    : IRequestHandler<CreateDocumentRequestCommand, DocumentRequestResponse>
{
    public Task<DocumentRequestResponse> Handle(CreateDocumentRequestCommand request, CancellationToken ct) =>
        requests.CreateAsync(request.Body, ct);
}
