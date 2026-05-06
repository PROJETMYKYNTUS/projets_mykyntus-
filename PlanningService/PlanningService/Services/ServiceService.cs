using Microsoft.EntityFrameworkCore;
using PlanningService.Data;
using PlanningService.DTOs;
using PlanningService.Interfaces;
using PlanningService.Models;

namespace PlanningService.Services;

public class ServiceService : IServiceService
{
    private readonly AppDbContext _context;

    public ServiceService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ServiceDto>> GetAllServicesAsync()
    {
        return await _context.Services
            .Include(s => s.Floor)
            .Include(s => s.SubServices)
            .Select(s => new ServiceDto
            {
                Id = s.Id,
                FloorId = s.FloorId,
                FloorName = s.Floor.Name,
                Name = s.Name,
                Code = s.Code,
                SubServicesCount = s.SubServices.Count
            })
            .OrderBy(s => s.FloorName)
            .ThenBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<List<ServiceDto>> GetServicesByFloorIdAsync(int floorId)
    {
        return await _context.Services
            .Include(s => s.Floor)
            .Include(s => s.SubServices)
            .Where(s => s.FloorId == floorId)
            .Select(s => new ServiceDto
            {
                Id = s.Id,
                FloorId = s.FloorId,
                FloorName = s.Floor.Name,
                Name = s.Name,
                Code = s.Code,
                SubServicesCount = s.SubServices.Count
            })
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<ServiceDto?> GetServiceByIdAsync(int id)
    {
        var service = await _context.Services
            .Include(s => s.Floor)
            .Include(s => s.SubServices)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (service == null)
            return null;

        return new ServiceDto
        {
            Id = service.Id,
            FloorId = service.FloorId,
            FloorName = service.Floor.Name,
            Name = service.Name,
            Code = service.Code,
            SubServicesCount = service.SubServices.Count
        };
    }

    public async Task<ServiceDetailDto?> GetServiceWithSubServicesAsync(int id)
    {
        var service = await _context.Services
            .Include(s => s.Floor)
            .Include(s => s.SubServices)
                .ThenInclude(ss => ss.Users)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (service == null)
            return null;

        return new ServiceDetailDto
        {
            Id = service.Id,
            FloorId = service.FloorId,
            FloorName = service.Floor.Name,
            Name = service.Name,
            Code = service.Code,
            SubServices = service.SubServices.Select(ss => new SubServiceDto
            {
                Id = ss.Id,
                ServiceId = ss.ServiceId,
                ServiceName = service.Name,
                Name = ss.Name,
                Code = ss.Code,
                EmployeesCount = ss.Users.Count
            }).ToList()
        };
    }

    public async Task<ServiceDto> CreateServiceAsync(CreateServiceDto dto)
    {
        // Vérifier que l'étage existe
        var floorExists = await _context.Floors.AnyAsync(f => f.Id == dto.FloorId);
        if (!floorExists)
        {
            throw new InvalidOperationException($"L'étage avec l'ID {dto.FloorId} n'existe pas.");
        }

        // Vérifier l'unicité du code
        var codeExists = await _context.Services.AnyAsync(s => s.Code == dto.Code);
        if (codeExists)
        {
            throw new InvalidOperationException($"Le code '{dto.Code}' est déjà utilisé par un autre service.");
        }

        var service = new Service
        {
            FloorId = dto.FloorId,
            Name = dto.Name,
            Code = dto.Code
        };

        _context.Services.Add(service);
        await _context.SaveChangesAsync();

        // Recharger avec les relations
        var floor = await _context.Floors.FindAsync(dto.FloorId);

        return new ServiceDto
        {
            Id = service.Id,
            FloorId = service.FloorId,
            FloorName = floor?.Name ?? "",
            Name = service.Name,
            Code = service.Code,
            SubServicesCount = 0
        };
    }

    public async Task<ServiceDto?> UpdateServiceAsync(int id, UpdateServiceDto dto)
    {
        var service = await _context.Services
            .Include(s => s.Floor)
            .Include(s => s.SubServices)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (service == null)
            return null;

        // Vérifier que le nouvel étage existe
        var floorExists = await _context.Floors.AnyAsync(f => f.Id == dto.FloorId);
        if (!floorExists)
        {
            throw new InvalidOperationException($"L'étage avec l'ID {dto.FloorId} n'existe pas.");
        }

        // Vérifier l'unicité du code (sauf pour le service actuel)
        var codeExists = await _context.Services
            .AnyAsync(s => s.Code == dto.Code && s.Id != id);
        if (codeExists)
        {
            throw new InvalidOperationException($"Le code '{dto.Code}' est déjà utilisé par un autre service.");
        }

        service.FloorId = dto.FloorId;
        service.Name = dto.Name;
        service.Code = dto.Code;

        await _context.SaveChangesAsync();

        return new ServiceDto
        {
            Id = service.Id,
            FloorId = service.FloorId,
            FloorName = service.Floor.Name,
            Name = service.Name,
            Code = service.Code,
            SubServicesCount = service.SubServices.Count
        };
    }

    public async Task<bool> DeleteServiceAsync(int id)
    {
        var service = await _context.Services
            .Include(s => s.SubServices)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (service == null)
            return false;

        // Vérifier si le service a des sous-services
        if (service.SubServices.Any())
        {
            throw new InvalidOperationException(
                "Impossible de supprimer ce service car il contient des sous-services. " +
                "Veuillez d'abord supprimer ou déplacer les sous-services.");
        }

        _context.Services.Remove(service);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ServiceExistsAsync(int id)
    {
        return await _context.Services.AnyAsync(s => s.Id == id);
    }

    public async Task<bool> IsCodeUniqueAsync(string code, int? excludeId = null)
    {
        if (excludeId.HasValue)
        {
            return !await _context.Services.AnyAsync(s => s.Code == code && s.Id != excludeId.Value);
        }

        return !await _context.Services.AnyAsync(s => s.Code == code);
    }
}