using Planning.Domain.Entities;

namespace Planning.Application.DTOs;

public class MediaAssetDto
{
    public int Id { get; set; }
    public string OwnerType { get; set; } = string.Empty;
    public int? OwnerId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int SortOrder { get; set; }
    public string Url { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class TicketCommentDto
{
    public int Id { get; set; }
    public string OwnerType { get; set; } = string.Empty;
    public int OwnerId { get; set; }
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorNom { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<MediaAssetDto> Media { get; set; } = new();
}

public class CreateTicketCommentDto
{
    public string Text { get; set; } = string.Empty;
    public List<int>? MediaIds { get; set; }
}

public static class MediaDtoMapper
{
    public static MediaAssetDto ToDto(MediaAsset m) => new()
    {
        Id = m.Id,
        OwnerType = m.OwnerType.ToString(),
        OwnerId = m.OwnerId,
        Kind = m.Kind.ToString(),
        FileName = m.FileName,
        ContentType = m.ContentType,
        SizeBytes = m.SizeBytes,
        SortOrder = m.SortOrder,
        Url = $"/api/media/{m.Id}",
        CreatedAt = m.CreatedAt
    };

    public static TicketCommentDto ToDto(TicketComment c, IEnumerable<MediaAsset>? media = null) => new()
    {
        Id = c.Id,
        OwnerType = c.OwnerType.ToString(),
        OwnerId = c.OwnerId,
        AuthorId = c.AuthorId,
        AuthorNom = c.AuthorNom,
        Text = c.Text,
        CreatedAt = c.CreatedAt,
        Media = media?.Select(ToDto).ToList() ?? new()
    };
}
