using Microsoft.EntityFrameworkCore;
using Planning.Messaging.Publishers;
using PlanningService.Data;
using PlanningService.DTOs;
using PlanningService.Interfaces;
using PlanningService.Models;

namespace PlanningService.Services;

file record AuthRegisterResult(int Id, string Email);
public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly IEmployePublisher _employePublisher; // 🆕
    private readonly HttpClient _httpClient;           // 🆕
    private readonly ILogger<UserService> _logger;     // 🆕

    public UserService(
           AppDbContext context,
           IEmployePublisher employePublisher,
           HttpClient httpClient,                         // 🆕
           ILogger<UserService> logger)                   // 🆕
    {
        _context = context;
        _employePublisher = employePublisher;
        _httpClient = httpClient;                      // 🆕
        _logger = logger;                              // 🆕
    }

    public async Task<List<UserDto>> GetAllUsersAsync()
    {
        var users = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.SubService)
            .Include(u => u.ManagedSubServices)
                .ThenInclude(us => us.SubService)
                    .ThenInclude(s => s.Service)
            // 🆕 AJOUTER
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
                .ThenInclude(ss => ss != null ? ss.Service : null)
            .Include(u => u.ManagedSubServices)
                .ThenInclude(us => us.SubService)
                    .ThenInclude(s => s.Service)
            // 🆕 AJOUTER
            .Include(u => u.ManagedServices)
                .ThenInclude(us => us.Service)
                    .ThenInclude(s => s.Floor)
            .FirstOrDefaultAsync(u => u.Id == id);

        return user == null ? null : ToDto(user);
    }

    public async Task<UserDto> CreateUserAsync(CreateUserDto dto)
    {
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

        // 🆕 Sync vers Auth Service (ne bloque pas si Auth est down)
        await SyncToAuthServiceAsync(user);

        // ✅ TOUT LE RESTE EXACTEMENT INCHANGÉ
        if (dto.ManagedSubServiceIds.Any())
        {
            foreach (var subId in dto.ManagedSubServiceIds)
            {
                _context.UserSubServices.Add(new UserSubService
                {
                    UserId = user.Id,
                    SubServiceId = subId
                });
            }
            await _context.SaveChangesAsync();
        }
        // 🆕 Ajouter les services gérés
        if (dto.ManagedServiceIds.Any())
        {
            foreach (var serviceId in dto.ManagedServiceIds)
            {
                _context.UserManagedServices.Add(new UserManagedService
                {
                    UserId = user.Id,
                    ServiceId = serviceId
                });
            }
            await _context.SaveChangesAsync();
        }
        var subService = dto.SubServiceId.HasValue
            ? await _context.SubServices
                .Include(ss => ss.Service)
                .FirstOrDefaultAsync(ss => ss.Id == dto.SubServiceId.Value)
            : null;

        var manager = dto.SubServiceId.HasValue
            ? await _context.UserSubServices
                .Include(us => us.User)
                    .ThenInclude(u => u.Role)
                .Where(us => us.SubServiceId == dto.SubServiceId.Value
                          && us.User.Role.Name == "Manager")
                .Select(us => us.User)
                .FirstOrDefaultAsync()
            : null;

        await _employePublisher.PublishEmployeCreatedAsync(
            employeId: user.Guid,
            nom: user.LastName,
            prenom: user.FirstName,
            email: user.Email,
            managerId: manager != null ? manager.Guid : Guid.Empty,
            serviceId: subService != null
                            ? Guid.Parse(subService.ServiceId.ToString().PadLeft(32, '0'))
                            : Guid.Empty,
            serviceNom: subService?.Service?.Name ?? string.Empty,
            dateEmbauche: user.HireDate,
            estMineur: false
        );

        return await GetUserByIdAsync(user.Id)
            ?? throw new Exception("Erreur création utilisateur.");
    }
    // 🆕 Méthode privée : sync vers Auth Service
    private async Task SyncToAuthServiceAsync(User user)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "api/auth/register-from-planning",
                new
                {
                    Email = user.Email,
                    DefaultPassword = "Azerty@123",
                    RoleId = user.RoleId
                });

            if (response.IsSuccessStatusCode)
            {
                // ✅ Récupérer l'Auth ID et le sauvegarder
                var result = await response.Content
                    .ReadFromJsonAsync<AuthRegisterResult>();
                if (result != null)
                {
                    user.AuthUserId = result.Id;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation(
                        "✅ Auth Service → User {Email} créé avec AuthId={Id}",
                        user.Email, result.Id);
                }
            }
            else
            {
                _logger.LogWarning("⚠️ Auth Service → {Status} pour {Email}",
                    response.StatusCode, user.Email);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("❌ Auth Service inaccessible : {Message}", ex.Message);
        }
    }
    public async Task SyncAllEmployesToCongeAsync()
    {
        var users = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.SubService)
                .ThenInclude(ss => ss != null ? ss.Service : null)
            .Where(u => u.IsActive)
            .ToListAsync();

        foreach (var user in users)
        {
            var manager = user.SubServiceId.HasValue
                ? await _context.UserSubServices
                    .Include(us => us.User).ThenInclude(u => u.Role)
                    .Where(us => us.SubServiceId == user.SubServiceId.Value
                              && us.User.Role.Name == "Manager")
                    .Select(us => us.User)
                    .FirstOrDefaultAsync()
                : null;

            await _employePublisher.PublishEmployeCreatedAsync(
                employeId: user.Guid,
                nom: user.LastName,
                prenom: user.FirstName,
                email: user.Email,
                managerId: manager?.Guid ?? Guid.Empty,
                serviceId: user.SubService != null
                                ? Guid.Parse(user.SubService.ServiceId.ToString().PadLeft(32, '0'))
                                : Guid.Empty,
                serviceNom: user.SubService?.Service?.Name ?? string.Empty,
                dateEmbauche: DateTime.SpecifyKind(user.HireDate, DateTimeKind.Utc), // ← FIX
                estMineur: false
            );
        }
    }
    public async Task<UserDto?> UpdateUserAsync(int id, UpdateUserDto dto)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return null;

        user.RoleId = dto.RoleId;
        user.SubServiceId = dto.SubServiceId;
        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.Email = dto.Email;
        user.HireDate = dto.HireDate;
        user.IsActive = dto.IsActive;
        user.Level = dto.Level;

        // ✅ SubServices inchangé
        var existing = _context.UserSubServices.Where(us => us.UserId == id);
        _context.UserSubServices.RemoveRange(existing);
        if (dto.ManagedSubServiceIds.Any())
        {
            foreach (var subId in dto.ManagedSubServiceIds)
                _context.UserSubServices.Add(new UserSubService { UserId = id, SubServiceId = subId });
        }

        // 🆕 Services gérés
        var existingServices = _context.UserManagedServices.Where(us => us.UserId == id);
        _context.UserManagedServices.RemoveRange(existingServices);
        if (dto.ManagedServiceIds.Any())
        {
            foreach (var serviceId in dto.ManagedServiceIds)
                _context.UserManagedServices.Add(new UserManagedService { UserId = id, ServiceId = serviceId });
        }

        await _context.SaveChangesAsync();

        // 🆕 Récupérer le sous-service mis à jour
        var subService = dto.SubServiceId.HasValue
            ? await _context.SubServices
                .Include(ss => ss.Service)
                .FirstOrDefaultAsync(ss => ss.Id == dto.SubServiceId.Value)
            : null;

        // 🆕 Récupérer le manager du sous-service
        var manager = dto.SubServiceId.HasValue
            ? await _context.UserSubServices
                .Include(us => us.User)
                    .ThenInclude(u => u.Role)
                .Where(us => us.SubServiceId == dto.SubServiceId.Value
                          && us.User.Role.Name == "Manager")
                .Select(us => us.User)
                .FirstOrDefaultAsync()
            : null;

        // 🆕 Publier l'event de mise à jour vers Conge Service
        await _employePublisher.PublishEmployeUpdatedAsync(
        employeId: user.Guid,         // ← REMPLACER
        nom: user.LastName,
        prenom: user.FirstName,
        email: user.Email,
        managerId: manager != null
                      ? manager.Guid // ← REMPLACER
                      : Guid.Empty,
        serviceId: subService != null
                      ? Guid.Parse(subService.ServiceId.ToString().PadLeft(32, '0'))
                      : Guid.Empty,
        serviceNom: subService?.Service?.Name ?? string.Empty
    );

        return await GetUserByIdAsync(id);
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return false;

        // ✅ inchangé
        var managedLinks = _context.UserSubServices.Where(us => us.UserId == id);
        _context.UserSubServices.RemoveRange(managedLinks);

        // 🆕 AJOUTER
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
    private static UserDto ToDto(User u) => new()
    {
        Id = u.Id,
        Guid = u.Guid,
        RoleId = u.RoleId,
        RoleName = u.Role?.Name ?? string.Empty,
        SubServiceId = u.SubServiceId,
        SubServiceName = u.SubService?.Name,
        ManagedSubServices = u.ManagedSubServices?.Select(us => new SubServiceSimpleDto
        {
            Id = us.SubService.Id,
            Name = us.SubService.Name,
            ServiceName = us.SubService.Service?.Name ?? string.Empty
        }).ToList() ?? new(),
        // 🆕 AJOUTER
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
