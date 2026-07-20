using Auth.Application.Commands.RegisterFromPlanning;
using Auth.Application.DTOs;
using MediatR;

namespace Auth.Application.Commands.RegisterFromPlanningBatch;

public record RegisterFromPlanningBatchCommand(RegisterFromPlanningBatchDto Dto)
    : IRequest<IReadOnlyList<RegisterFromPlanningBatchItemResultDto>>;

public sealed class RegisterFromPlanningBatchCommandHandler(IMediator mediator)
    : IRequestHandler<RegisterFromPlanningBatchCommand, IReadOnlyList<RegisterFromPlanningBatchItemResultDto>>
{
    public async Task<IReadOnlyList<RegisterFromPlanningBatchItemResultDto>> Handle(
        RegisterFromPlanningBatchCommand request,
        CancellationToken ct)
    {
        var results = new List<RegisterFromPlanningBatchItemResultDto>();

        foreach (var item in request.Dto.Items)
        {
            try
            {
                var response = await mediator.Send(new RegisterFromPlanningCommand(item), ct);
                results.Add(new RegisterFromPlanningBatchItemResultDto
                {
                    Email = item.Email,
                    Success = true,
                    AuthUserId = response.Id,
                });
            }
            catch (RegisterFromPlanningRoleNotFoundException)
            {
                results.Add(new RegisterFromPlanningBatchItemResultDto
                {
                    Email = item.Email,
                    Success = false,
                    Message = "Rôle introuvable",
                });
            }
            catch (Exception ex)
            {
                results.Add(new RegisterFromPlanningBatchItemResultDto
                {
                    Email = item.Email,
                    Success = false,
                    Message = ex.Message,
                });
            }
        }

        return results;
    }
}
