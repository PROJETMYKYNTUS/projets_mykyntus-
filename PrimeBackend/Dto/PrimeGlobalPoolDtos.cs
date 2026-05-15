namespace PrimeBackend.Dto;

public sealed class CelluleDraftGlobalPoolStateDto
{
    public Guid DraftId { get; init; }
    public string CelluleId { get; init; } = "";
    public string Period { get; init; } = "";
    public bool HasFile { get; init; }
    public string? FileName { get; init; }
    public DateTimeOffset? UploadedAt { get; init; }
    public DateTimeOffset? ManagerApprovedAt { get; init; }
    public DateTimeOffset? RhApprovedAt { get; init; }
    public DateTimeOffset? ComptaAckAt { get; init; }
    public bool PoolDistributionUnlocked { get; init; }
}

public sealed class GlobalPoolActingUserRequest
{
    public string UserId { get; set; } = "";
}
