using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ParrainageBackend.Data;
using ParrainageBackend.Models;
using ParrainageBackend.Services;
using Xunit;

namespace ParrainageBackend.Tests;

public sealed class ReferralRuleResolverTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ParrainageDbContext _db;
    private readonly ReferralRuleResolver _resolver;

    public ReferralRuleResolverTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ParrainageDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new ParrainageDbContext(options);
        _db.Database.EnsureCreated();
        _resolver = new ReferralRuleResolver(_db);
    }

    [Fact]
    public async Task ValidateUniqueActiveTarget_Throws_WhenDuplicateActiveTarget()
    {
        _db.ReferralRules.Add(new ReferralRuleEntity
        {
            Id = "rule-a",
            Name = "Dev",
            Type = ReferralRuleResolver.PositionRuleType,
            Target = "Développeur",
            Value = 600,
            MinDurationMonths = 6,
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _resolver.ValidateUniqueActiveTargetAsync("Développeur", ReferralRuleResolver.PositionRuleType, null));
    }

    [Fact]
    public async Task ValidateUniqueActiveTarget_AllowsSameTarget_WhenEditingSameRule()
    {
        _db.ReferralRules.Add(new ReferralRuleEntity
        {
            Id = "rule-a",
            Name = "Dev",
            Type = ReferralRuleResolver.PositionRuleType,
            Target = "Développeur",
            Value = 600,
            MinDurationMonths = 6,
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        await _resolver.ValidateUniqueActiveTargetAsync(
            "Développeur",
            ReferralRuleResolver.PositionRuleType,
            "rule-a");
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
