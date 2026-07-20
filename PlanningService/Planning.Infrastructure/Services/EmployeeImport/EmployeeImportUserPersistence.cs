using Microsoft.EntityFrameworkCore;
using Planning.Application.DTOs;
using Planning.Application.Abstractions;
using Planning.Infrastructure.Persistence;

namespace Planning.Infrastructure.Services.EmployeeImport;

public sealed class EmployeeImportUserPersistence(
    IUserService userService,
    AppDbContext db) : IEmployeeImportUserPersistence
{
    public async Task<EmployeeImportUserResult> CreateFromImportAsync(
        CreateUserFromImportDto dto,
        CancellationToken ct = default)
    {
        var created = await userService.CreateUserFromImportAsync(dto);
        return ToResult(created);
    }

    public async Task<EmployeeImportUserResult?> GetByIdAsync(int userId, CancellationToken ct = default)
    {
        // Lean : pas de GetUserByIdAsync (évite LoadOrgNameContext / GET Directory all employees).
        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new EmployeeImportUserResult
            {
                Id = u.Id,
                Guid = u.Guid,
                RoleId = u.RoleId,
                SubServiceId = u.SubServiceId,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                HireDate = u.HireDate,
                IsActive = u.IsActive,
                Level = u.Level,
                AuthUserId = u.AuthUserId,
            })
            .FirstOrDefaultAsync(ct);
        return user;
    }

    public async Task<EmployeeImportUserResult?> UpdateAsync(
        int userId,
        UpdateUserDto dto,
        CancellationToken ct = default)
    {
        var updated = await userService.UpdateUserAsync(userId, dto);
        return updated is null ? null : ToResult(updated);
    }

    private static EmployeeImportUserResult ToResult(UserDto user) => new()
    {
        Id = user.Id,
        Guid = user.Guid,
        RoleId = user.RoleId,
        SubServiceId = user.SubServiceId,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Email = user.Email,
        HireDate = user.HireDate,
        IsActive = user.IsActive,
        Level = user.Level,
        AuthUserId = user.AuthUserId
    };
}
