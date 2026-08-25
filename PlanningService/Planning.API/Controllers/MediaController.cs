using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planning.Application.Abstractions;
using Planning.Application.DTOs;
using Planning.Domain.Entities;

namespace Planning.API.Controllers;

[ApiController]
[Route("api/media")]
[Authorize]
public class MediaController : ControllerBase
{
    private readonly IMediaAssetService _media;
    private readonly ITicketCommentService _comments;

    public MediaController(IMediaAssetService media, ITicketCommentService comments)
    {
        _media = media;
        _comments = comments;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

    private string CurrentUserName =>
        User.FindFirstValue(ClaimTypes.Name)
        ?? User.FindFirstValue("name")
        ?? CurrentUserId;

    private bool IsAdminLike =>
        User.IsInRole("Admin") || User.IsInRole("RH") || User.IsInRole("Manager")
        || User.IsInRole("RP") || User.IsInRole("Audit");

    [HttpPost]
    [RequestSizeLimit(160_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 160_000_000)]
    public async Task<ActionResult<MediaAssetDto>> Upload(IFormFile file, CancellationToken ct)
    {
        try
        {
            var result = await _media.UploadAsync(file, CurrentUserId, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var opened = await _media.OpenReadAsync(id, ct);
        if (opened is null) return NotFound();
        var (stream, contentType, fileName) = opened.Value;
        return File(stream, contentType, fileName);
    }

    [HttpGet("by-owner")]
    public async Task<ActionResult<IReadOnlyList<MediaAssetDto>>> ListByOwner(
        [FromQuery] string ownerType,
        [FromQuery] int ownerId,
        CancellationToken ct)
    {
        if (!Enum.TryParse<MediaOwnerType>(ownerType, true, out var type))
            return BadRequest(new { message = "ownerType invalide." });
        return Ok(await _media.ListByOwnerAsync(type, ownerId, ct));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var ok = await _media.DeleteAsync(id, CurrentUserId, IsAdminLike, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    [HttpGet("comments")]
    public async Task<ActionResult<IReadOnlyList<TicketCommentDto>>> ListComments(
        [FromQuery] string ownerType,
        [FromQuery] int ownerId,
        CancellationToken ct)
    {
        if (!Enum.TryParse<MediaOwnerType>(ownerType, true, out var type)
            || type is not (MediaOwnerType.Reclamation or MediaOwnerType.Proposition))
            return BadRequest(new { message = "ownerType invalide." });
        return Ok(await _comments.ListAsync(type, ownerId, ct));
    }

    [HttpPost("comments")]
    public async Task<ActionResult<TicketCommentDto>> AddComment(
        [FromQuery] string ownerType,
        [FromQuery] int ownerId,
        [FromBody] CreateTicketCommentDto dto,
        CancellationToken ct)
    {
        if (!Enum.TryParse<MediaOwnerType>(ownerType, true, out var type)
            || type is not (MediaOwnerType.Reclamation or MediaOwnerType.Proposition))
            return BadRequest(new { message = "ownerType invalide." });
        try
        {
            var result = await _comments.AddAsync(type, ownerId, dto, CurrentUserId, CurrentUserName, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
