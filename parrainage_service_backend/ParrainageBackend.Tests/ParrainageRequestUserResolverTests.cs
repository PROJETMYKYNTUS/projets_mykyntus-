using Microsoft.AspNetCore.Http;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using ParrainageBackend.Services;

namespace ParrainageBackend.Tests;

public sealed class ParrainageRequestUserResolverTests
{
    private readonly ParrainageRequestUserResolver _resolver = new(NullLogger<ParrainageRequestUserResolver>.Instance);

    [Fact]
    public void Resolve_UsesHeaders_WhenPresent()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers[IParrainageRequestUserResolver.HeaderRole] = "RH";
        ctx.Request.Headers[IParrainageRequestUserResolver.HeaderUserId] = "rh-1";

        var user = _resolver.Resolve(ctx.Request);

        Assert.Equal("rh-1", user.UserId);
        Assert.Equal("RH", user.Role);
        Assert.False(user.IsDefault);
    }

    [Fact]
    public void Resolve_FallsBackToPilote_WhenHeadersMissing()
    {
        var ctx = new DefaultHttpContext();
        var user = _resolver.Resolve(ctx.Request);

        Assert.Equal("emp-1", user.UserId);
        Assert.Equal("PILOTE", user.Role);
        Assert.True(user.IsDefault);
    }

    [Fact]
    public void Resolve_QueryOverrides_WhenHeadersMissing()
    {
        var ctx = new DefaultHttpContext();
        var user = _resolver.Resolve(ctx.Request, queryRole: "ADMIN", queryUserId: "admin-1");

        Assert.Equal("admin-1", user.UserId);
        Assert.Equal("ADMIN", user.Role);
    }
}
