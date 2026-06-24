namespace Parrainage.Infrastructure.Services;

/// <summary>
/// Hiérarchie portail chargée depuis <c>parrainage_portal_user</c> (ParentId synchronisé depuis Prime).
/// </summary>
public static class OrgHierarchy
{
    public static bool IsReferrerUnderManager(
        string viewerId,
        string referrerId,
        IReadOnlyDictionary<string, string?> parentByUserId)
    {
        if (string.IsNullOrWhiteSpace(viewerId) || string.IsNullOrWhiteSpace(referrerId))
            return false;
        if (viewerId == referrerId) return true;

        var guard = new HashSet<string>(StringComparer.Ordinal);
        var cur = referrerId;
        while (parentByUserId.TryGetValue(cur, out var parentId) && !string.IsNullOrWhiteSpace(parentId))
        {
            if (parentId == viewerId) return true;
            if (!guard.Add(cur)) break;
            cur = parentId;
        }

        return false;
    }
}
