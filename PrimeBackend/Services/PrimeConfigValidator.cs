namespace PrimeBackend.Services;

using PrimeBackend.Dto;

public static class PrimeConfigValidator
{
    public static void ValidateOrThrow(PrimeConfigUpsertRequest req)
    {
        var kind = (req.Kind ?? "").Trim();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "KpiDefinition", "KpiThreshold", "KpiWeight", "ChallengeConfig", "PrimeCapSettings" };
        if (!allowed.Contains(kind)) throw new ArgumentException("kind invalide.");
        if (string.IsNullOrWhiteSpace(req.Sector) || string.IsNullOrWhiteSpace(req.GroupCode) || string.IsNullOrWhiteSpace(req.ActivityType))
            throw new ArgumentException("sector/groupCode/activityType requis.");

        if (kind is "KpiThreshold" or "ChallengeConfig")
        {
            if (req.Min is null || req.Max is null) throw new ArgumentException("min/max requis.");
            if (!req.InvertedLogic && req.Min >= req.Max) throw new ArgumentException("min doit être < max.");
            if (req.InvertedLogic && req.Min <= req.Max) throw new ArgumentException("en logique inversée, min doit être > max.");
        }
        if (kind == "KpiWeight" && req.Weight is null) throw new ArgumentException("weight requis.");
        if (kind == "PrimeCapSettings" && (req.PrimeCap is null || req.ChallengeCap is null))
            throw new ArgumentException("primeCap/challengeCap requis.");
    }
}
