using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanningService.DTOs;
using PlanningService.Interfaces;

namespace PlanningService.Controllers;

[ApiController]
[Route("api/[controller]")]
//[Authorize] // Tous les endpoints nécessitent l'authentification
public class FloorController : ControllerBase
{
    private readonly IFloorService _floorService;

    public FloorController(IFloorService floorService)
    {
        _floorService = floorService;
    }

    /// <summary>
    /// Récupérer tous les étages
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<FloorDto>>> GetAllFloors()
    {
        var floors = await _floorService.GetAllFloorsAsync();
        return Ok(floors);
    }

    /// <summary>
    /// Récupérer un étage par ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<FloorDto>> GetFloorById(int id)
    {
        var floor = await _floorService.GetFloorByIdAsync(id);

        if (floor == null)
            return NotFound(new { message = $"L'étage avec l'ID {id} n'existe pas." });

        return Ok(floor);
    }

    /// <summary>
    /// Récupérer un étage avec ses services
    /// </summary>
    [HttpGet("{id}/details")]
    public async Task<ActionResult<FloorDetailDto>> GetFloorWithServices(int id)
    {
        var floor = await _floorService.GetFloorWithServicesAsync(id);

        if (floor == null)
            return NotFound(new { message = $"L'étage avec l'ID {id} n'existe pas." });

        return Ok(floor);
    }

    /// <summary>
    /// Créer un nouvel étage
    /// </summary>
    [HttpPost]
    public IActionResult CreateFloor([FromBody] CreateFloorDto dto)
    {
        return StatusCode(403, new { message = "Structure en lecture seule. Gérer dans Organisation RH (Prime)." });
    }

    [HttpPut("{id}")]
    public IActionResult UpdateFloor(int id, [FromBody] UpdateFloorDto dto)
    {
        return StatusCode(403, new { message = "Structure en lecture seule. Gérer dans Organisation RH (Prime)." });
    }

    [HttpDelete("{id}")]
    public IActionResult DeleteFloor(int id)
    {
        return StatusCode(403, new { message = "Structure en lecture seule. Gérer dans Organisation RH (Prime)." });
    }
}