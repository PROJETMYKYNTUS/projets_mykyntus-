using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Parrainage.Application.Abstractions;
using Parrainage.Infrastructure.Services;
using Xunit;

namespace ParrainageBackend.Tests;

public sealed class ParrainageRequestUserResolverTests
{
    private readonly ParrainageRequestUserResolver _resolver = new(
        new HttpContextAccessor(),
        new ServiceCollection().BuildServiceProvider(),
        new TestHostEnvironment { EnvironmentName = Environments.Development },
        NullLogger<ParrainageRequestUserResolver>.Instance);

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

    [Fact]
    public void ResolveEffectiveRole_PrefersJwtRh_OverPortalPilote()
    {
        Assert.Equal("RH", ParrainageRequestUserResolver.ResolveEffectiveRole("RH", "PILOTE"));
        Assert.Equal("PILOTE", ParrainageRequestUserResolver.ResolveEffectiveRole("PILOTE", "PILOTE"));
        Assert.Equal("MANAGER", ParrainageRequestUserResolver.ResolveEffectiveRole("PILOTE", "MANAGER"));
    }

    [Fact]
    public void ResolveEffectiveRole_PrefersHeaderRh_WhenJwtPortalIsPilote()
    {
        Assert.Equal("RH", ParrainageRequestUserResolver.ResolveEffectiveRole("PILOTE", "RH"));
    }

    [Fact]
    public void Resolve_UsesHeaders_InProduction_WhenJwtAbsent()
    {
        var resolver = new ParrainageRequestUserResolver(
            new HttpContextAccessor(),
            new ServiceCollection().BuildServiceProvider(),
            new TestHostEnvironment { EnvironmentName = Environments.Production },
            NullLogger<ParrainageRequestUserResolver>.Instance);

        var ctx = new DefaultHttpContext();
        ctx.Request.Headers[IParrainageRequestUserResolver.HeaderRole] = "RH";
        ctx.Request.Headers[IParrainageRequestUserResolver.HeaderUserId] = "rh-1";

        var user = resolver.Resolve(ctx.Request);

        Assert.Equal("rh-1", user.UserId);
        Assert.Equal("RH", user.Role);
        Assert.False(user.IsDefault);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
