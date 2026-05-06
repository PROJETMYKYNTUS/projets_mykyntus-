using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PlanningService.DTOs;
using PlanningService.Interfaces;

namespace PlanningService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContractController : ControllerBase
    {
        private readonly IContractService _contractService;

        public ContractController(IContractService contractService)
        {
            _contractService = contractService;
        }

        // ════════════════════════════════════════════════
        // CONTRATS
        // ════════════════════════════════════════════════

        // GET api/contract
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _contractService.GetAllContractsAsync();
            return Ok(result);
        }

        // GET api/contract/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _contractService.GetContractByIdAsync(id);
            return result == null ? NotFound() : Ok(result);
        }

        // GET api/contract/user/{userId}
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetByUser(int userId)
        {
            var result = await _contractService.GetContractsByUserIdAsync(userId);
            return Ok(result);
        }

        // POST api/contract
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateContractDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _contractService.CreateContractAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        // PUT api/contract/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateContractDto dto)
        {
            var result = await _contractService.UpdateContractAsync(id, dto);
            return result == null ? NotFound() : Ok(result);
        }

        // DELETE api/contract/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _contractService.DeleteContractAsync(id);
            return success ? NoContent() : NotFound();
        }

        // ════════════════════════════════════════════════
        // NOTIFICATIONS
        // ════════════════════════════════════════════════

        // GET api/contract/notifications
        [HttpGet("notifications")]
        public async Task<IActionResult> GetNotifications()
        {
            var result = await _contractService.GetUnreadNotificationsAsync();
            return Ok(result);
        }

        // GET api/contract/notifications/count
        [HttpGet("notifications/count")]
        public async Task<IActionResult> GetNotificationsCount()
        {
            var count = await _contractService.GetUnreadNotificationsCountAsync();
            return Ok(new { count });
        }

        // PUT api/contract/notifications/{id}/read
        [HttpPut("notifications/{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            await _contractService.MarkNotificationAsReadAsync(id);
            return NoContent();
        }

        // PUT api/contract/notifications/read-all
        [HttpPut("notifications/read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            await _contractService.MarkAllNotificationsAsReadAsync();
            return NoContent();
        }
    }
}