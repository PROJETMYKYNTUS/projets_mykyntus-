namespace Auth.Application.Security;

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
