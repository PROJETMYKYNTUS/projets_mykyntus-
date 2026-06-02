using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Microsoft.EntityFrameworkCore;
using ParrainageBackend.Data;
using ParrainageBackend.Dto;
using ParrainageBackend.Models;
using ParrainageBackend.Services;

namespace ParrainageBackend.Tests;

public sealed class ReferralWorkflowServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ParrainageDbContext _db;
    private readonly ReferralWorkflowService _workflow;

    public ReferralWorkflowServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ParrainageDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new ParrainageDbContext(options);
        _db.Database.EnsureCreated();
        _db.SystemConfigs.Add(new SystemConfigEntity
        {
            Id = 1,
            DefaultBonusAmount = 1500,
            MinDurationMonths = 6,
            ReferralLimitPerEmployee = 2,
            ReferralProgramRules = DefaultSystemConfig.ProgramRules(),
            AdminWorkflow = DefaultSystemConfig.Workflow(),
        });
        _db.SaveChanges();
        _workflow = new ReferralWorkflowService(_db);
    }

    [Fact]
    public async Task SubmitReferral_Throws_WhenLimitReached()
    {
        await SeedReferralAsync("ref-a", "emp-1");
        await SeedReferralAsync("ref-b", "emp-1");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _workflow.SubmitReferralAsync(new CreateReferralRequest
            {
                ReferrerId = "emp-1",
                ReferrerName = "Jean",
                CandidateName = "Test",
                CandidateEmail = "t@example.com",
                CandidatePhone = "+33",
                Position = "Dev",
            }, CancellationToken.None));
    }

    [Fact]
    public async Task SubmitReferral_PersistsNotes_OnEntityAndHistory()
    {
        var created = await _workflow.SubmitReferralAsync(new CreateReferralRequest
        {
            ReferrerId = "emp-notes",
            ReferrerName = "Marie",
            CandidateName = "Paul",
            CandidateEmail = "paul@test.com",
            CandidatePhone = "+33",
            Position = "Dev",
            Notes = "Excellent profil full-stack",
        }, CancellationToken.None);

        Assert.Equal("Excellent profil full-stack", created.Notes);
        var hist = await _db.ReferralHistory.FirstAsync(h => h.ReferralId == created.Id);
        Assert.Equal("Excellent profil full-stack", hist.Comment);
    }

    [Fact]
    public async Task ProcessReferral_SetsProcessed_FromSubmitted()
    {
        var created = await _workflow.SubmitReferralAsync(new CreateReferralRequest
        {
            ReferrerId = "emp-7",
            ReferrerName = "Jean",
            CandidateName = "Bob",
            CandidateEmail = "bob@test.com",
            CandidatePhone = "+33",
            Position = "Dev",
        }, CancellationToken.None);

        var updated = await _workflow.ProcessReferralAsync(created.Id, new ProcessReferralRequest
        {
            Comment = "Entretien OK",
        }, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("PROCESSED", updated!.Status);
        Assert.Contains(await _db.ReferralHistory.ToListAsync(), h => h.Action == "PROCESSED");
    }

    [Fact]
    public async Task ApproveReferral_Throws_WhenNotProcessed()
    {
        var created = await _workflow.SubmitReferralAsync(new CreateReferralRequest
        {
            ReferrerId = "emp-8",
            ReferrerName = "Jean",
            CandidateName = "Carla",
            CandidateEmail = "carla@test.com",
            CandidatePhone = "+33",
            Position = "Dev",
        }, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _workflow.ApproveReferralAsync(created.Id, new ApproveReferralRequest
            {
                CandidateStartDate = new DateOnly(2026, 3, 1),
                RewardAmount = 1500m,
            }, CancellationToken.None));
    }

    [Fact]
    public async Task ApproveReferral_SetsEligibleDate_FromStartDateAndMinDuration()
    {
        var created = await _workflow.SubmitReferralAsync(new CreateReferralRequest
        {
            ReferrerId = "emp-3",
            ReferrerName = "Jean",
            CandidateName = "Alice",
            CandidateEmail = "alice@test.com",
            CandidatePhone = "+33",
            Position = "Dev",
        }, CancellationToken.None);

        await _workflow.ProcessReferralAsync(created.Id, new ProcessReferralRequest(), CancellationToken.None);

        var start = new DateOnly(2026, 1, 15);
        var updated = await _workflow.ApproveReferralAsync(created.Id, new ApproveReferralRequest
        {
            CandidateStartDate = start,
            RewardAmount = 1500m,
        }, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("APPROVED", updated!.Status);
        Assert.Equal(start, updated.CandidateStartDate);
        Assert.Equal(1500m, updated.RewardAmount);
        Assert.Equal(
            ReferralEligibilityCalculator.ComputeEligibleForPayment(start, 6),
            updated.EligibleForPaymentAt);
        Assert.Equal(ReferralPaymentStatus.NotEligible, updated.PaymentStatus);
    }

    [Fact]
    public async Task MarkReferralPaid_Throws_WhenNotReady()
    {
        var entity = await SeedReferralAsync("ref-not-ready", "emp-4", "APPROVED", ReferralPaymentStatus.NotEligible);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _workflow.MarkReferralPaidAsync(entity.Id, new MarkReferralPaymentRequest { Paid = true }, CancellationToken.None));
    }

    [Fact]
    public async Task MarkReferralPaid_SetsRewarded_WhenReady()
    {
        var entity = await SeedReferralAsync("ref-ready", "emp-5", "APPROVED", ReferralPaymentStatus.Ready, 1200m);
        var updated = await _workflow.MarkReferralPaidAsync(
            entity.Id,
            new MarkReferralPaymentRequest { Paid = true, Reference = "VIR-001" },
            CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal("REWARDED", updated!.Status);
        Assert.Equal(ReferralPaymentStatus.Paid, updated.PaymentStatus);
        Assert.Equal("VIR-001", updated.PaymentReference);
    }

    [Fact]
    public async Task EligibilityService_MarksReady_AndNotifiesOnce()
    {
        var entity = await SeedReferralAsync("ref-elig", "emp-6", "APPROVED", ReferralPaymentStatus.NotEligible, 800m);
        entity.EligibleForPaymentAt = DateTimeOffset.UtcNow.AddDays(-1);
        await _db.SaveChangesAsync();

        var scopeFactory = new TestScopeFactory(_db, _workflow);
        var service = new ReferralEligibilityService(scopeFactory, Microsoft.Extensions.Logging.Abstractions.NullLogger<ReferralEligibilityService>.Instance);
        var count = await service.ProcessEligibleReferralsAsync();
        Assert.Equal(1, count);

        var refreshed = await _db.Referrals.FirstAsync(r => r.Id == entity.Id);
        Assert.Equal(ReferralPaymentStatus.Ready, refreshed.PaymentStatus);
        Assert.NotNull(refreshed.EligibilityNotifiedAt);
        Assert.Contains(await _db.ReferralNotifications.ToListAsync(), n => n.Type == "REFERRAL_PAYMENT_READY");
    }

    private async Task<ReferralEntity> SeedReferralAsync(
        string id,
        string referrerId,
        string status = "SUBMITTED",
        string paymentStatus = ReferralPaymentStatus.NotEligible,
        decimal rewardAmount = 0)
    {
        var e = new ReferralEntity
        {
            Id = id,
            ReferrerId = referrerId,
            ReferrerName = "Test",
            ProjectId = "proj-1",
            ProjectName = "P",
            TeamId = "t",
            CandidateName = "C",
            CandidateEmail = $"{id}@test.com",
            CandidatePhone = "+33",
            Position = "Dev",
            Status = status,
            PaymentStatus = paymentStatus,
            RewardAmount = rewardAmount,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Referrals.Add(e);
        await _db.SaveChangesAsync();
        return e;
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private sealed class TestScopeFactory(ParrainageDbContext db, ReferralWorkflowService workflow) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new TestScope(db, workflow);
    }

    private sealed class TestScope(ParrainageDbContext db, ReferralWorkflowService workflow) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new TestProvider(db, workflow);
        public void Dispose() { }
    }

    private sealed class TestProvider(ParrainageDbContext db, ReferralWorkflowService workflow) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(ParrainageDbContext)) return db;
            if (serviceType == typeof(ReferralWorkflowService)) return workflow;
            return null;
        }
    }
}
