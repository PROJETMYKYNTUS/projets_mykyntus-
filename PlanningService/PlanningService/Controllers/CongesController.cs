using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlanningService.Data;
using PlanningService.Enums;
using PlanningService.Models;
using PlanningService.DTOs;
namespace PlanningService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CongesController : ControllerBase
{
    private readonly AppDbContext _context;
    public CongesController(AppDbContext context) => _context = context;

    // GET /api/Conges/subservice/{id}?weekStart=2026-03-16
    [HttpGet("subservice/{subServiceId}")]
    public async Task<IActionResult> GetBySubService(
        int subServiceId, [FromQuery] string? weekStart = null)
    {
        var userIds = await _context.Users
            .Where(u => u.SubServiceId == subServiceId && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync();

        var query = _context.Conges
            .Include(c => c.User)
            .Where(c => userIds.Contains(c.UserId));

        if (weekStart != null && DateOnly.TryParse(weekStart, out var start))
        {
            var end = start.AddDays(6);
            query = query.Where(c => c.StartDate <= end && c.EndDate >= start);
        }

        var result = await query
            .OrderBy(c => c.StartDate)
            .Select(c => new
            {
                id = c.Id,
                userId = c.UserId,
                fullName = $"{c.User.FirstName} {c.User.LastName}",
                startDate = c.StartDate,
                endDate = c.EndDate,
                reason = c.Reason,
                absenceType = c.AbsenceType.ToString(),  // ← NOUVEAU
                status = c.Status.ToString()
            })
            .ToListAsync();

        return Ok(result);
    }
    // GET /api/Conges/new-employees/{subServiceId}  ← MANQUANT
    [HttpGet("new-employees/{subServiceId}")]
    public async Task<IActionResult> GetNewEmployees(int subServiceId)
    {
        var employees = await _context.Users
            .Where(u => u.SubServiceId == subServiceId
                     && u.IsActive
                     && u.IsNewEmployee)
            .ToListAsync();

        var userIds = employees.Select(e => e.Id).ToList();
        var groups = await _context.SaturdayGroups
            .Where(sg => userIds.Contains(sg.UserId))
            .ToListAsync();

        var result = employees.Select(emp =>
        {
            var group = groups.FirstOrDefault(g => g.UserId == emp.Id);
            var monthsHere = (int)((DateTime.UtcNow - emp.HireDate).TotalDays / 30);
            return new
            {
                id = emp.Id,
                fullName = $"{emp.FirstName} {emp.LastName}",
                hireDate = emp.HireDate,
                monthsHere = monthsHere,
                isNewEmployee = emp.IsNewEmployee,
                saturdaySlot = group?.NewEmployeeSlot ?? 0,
                saturdaySlotLabel = group?.NewEmployeeSlot == 1 ? "8h00–12h00"
                                  : group?.NewEmployeeSlot == 2 ? "12h00–16h00"
                                  : "Non configuré"
            };
        });

        return Ok(result);
    }
    // POST /api/Conges
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCongeDto dto)
    {
        if (!Enum.TryParse<AbsenceType>(dto.AbsenceType, out var absenceType))
            return BadRequest(new { message = "Type d'absence invalide." });

        var conge = new Conge
        {
            UserId = dto.UserId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Reason = dto.Reason ?? "",
            AbsenceType = absenceType,  // ← NOUVEAU
            Status = CongeStatus.Approved,
            CreatedAt = DateTime.UtcNow
        };

        _context.Conges.Add(conge);
        await _context.SaveChangesAsync();
        return Ok(conge);
    }
  

    // DELETE /api/Conges/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var conge = await _context.Conges.FindAsync(id);
        if (conge == null) return NotFound();
        _context.Conges.Remove(conge);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // POST /api/Conges/saturday-slot
    // Manager choisit le slot samedi pour un nouvel employe
    [HttpPost("saturday-slot")]
    public async Task<IActionResult> SetSaturdaySlot([FromBody] SetSaturdaySlotDto dto)
    {
        var group = await _context.SaturdayGroups
            .FirstOrDefaultAsync(sg => sg.UserId == dto.UserId);

        if (group == null)
            return NotFound(new { message = "Employe non trouve dans les groupes samedi." });

        group.NewEmployeeSlot = dto.Slot; // 1 = Matin, 2 = Apres-midi
        await _context.SaveChangesAsync();

        return Ok(new
        {
            userId = dto.UserId,
            slot = dto.Slot,
            slotLabel = dto.Slot == 1 ? "8h00-12h00" : "12h00-16h00"
        });
    }
}