using Microsoft.AspNetCore.Mvc;
using Planning.Application.DTOs;
using Planning.Application.Abstractions;

namespace Planning.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubServicesController : ControllerBase
{
    private readonly ISubServiceService _subServiceService;

    public SubServicesController(ISubServiceService subServiceService)
    {
        _subServiceService = subServiceService;
    }

    [HttpGet]
    public async Task<ActionResult<List<SubServiceDto>>> GetAllSubServices()
    {
        var subServices = await _subServiceService.GetAllSubServicesAsync();
        return Ok(subServices);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SubServiceDto>> GetSubServiceById(int id)
    {
        var subService = await _subServiceService.GetSubServiceByIdAsync(id);
        if (subService == null)
            return NotFound(new { message = $"Le service avec l'ID {id} n'existe pas." });
        return Ok(subService);
    }

    [HttpGet("service/{serviceId}")]
    public async Task<ActionResult<List<SubServiceDto>>> GetSubServicesByServiceId(int serviceId)
    {
        var subServices = await _subServiceService.GetSubServicesByServiceIdAsync(serviceId);
        return Ok(subServices);
    }

    [HttpGet("{id}/details")]
    public async Task<ActionResult<SubServiceDetailDto>> GetSubServiceWithEmployees(int id)
    {
        var subService = await _subServiceService.GetSubServiceWithEmployeesAsync(id);
        if (subService == null)
            return NotFound(new { message = $"Le service avec l'ID {id} n'existe pas." });
        return Ok(subService);
    }

    [HttpGet("{id}/employees")]
    public async Task<ActionResult<List<UserSimpleDto>>> GetEmployeesBySubService(int id)
    {
        var employees = await _subServiceService.GetEmployeesBySubServiceAsync(id);
        return Ok(employees);
    }

    [HttpPost]
    public IActionResult CreateSubService([FromBody] CreateSubServiceDto dto) =>
        StatusCode(403, new { message = "Structure en lecture seule. Gérer dans Organisation RH (Prime)." });

    [HttpPut("{id}")]
    public IActionResult UpdateSubService(int id, [FromBody] UpdateSubServiceDto dto) =>
        StatusCode(403, new { message = "Structure en lecture seule. Gérer dans Organisation RH (Prime)." });

    [HttpDelete("{id}")]
    public IActionResult DeleteSubService(int id) =>
        StatusCode(403, new { message = "Structure en lecture seule. Gérer dans Organisation RH (Prime)." });

    [HttpGet("check-code/{code}")]
    public async Task<ActionResult<bool>> CheckCodeUnique(string code, [FromQuery] int? excludeId = null)
    {
        var isUnique = await _subServiceService.IsCodeUniqueAsync(code, excludeId);
        return Ok(new { isUnique });
    }
}
