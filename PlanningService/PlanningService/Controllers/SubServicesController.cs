using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlanningService.Data;
using PlanningService.DTOs;
using PlanningService.Interfaces;

namespace PlanningService.Controllers;

[ApiController]
[Route("api/[controller]")]
//[Authorize]
public class SubServicesController : ControllerBase
{
    private readonly ISubServiceService _subServiceService;
    private readonly AppDbContext _context; // ✅ AJOUTÉ

    public SubServicesController(
        ISubServiceService subServiceService,
        AppDbContext context)              // ✅ AJOUTÉ
    {
        _subServiceService = subServiceService;
        _context = context;               // ✅ AJOUTÉ
    }

    /// <summary>
    /// Récupérer tous les sous-services
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<SubServiceDto>>> GetAllSubServices()
    {
        var subServices = await _subServiceService.GetAllSubServicesAsync();
        return Ok(subServices);
    }

    /// <summary>
    /// Récupérer les sous-services d'un service spécifique
    /// </summary>
    [HttpGet("by-service/{serviceId}")]
    public async Task<ActionResult<List<SubServiceDto>>> GetSubServicesByService(int serviceId)
    {
        var subServices = await _subServiceService.GetSubServicesByServiceIdAsync(serviceId);
        return Ok(subServices);
    }

    /// <summary>
    /// Récupérer un sous-service par ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<SubServiceDto>> GetSubServiceById(int id)
    {
        var subService = await _subServiceService.GetSubServiceByIdAsync(id);

        if (subService == null)
            return NotFound(new { message = $"Le sous-service avec l'ID {id} n'existe pas." });

        return Ok(subService);
    }

    /// <summary>
    /// Récupérer un sous-service avec ses employés
    /// </summary>
    [HttpGet("{id}/details")]
    public async Task<ActionResult<SubServiceDetailDto>> GetSubServiceWithEmployees(int id)
    {
        var subService = await _subServiceService.GetSubServiceWithEmployeesAsync(id);

        if (subService == null)
            return NotFound(new { message = $"Le sous-service avec l'ID {id} n'existe pas." });

        return Ok(subService);
    }

    // ✅ NOUVEAU — Récupérer les employés d'un sous-service (pour le formulaire congés)
    [HttpGet("{id}/employees")]
    public async Task<IActionResult> GetEmployees(int id)
    {
        var employees = await _context.Users
            .Where(u => u.SubServiceId == id && u.IsActive)
            .OrderBy(u => u.FirstName)
            .Select(u => new
            {
                id = u.Id,
                fullName = $"{u.FirstName} {u.LastName}"
            })
            .ToListAsync();

        return Ok(employees);
    }

    /// <summary>
    /// Créer un nouveau sous-service
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SubServiceDto>> CreateSubService([FromBody] CreateSubServiceDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var subService = await _subServiceService.CreateSubServiceAsync(dto);
            return CreatedAtAction(nameof(GetSubServiceById), new { id = subService.Id }, subService);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Mettre à jour un sous-service
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<SubServiceDto>> UpdateSubService(int id, [FromBody] UpdateSubServiceDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var subService = await _subServiceService.UpdateSubServiceAsync(id, dto);

            if (subService == null)
                return NotFound(new { message = $"Le sous-service avec l'ID {id} n'existe pas." });

            return Ok(subService);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Supprimer un sous-service
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSubService(int id)
    {
        try
        {
            var result = await _subServiceService.DeleteSubServiceAsync(id);

            if (!result)
                return NotFound(new { message = $"Le sous-service avec l'ID {id} n'existe pas." });

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Vérifier si un code de sous-service est unique
    /// </summary>
    [HttpGet("check-code/{code}")]
    public async Task<ActionResult<bool>> CheckCodeUnique(string code, [FromQuery] int? excludeId = null)
    {
        var isUnique = await _subServiceService.IsCodeUniqueAsync(code, excludeId);
        return Ok(new { isUnique });
    }
}