using Kyntus.Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using Planning.Messaging.Publishers;
using PlanningService.Data;
using PlanningService.DTOs;
using PlanningService.Interfaces;
using PlanningService.Models;

namespace PlanningService.Services;

file record AuthRegisterResult(int Id, string Email, Guid SubjectId);
public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly IEmployePublisher _employePublisher;
    private readonly HttpClient _httpClient;
    private readonly IDirectoryEmployeeEnsureClient _directoryEmployeeEnsure;
    private readonly IDirectoryEmployeeWriteClient _directoryEmployeeWrite;
    private readonly IDirectoryHierarchyClient _directoryHierarchy;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserService> _logger;

    public UserService(
           AppDbContext context,
           IEmployePublisher employePublisher,
           HttpClient httpClient,
           IDirectoryEmployeeEnsureClient directoryEmployeeEnsure,
           IDirectoryEmployeeWriteClient directoryEmployeeWrite,
           IDirectoryHierarchyClient directoryHierarchy,
           IConfiguration configuration,
           ILogger<UserService> logger)
    {
        _context = context;
        _employePublisher = employePublisher;
        _httpClient = httpClient;
        _directoryEmployeeEnsure = directoryEmployeeEnsure;
        _directoryEmployeeWrite = directoryEmployeeWrite;
        _directoryHierarchy = directoryHierarchy;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<List<UserDto>> GetAllUsersAsync()
    {
        var users = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.SubService)
                .ThenInclude(ss => ss != null ? ss.Service : null!)
                    .ThenInclude(s => s.Floor)
            .Include(u => u.ManagedSubServices)
                .ThenInclude(us => us.SubService)
                    .ThenInclude(s => s.Service)
                        .ThenInclude(s => s.Floor)
            .Include(u => u.ManagedServices)
                .ThenInclude(us => us.Service)
                    .ThenInclude(s => s.Floor)
            .ToListAsync();

        return users.Select(ToDto).ToList();
    }
    public async Task<List<UserDto>> GetUsersBySubServiceAsync(int subServiceId)
    {
        var users = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.SubService)
            .Include(u => u.ManagedSubServices)
                .ThenInclude(us => us.SubService)
                    .ThenInclude(s => s.Service)
            .Where(u => u.SubServiceId == subServiceId)
            .ToListAsync();

        return users.Select(ToDto).ToList();
    }

    public async Task<UserDto?> GetUserByIdAsync(int id)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.SubService)
                .ThenInclude(ss => ss != null ? ss.Service : null!)
                    .ThenInclude(s => s.Floor)
            .Include(u => u.ManagedSubServices)
                .ThenInclude(us => us.SubService)
                    .ThenInclude(s => s.Service)
                        .ThenInclude(s => s.Floor)
            .Include(u => u.ManagedServices)
                .ThenInclude(us => us.Service)
                    .ThenInclude(s => s.Floor)
            .FirstOrDefaultAsync(u => u.Id == id);

        return user == null ? null : ToDto(user);
    }
    public async Task SyncMissingAuthUsersAsync()
    {
        var users = await _context.Users
            .Where(u => u.AuthUserId == null && u.IsActive)
            .ToListAsync();

        _logger.LogInformation("{Count} users sans AuthUserId à synchroniser", users.Count);

        foreach (var user in users)
            await SyncToAuthServiceAsync(user);
    }
    public async Task<UserDto> CreateUserAsync(CreateUserDto dto)
    {
        if (!await IsEmailUniqueAsync(dto.Email))
            throw new InvalidOperationException($"L'adresse email « {dto.Email.Trim()} » est déjà utilisée.");

        if (IsDirectoryWriteMaster())
            return await CreateUserDirectoryFirstAsync(dto, null);

        var user = new User
        {
            RoleId = dto.RoleId,
            SubServiceId = dto.SubServiceId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            HireDate = dto.HireDate,
            Level = dto.Level,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Azerty@123"),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await _context.Entry(user).Reference(u => u.Role).LoadAsync();
        await SyncToAuthServiceAsync(user);

        await PublishEmployeCreatedForUserAsync(user, dto.SubServiceId);
        await _context.SaveChangesAsync();

        var directoryOk = await _directoryEmployeeEnsure.TryEnsureFromPlanningAsync(user);
        if (!directoryOk && IsDirectoryEnsureRequired())
        {
            await RollbackCreatedUserAsync(user);
            throw new InvalidOperationException(
                "L'employé a été créé localement mais la synchronisation avec l'annuaire (Directory) a échoué. Réessayez ou contactez l'administrateur.");
        }

        return await GetUserByIdAsync(user.Id)
            ?? throw new Exception("Erreur création utilisateur.");
    }

    public async Task<UserDto> CreateUserFromImportAsync(CreateUserFromImportDto dto)
    {
        if (!await IsEmailUniqueAsync(dto.Email))
            throw new InvalidOperationException($"L'adresse email « {dto.Email.Trim()} » est déjà utilisée.");

        if (!IsDirectoryWriteMaster())
            throw new InvalidOperationException(
                "L'import guidé requiert Directory__WriteMaster=true pour garantir la synchronisation plateforme.");

        return await CreateUserDirectoryFirstAsync(dto, ResolveImportPassword(dto.Password), requireAuthSuccess: true);
    }

    private async Task<UserDto> CreateUserDirectoryFirstAsync(
        CreateUserDto dto,
        string? importPassword,
        bool requireAuthSuccess = false)
    {
        if (dto is CreateUserFromImportDto importDto)
            return await CreateUserDirectoryFirstFromImportAsync(importDto, requireAuthSuccess);

        return await CreateUserDirectoryFirstCoreAsync(dto, importPassword, requireAuthSuccess);
    }

    private async Task<UserDto> CreateUserDirectoryFirstFromImportAsync(
        CreateUserFromImportDto dto,
        bool requireAuthSuccess)
    {
        var isActive = dto.IsActiveOnImport ?? true;
        var userDto = await CreateUserDirectoryFirstCoreAsync(dto, ResolveImportPassword(dto.Password), requireAuthSuccess, isActive);

        if (!isActive && userDto.Id > 0)
        {
            var updateDto = new UpdateUserDto
            {
                RoleId = dto.RoleId,
                SubServiceId = dto.SubServiceId,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                HireDate = dto.HireDate,
                Level = dto.Level,
                IsActive = false,
            };
            return await UpdateUserAsync(userDto.Id, updateDto)
                ?? throw new InvalidOperationException("Mise à jour isActive après import échouée.");
        }

        return userDto;
    }

    private static string ResolveImportPassword(string? password) =>
        string.IsNullOrWhiteSpace(password) ? "Azerty@123" : password.Trim();

    private async Task<UserDto> CreateUserDirectoryFirstCoreAsync(
        CreateUserDto dto,
        string? importPassword,
        bool requireAuthSuccess = false,
        bool isActive = true)
    {
        var role = await _context.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == dto.RoleId);
        var roleName = role?.Name ?? KyntusRoleNames.Employee;

        string? primeServiceId = null;
        if (dto.SubServiceId.HasValue)
        {
            primeServiceId = await _context.SubServices.AsNoTracking()
                .Where(ss => ss.Id == dto.SubServiceId.Value)
                .Select(ss => ss.PrimeServiceId)
                .FirstOrDefaultAsync();
        }

        var directoryResult = await _directoryEmployeeWrite.TryCreateEmployeeAsync(
            dto.FirstName,
            dto.LastName,
            dto.Email,
            roleName,
            primeServiceId,
            dto.HireDate);

        if (!directoryResult.Success)
            throw new InvalidOperationException(
                "La création dans l'annuaire (Directory) a échoué. Réessayez ou contactez l'administrateur.");

        var password = ResolveImportPassword(importPassword);
        var user = new User
        {
            Guid = directoryResult.EmployeeId,
            RoleId = dto.RoleId,
            SubServiceId = dto.SubServiceId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            HireDate = dto.HireDate,
            Level = dto.Level,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        await _context.Entry(user).Reference(u => u.Role).LoadAsync();

        await SyncToAuthServiceAsync(user, password);

        if (requireAuthSuccess && !user.AuthUserId.HasValue)
        {
            await RollbackImportUserAsync(user);
            throw new InvalidOperationException(
                "La synchronisation Auth a échoué. L'employé n'a pas été conservé.");
        }

        return await GetUserByIdAsync(user.Id)
            ?? throw new Exception("Erreur création utilisateur.");
    }

    private async Task SyncToAuthServiceAsync(User user, string? defaultPassword = null)
    {
        var maxRetries = 3;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var password = ResolveImportPassword(defaultPassword);
                var response = await _httpClient.PostAsJsonAsync(
                    "api/auth/register-from-planning",
                    new
                    {
                        Email = user.Email,
                        DefaultPassword = password,
                        RoleId = user.RoleId,
                        RoleName = user.Role?.Name
                    });

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content
                        .ReadFromJsonAsync<AuthRegisterResult>();
                    if (result != null)
                    {
                        user.AuthUserId = result.Id;
                        await _context.SaveChangesAsync();
                        if (result.SubjectId != Guid.Empty)
                            await _directoryEmployeeWrite.TryLinkAuthSubjectAsync(user.Guid, result.SubjectId);
                        _logger.LogInformation("AuthUserId={Id} lié à {Email}",
                            result.Id, user.Email);
                        return;
                    }
                }

                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Tentative {Attempt} → {Status} : {Body}",
                    attempt, response.StatusCode, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tentative {Attempt}/{Max} sync Auth", attempt, maxRetries);
            }

            if (attempt < maxRetries)
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
        }

        _logger.LogError("Sync Auth échouée après {Max} tentatives pour {Email}",
            maxRetries, user.Email);
    }
    public async Task SyncAllEmployesToCongeAsync()
    {
        var users = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.SubService)
            .Where(u => u.IsActive)
            .ToListAsync();

        foreach (var user in users)
            await PublishEmployeCreatedForUserAsync(user, user.SubServiceId);
    }
    public async Task<UserDto?> UpdateUserAsync(int id, UpdateUserDto dto)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return null;

        user.RoleId = dto.RoleId;
        user.SubServiceId = dto.SubServiceId;
        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.Email = dto.Email;
        user.HireDate = dto.HireDate;
        user.IsActive = dto.IsActive;
        user.Level = dto.Level;

        var existing = _context.UserSubServices.Where(us => us.UserId == id);
        _context.UserSubServices.RemoveRange(existing);

        var existingServices = _context.UserManagedServices.Where(us => us.UserId == id);
        _context.UserManagedServices.RemoveRange(existingServices);

        await _context.SaveChangesAsync();
        await _context.Entry(user).Reference(u => u.Role).LoadAsync();

        if (IsDirectoryWriteMaster())
        {
            await _directoryEmployeeWrite.TryUpdateEmployeeAsync(user);
        }
        else
        {
            await PublishEmployeUpdatedForUserAsync(user, dto.SubServiceId);
            await _context.SaveChangesAsync();
            await _directoryEmployeeEnsure.TryEnsureFromPlanningAsync(user);
        }

        return await GetUserByIdAsync(id);
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return false;

        var employeeGuid = user.Guid;
        var authUserId = user.AuthUserId;

        if (authUserId.HasValue)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"api/auth/users/from-planning/{authUserId.Value}");
                if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Suppression Auth échouée pour {Email} ({Status}): {Body}",
                        user.Email, response.StatusCode, body);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Suppression Auth indisponible pour {Email}", user.Email);
            }
        }

        if (IsDirectoryWriteMaster())
            await _directoryEmployeeWrite.TryDeleteEmployeeAsync(employeeGuid);
        else
            await _directoryEmployeeEnsure.TryDeleteFromPlanningAsync(employeeGuid);

        var managedLinks = _context.UserSubServices.Where(us => us.UserId == id);
        _context.UserSubServices.RemoveRange(managedLinks);

        var managedServiceLinks = _context.UserManagedServices.Where(us => us.UserId == id);
        _context.UserManagedServices.RemoveRange(managedServiceLinks);

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsEmailUniqueAsync(string email, int? excludeId = null)
    {
        return !await _context.Users
            .AnyAsync(u => u.Email == email && u.Id != excludeId);
    }
    public async Task<UserDto?> GetUserByAuthIdAsync(int authUserId)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.SubService)
                .ThenInclude(ss => ss != null ? ss.Service : null)
            .Include(u => u.ManagedSubServices)
                .ThenInclude(us => us.SubService)
                    .ThenInclude(s => s.Service)
            .Include(u => u.ManagedServices)
                .ThenInclude(us => us.Service)
                    .ThenInclude(s => s.Floor)
            .FirstOrDefaultAsync(u => u.AuthUserId == authUserId);

        return user == null ? null : ToDto(user);
    }

    private async Task PublishEmployeCreatedForUserAsync(User user, int? subServiceId)
    {
        var ctx = await BuildEmployePublishContextAsync(user, subServiceId);
        await _employePublisher.PublishEmployeCreatedAsync(
            employeId: user.Guid,
            nom: user.LastName,
            prenom: user.FirstName,
            email: user.Email,
            managerId: ctx.SupervisorId,
            serviceId: ctx.ServiceId,
            serviceNom: ctx.ServiceNom,
            dateEmbauche: DateTime.SpecifyKind(user.HireDate, DateTimeKind.Utc),
            estMineur: false,
            role: user.Role?.Name ?? KyntusRoleNames.Employee,
            subServiceId: ctx.SubServiceId,
            primeServiceId: ctx.PrimeServiceId,
            supervisorId: ctx.SupervisorId);
    }

    private async Task PublishEmployeUpdatedForUserAsync(User user, int? subServiceId)
    {
        var ctx = await BuildEmployePublishContextAsync(user, subServiceId);
        await _employePublisher.PublishEmployeUpdatedAsync(
            employeId: user.Guid,
            nom: user.LastName,
            prenom: user.FirstName,
            email: user.Email,
            managerId: ctx.SupervisorId,
            serviceId: ctx.ServiceId,
            serviceNom: ctx.ServiceNom,
            role: user.Role?.Name ?? KyntusRoleNames.Employee,
            subServiceId: ctx.SubServiceId,
            primeServiceId: ctx.PrimeServiceId,
            supervisorId: ctx.SupervisorId);
    }

    private async Task<EmployePublishContext> BuildEmployePublishContextAsync(User user, int? subServiceId)
    {
        if (!subServiceId.HasValue)
        {
            var parentOnly = await _directoryHierarchy.ResolveSupervisorIdAsync(user.Guid);
            return new EmployePublishContext(parentOnly, Guid.Empty, string.Empty, null, null);
        }

        var subService = await _context.SubServices
            .AsNoTracking()
            .FirstOrDefaultAsync(ss => ss.Id == subServiceId.Value);

        if (subService == null)
            return new EmployePublishContext(Guid.Empty, Guid.Empty, string.Empty, subServiceId, null);

        var supervisorId = await _directoryHierarchy.ResolveSupervisorIdAsync(user.Guid);
        if (supervisorId == Guid.Empty)
        {
            var legacySupervisor = await _context.UserSubServices
                .AsNoTracking()
                .Include(us => us.User)
                    .ThenInclude(u => u.Role)
                .Where(us => us.SubServiceId == subServiceId.Value
                          && us.User.Role != null
                          && (us.User.Role.Name == KyntusRoleNames.Superviseur
                              || us.User.Role.Name == KyntusRoleNames.Manager))
                .Select(us => us.User)
                .FirstOrDefaultAsync();
            supervisorId = legacySupervisor?.Guid ?? Guid.Empty;
        }

        return new EmployePublishContext(
            SupervisorId: supervisorId,
            ServiceId: KyntusGuidEncoding.FromIntId(subService.Id),
            ServiceNom: subService.Name,
            SubServiceId: subService.Id,
            PrimeServiceId: subService.PrimeServiceId);
    }

    private sealed record EmployePublishContext(
        Guid SupervisorId,
        Guid ServiceId,
        string ServiceNom,
        int? SubServiceId,
        string? PrimeServiceId);

    private static UserDto ToDto(User u)
    {
        var (pole, cellule, service) = ResolveOrgNames(u);
        return new UserDto
        {
            Id = u.Id,
            Guid = u.Guid,
            RoleId = u.RoleId,
            RoleName = u.Role?.Name ?? string.Empty,
            SubServiceId = u.SubServiceId,
            SubServiceName = u.SubService?.Name,
            OrgPoleName = pole,
            OrgCelluleName = cellule,
            OrgServiceName = service,
            ManagedSubServices = u.ManagedSubServices?.Select(us => new SubServiceSimpleDto
            {
                Id = us.SubService.Id,
                Name = us.SubService.Name,
                ServiceName = us.SubService.Service?.Name ?? string.Empty
            }).ToList() ?? new(),
            ManagedServices = u.ManagedServices?.Select(us => new ServiceSimpleDto
            {
                Id = us.Service.Id,
                Name = us.Service.Name,
                FloorName = us.Service.Floor?.Name ?? string.Empty
            }).ToList() ?? new(),
            FirstName = u.FirstName,
            LastName = u.LastName,
            Email = u.Email,
            HireDate = u.HireDate,
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt,
            Level = u.Level
        };
    }

    private static (string? Pole, string? Cellule, string? Service) ResolveOrgNames(User u)
    {
        if (u.SubService?.Service != null)
        {
            return (
                u.SubService.Service.Floor?.Name,
                u.SubService.Service.Name,
                u.SubService.Name);
        }

        var managedSub = u.ManagedSubServices?.FirstOrDefault()?.SubService;
        if (managedSub?.Service != null)
        {
            return (
                managedSub.Service.Floor?.Name,
                managedSub.Service.Name,
                managedSub.Name);
        }

        var managedSvc = u.ManagedServices?.FirstOrDefault()?.Service;
        if (managedSvc != null)
        {
            return (managedSvc.Floor?.Name, managedSvc.Name, null);
        }

        return (null, null, null);
    }

    private bool IsDirectoryEnsureRequired() =>
        _configuration.GetValue("Directory:RequireEnsureOnWrite", false);

    private bool IsDirectoryWriteMaster() =>
        _configuration.GetValue("Directory:WriteMaster", true);

    private async Task RollbackImportUserAsync(User user)
    {
        var employeeGuid = user.Guid;
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        await _directoryEmployeeWrite.TryDeleteEmployeeAsync(employeeGuid);
    }

    private async Task RollbackCreatedUserAsync(User user)
    {
        var managedLinks = _context.UserSubServices.Where(us => us.UserId == user.Id);
        _context.UserSubServices.RemoveRange(managedLinks);
        var managedServiceLinks = _context.UserManagedServices.Where(us => us.UserId == user.Id);
        _context.UserManagedServices.RemoveRange(managedServiceLinks);
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        if (user.AuthUserId.HasValue)
        {
            try
            {
                await _httpClient.DeleteAsync($"api/auth/users/from-planning/{user.AuthUserId.Value}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Rollback Auth échoué pour {Email}", user.Email);
            }
        }
    }
}
