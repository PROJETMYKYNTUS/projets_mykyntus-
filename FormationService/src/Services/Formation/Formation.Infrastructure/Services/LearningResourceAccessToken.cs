using System.Security.Cryptography;
using System.Text;

namespace Formation.Infrastructure.Services;

/// <summary>Jeton HMAC court pour servir un média sans re-télécharger via blob auth.</summary>
public static class LearningResourceAccessToken
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(2);

    public static (string Token, DateTime ExpiresAt) Issue(Guid resourceId, string signingKey, TimeSpan? ttl = null)
    {
        var expires = DateTime.UtcNow.Add(ttl ?? DefaultTtl);
        var payload = $"{resourceId:N}.{expires.Ticks}";
        var sig = Sign(payload, signingKey);
        return ($"{payload}.{sig}", expires);
    }

    public static bool TryValidate(Guid resourceId, string? token, string signingKey)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(signingKey))
            return false;
        var parts = token.Split('.', 3);
        if (parts.Length != 3) return false;
        if (!Guid.TryParseExact(parts[0], "N", out var id) || id != resourceId)
            return false;
        if (!long.TryParse(parts[1], out var ticks))
            return false;
        var expires = new DateTime(ticks, DateTimeKind.Utc);
        if (expires < DateTime.UtcNow) return false;
        var expected = Sign($"{parts[0]}.{parts[1]}", signingKey);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(parts[2]));
    }

    private static string Sign(string payload, string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var data = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(keyBytes, data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
