using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanningService.DTOs;
using PlanningService.Interfaces;

namespace PlanningService.Controllers;

[ApiController]
[Route("api/[controller]")]
//[Authorize]
public class ServicesController : ControllerBase
{
    private readonly IServiceService _serviceService;

    public ServicesController(IServiceService serviceService)
    {
        _serviceService = serviceService;
    }

    /// <summary>
    /// Récupérer tous les services
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ServiceDto>>> GetAllServices()
    {
        var services = await _serviceService.GetAllServicesAsync();
        return Ok(services);
    }

    /// <summary>
    /// Récupérer les services d'un étage spécifique
    /// </summary>
    [HttpGet("by-floor/{floorId}")]
    public async Task<ActionResult<List<ServiceDto>>> GetServicesByFloor(int floorId)
    {
        var services = await _serviceService.GetServicesByFloorIdAsync(floorId);
        return Ok(services);
    }

    /// <summary>
    /// Récupérer un service par ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ServiceDto>> GetServiceById(int id)
    {
        var service = await _serviceService.GetServiceByIdAsync(id);

        if (service == null)
            return NotFound(new { message = $"Le service avec l'ID {id} n'existe pas." });

        return Ok(service);
    }

    /// <summary>
    /// Récupérer un service avec ses sous-services
    /// </summary>
    [HttpGet("{id}/details")]
    public async Task<ActionResult<ServiceDetailDto>> GetServiceWithSubServices(int id)
    {
        var service = await _serviceService.GetServiceWithSubServicesAsync(id);

        if (service == null)
            return NotFound(new { message = $"Le service avec l'ID {id} n'existe pas." });

        return Ok(service);
    }

    /// <summary>
    /// Créer un nouveau service
    /// </summary>
    [HttpPost]
    //[Authorize(Roles = "RH")]
    public async Task<ActionResult<ServiceDto>> CreateService([FromBody] CreateServiceDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var service = await _serviceService.CreateServiceAsync(dto);
            return CreatedAtAction(nameof(GetServiceById), new { id = service.Id }, service);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Mettre à jour un service
    /// </summary>
    [HttpPut("{id}")]
   // [Authorize(Roles = "RH")]
    public async Task<ActionResult<ServiceDto>> UpdateService(int id, [FromBody] UpdateServiceDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var service = await _serviceService.UpdateServiceAsync(id, dto);

            if (service == null)
                return NotFound(new { message = $"Le service avec l'ID {id} n'existe pas." });

            return Ok(service);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Supprimer un service
    /// </summary>
    [HttpDelete("{id}")]
    //[Authorize(Roles = "RH")]
    public async Task<IActionResult> DeleteService(int id)
    {
        try
        {
            var result = await _serviceService.DeleteServiceAsync(id);

            if (!result)
                return NotFound(new { message = $"Le service avec l'ID {id} n'existe pas." });

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Vérifier si un code de service est unique
    /// </summary>
    [HttpGet("check-code/{code}")]
    //[Authorize(Roles = "RH")]
    public async Task<ActionResult<bool>> CheckCodeUnique(string code, [FromQuery] int? excludeId = null)
    {
        var isUnique = await _serviceService.IsCodeUniqueAsync(code, excludeId);
        return Ok(new { isUnique });
    }
}