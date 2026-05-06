using Microsoft.AspNetCore.Mvc;
using PlanningService.DTOs.Planning;
using PlanningService.Interfaces;

namespace PlanningService.Controllers;   // ✅ namespace ajouté

[ApiController]
[Route("api/planning")]
public class PlanningController : ControllerBase
{
    private readonly IPlanningService _planningService;

    public PlanningController(IPlanningService planningService)
    {
        _planningService = planningService;
    }

    // ════════════════════════════════════════════════════
    // CRUD PLANNING
    // ════════════════════════════════════════════════════

    // GET api/planning/subservice/3
    [HttpGet("subservice/{subServiceId}")]
    public async Task<IActionResult> GetBySubService(int subServiceId)
    {
        var result = await _planningService.GetPlanningsBySubServiceAsync(subServiceId);
        return Ok(result);
    }

    // GET api/planning/5
    [HttpGet("{id:int}")]  // ← UNIQUEMENT les entiers
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _planningService.GetPlanningByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    // POST api/planning
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWeeklyPlanningDto dto)
    {
        try
        {
            var result = await _planningService.CreatePlanningAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // DELETE api/planning/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _planningService.DeletePlanningAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // ════════════════════════════════════════════════════
    // ✅ NOUVEAU — CONFIG SHIFTS
    // ════════════════════════════════════════════════════

    // POST api/planning/config
    // Le responsable sauvegarde sa config shifts pour la semaine
    [HttpPost("config")]
    public async Task<IActionResult> SaveShiftConfig([FromBody] SaveShiftConfigDto dto)
    {
        try
        {
            var result = await _planningService.SaveShiftConfigAsync(dto);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // GET api/planning/config/3/2026-W10
    // Lire la config d'un sous-service pour une semaine
    [HttpGet("config/{subServiceId}/{weekCode}")]
    public async Task<IActionResult> GetShiftConfig(int subServiceId, string weekCode)
    {
        var result = await _planningService.GetShiftConfigAsync(subServiceId, weekCode);
        return result == null ? NotFound() : Ok(result);
    }

    // ════════════════════════════════════════════════════
    // GÉNÉRATION
    // ════════════════════════════════════════════════════

    // POST api/planning/generate
    // Ancienne génération (compatibilité)
    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GeneratePlanningDto dto)
    {
        try
        {
            var result = await _planningService.GeneratePlanningAsync(dto);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST api/planning/generate-from-config
    // ✅ Nouvelle génération depuis la config du responsable
    [HttpPost("generate-from-config")]
    public async Task<IActionResult> GenerateFromConfig(
        [FromBody] GeneratePlanningFromConfigDto dto)
    {
        try
        {
            var result = await _planningService.GeneratePlanningFromConfigAsync(dto);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ════════════════════════════════════════════════════
    // PUBLICATION + OVERRIDE
    // ════════════════════════════════════════════════════

    // POST api/planning/5/publish
    [HttpPost("{id}/publish")]
    public async Task<IActionResult> Publish(int id, [FromQuery] int validatorId)
    {
        try
        {
            var result = await _planningService.PublishPlanningAsync(id, validatorId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // PUT api/planning/override
    [HttpPut("override")]
    public async Task<IActionResult> OverrideShift([FromBody] OverrideShiftDto dto)
    {
        try
        {
            var result = await _planningService.OverrideShiftAsync(dto);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ════════════════════════════════════════════════════
    // GROUPES SAMEDI
    // ════════════════════════════════════════════════════

    // POST api/planning/saturday-groups/auto/3
    [HttpPost("saturday-groups/auto/{subServiceId}")]
    public async Task<IActionResult> AutoAssignGroups(int subServiceId)
    {
        await _planningService.AutoAssignSaturdayGroupsAsync(subServiceId);
        return Ok(new { message = "Groupes samedi assignés automatiquement." });
    }

    // POST api/planning/saturday-group
    [HttpPost("saturday-group")]
    public async Task<IActionResult> SetSaturdayGroup([FromBody] SetSaturdayGroupDto dto)
    {
        await _planningService.SetSaturdayGroupAsync(dto);
        return Ok(new { message = "Groupe samedi configuré." });
    }

    // GET api/planning/saturday-groups/3
    [HttpGet("saturday-groups/{subServiceId}")]
    public async Task<IActionResult> GetSaturdayGroups(int subServiceId)
    {
        var result = await _planningService.GetSaturdayGroupsAsync(subServiceId);
        return Ok(result);
    }

    // ════════════════════════════════════════════════════
    // VUE EMPLOYÉ
    // ════════════════════════════════════════════════════
    // VUE EMPLOYÉ
    // ════════════════════════════════════════════════════

    // ✅ DOIT être AVANT my/{weekCode}
    [HttpGet("my/current")]
    public async Task<IActionResult> GetMyCurrentPlanning([FromQuery] int userId)
    {
        var result = await _planningService.GetMyCurrentPlanningAsync(userId);
        return result == null ? NotFound() : Ok(result);
    }

    // ✅ DOIT être AVANT my/history aussi
    [HttpGet("my/history")]
    public async Task<IActionResult> GetMyHistory([FromQuery] int userId)
    {
        var result = await _planningService.GetMyPlanningHistoryAsync(userId);
        return Ok(result);
    }

    // ✅ Route paramétrique EN DERNIER
    [HttpGet("my/{weekCode}")]
    public async Task<IActionResult> GetMyPlanning(string weekCode, [FromQuery] int userId)
    {
        var result = await _planningService.GetMyPlanningAsync(userId, weekCode);
        return result == null ? NotFound() : Ok(result);
    }

    // GET api/planning/my/history?userId=5

    // POST api/planning/comment
    [HttpPost("comment")]
    public async Task<IActionResult> SaveComment([FromBody] SavePlanningCommentDto dto)
    {
        try
        {
            var result = await _planningService.SaveCommentAsync(dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // DELETE api/planning/5/comment/3
    [HttpDelete("{planningId}/comment/{userId}")]
    public async Task<IActionResult> DeleteComment(int planningId, int userId)
    {
        await _planningService.DeleteCommentAsync(planningId, userId);
        return NoContent();
    }

    // GET api/planning/5/comments
    [HttpGet("{planningId}/comments")]
    public async Task<IActionResult> GetComments(int planningId)
    {
        var result = await _planningService.GetCommentsAsync(planningId);
        return Ok(result);
    }

    [HttpPut("override-break")]
    public async Task<IActionResult> OverrideBreak(
    [FromBody] OverrideBreakDto dto)
    {
        try
        {
            var result = await _planningService.OverrideBreakAsync(dto);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    // GET /api/planning/saturday-history/{subServiceId}/{weekCode}
    [HttpGet("saturday-history/{subServiceId}/{weekCode}")]
    public async Task<IActionResult> GetSaturdayHistory(
        int subServiceId, string weekCode)
        => Ok(await _planningService.GetSaturdayHistoryAsync(subServiceId, weekCode));

    // POST /api/planning/saturday-history
    [HttpPost("saturday-history")]
    public async Task<IActionResult> SaveSaturdayHistory(
        [FromBody] SetSaturdayHistoryDto dto)
    {
        await _planningService.SaveSaturdayHistoryAsync(dto, true);
        return Ok(new { message = "Historique samedi sauvegardé." });
    }
    // PUT api/planning/override-saturday
    [HttpPut("override-saturday")]
    public async Task<IActionResult> OverrideSaturdayShift(
        [FromBody] OverrideSaturdayDto dto)
    {
        try
        {
            var result = await _planningService.OverrideSaturdayShiftAsync(dto);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    [HttpDelete("{planningId}/saturday/{userId}/off")]
    public async Task<IActionResult> SetSaturdayOff(int planningId, int userId)
    {
        try
        {
            await _planningService.SetSaturdayOffAsync(planningId, userId);
            return Ok(new { message = "Samedi mis à OFF." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }


}