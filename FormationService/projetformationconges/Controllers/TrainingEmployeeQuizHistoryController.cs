using Formation.Application.DTOs;
using Formation.Infrastructure.Services;
using Kyntus.Identity.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Formation.API.Controllers;

[ApiController]
[Route("api/formations/employees")]
[Authorize]
public sealed class TrainingEmployeeQuizHistoryController(TrainingWorkflowService training) : ControllerBase
{
    /// <summary>
    /// Historique transversal des tentatives de l'employé (toutes séances).
    /// Distinct de GET .../sessions/{id}/quiz/my-attempts (détail scoped à une séance).
    /// </summary>
    [HttpGet("me/quiz-attempts")]
    public async Task<ActionResult<IReadOnlyList<MyQuizAttemptHistoryItemDto>>> ListMyHistory(CancellationToken ct)
    {
        try
        {
            var actor = User.GetSubjectId() ?? Guid.Empty;
            return Ok(await training.ListMyQuizAttemptsAcrossSessionsAsync(actor, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
