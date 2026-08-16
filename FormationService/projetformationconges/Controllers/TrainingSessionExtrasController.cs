using Formation.Application.DTOs;
using Formation.Infrastructure.Services;
using Kyntus.Identity.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Formation.API.Controllers;

[ApiController]
[Route("api/formations/sessions/{sessionId:guid}")]
[Authorize]
public sealed class TrainingSessionExtrasController(
    TrainingWorkflowService training,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("report")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<TrainingSessionReportDto>> UploadReport(
        Guid sessionId,
        IFormFile file,
        CancellationToken ct)
    {
        try
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { error = "Fichier manquant." });
            var actor = User.GetSubjectId() ?? Guid.Empty;
            if (actor == Guid.Empty)
                return BadRequest(new { error = "Identifiant utilisateur manquant dans le jeton." });

            await using var stream = file.OpenReadStream();
            var dto = await training.UploadSessionReportAsync(
                sessionId,
                actor,
                file.FileName,
                file.ContentType ?? "application/octet-stream",
                stream,
                configuration["Formation:Reports:RootPath"],
                ct);
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("report")]
    public async Task<IActionResult> DownloadReport(Guid sessionId, CancellationToken ct)
    {
        var result = await training.GetSessionReportAsync(sessionId, ct);
        if (result is null) return NotFound();
        var (report, bytes) = result.Value;
        return File(bytes, report.ContentType, report.FileName);
    }

    [HttpGet("quiz")]
    public async Task<ActionResult<TrainingQuizDto>> GetQuiz(Guid sessionId, CancellationToken ct)
    {
        var actor = User.GetSubjectId() ?? Guid.Empty;
        if (actor == Guid.Empty) return BadRequest(new { error = "Identifiant utilisateur manquant dans le jeton." });
        return Ok(await training.GetQuizForAnimatorAsync(sessionId, actor, ct));
    }

    [HttpPut("quiz")]
    public async Task<ActionResult<TrainingQuizDto>> UpsertQuiz(
        Guid sessionId,
        [FromBody] UpsertTrainingQuizRequest body,
        CancellationToken ct)
    {
        try
        {
            body.AnimatorUserId = User.GetSubjectId() ?? Guid.Empty;
            return Ok(await training.UpsertQuizAsync(sessionId, body, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("quiz/publish")]
    public async Task<ActionResult<TrainingQuizDto>> PublishQuiz(
        Guid sessionId,
        [FromBody] ValidateTrainingQuizRequest body,
        CancellationToken ct)
    {
        try
        {
            var actor = User.GetSubjectId() ?? Guid.Empty;
            return Ok(await training.PublishQuizAsync(sessionId, actor, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("quiz/for-employee")]
    public async Task<ActionResult<TrainingQuizForEmployeeDto>> QuizForEmployee(
        Guid sessionId,
        CancellationToken ct)
    {
        try
        {
            var employeeId = User.GetSubjectId() ?? Guid.Empty;
            return Ok(await training.GetPublishedQuizForEmployeeAsync(sessionId, employeeId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("quiz/attempts")]
    public async Task<ActionResult<TrainingQuizAttemptDto>> SubmitAttempt(
        Guid sessionId,
        [FromBody] SubmitTrainingQuizAttemptRequest body,
        CancellationToken ct)
    {
        try
        {
            body.EmployeeId = User.GetSubjectId() ?? Guid.Empty;
            return Ok(await training.SubmitQuizAttemptAsync(sessionId, body, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("quiz/attempts")]
    public async Task<ActionResult<IReadOnlyList<TrainingQuizAttemptDto>>> ListAttempts(
        Guid sessionId,
        CancellationToken ct)
    {
        try
        {
            var actor = User.GetSubjectId() ?? Guid.Empty;
            return Ok(await training.ListQuizAttemptsAsync(sessionId, actor, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Tentatives de l'employé pour cette séance uniquement (détail réponses).
    /// Historique multi-séances : GET api/formations/employees/me/quiz-attempts.
    /// </summary>
    [HttpGet("quiz/my-attempts")]
    public async Task<ActionResult<IReadOnlyList<TrainingQuizAttemptDto>>> ListMyAttempts(
        Guid sessionId,
        CancellationToken ct)
    {
        try
        {
            var actor = User.GetSubjectId() ?? Guid.Empty;
            return Ok(await training.ListMyQuizAttemptsAsync(sessionId, actor, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("quiz/questions/{questionId:guid}/image")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<TrainingQuizQuestionDto>> UploadQuestionImage(
        Guid sessionId,
        Guid questionId,
        IFormFile file,
        CancellationToken ct)
    {
        try
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { error = "Fichier manquant." });
            var actor = User.GetSubjectId() ?? Guid.Empty;
            if (actor == Guid.Empty)
                return BadRequest(new { error = "Identifiant utilisateur manquant dans le jeton." });

            await using var stream = file.OpenReadStream();
            var dto = await training.UploadQuizQuestionImageAsync(
                sessionId,
                questionId,
                actor,
                file.FileName,
                file.ContentType ?? "application/octet-stream",
                stream,
                configuration["Formation:QuizImages:RootPath"],
                ct);
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("quiz/questions/{questionId:guid}/image")]
    public async Task<IActionResult> DownloadQuestionImage(
        Guid sessionId,
        Guid questionId,
        CancellationToken ct)
    {
        var result = await training.GetQuizQuestionImageAsync(sessionId, questionId, ct);
        if (result is null) return NotFound();
        var (question, bytes) = result.Value;
        var ext = Path.GetExtension(question.ImageStoragePath ?? "").ToLowerInvariant();
        var contentType = ext switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".ogg" => "video/ogg",
            ".mov" => "video/quicktime",
            _ => "image/jpeg",
        };
        return File(bytes, contentType);
    }

    [HttpPost("quiz/attempts/{attemptId:guid}/grade")]
    public async Task<ActionResult<TrainingQuizAttemptDto>> GradeAttempt(
        Guid sessionId,
        Guid attemptId,
        [FromBody] GradeTrainingQuizAttemptRequest body,
        CancellationToken ct)
    {
        try
        {
            body.AnimatorUserId = User.GetSubjectId() ?? Guid.Empty;
            return Ok(await training.GradeQuizAttemptAsync(sessionId, attemptId, body, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("quiz/attempts/{attemptId:guid}/free-text-grade")]
    public async Task<ActionResult<TrainingQuizAttemptDto>> GradeFreeText(
        Guid sessionId,
        Guid attemptId,
        [FromBody] GradeFreeTextAnswerRequest body,
        CancellationToken ct)
    {
        try
        {
            body.AnimatorUserId = User.GetSubjectId() ?? Guid.Empty;
            return Ok(await training.GradeFreeTextAnswerAsync(sessionId, attemptId, body, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("quiz/validate")]
    public async Task<ActionResult<TrainingQuizDto>> ValidateQuiz(
        Guid sessionId,
        [FromBody] ValidateTrainingQuizRequest body,
        CancellationToken ct)
    {
        try
        {
            body.ActorUserId = User.GetSubjectId() ?? Guid.Empty;
            return Ok(await training.ValidateQuizAsync(sessionId, body, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("quiz/reject")]
    public async Task<ActionResult<TrainingQuizDto>> RejectQuiz(
        Guid sessionId,
        [FromBody] RejectTrainingQuizRequest body,
        CancellationToken ct)
    {
        try
        {
            body.ActorUserId = User.GetSubjectId() ?? Guid.Empty;
            return Ok(await training.RejectQuizAsync(sessionId, body, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
