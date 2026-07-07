using Planning.Application.DTOs;

namespace Planning.Infrastructure.Services.EmployeeImport;

/// <summary>
/// Point d'extension : branchez votre propre persistance employé (sans annuaire ni sync inter-modules).
/// </summary>
public interface IEmployeeImportUserPersistence
{
    Task<EmployeeImportUserResult> CreateFromImportAsync(CreateUserFromImportDto dto, CancellationToken ct = default);

    Task<EmployeeImportUserResult?> GetByIdAsync(int userId, CancellationToken ct = default);

    Task<EmployeeImportUserResult?> UpdateAsync(int userId, UpdateUserDto dto, CancellationToken ct = default);
}

public sealed class EmployeeImportUserResult
{
    public int Id { get; init; }
    public Guid Guid { get; init; }
    public int RoleId { get; init; }
    public int? SubServiceId { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public DateTime HireDate { get; init; }
    public bool IsActive { get; init; }
    public int Level { get; init; }
    public int? AuthUserId { get; init; }
}
