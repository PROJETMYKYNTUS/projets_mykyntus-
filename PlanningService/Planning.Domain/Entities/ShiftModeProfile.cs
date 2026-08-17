using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Planning.Domain.Entities;

/// <summary>
/// Profil de shifts nommé pour une cellule (ex. Mode 1 = 3 shifts, Mode 2 = 4 shifts).
/// Null / mono-mode : la cellule conserve le comportement historique.
/// </summary>
public class ShiftModeProfile
{
    public int Id { get; set; }

    [ForeignKey(nameof(SubService))]
    public int SubServiceId { get; set; }
    public SubService SubService { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Présence min plateau propre à ce mode (50–100, ou 0 = désactivé).</summary>
    public int MinPresencePercent { get; set; } = 70;

    /// <summary>Extrêmes pause +3h/+5h propres à ce mode.</summary>
    public bool IsCriticalCell { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }

    public ICollection<SubServiceShiftConfig> ShiftConfigs { get; set; } = new List<SubServiceShiftConfig>();
}
