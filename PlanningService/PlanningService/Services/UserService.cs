using Microsoft.EntityFrameworkCore;
using Planning.Messaging.Publishers;
using PlanningService.Data;
using PlanningService.DTOs;
using PlanningService.Interfaces;
using PlanningService.Models;

namespace PlanningService.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly IEmployePublisher _employePublisher; // 🆕

    public UserService(AppDbContext context, IEmployePublisher employePublisher) // 🆕
    {
        _context = context;
        _employePublisher = employePublisher; // 🆕
    }

    public async Task<List<UserDto>> GetAllUsersAsync()
    {
        var users = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.SubService)
            .Include(u => u.ManagedSubServices)
                .ThenInclude(us => us.SubService)
                    .ThenInclude(s => s.Service)
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

        // Ajouter les sous-services gérés (Manager/Coach)
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

        // 🆕 Récupérer le sous-service pour avoir le nom du service
        var subService = dto.SubServiceId.HasValue
            ? await _context.SubServices
                .Include(ss => ss.Service)
                .FirstOrDefaultAsync(ss => ss.Id == dto.SubServiceId.Value)
            : null;

        // 🆕 Récupérer le manager du sous-service (premier manager trouvé)
        var manager = dto.SubServiceId.HasValue
            ? await _context.UserSubServices
                .Include(us => us.User)
                    .ThenInclude(u => u.Role)
                .Where(us => us.SubServiceId == dto.SubServiceId.Value
                          && us.User.Role.Name == "Manager")
                .Select(us => us.User)
                .FirstOrDefaultAsync()
            : null;

        // 🆕 Publier l'event vers Conge Service via RabbitMQ
        // CreateUserAsync — utiliser user.Guid au lieu du faux Guid
        await _employePublisher.PublishEmployeCreatedAsync(
            employeId: user.Guid,          // ← REMPLACER le PadLeft
            nom: user.LastName,
            prenom: user.FirstName,
            email: user.Email,
            managerId: manager != null
                          ? manager.Guid  // ← REMPLACER le PadLeft
                          : Guid.Empty,
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

        // Mettre à jour les sous-services gérés
        var existing = _context.UserSubServices.Where(us => us.UserId == id);
        _context.UserSubServices.RemoveRange(existing);

        if (dto.ManagedSubServiceIds.Any())
        {
            foreach (var subId in dto.ManagedSubServiceIds)
            {
                _context.UserSubServices.Add(new UserSubService
                {
                    UserId = id,
                    SubServiceId = subId
                });
            }
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

        // Supprimer les liaisons Manager/Coach
        var managedLinks = _context.UserSubServices.Where(us => us.UserId == id);
        _context.UserSubServices.RemoveRange(managedLinks);

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> IsEmailUniqueAsync(string email, int? excludeId = null)
    {
        return !await _context.Users
            .AnyAsync(u => u.Email == email && u.Id != excludeId);
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
        FirstName = u.FirstName,
        LastName = u.LastName,
        Email = u.Email,
        HireDate = u.HireDate,
        IsActive = u.IsActive,
        CreatedAt = u.CreatedAt,
        Level = u.Level
    };
}
