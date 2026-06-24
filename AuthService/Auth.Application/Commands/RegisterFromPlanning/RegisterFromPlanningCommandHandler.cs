using Auth.Application.DTOs;
using Auth.Domain.Entities;
using Auth.Domain.Interfaces;
using MediatR;

namespace Auth.Application.Commands.RegisterFromPlanning;

public record RegisterFromPlanningCommand(RegisterFromPlanningDto Dto) : IRequest<RegisterFromPlanningResponseDto>;

public class RegisterFromPlanningCommandHandler
    : IRequestHandler<RegisterFromPlanningCommand, RegisterFromPlanningResponseDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISubjectIdResolver _subjectIdResolver;

    public RegisterFromPlanningCommandHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPasswordHasher passwordHasher,
        ISubjectIdResolver subjectIdResolver)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _passwordHasher = passwordHasher;
        _subjectIdResolver = subjectIdResolver;
    }

    public async Task<RegisterFromPlanningResponseDto> Handle(
        RegisterFromPlanningCommand request,
        CancellationToken ct)
    {
        var dto = request.Dto;
        var existing = await _userRepository.GetByEmailAsync(dto.Email, ct);
        if (existing != null)
        {
            return new RegisterFromPlanningResponseDto
            {
                Id = existing.Id,
                Email = existing.Email,
                SubjectId = existing.SubjectId,
            };
        }

        Role? role = null;
        if (!string.IsNullOrWhiteSpace(dto.RoleName))
            role = await _roleRepository.GetByNameAsync(dto.RoleName.Trim(), ct);
        if (role is null && dto.RoleId > 0)
            role = await _roleRepository.GetByIdAsync(dto.RoleId, ct);

        if (role == null)
            throw new RegisterFromPlanningRoleNotFoundException();

        var user = new User
        {
            Username = dto.Email,
            Email = dto.Email,
            SubjectId = _subjectIdResolver.ResolveForEmail(dto.Email),
            PasswordHash = _passwordHasher.HashPassword(dto.DefaultPassword),
            RoleId = role.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            RefreshToken = null,
            RefreshTokenExpiryTime = null,
        };

        await _userRepository.CreateAsync(user, ct);

        return new RegisterFromPlanningResponseDto
        {
            Id = user.Id,
            Email = user.Email,
            SubjectId = user.SubjectId,
        };
    }
}

public class RegisterFromPlanningRoleNotFoundException : Exception;
