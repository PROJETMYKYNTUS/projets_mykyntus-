using DocumentationBackend.Data;

namespace DocumentationBackend.Api;

/// <summary>Analyse des rôles transmis par en-tête (gateway / SSO), en slug français ou nom d'énumération.</summary>
public static class AppRoleHeaderParser
{
    public static bool TryParse(string? raw, out AppRole role)
    {
        role = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var s = raw.Trim().ToLowerInvariant();
        switch (s)
        {
            case "pilote":
                role = AppRole.Pilote;
                return true;
            case "coach":
                role = AppRole.Coach;
                return true;
            case "manager":
                role = AppRole.Manager;
                return true;
            case "rp":
                role = AppRole.Rp;
                return true;
            case "rh":
                role = AppRole.Rh;
                return true;
            case "admin":
                role = AppRole.Admin;
                return true;
            case "audit":
                role = AppRole.Audit;
                return true;
            default:
                return Enum.TryParse(raw.Trim(), ignoreCase: true, out role)
                    && Enum.IsDefined(typeof(AppRole), role);
        }
    }
}
