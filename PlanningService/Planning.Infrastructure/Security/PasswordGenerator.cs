using System.Security.Cryptography;
using System.Text;

namespace Planning.Infrastructure.Security;

public static class PasswordPolicy
{
    public const int MinLength = 12;
    public const int MaxLength = 64;
    public const int GeneratedLength = 16;

    private static readonly HashSet<string> Blacklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "Azerty@123",
        "password",
        "password123",
        "azerty",
        "azerty123",
        "12345678",
        "123456789012",
        "qwerty123456",
        "motdepasse",
        "changeme",
    };

    public static bool TryValidate(string? password, out string error)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            error = "Le mot de passe est requis.";
            return false;
        }

        var trimmed = password.Trim();
        if (trimmed.Length < MinLength || trimmed.Length > MaxLength)
        {
            error = $"Le mot de passe doit contenir entre {MinLength} et {MaxLength} caractères.";
            return false;
        }

        if (Blacklist.Contains(trimmed))
        {
            error = "Ce mot de passe n'est pas autorisé.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

public static class PasswordGenerator
{
    // Avoid ambiguous chars 0 O I l 1
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%&*-_";

    public static string Generate(int length = PasswordPolicy.GeneratedLength)
    {
        if (length < PasswordPolicy.MinLength)
            length = PasswordPolicy.MinLength;

        var bytes = RandomNumberGenerator.GetBytes(length);
        var sb = new StringBuilder(length);
        for (var i = 0; i < length; i++)
            sb.Append(Alphabet[bytes[i] % Alphabet.Length]);

        return sb.ToString();
    }

    /// <summary>
    /// Empty/whitespace → generate. Non-empty → validate or throw.
    /// </summary>
    public static string ResolveOrGenerate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return Generate();

        var trimmed = password.Trim();
        if (!PasswordPolicy.TryValidate(trimmed, out var error))
            throw new InvalidOperationException(error);

        return trimmed;
    }

    public static bool TryResolveOrGenerate(string? password, out string resolved, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(password))
        {
            resolved = Generate();
            return true;
        }

        var trimmed = password.Trim();
        if (!PasswordPolicy.TryValidate(trimmed, out var validationError))
        {
            resolved = string.Empty;
            error = validationError;
            return false;
        }

        resolved = trimmed;
        return true;
    }
}
