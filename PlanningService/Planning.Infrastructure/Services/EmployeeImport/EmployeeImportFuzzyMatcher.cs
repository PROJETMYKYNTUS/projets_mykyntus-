namespace Planning.Infrastructure.Services.EmployeeImport;

public static class EmployeeImportFuzzyMatcher
{
    public const double HighThreshold = 0.92;
    public const double MediumThreshold = 0.85;
    public const double SafeInclusionScore = 0.95;

    public static double Score(string? left, string? right)
    {
        var a = EmployeeImportColumnMatcher.Normalize(left ?? string.Empty);
        var b = EmployeeImportColumnMatcher.Normalize(right ?? string.Empty);
        return ScoreNormalized(a, b);
    }

    public static double ScoreOrgName(string fieldKey, string? left, string? right)
    {
        var a = EmployeeImportOrgNameNormalizer.StripLevelPrefix(left, fieldKey);
        var b = EmployeeImportOrgNameNormalizer.StripLevelPrefix(right, fieldKey);

        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return Score(left, right);

        if (a == b)
            return 1.0;

        if (IsSafeInclusion(a, b))
            return SafeInclusionScore;

        return ScoreNormalized(a, b);
    }

    public static string ConfidenceFromScore(double score) =>
        score >= HighThreshold ? "high" : score >= MediumThreshold ? "medium" : "low";

    public static FuzzyMatchResult? FindBestMatch(
        string? input,
        IReadOnlyList<string> candidates,
        double minimumScore = MediumThreshold) =>
        FindBestMatchInternal(input, candidates, minimumScore, scoreFunc: Score);

    public static FuzzyMatchResult? FindBestOrgMatch(
        string fieldKey,
        string? input,
        IReadOnlyList<string> candidates,
        double minimumScore = MediumThreshold) =>
        FindBestMatchInternal(input, candidates, minimumScore, (left, right) => ScoreOrgName(fieldKey, left, right));

    public static bool HasLikelyExistingOrgMatch(string fieldKey, string? input, IReadOnlyList<string> candidates) =>
        FindBestOrgMatch(fieldKey, input, candidates) is not null;

    private static FuzzyMatchResult? FindBestMatchInternal(
        string? input,
        IReadOnlyList<string> candidates,
        double minimumScore,
        Func<string?, string?, double> scoreFunc)
    {
        if (string.IsNullOrWhiteSpace(input) || candidates.Count == 0)
            return null;

        FuzzyMatchResult? best = null;
        FuzzyMatchResult? second = null;

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var score = scoreFunc(input, candidate);
            if (score < minimumScore)
                continue;

            var match = new FuzzyMatchResult(candidate, score, ConfidenceFromScore(score));
            if (best is null || score > best.Score)
            {
                second = best;
                best = match;
            }
            else if (second is null || score > second.Score)
            {
                second = match;
            }
        }

        if (best is not null && second is not null && Math.Abs(best.Score - second.Score) < 0.02)
            return null;

        return best;
    }

    private static double ScoreNormalized(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return 0;

        if (a == b)
            return 1.0;

        if (IsSafeInclusion(a, b))
            return SafeInclusionScore;

        if (a.Contains(b, StringComparison.Ordinal) || b.Contains(a, StringComparison.Ordinal))
            return 0.88;

        var distance = LevenshteinDistance(a, b);
        var maxLen = Math.Max(a.Length, b.Length);
        return maxLen == 0 ? 0 : 1.0 - (double)distance / maxLen;
    }

    private static bool IsSafeInclusion(string a, string b)
    {
        if (a == b)
            return true;

        var shorter = a.Length <= b.Length ? a : b;
        var longer = a.Length > b.Length ? a : b;

        if (EmployeeImportOrgNameNormalizer.ContainsAllTokens(longer, shorter))
            return shorter.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 1;

        return false;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[a.Length, b.Length];
    }
}

public sealed record FuzzyMatchResult(string Value, double Score, string Confidence);
