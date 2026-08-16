using Formation.Application.DTOs;
using Formation.Infrastructure.Services;
using Kyntus.Identity.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Formation.API.Controllers;

[ApiController]
[Route("api/formations/quiz-templates")]
[Authorize(Policy = "CanPlanContinue")]
public sealed class TrainingQuizLibraryController(
    TrainingQuizLibraryService library) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TrainingQuizTemplateListItemDto>>> List(
        [FromQuery] bool includeArchived = false,
        CancellationToken ct = default) =>
        Ok(await library.ListTemplatesAsync(includeArchived, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TrainingQuizTemplateDto>> Get(Guid id, CancellationToken ct)
    {
        var item = await library.GetTemplateAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<TrainingQuizTemplateDto>> Create(
        [FromBody] UpsertTrainingQuizTemplateRequest body,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(body.CreatedByUserId))
                body.CreatedByUserId = User.GetSubjectId()?.ToString() ?? "";
            return Ok(await library.CreateTemplateAsync(body, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TrainingQuizTemplateDto>> Update(
        Guid id,
        [FromBody] UpsertTrainingQuizTemplateRequest body,
        CancellationToken ct)
    {
        try { return Ok(await library.UpdateTemplateAsync(id, body, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<TrainingQuizTemplateDto>> Publish(Guid id, CancellationToken ct)
    {
        try { return Ok(await library.PublishTemplateAsync(id, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<TrainingQuizTemplateDto>> Archive(Guid id, CancellationToken ct)
    {
        try { return Ok(await library.ArchiveTemplateAsync(id, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("{id:guid}/duplicate")]
    public async Task<ActionResult<TrainingQuizTemplateDto>> Duplicate(Guid id, CancellationToken ct)
    {
        try
        {
            var actor = User.GetSubjectId()?.ToString() ?? "";
            return Ok(await library.DuplicateTemplateAsync(id, actor, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/instantiate")]
    public async Task<ActionResult<TrainingQuizDto>> Instantiate(
        Guid id,
        [FromBody] InstantiateQuizTemplateRequest body,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(body.ActorUserId))
                body.ActorUserId = User.GetSubjectId()?.ToString() ?? "";
            return Ok(await library.InstantiateToSessionAsync(body.SessionId, id, body.ActorUserId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("promote")]
    public async Task<ActionResult<TrainingQuizTemplateDto>> Promote(
        [FromBody] PromoteSessionQuizRequest body,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(body.ActorUserId))
                body.ActorUserId = User.GetSubjectId()?.ToString() ?? "";
            return Ok(await library.PromoteSessionQuizAsync(
                body.SessionId,
                body.ActorUserId,
                body.Title,
                body.Description,
                body.Category,
                body.CatalogItemId,
                ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{templateId:guid}/questions/{questionId:guid}/media")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<TrainingQuizTemplateQuestionDto>> UploadQuestionMedia(
        Guid templateId,
        Guid questionId,
        IFormFile file,
        CancellationToken ct)
    {
        try
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { error = "Fichier manquant." });
            await using var stream = file.OpenReadStream();
            return Ok(await library.UploadTemplateQuestionMediaAsync(
                templateId,
                questionId,
                file.FileName,
                file.ContentType ?? "application/octet-stream",
                stream,
                ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{templateId:guid}/questions/{questionId:guid}/media")]
    public async Task<IActionResult> DownloadQuestionMedia(
        Guid templateId,
        Guid questionId,
        CancellationToken ct)
    {
        var result = await library.GetTemplateQuestionMediaAsync(templateId, questionId, ct);
        if (result is null) return NotFound();
        var (_, bytes, contentType) = result.Value;
        return File(bytes, contentType);
    }
}
