using Auth.Domain.Interfaces;

namespace Auth.Infrastructure.Security;

/// <summary>SubjectId alignés sur init/demo/kyntus-users.manifest.json (documentationUserId).</summary>
internal sealed class KyntusSubjectIdCatalog : ISubjectIdResolver
{
    private static readonly IReadOnlyDictionary<string, Guid> ByEmail =
        new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase)
        {
            ["employee@kyntus.ma"] = Guid.Parse("11111111-1111-4111-8111-111111111103"),
            ["rh@kyntus.ma"] = Guid.Parse("11111111-1111-4111-8111-111111111104"),
            ["manager@kyntus.ma"] = Guid.Parse("11111111-1111-4111-8111-111111111105"),
            ["coach@kyntus.ma"] = Guid.Parse("11111111-1111-4111-8111-111111111106"),
            ["rp@kyntus.ma"] = Guid.Parse("11111111-1111-4111-8111-111111111107"),
            ["admin@kyntus.ma"] = Guid.Parse("11111111-1111-4111-8111-111111111108"),
            ["audit@kyntus.ma"] = Guid.Parse("11111111-1111-4111-8111-111111111109"),
            ["formation@kyntus.ma"] = Guid.Parse("11111111-1111-4111-8111-111111111110"),
            ["superviseur@kyntus.ma"] = Guid.Parse("11111111-1111-4111-8111-111111111111"),
            ["yasmine.elamrani@atlas-tech-demo.dev"] = Guid.Parse("11111111-1111-4111-8111-111111111101"),
            ["fatima.alaoui@atlas-tech-demo.dev"] = Guid.Parse("11111111-1111-4111-8111-111111111102"),
        };

    public Guid ResolveForEmail(string email)
    {
        var key = email.Trim().ToLowerInvariant();
        return ByEmail.TryGetValue(key, out var id) ? id : Guid.NewGuid();
    }
}
