namespace ParrainageBackend.Services;

/// <summary>
/// Port of parrainage-angular/src/app/parrainage/lib/org-hierarchy.ts. Used to scope
/// notifications for MANAGER/COACH viewers (same logic as isReferrerUnderManager).
/// </summary>
public static class OrgHierarchy
{
    private sealed record OrgNode(string Id, string? ParentId);

    private static readonly List<OrgNode> Nodes = new()
    {
        new OrgNode("rp-1", null),
        new OrgNode("mgr-1", "rp-1"),
        new OrgNode("coach-1", "mgr-1"),
        new OrgNode("emp-1", "coach-1"),
        new OrgNode("emp-2", "coach-1"),
        new OrgNode("emp-3", "coach-1"),
        new OrgNode("emp-4", "coach-1"),
        new OrgNode("emp-5", "coach-1"),
    };

    public static bool IsReferrerUnderManager(string viewerId, string referrerId)
    {
        if (viewerId == referrerId) return true;
        var cur = Nodes.FirstOrDefault(n => n.Id == referrerId);
        var guard = new HashSet<string>();
        while (cur?.ParentId != null)
        {
            if (cur.ParentId == viewerId) return true;
            if (guard.Contains(cur.Id)) break;
            guard.Add(cur.Id);
            var parentId = cur.ParentId;
            cur = Nodes.FirstOrDefault(n => n.Id == parentId);
        }

        return false;
    }
}
