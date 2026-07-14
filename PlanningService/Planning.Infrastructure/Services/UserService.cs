using System.Security.Claims;
using Kyntus.Messaging.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Planning.Infrastructure.Messaging.Publishers;
using Planning.Infrastructure.Persistence;
using Planning.Application.DTOs;
using Planning.Application.Abstractions;
using Planning.Domain.Entities;

using Planning.Application.Abstractions.EmployeeImport;

namespace Planning.Infrastructure.Services;

file record AuthRegisterResult(int Id, string Email, Guid SubjectId);
public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly IEmployePublisher _employePublisher;
    private readonly HttpClient _httpClient;
    private readonly IDirectoryEmployeeEnsureClient _directoryEmployeeEnsure;
    private readonly IDirectoryEmployeeWriteClient _directoryEmployeeWrite;
    private readonly IDirectoryHierarchyClient _directoryHierarchy;
    private readonly IDirectoryOrgWriteClient _directoryOrg;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserService> _logger;
    private readonly IEmployeeFieldService _fieldService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserService(
           AppDbContext context,
           IEmployePublisher employePublisher,
           HttpClient httpClient,
           IDirectoryEmployeeEnsureClient directoryEmployeeEnsure,
           IDirectoryEmployeeWriteClient directoryEmployeeWrite,
           IDirectoryHierarchyClient directoryHierarchy,
           IDirectoryOrgWriteClient directoryOrg,
           IConfiguration configuration,
           ILogger<UserService> logger,
           IEmployeeFieldService fieldService,
           IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _employePublisher = employePublisher;
        _httpClient = httpClient;
        _directoryEmployeeEnsure = directoryEmployeeEnsure;
        _directoryEmployeeWrite = directoryEmployeeWrite;
        _directoryHierarchy = directoryHierarchy;
        _directoryOrg = directoryOrg;
        _configuration = configuration;
        _logger = logger;
        _fieldService = fieldService;
        _httpContextAccessor = httpContextAccessor;
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

        var customByUser = await _fieldService.LoadCustomFieldsForUsersAsync(users.Select(u => u.Id).ToList());
        var hrByUser = await LoadHrProfilesAsync(users.Select(u => u.Id).ToList());
        var orgCtx = await LoadOrgNameContextAsync();
        return users.Select(u => ToDto(u, customByUser.GetValueOrDefault(u.Id), orgCtx, hrByUser.GetValueOrDefault(u.Id))).ToList();
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

        var customByUser = await _fieldService.LoadCustomFieldsForUsersAsync(users.Select(u => u.Id).ToList());
        var hrByUser = await LoadHrProfilesAsync(users.Select(u => u.Id).ToList());
        var orgCtx = await LoadOrgNameContextAsync();
        return users.Select(u => ToDto(u, customByUser.GetValueOrDefault(u.Id), orgCtx, hrByUser.GetValueOrDefault(u.Id))).ToList();
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

        if (user is null)
            return null;

        var customByUser = await _fieldService.LoadCustomFieldsForUsersAsync([user.Id]);
        var hrProfile = await _context.UserHrProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == user.Id);
        var orgCtx = await LoadOrgNameContextAsync();
        return ToDto(user, customByUser.GetValueOrDefault(user.Id), orgCtx, hrProfile);
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

        await _fieldService.ValidateCustomFieldsForCreateAsync(dto.CustomFields);

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

        await _fieldService.UpsertCustomFieldsAsync(user.Id, dto.CustomFields, isCreate: true);

        if (user.SubServiceId is int subId)
            await EnsureBalancedSaturdayGroupAsync(user.Id, subId);

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
                string.IsNullOrWhiteSpace(directoryResult.ErrorMessage)
                    ? "La création dans l'annuaire (Directory) a échoué. Réessayez ou contactez l'administrateur."
                    : directoryResult.ErrorMessage);

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

        await _fieldService.UpsertCustomFieldsAsync(user.Id, dto.CustomFields, isCreate: true);
        await UpsertLocalHrProfileAsync(user.Id, dto.ChefDeProjetId, dto.SuperviseurId, dto.ReferentTechniqueId, dto.HrProfile, dto.NiveauExpertiseMetier);
        await _directoryEmployeeWrite.TryUpdateEmployeeAsync(user);

        await PublishEmployeCreatedForUserAsync(user, dto.SubServiceId);
        await _context.SaveChangesAsync();

        if (user.SubServiceId is int subId)
            await EnsureBalancedSaturdayGroupAsync(user.Id, subId);

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
                        RoleName = user.Role?.Name,
                        EmployeeId = user.Guid,
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

        var callerRole = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value
            ?? _httpContextAccessor.HttpContext?.User?.FindFirst("role")?.Value
            ?? string.Empty;
        if (dto.NiveauExpertiseMetier.HasValue && !IsHrOrAdminRole(callerRole))
            throw new UnauthorizedAccessException("Seuls RH et Admin peuvent modifier l'expertise métier.");

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

        if (user.AuthUserId.HasValue)
            await SyncToAuthServiceAsync(user);

        if (!IsDirectoryWriteMaster())
        {
            await PublishEmployeUpdatedForUserAsync(user, dto.SubServiceId);
            await _context.SaveChangesAsync();
            await _directoryEmployeeEnsure.TryEnsureFromPlanningAsync(user);
        }

        await _fieldService.UpsertCustomFieldsAsync(id, dto.CustomFields, isCreate: false);
        await UpsertLocalHrProfileAsync(id, dto.ChefDeProjetId, dto.SuperviseurId, dto.ReferentTechniqueId, dto.HrProfile, dto.NiveauExpertiseMetier);
        if (IsDirectoryWriteMaster())
            await _directoryEmployeeWrite.TryUpdateEmployeeAsync(user);

        if (user.SubServiceId is int subId)
            await EnsureBalancedSaturdayGroupAsync(user.Id, subId);

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

    public async Task<bool> ExitAfterInitialTrainingRejectionAsync(
        Guid employeeGuid,
        string reason,
        CancellationToken ct = default)
    {
        if (employeeGuid == Guid.Empty) return false;

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Guid == employeeGuid, ct);
        if (user is null) return false;

        user.IsActive = false;

        var profile = await _context.UserHrProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
        if (profile is null)
        {
            profile = new UserHrProfile { UserId = user.Id };
            _context.UserHrProfiles.Add(profile);
        }

        profile.EnFormation = false;
        profile.DateSortie ??= DateOnly.FromDateTime(DateTime.UtcNow);

        await _context.SaveChangesAsync(ct);

        if (IsDirectoryWriteMaster())
            await _directoryEmployeeWrite.TryUpdateEmployeeAsync(user, ct);
        else
            await _directoryEmployeeEnsure.TryEnsureFromPlanningAsync(user);

        if (user.AuthUserId.HasValue)
        {
            try
            {
                var response = await _httpClient.DeleteAsync(
                    $"api/auth/users/from-planning/{user.AuthUserId.Value}",
                    ct);
                if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning(
                        "Suppression Auth après rejet formation échouée pour {Email} ({Status}): {Body}",
                        user.Email,
                        response.StatusCode,
                        body);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auth indisponible après rejet formation pour {Email}", user.Email);
            }
        }

        _logger.LogInformation(
            "Sortie employé {Guid} ({Email}) après rejet formation initiale. Motif: {Reason}",
            employeeGuid,
            user.Email,
            reason);
        return true;
    }

    public async Task<bool> CompleteInitialTrainingAsync(
        Guid employeeGuid,
        int niveauExpertiseMetier,
        DateOnly productionStartDate,
        CancellationToken ct = default)
    {
        if (employeeGuid == Guid.Empty) return false;

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Guid == employeeGuid, ct);
        if (user is null) return false;

        var profile = await _context.UserHrProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
        if (profile is null)
        {
            profile = new UserHrProfile { UserId = user.Id };
            _context.UserHrProfiles.Add(profile);
        }

        profile.EnFormation = false;
        profile.DateFinFormationPrevue = productionStartDate == default
            ? DateOnly.FromDateTime(DateTime.UtcNow)
            : productionStartDate;

        var expertise = niveauExpertiseMetier is >= 1 and <= 3
            ? niveauExpertiseMetier
            : 1;
        profile.NiveauExpertiseMetier = expertise;

        await _context.SaveChangesAsync(ct);

        if (IsDirectoryWriteMaster())
            await _directoryEmployeeWrite.TryUpdateEmployeeAsync(user, ct);
        else
            await _directoryEmployeeEnsure.TryEnsureFromPlanningAsync(user);

        _logger.LogInformation(
            "Passage production employé {Guid} ({Email}) — EnFormation=false, expertise={Expertise}.",
            employeeGuid,
            user.Email,
            expertise);
        return true;
    }

    public async Task<bool> IsEmailUniqueAsync(string email, int? excludeId = null)
    {
        var trimmed = email.Trim();
        var usedInPlanning = await _context.Users
            .AnyAsync(u => u.Email == trimmed && u.Id != excludeId);
        if (usedInPlanning)
            return false;

        if (!IsDirectoryWriteMaster())
            return true;

        Guid? excludeGuid = null;
        if (excludeId.HasValue)
        {
            excludeGuid = await _context.Users.AsNoTracking()
                .Where(u => u.Id == excludeId.Value)
                .Select(u => u.Guid)
                .FirstOrDefaultAsync();
            if (excludeGuid == Guid.Empty)
                excludeGuid = null;
        }

        var usedInDirectory = await _directoryEmployeeWrite.IsEmailUsedInDirectoryAsync(trimmed, excludeGuid);
        return !usedInDirectory;
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

        if (user is null)
            return null;

        var customByUser = await _fieldService.LoadCustomFieldsForUsersAsync([user.Id]);
        var hrProfile = await _context.UserHrProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == user.Id);
        var orgCtx = await LoadOrgNameContextAsync();
        return ToDto(user, customByUser.GetValueOrDefault(user.Id), orgCtx, hrProfile);
    }

    public async Task<UserDto?> GetUserByEmailAsync(string email)
    {
        var needle = email.Trim().ToLowerInvariant();
        if (needle.Length == 0)
            return null;

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
            .FirstOrDefaultAsync(u => u.Email.ToLower() == needle);

        if (user is null)
            return null;

        var customByUser = await _fieldService.LoadCustomFieldsForUsersAsync([user.Id]);
        var hrProfile = await _context.UserHrProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == user.Id);
        var orgCtx = await LoadOrgNameContextAsync();
        return ToDto(user, customByUser.GetValueOrDefault(user.Id), orgCtx, hrProfile);
    }

    public async Task<UserDto?> GetOrLinkUserForAuthAsync(int authUserId, string? email)
    {
        var user = await GetUserByAuthIdAsync(authUserId);
        if (user is not null)
            return user;

        if (string.IsNullOrWhiteSpace(email))
            return null;

        var row = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.Trim().ToLowerInvariant());
        if (row is null)
            return null;

        if (row.AuthUserId != authUserId)
        {
            row.AuthUserId = authUserId;
            await _context.SaveChangesAsync();
            _logger.LogInformation("AuthUserId={AuthId} lié à {Email} (planning id={PlanningId})",
                authUserId, row.Email, row.Id);
        }

        return await GetUserByAuthIdAsync(authUserId)
            ?? await GetUserByEmailAsync(email);
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

    private sealed record OrgNameContext(
        Dictionary<string, string> PoleIdToDeptName,
        Dictionary<string, string> DeptIdToName,
        Dictionary<Guid, string> EmployeeOperationalDeptId);

    private async Task<OrgNameContext> LoadOrgNameContextAsync()
    {
        var depts = await _directoryOrg.GetOperationalDepartmentsAsync();
        var poleToDept = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var deptIdToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dept in depts)
        {
            if (!string.IsNullOrWhiteSpace(dept.Id))
                deptIdToName[dept.Id.Trim()] = dept.Name?.Trim() ?? dept.Code?.Trim() ?? dept.Id;
            foreach (var poleId in dept.PoleIds ?? [])
            {
                if (string.IsNullOrWhiteSpace(poleId)) continue;
                poleToDept[poleId.Trim()] = dept.Name?.Trim() ?? dept.Code?.Trim() ?? dept.Id;
            }
        }

        var employeeDeptIds = await _directoryOrg.GetEmployeeOperationalBusinessDepartmentIdsAsync();
        return new OrgNameContext(poleToDept, deptIdToName, employeeDeptIds.ToDictionary(k => k.Key, v => v.Value));
    }

    private static UserDto ToDto(
        User u,
        Dictionary<string, string?>? customFields,
        OrgNameContext orgCtx,
        UserHrProfile? hr = null)
    {
        var (pole, cellule, service) = ResolveOrgNames(u);
        return new UserDto
        {
            Id = u.Id,
            Guid = u.Guid,
            AuthUserId = u.AuthUserId,
            RoleId = u.RoleId,
            RoleName = u.Role?.Name ?? string.Empty,
            SubServiceId = u.SubServiceId,
            SubServiceName = u.SubService?.Name,
            OrgPoleName = pole,
            OrgCelluleName = cellule,
            OrgServiceName = service,
            OrgOperationalDepartmentName = ResolveOperationalDepartmentName(u, orgCtx),
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
            Level = u.Level,
            NiveauExpertiseMetier = hr?.NiveauExpertiseMetier,
            ChefDeProjetId = hr?.ChefDeProjetId,
            SuperviseurId = hr?.SuperviseurId,
            ReferentTechniqueId = hr?.ReferentTechniqueId,
            HrProfile = hr is null ? null : MapHrProfileDto(hr),
            CustomFields = customFields ?? new Dictionary<string, string?>()
        };
    }

    private static UserHrProfileDto MapHrProfileDto(UserHrProfile p) => new()
    {
        DateNaissance = p.DateNaissance,
        VilleNaissance = p.VilleNaissance,
        Nationalite = p.Nationalite,
        NumeroCarteAutoentrepreneur = p.NumeroCarteAutoentrepreneur,
        Sexe = p.Sexe,
        SituationFamiliale = p.SituationFamiliale,
        NombreEnfants = p.NombreEnfants,
        Cin = p.Cin,
        Adresse = p.Adresse,
        EmailPersonnel = p.EmailPersonnel,
        Telephone1 = p.Telephone1,
        TelephoneUrgence = p.TelephoneUrgence,
        RelationUrgence = p.RelationUrgence,
        Rib = p.Rib,
        ImmatriculationInterne = p.ImmatriculationInterne,
        ImmatriculationCnss = p.ImmatriculationCnss,
        DateEntree = p.DateEntree,
        DateEmbauche = p.DateEmbauche,
        DateAnciennete = p.DateAnciennete,
        DateSortie = p.DateSortie,
        DateEvolutionPoste = p.DateEvolutionPoste,
        AncienPoste = p.AncienPoste,
        AncienService = p.AncienService,
        NiveauScolaire = p.NiveauScolaire,
        IntitulesEtudes = p.IntitulesEtudes,
        EnFormation = p.EnFormation,
        DateDebutFormation = p.DateDebutFormation,
        DateFinFormationPrevue = p.DateFinFormationPrevue,
    };

    private async Task<Dictionary<int, UserHrProfile>> LoadHrProfilesAsync(IReadOnlyList<int> userIds)
    {
        if (userIds.Count == 0) return new Dictionary<int, UserHrProfile>();
        var profiles = await _context.UserHrProfiles.AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .ToListAsync();
        return profiles.ToDictionary(p => p.UserId);
    }

    private async Task UpsertLocalHrProfileAsync(
        int userId,
        Guid? chefDeProjetId,
        Guid? superviseurId,
        Guid? referentTechniqueId,
        UserHrProfileDto? dto,
        int? niveauExpertiseMetier)
    {
        if (dto is null
            && !chefDeProjetId.HasValue
            && !superviseurId.HasValue
            && !referentTechniqueId.HasValue
            && !niveauExpertiseMetier.HasValue)
            return;

        var profile = await _context.UserHrProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile is null)
        {
            profile = new UserHrProfile { UserId = userId };
            _context.UserHrProfiles.Add(profile);
        }

        if (chefDeProjetId.HasValue) profile.ChefDeProjetId = chefDeProjetId;
        if (superviseurId.HasValue) profile.SuperviseurId = superviseurId;
        if (referentTechniqueId.HasValue) profile.ReferentTechniqueId = referentTechniqueId;
        if (niveauExpertiseMetier.HasValue) profile.NiveauExpertiseMetier = niveauExpertiseMetier;

        if (dto is not null)
        {
            profile.DateNaissance = dto.DateNaissance;
            profile.VilleNaissance = dto.VilleNaissance;
            profile.Nationalite = dto.Nationalite;
            profile.NumeroCarteAutoentrepreneur = dto.NumeroCarteAutoentrepreneur;
            profile.Sexe = dto.Sexe;
            profile.SituationFamiliale = dto.SituationFamiliale;
            profile.NombreEnfants = dto.NombreEnfants;
            profile.Cin = dto.Cin;
            profile.Adresse = dto.Adresse;
            profile.EmailPersonnel = dto.EmailPersonnel;
            profile.Telephone1 = dto.Telephone1;
            profile.TelephoneUrgence = dto.TelephoneUrgence;
            profile.RelationUrgence = dto.RelationUrgence;
            profile.Rib = dto.Rib;
            profile.ImmatriculationInterne = dto.ImmatriculationInterne;
            profile.ImmatriculationCnss = dto.ImmatriculationCnss;
            profile.DateEntree = dto.DateEntree;
            profile.DateEmbauche = dto.DateEmbauche;
            profile.DateAnciennete = dto.DateAnciennete;
            profile.DateSortie = dto.DateSortie;
            profile.DateEvolutionPoste = dto.DateEvolutionPoste;
            profile.AncienPoste = dto.AncienPoste;
            profile.AncienService = dto.AncienService;
            profile.NiveauScolaire = dto.NiveauScolaire;
            profile.IntitulesEtudes = dto.IntitulesEtudes;
            profile.EnFormation = dto.EnFormation;
            profile.DateDebutFormation = dto.DateDebutFormation;
            profile.DateFinFormationPrevue = dto.DateFinFormationPrevue;
        }

        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<UserDto?> UpdateContractualLevelAsync(
        int targetUserId,
        int level,
        Guid actorSubjectId,
        string actorRole,
        CancellationToken ct = default)
    {
        if (level is < 1 or > 3)
            throw new InvalidOperationException("Niveau contractuel invalide.");

        var target = await _context.Users
            .Include(u => u.SubService)
                .ThenInclude(ss => ss != null ? ss.Service : null!)
            .FirstOrDefaultAsync(u => u.Id == targetUserId, ct);
        if (target is null) return null;

        var role = actorRole.Trim();
        if (!IsHrOrAdminRole(role))
        {
            var actor = await _context.Users.AsNoTracking()
                .Include(u => u.ManagedSubServices)
                    .ThenInclude(us => us.SubService)
                        .ThenInclude(s => s.Service)
                .FirstOrDefaultAsync(u => u.Guid == actorSubjectId, ct);
            if (actor is null)
                throw new UnauthorizedAccessException();

            if (KyntusRoleNames.IsSuperviseur(role))
            {
                if (!await IsUserInSupervisorCelluleAsync(actor, target, ct))
                    throw new UnauthorizedAccessException();
            }
            else if (KyntusRoleNames.IsReferentTechnique(role))
            {
                if (!await IsUserInReferentServiceResponsibilityAsync(actor, target, ct))
                    throw new UnauthorizedAccessException();
            }
            else
            {
                throw new UnauthorizedAccessException();
            }
        }

        target.Level = level;
        await _context.SaveChangesAsync(ct);
        return await GetUserByIdAsync(targetUserId);
    }

    private static bool IsHrOrAdminRole(string role) =>
        string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, "RH", StringComparison.OrdinalIgnoreCase);

    private async Task<bool> IsUserInSupervisorCelluleAsync(User supervisor, User target, CancellationToken ct)
    {
        var targetCellule = target.SubService?.Service?.PrimeCelluleId;
        if (string.IsNullOrWhiteSpace(targetCellule)) return false;

        var supervisorCellules = await _context.UserSubServices.AsNoTracking()
            .Where(us => us.UserId == supervisor.Id)
            .Select(us => us.SubService.Service.PrimeCelluleId)
            .ToListAsync(ct);

        if (supervisor.SubService?.Service?.PrimeCelluleId is { } ownCellule)
            supervisorCellules.Add(ownCellule);

        return supervisorCellules.Any(c => string.Equals(c, targetCellule, StringComparison.Ordinal));
    }

    private async Task<bool> IsUserInReferentServiceResponsibilityAsync(User referent, User target, CancellationToken ct)
    {
        var targetService = target.SubService?.PrimeServiceId;
        var referentService = referent.SubService?.PrimeServiceId;
        if (string.IsNullOrWhiteSpace(targetService) || string.IsNullOrWhiteSpace(referentService))
            return false;
        if (!string.Equals(targetService, referentService, StringComparison.Ordinal))
            return false;

        var targetHr = await _context.UserHrProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == target.Id, ct);
        if (targetHr?.ReferentTechniqueId is { } refId && refId != Guid.Empty)
            return refId == referent.Guid;

        return true;
    }

    private static string? ResolveOperationalDepartmentName(User u, OrgNameContext orgCtx)
    {
        if (orgCtx.EmployeeOperationalDeptId.TryGetValue(u.Guid, out var deptId)
            && orgCtx.DeptIdToName.TryGetValue(deptId, out var managerDeptName))
        {
            return managerDeptName;
        }

        var poleId = u.SubService?.Service?.Floor?.PrimePoleId
            ?? u.ManagedSubServices?.FirstOrDefault()?.SubService?.Service?.Floor?.PrimePoleId
            ?? u.ManagedServices?.FirstOrDefault()?.Service?.Floor?.PrimePoleId;
        if (!string.IsNullOrWhiteSpace(poleId)
            && orgCtx.PoleIdToDeptName.TryGetValue(poleId.Trim(), out var deptName))
        {
            return deptName;
        }

        return null;
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

    public async Task RollbackImportCreatedUserAsync(int planningUserId, CancellationToken ct = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == planningUserId, ct);
        if (user is null)
            return;

        await RollbackImportCreatedUserCoreAsync(user, ct);
    }

    public async Task RollbackImportUpdatedUserAsync(
        int planningUserId,
        UpdateUserDto previousState,
        Dictionary<string, string?> previousCustomFields,
        CancellationToken ct = default)
    {
        await UpdateUserAsync(planningUserId, previousState);
        await _fieldService.UpsertCustomFieldsAsync(planningUserId, previousCustomFields, isCreate: false);
    }

    private async Task RollbackImportUserAsync(User user) =>
        await RollbackImportCreatedUserCoreAsync(user);

    private async Task RollbackImportCreatedUserCoreAsync(User user, CancellationToken ct = default)
    {
        var employeeGuid = user.Guid;
        var authUserId = user.AuthUserId;

        var managedLinks = _context.UserSubServices.Where(us => us.UserId == user.Id);
        _context.UserSubServices.RemoveRange(managedLinks);
        var managedServiceLinks = _context.UserManagedServices.Where(us => us.UserId == user.Id);
        _context.UserManagedServices.RemoveRange(managedServiceLinks);
        _context.Users.Remove(user);
        await _context.SaveChangesAsync(ct);

        await _directoryEmployeeWrite.TryDeleteEmployeeAsync(employeeGuid, ct);

        if (authUserId.HasValue)
        {
            try
            {
                await _httpClient.DeleteAsync($"api/auth/users/from-planning/{authUserId.Value}", ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Rollback Auth échoué pour {Email}", user.Email);
            }
        }
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

    public async Task<List<UserDto>> GetManagersBySubServiceAsync(int subServiceId, CancellationToken ct = default)
    {
        var users = await _context.UserSubServices
            .Include(us => us.User).ThenInclude(u => u.Role)
            .Include(us => us.User).ThenInclude(u => u.SubService)
            .Include(us => us.User)
                .ThenInclude(u => u.ManagedSubServices)
                    .ThenInclude(ms => ms.SubService)
                        .ThenInclude(s => s.Service)
            .Where(us => us.SubServiceId == subServiceId)
            .Select(us => us.User)
            .Distinct()
            .ToListAsync(ct);

        return users.Select(u => new UserDto
        {
            Id = u.Id,
            RoleId = u.RoleId,
            RoleName = u.Role?.Name ?? string.Empty,
            SubServiceId = u.SubServiceId,
            SubServiceName = u.SubService?.Name,
            ManagedSubServices = u.ManagedSubServices?.Select(ms => new SubServiceSimpleDto
            {
                Id = ms.SubService.Id,
                Name = ms.SubService.Name,
                ServiceName = ms.SubService.Service?.Name ?? string.Empty
            }).ToList() ?? [],
            FirstName = u.FirstName,
            LastName = u.LastName,
            HireDate = u.HireDate,
            Email = u.Email,
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt
        }).ToList();
    }

    /// <summary>
    /// Place l'employé dans le groupe samedi minoritaire (ex. 2:3 → 3:3).
    /// No-op s'il a déjà un SaturdayGroup.
    /// </summary>
    private async Task EnsureBalancedSaturdayGroupAsync(int userId, int subServiceId)
    {
        var already = await _context.SaturdayGroups.AnyAsync(sg => sg.UserId == userId);
        if (already) return;

        var peerUserIds = await _context.Users
            .Where(u => u.SubServiceId == subServiceId && u.IsActive && u.Id != userId)
            .Select(u => u.Id)
            .ToListAsync();

        var group1Count = await _context.SaturdayGroups
            .CountAsync(sg => peerUserIds.Contains(sg.UserId) && sg.GroupNumber == 1);
        var group2Count = await _context.SaturdayGroups
            .CountAsync(sg => peerUserIds.Contains(sg.UserId) && sg.GroupNumber == 2);

        var groupNumber = group1Count <= group2Count ? 1 : 2;
        _context.SaturdayGroups.Add(new SaturdayGroup
        {
            UserId = userId,
            GroupNumber = groupNumber,
            IsNewEmployee = false,
            ManagerOverride = false,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = 0
        });
        await _context.SaveChangesAsync();
    }

    public async Task<SetNewEmployeeStatusResultDto?> SetNewEmployeeStatusAsync(
        int id,
        SetNewEmployeeDto dto,
        CancellationToken ct = default)
    {
        var user = await _context.Users.FindAsync([id], ct);
        if (user is null)
            return null;

        user.IsNewEmployee = dto.IsNewEmployee;
        await _context.SaveChangesAsync(ct);

        return new SetNewEmployeeStatusResultDto(
            user.Id,
            $"{user.FirstName} {user.LastName}",
            user.IsNewEmployee,
            user.HireDate);
    }
}
