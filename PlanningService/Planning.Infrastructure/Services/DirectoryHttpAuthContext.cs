namespace Planning.Infrastructure.Services;

/// <summary>
/// Propagation du Bearer token hors HttpContext (jobs d'import asynchrones / Parallel.ForEachAsync).
/// </summary>
public static class DirectoryHttpAuthContext
{
    public static readonly AsyncLocal<string?> AuthorizationHeader = new();
}
