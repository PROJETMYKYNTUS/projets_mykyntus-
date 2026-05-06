using System.Text.RegularExpressions;
using DocumentationBackend.Configuration;
using Microsoft.Extensions.Options;

namespace DocumentationBackend.Services;

public sealed class RibValidationService(IOptions<RibValidationOptions> options) : IRibValidationService
{
    private readonly RibValidationOptions _opt = options.Value;

    /// <inheritdoc />
    public string DigitsOnlyValidationPattern =>
        $"^[0-9]{{{Math.Max(1, _opt.MinDigits)},{Math.Max(_opt.MinDigits, _opt.MaxDigits)}}}$";

    /// <inheritdoc />
    public bool IsValidRibDigits(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        var digits = DigitsOnly(raw);
        return digits.Length >= _opt.MinDigits && digits.Length <= _opt.MaxDigits;
    }

    internal static string DigitsOnly(string? raw) =>
        string.IsNullOrEmpty(raw) ? string.Empty : Regex.Replace(raw, @"\D", string.Empty);
}
