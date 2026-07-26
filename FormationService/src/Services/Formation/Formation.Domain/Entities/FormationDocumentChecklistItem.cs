namespace Formation.Domain.Entities;

public class FormationDocumentChecklistItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }
    public Guid? InitialTrainingPathId { get; set; }
    public InitialTrainingPath? Path { get; set; }
    public Guid DefinitionId { get; set; }
    public FormationDocumentDefinition? Definition { get; set; }
    public bool IsReceived { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public string? ReceivedBy { get; set; }
    public string? Note { get; set; }
}
