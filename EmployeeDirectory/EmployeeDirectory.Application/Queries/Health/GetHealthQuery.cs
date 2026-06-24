using MediatR;

namespace EmployeeDirectory.Application.Queries.Health;

public record GetHealthQuery : IRequest<HealthDto>;

public record HealthDto(string Status, string Service, string Architecture);
