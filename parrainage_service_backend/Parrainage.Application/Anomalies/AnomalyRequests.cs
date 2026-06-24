using MediatR;
using Parrainage.Application.Abstractions;
using Parrainage.Application.DTOs;

namespace Parrainage.Application.Anomalies;

public record GetAnomaliesQuery : IRequest<AnomaliesDto>;
public sealed class GetAnomaliesQueryHandler(IAnomalyQueryService anomalies)
    : IRequestHandler<GetAnomaliesQuery, AnomaliesDto>
{
    public Task<AnomaliesDto> Handle(GetAnomaliesQuery request, CancellationToken ct) =>
        anomalies.GetAsync(ct);
}
