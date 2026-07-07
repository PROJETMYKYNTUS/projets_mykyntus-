using MediatR;
using Prime.Application.Abstractions;
using Prime.Application.DTOs;

namespace Prime.Application.Admin;

public record GetPrimeAbsenceSanctionConfigQuery : IRequest<PrimeAbsenceSanctionConfigDto>;

public sealed class GetPrimeAbsenceSanctionConfigQueryHandler(IPrimeAbsenceSanctionConfigAppService service)
    : IRequestHandler<GetPrimeAbsenceSanctionConfigQuery, PrimeAbsenceSanctionConfigDto>
{
    public Task<PrimeAbsenceSanctionConfigDto> Handle(GetPrimeAbsenceSanctionConfigQuery request, CancellationToken ct) =>
        service.GetAsync(ct);
}

public record SavePrimeAbsenceSanctionConfigCommand(SavePrimeAbsenceSanctionConfigRequest Body)
    : IRequest<PrimeAbsenceSanctionConfigDto>;

public sealed class SavePrimeAbsenceSanctionConfigCommandHandler(IPrimeAbsenceSanctionConfigAppService service)
    : IRequestHandler<SavePrimeAbsenceSanctionConfigCommand, PrimeAbsenceSanctionConfigDto>
{
    public Task<PrimeAbsenceSanctionConfigDto> Handle(SavePrimeAbsenceSanctionConfigCommand request, CancellationToken ct) =>
        service.SaveAsync(request.Body, ct);
}
