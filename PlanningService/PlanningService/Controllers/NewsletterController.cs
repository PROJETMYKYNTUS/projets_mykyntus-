using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanningService.DTOs.Newsletter;
using PlanningService.Interfaces;
using System.Security.Claims;

namespace PlanningService.Controllers
{
    [ApiController]
    [Route("api/newsletter")]
    [Authorize]
    public class NewsletterController : ControllerBase
    {
        private readonly INewsletterService _newsletterService;

        public NewsletterController(INewsletterService newsletterService)
        {
            _newsletterService = newsletterService;
        }

        private string CurrentUserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

        // ────────────────────────────────────────────────────────────────────────
        // NEWSLETTERS (contenu)
        // ────────────────────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> GetAll()
            => Ok(await _newsletterService.GetAllNewslettersAsync());

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _newsletterService.GetNewsletterByIdAsync(id);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateNewsletterDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _newsletterService.CreateNewsletterAsync(dto, CurrentUserId);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateNewsletterDto dto)
        {
            var result = await _newsletterService.UpdateNewsletterAsync(id, dto);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _newsletterService.DeleteNewsletterAsync(id);
            return success ? NoContent() : NotFound();
        }

        // ────────────────────────────────────────────────────────────────────────
        // CAMPAIGNS
        // ────────────────────────────────────────────────────────────────────────

        [HttpGet("campaigns")]
        public async Task<IActionResult> GetCampaigns()
            => Ok(await _newsletterService.GetAllCampaignsAsync());

        [HttpGet("campaigns/{id:int}")]
        public async Task<IActionResult> GetCampaignById(int id)
        {
            var result = await _newsletterService.GetCampaignByIdAsync(id);
            return result is null ? NotFound() : Ok(result);
        }

        [HttpPost("campaigns")]
        public async Task<IActionResult> CreateCampaign([FromBody] CreateCampaignDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _newsletterService.CreateCampaignAsync(dto, CurrentUserId);
            return CreatedAtAction(nameof(GetCampaignById), new { id = result.Id }, result);
        }

        /// <summary>
        /// Publie la newsletter → apparaît immédiatement dans les dashboards employees
        /// + notification SignalR en temps réel.
        /// </summary>
        [HttpPost("campaigns/{id:int}/publish")]
        public async Task<IActionResult> Publish(int id)
        {
            var success = await _newsletterService.PublishCampaignAsync(id);
            if (!success)
                return BadRequest(new { message = "Impossible de publier la campagne (déjà envoyée ou introuvable)." });

            return Ok(new { message = "Newsletter publiée et envoyée aux dashboards des employés." });
        }

        [HttpPost("campaigns/{id:int}/schedule")]
        public async Task<IActionResult> Schedule(int id, [FromBody] DateTime scheduledAt)
        {
            var success = await _newsletterService.ScheduleCampaignAsync(id, scheduledAt);
            if (!success) return NotFound();
            return Ok(new { message = $"Campagne planifiée pour le {scheduledAt:f}." });
        }

        [HttpPost("campaigns/{id:int}/cancel")]
        public async Task<IActionResult> Cancel(int id)
        {
            var success = await _newsletterService.CancelCampaignAsync(id);
            if (!success)
                return BadRequest(new { message = "Impossible d'annuler (campagne déjà envoyée ou introuvable)." });
            return Ok(new { message = "Campagne annulée." });
        }

        // ────────────────────────────────────────────────────────────────────────
        // ANALYTICS
        // ────────────────────────────────────────────────────────────────────────

        /// <summary>Taux de lecture de la newsletter dans les dashboards</summary>
        [HttpGet("campaigns/{id:int}/analytics")]
        public async Task<IActionResult> GetAnalytics(int id)
        {
            var analytics = await _newsletterService.GetCampaignAnalyticsAsync(id);
            return analytics is null ? NotFound() : Ok(analytics);
        }

        // ────────────────────────────────────────────────────────────────────────
        // SUBSCRIBERS
        // ────────────────────────────────────────────────────────────────────────

      
    }
}