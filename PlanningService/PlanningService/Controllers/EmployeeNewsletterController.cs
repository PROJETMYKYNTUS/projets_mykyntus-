using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PlanningService.Interfaces;
using System.Security.Claims;

namespace PlanningService.Controllers
{
    [ApiController]
    [Route("api/my-newsletters")]
    [Authorize] // Employee ou Manager, peu importe le rôle
    public class EmployeeNewsletterController : ControllerBase
    {
        private readonly INewsletterService _newsletterService;

        public EmployeeNewsletterController(INewsletterService newsletterService)
        {
            _newsletterService = newsletterService;
        }

        private string CurrentUserId =>
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

        /// <summary>
        /// Récupère toutes les newsletters reçues pour l'utilisateur connecté.
        /// Le filtre par rôle est géré à la publication (via UserManager), pas ici.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMyNewsletters()
        {
            var newsletters = await _newsletterService.GetNewslettersForEmployeeAsync(CurrentUserId);
            return Ok(newsletters);
        }

        /// <summary>
        /// Marque une newsletter comme lue (appelé à l'ouverture dans le dashboard).
        /// </summary>
        [HttpPatch("{analyticsId:int}/read")]
        public async Task<IActionResult> MarkAsRead(int analyticsId)
        {
            var success = await _newsletterService.MarkAsReadAsync(analyticsId, CurrentUserId);
            if (!success)
                return BadRequest(new { message = "Impossible de marquer comme lu." });

            return Ok(new { message = "Newsletter marquée comme lue." });
        }
    }
}