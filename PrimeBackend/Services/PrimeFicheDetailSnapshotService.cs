using System.Text.Json;
using System.Text.Json.Serialization;
using PrimeBackend.Data;

namespace PrimeBackend.Services;

/// <summary>Persiste et fige la grille détaillée recalculée d'une fiche pilote (approche hybride).</summary>
public static class PrimeFicheDetailSnapshotService
{
    public const int SnapshotVersion = 1;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public sealed class DetailSnapshotV1
    {
        public int Version { get; set; } = SnapshotVersion;
        public string? PreviewSheetName { get; set; }
        public string? TemplateVersionRef { get; set; }
        public List<List<string>> Rows { get; set; } = [];
        public List<string> Errors { get; set; } = [];
        public string? ComputedAt { get; set; }
        public decimal? PrimeAmount { get; set; }
        public decimal? ChallengeAmount { get; set; }
        public decimal? TotalAmount { get; set; }
    }

    public static bool IsFrozen(EmployeePrimeServiceFicheEntity fiche) =>
        fiche.DetailGridFrozenAt.HasValue;

    public static string BuildTemplateVersionRef(string templateId, int templateFormatVersion) =>
        $"{templateId.Trim()}:v{templateFormatVersion}";

    public static string SerializeSnapshot(DetailSnapshotV1 snap) =>
        JsonSerializer.Serialize(snap, JsonOpts);

    public static DetailSnapshotV1? TryParseSnapshot(string? json)
    {
        var t = (json ?? string.Empty).Trim();
        if (t.Length == 0) return null;
        try
        {
            var snap = JsonSerializer.Deserialize<DetailSnapshotV1>(t, JsonOpts);
            if (snap is null || snap.Version != SnapshotVersion || snap.Rows.Count == 0) return null;
            return snap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Enregistre ou met à jour le snapshot (brouillon). Refuse si déjà figé.</summary>
    public static bool TryApplySnapshot(
        EmployeePrimeServiceFicheEntity fiche,
        DetailSnapshotV1 snap,
        bool freeze,
        DateTimeOffset now,
        out string? error)
    {
        error = null;
        if (IsFrozen(fiche))
        {
            error = "Le snapshot détaillé est figé — modification impossible.";
            return false;
        }

        if (snap.Rows.Count == 0)
        {
            error = "La grille détaillée est vide.";
            return false;
        }

        fiche.DetailGridJson = SerializeSnapshot(snap);
        fiche.DetailGridPreviewSheetName = (snap.PreviewSheetName ?? string.Empty).Trim();
        fiche.TemplateVersionRef = (snap.TemplateVersionRef ?? string.Empty).Trim();
        if (PrimeEmployeeFicheAmountService.IsNonNegative(snap.PrimeAmount) &&
            PrimeEmployeeFicheAmountService.IsNonNegative(snap.ChallengeAmount) &&
            PrimeEmployeeFicheAmountService.IsNonNegative(snap.TotalAmount))
        {
            fiche.PrimeAmount = snap.PrimeAmount;
            fiche.ChallengeAmount = snap.ChallengeAmount;
            fiche.TotalAmount = snap.TotalAmount;
        }

        fiche.UpdatedAt = now;
        if (freeze)
            fiche.DetailGridFrozenAt = now;
        return true;
    }

    /// <summary>Fige le snapshot existant (validation terminale).</summary>
    public static void FreezeExistingSnapshot(EmployeePrimeServiceFicheEntity fiche, DateTimeOffset now)
    {
        if (IsFrozen(fiche)) return;
        if (string.IsNullOrWhiteSpace(fiche.DetailGridJson)) return;
        fiche.DetailGridFrozenAt = now;
        fiche.UpdatedAt = now;
    }
}
