namespace Planning.Application.DTOs.Planning;

public class PendingRequestItemDto
{
    public int Id { get; set; }
    /// <summary>Change | Exceptional</summary>
    public string Type { get; set; } = "";
    public string WeekCode { get; set; } = "";
    public int SubServiceId { get; set; }
    public string SubServiceName { get; set; } = "";
    public string Status { get; set; } = "";
    public string RequesterName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class PendingRequestsSummaryDto
{
    public int ChangePendingCount { get; set; }
    public int ExceptionalPendingCount { get; set; }
    public int TotalPendingCount => ChangePendingCount + ExceptionalPendingCount;
    public int ChangePendingPartner { get; set; }
    public int ChangePendingSupervisor { get; set; }
    public int ExceptionalPendingSupervisor { get; set; }
    public int ExceptionalPendingRh { get; set; }
    public List<PendingRequestItemDto> Items { get; set; } = new();
}
