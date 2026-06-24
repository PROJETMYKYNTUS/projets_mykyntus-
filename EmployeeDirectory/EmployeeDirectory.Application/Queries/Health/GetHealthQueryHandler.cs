using MediatR;

namespace EmployeeDirectory.Application.Queries.Health;

public sealed class GetHealthQueryHandler : IRequestHandler<GetHealthQuery, HealthDto>
{
    public Task<HealthDto> Handle(GetHealthQuery request, CancellationToken ct) =>
        Task.FromResult(new HealthDto("healthy", "employee-directory", "clean-architecture"));
}
