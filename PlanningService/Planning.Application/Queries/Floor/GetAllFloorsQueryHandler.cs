using Planning.Application.Abstractions;
using Planning.Application.DTOs;
using MediatR;

namespace Planning.Application.Queries.Floor;

public record GetAllFloorsQuery : IRequest<List<FloorDto>>;

public class GetAllFloorsQueryHandler : IRequestHandler<GetAllFloorsQuery, List<FloorDto>>
{
    private readonly IFloorService _floorService;

    public GetAllFloorsQueryHandler(IFloorService floorService) => _floorService = floorService;

    public Task<List<FloorDto>> Handle(GetAllFloorsQuery request, CancellationToken cancellationToken) =>
        _floorService.GetAllFloorsAsync();
}
