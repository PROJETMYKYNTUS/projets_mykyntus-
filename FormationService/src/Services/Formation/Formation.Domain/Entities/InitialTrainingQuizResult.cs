namespace Formation.Domain.Entities;

public class InitialTrainingQuizResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InitialTrainingPathId { get; set; }
    public InitialTrainingPath? Path { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public bool Passed { get; set; }
    public string? RecordedBy { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
