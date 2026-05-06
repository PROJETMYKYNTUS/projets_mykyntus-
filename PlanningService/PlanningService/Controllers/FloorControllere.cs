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
  //  [Authorize(Roles = "RH")] // Seulement le RH peut créer des étages
    public async Task<ActionResult<FloorDto>> CreateFloor([FromBody] CreateFloorDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var floor = await _floorService.CreateFloorAsync(dto);
        return CreatedAtAction(nameof(GetFloorById), new { id = floor.Id }, floor);
    }

    /// <summary>
    /// Mettre à jour un étage
    /// </summary>
    [HttpPut("{id}")]
    //[Authorize(Roles = "RH")] // Seulement le RH peut modifier des étages
    public async Task<ActionResult<FloorDto>> UpdateFloor(int id, [FromBody] UpdateFloorDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var floor = await _floorService.UpdateFloorAsync(id, dto);

            if (floor == null)
                return NotFound(new { message = $"L'étage avec l'ID {id} n'existe pas." });

            return Ok(floor);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Supprimer un étage
    /// </summary>
    [HttpDelete("{id}")]
    //[Authorize(Roles = "RH")] // Seulement le RH peut supprimer des étages
    public async Task<IActionResult> DeleteFloor(int id)
    {
        try
        {
            var result = await _floorService.DeleteFloorAsync(id);

            if (!result)
                return NotFound(new { message = $"L'étage avec l'ID {id} n'existe pas." });

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}