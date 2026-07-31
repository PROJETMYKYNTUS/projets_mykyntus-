using Formation.Domain.Enums;

namespace Formation.Domain.Entities;

public class TrainingResource
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LessonId { get; set; }
    public TrainingResourceType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>URL externe (YouTube, Vimeo, lien PDF) ou chemin relatif servi.</summary>
    public string? Url { get; set; }
    /// <summary>Chemin disque local pour fichier uploadé.</summary>
    public string? StoragePath { get; set; }
    public string? ContentType { get; set; }
    public string? FileName { get; set; }
    /// <summary>Contenu texte riche pour Type=Text.</summary>
    public string? TextContent { get; set; }
    public int SortOrder { get; set; }
    public int? DurationMinutes { get; set; }

    public TrainingLesson? Lesson { get; set; }
}
