using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
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
        var resolver = new ReferralRuleResolver(_db);
        var cvStorage = new ReferralCvStorageService(new ConfigurationBuilder().Build(), new TestWebHostEnvironment());
        _workflow = new ReferralWorkflowService(_db, resolver, cvStorage);
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
        await SetCvUrlAsync(created.Id);

        var updated = await _workflow.ProcessReferralAsync(created.Id, new ProcessReferralRequest
        {
            Comment = "Entretien OK",
        }, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("PROCESSED", updated!.Status);
        Assert.Contains(await _db.ReferralHistory.ToListAsync(), h => h.Action == "PROCESSED");
    }

    [Fact]
    public async Task ProcessReferral_Throws_WhenCvMissing()
    {
        var created = await _workflow.SubmitReferralAsync(new CreateReferralRequest
        {
            ReferrerId = "emp-cv",
            ReferrerName = "Jean",
            CandidateName = "Bob",
            CandidateEmail = "bob-cv@test.com",
            CandidatePhone = "+33",
            Position = "Dev",
        }, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _workflow.ProcessReferralAsync(created.Id, new ProcessReferralRequest(), CancellationToken.None));
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

        await SetCvUrlAsync(created.Id);
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
        Assert.Null(updated.AppliedRuleId);
        Assert.Equal(ReferralPositionMode.Custom, updated.PositionMode);
    }

    [Fact]
    public async Task SubmitReferral_WithRuleId_SetsAppliedRuleAndCatalogPosition()
    {
        _db.ReferralRules.Add(new ReferralRuleEntity
        {
            Id = "rule-dev",
            Name = "Dev",
            Type = ReferralRuleResolver.PositionRuleType,
            Target = "Développeur",
            Value = 600,
            MinDurationMonths = 6,
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        var created = await _workflow.SubmitReferralAsync(new CreateReferralRequest
        {
            ReferrerId = "emp-rule",
            ReferrerName = "Jean",
            CandidateName = "Bob",
            CandidateEmail = "bob-rule@test.com",
            CandidatePhone = "+33",
            RuleId = "rule-dev",
        }, CancellationToken.None);

        Assert.Equal("rule-dev", created.AppliedRuleId);
        Assert.Equal(ReferralPositionMode.Catalog, created.PositionMode);
        Assert.Equal("Développeur", created.Position);
    }

    [Fact]
    public async Task SubmitReferral_CustomPosition_LeavesAppliedRuleNull()
    {
        var created = await _workflow.SubmitReferralAsync(new CreateReferralRequest
        {
            ReferrerId = "emp-custom",
            ReferrerName = "Jean",
            CandidateName = "Carla",
            CandidateEmail = "carla-custom@test.com",
            CandidatePhone = "+33",
            Position = "Développeur",
        }, CancellationToken.None);

        Assert.Null(created.AppliedRuleId);
        Assert.Equal(ReferralPositionMode.Custom, created.PositionMode);
        Assert.Equal("Développeur", created.Position);
    }

    [Fact]
    public async Task ApproveReferral_UsesRuleMinDuration_WhenCatalogRuleApplied()
    {
        _db.ReferralRules.Add(new ReferralRuleEntity
        {
            Id = "rule-3m",
            Name = "Chef de projet",
            Type = ReferralRuleResolver.PositionRuleType,
            Target = "Chef de projet",
            Value = 750,
            MinDurationMonths = 3,
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        var created = await _workflow.SubmitReferralAsync(new CreateReferralRequest
        {
            ReferrerId = "emp-pm",
            ReferrerName = "Jean",
            CandidateName = "Paul",
            CandidateEmail = "paul-pm@test.com",
            CandidatePhone = "+33",
            RuleId = "rule-3m",
        }, CancellationToken.None);

        await SetCvUrlAsync(created.Id);
        await _workflow.ProcessReferralAsync(created.Id, new ProcessReferralRequest(), CancellationToken.None);

        var start = new DateOnly(2026, 2, 1);
        var updated = await _workflow.ApproveReferralAsync(created.Id, new ApproveReferralRequest
        {
            CandidateStartDate = start,
            RewardAmount = 750m,
        }, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(
            ReferralEligibilityCalculator.ComputeEligibleForPayment(start, 3),
            updated!.EligibleForPaymentAt);
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
    public async Task EligibilityService_MarksAwaitingRh_AndNotifiesRh()
    {
        var entity = await SeedReferralAsync("ref-elig", "emp-6", "APPROVED", ReferralPaymentStatus.NotEligible, 800m);
        entity.EligibleForPaymentAt = DateTimeOffset.UtcNow.AddDays(-1);
        await _db.SaveChangesAsync();

        var scopeFactory = new TestScopeFactory(_db, _workflow);
        var service = new ReferralEligibilityService(scopeFactory, Microsoft.Extensions.Logging.Abstractions.NullLogger<ReferralEligibilityService>.Instance);
        var count = await service.ProcessEligibleReferralsAsync();
        Assert.Equal(1, count);

        var refreshed = await _db.Referrals.FirstAsync(r => r.Id == entity.Id);
        Assert.Equal(ReferralPaymentStatus.AwaitingRh, refreshed.PaymentStatus);
        Assert.Contains(await _db.ReferralNotifications.ToListAsync(), n => n.Type == "REFERRAL_ELIGIBILITY_DUE");
    }

    [Fact]
    public async Task ConfirmPaymentEligibility_SetsReady_ForCompta()
    {
        var entity = await SeedReferralAsync("ref-confirm", "emp-9", "APPROVED", ReferralPaymentStatus.AwaitingRh, 900m);
        entity.EligibleForPaymentAt = DateTimeOffset.UtcNow.AddDays(-10);
        await _db.SaveChangesAsync();

        var updated = await _workflow.ConfirmPaymentEligibilityAsync(
            entity.Id,
            new ConfirmPaymentEligibilityRequest { Comment = "Toujours en poste" },
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(ReferralPaymentStatus.Ready, updated!.PaymentStatus);
        Assert.NotNull(updated.EligibilityNotifiedAt);
        Assert.Contains(await _db.ReferralNotifications.ToListAsync(), n => n.Type == "REFERRAL_PAYMENT_READY");
    }

    private async Task SetCvUrlAsync(string referralId)
    {
        var entity = await _db.Referrals.FirstAsync(r => r.Id == referralId);
        entity.CvUrl = ReferralCvStorageService.CvApiPath(referralId);
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task ApproveReferral_WithTraining_SetsInTraining_WithoutEligibleDate()
    {
        var created = await _workflow.SubmitReferralAsync(new CreateReferralRequest
        {
            ReferrerId = "emp-train",
            ReferrerName = "Jean",
            CandidateName = "Trainee",
            CandidateEmail = "trainee@test.com",
            CandidatePhone = "+33",
            Position = "Dev",
        }, CancellationToken.None);

        await SetCvUrlAsync(created.Id);
        await _workflow.ProcessReferralAsync(created.Id, new ProcessReferralRequest(), CancellationToken.None);

        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 2, 28);
        var updated = await _workflow.ApproveReferralAsync(created.Id, new ApproveReferralRequest
        {
            CandidateStartDate = start,
            TrainingEndDate = end,
            RequiresTraining = true,
            RewardAmount = 1500m,
        }, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("IN_TRAINING", updated!.Status);
        Assert.Equal(start, updated.CandidateStartDate);
        Assert.Equal(end, updated.TrainingEndDate);
        Assert.Null(updated.EligibleForPaymentAt);
        Assert.Null(updated.ApprovedAt);
        Assert.Equal(1500m, updated.RewardAmount);
    }

    [Fact]
    public async Task ConfirmProductionStart_SetsApproved_WithEligibleFromProductionDate()
    {
        var entity = await SeedInTrainingAsync("ref-train-prod", "emp-tp", new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 28), 750m);

        var prodStart = new DateOnly(2026, 3, 1);
        var updated = await _workflow.ConfirmProductionStartAsync(
            entity.Id,
            new ConfirmProductionStartRequest { ProductionStartDate = prodStart },
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("APPROVED", updated!.Status);
        Assert.Equal(prodStart, updated.ProductionStartDate);
        Assert.Equal(
            ReferralEligibilityCalculator.ComputeEligibleForPayment(prodStart, 6),
            updated.EligibleForPaymentAt);
    }

    [Fact]
    public async Task ExtendTraining_UpdatesDate_AndResetsNotification()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var entity = await SeedInTrainingAsync(
            "ref-train-ext",
            "emp-te",
            today.AddDays(-60),
            today.AddDays(-10),
            750m);
        entity.TrainingEndNotifiedAt = DateTimeOffset.UtcNow.AddDays(-1);
        await _db.SaveChangesAsync();

        var newEnd = today.AddDays(30);
        var updated = await _workflow.ExtendTrainingAsync(
            entity.Id,
            new ExtendTrainingRequest { TrainingEndDate = newEnd },
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(newEnd, updated!.TrainingEndDate);
        Assert.Null(updated.TrainingEndNotifiedAt);
    }

    [Fact]
    public async Task RejectEarlyDeparture_FromInTraining_ClearsTrainingFields()
    {
        var entity = await SeedInTrainingAsync("ref-train-rej", "emp-tr", new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 28), 750m);

        var updated = await _workflow.RejectEarlyDepartureAsync(
            entity.Id,
            new RejectEarlyDepartureRequest
            {
                DepartureDate = new DateOnly(2026, 3, 1),
                Comment = "Abandon formation",
            },
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("REJECTED", updated!.Status);
        Assert.Equal(0m, updated.RewardAmount);
        Assert.Null(updated.TrainingEndDate);
        Assert.Null(updated.ProductionStartDate);
        Assert.Null(updated.TrainingEndNotifiedAt);
        Assert.Null(updated.CandidateStartDate);
        Assert.Contains(await _db.ReferralHistory.ToListAsync(), h => h.Action == "EARLY_DEPARTURE");
    }

    [Fact]
    public async Task RejectEarlyDeparture_FromApproved_CancelsBonus()
    {
        var entity = await SeedApprovedAsync("ref-app-rej", "emp-ar", 600m);

        var updated = await _workflow.RejectEarlyDepartureAsync(
            entity.Id,
            new RejectEarlyDepartureRequest { DepartureDate = new DateOnly(2026, 4, 1) },
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("REJECTED", updated!.Status);
        Assert.Equal(0m, updated.RewardAmount);
        Assert.Equal(ReferralPaymentStatus.NotEligible, updated.PaymentStatus);
    }

    [Fact]
    public async Task RejectEarlyDeparture_Throws_WhenSubmitted()
    {
        var created = await _workflow.SubmitReferralAsync(new CreateReferralRequest
        {
            ReferrerId = "emp-ed",
            ReferrerName = "Jean",
            CandidateName = "Bob",
            CandidateEmail = "bob-ed@test.com",
            CandidatePhone = "+33",
            Position = "Dev",
        }, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _workflow.RejectEarlyDepartureAsync(
                created.Id,
                new RejectEarlyDepartureRequest(),
                CancellationToken.None));
    }

    [Fact]
    public async Task EligibilityService_NotifiesTrainingEndDue()
    {
        var entity = await SeedInTrainingAsync("ref-train-due", "emp-td", new DateOnly(2026, 1, 1), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)), 750m);

        var scopeFactory = new TestScopeFactory(_db, _workflow);
        var service = new ReferralEligibilityService(scopeFactory, Microsoft.Extensions.Logging.Abstractions.NullLogger<ReferralEligibilityService>.Instance);
        var count = await service.ProcessEligibleReferralsAsync();
        Assert.True(count >= 1);

        var refreshed = await _db.Referrals.FirstAsync(r => r.Id == entity.Id);
        Assert.NotNull(refreshed.TrainingEndNotifiedAt);
        Assert.Contains(await _db.ReferralNotifications.ToListAsync(), n => n.Type == "REFERRAL_TRAINING_END_DUE");
    }

    private async Task<ReferralEntity> SeedApprovedAsync(string id, string referrerId, decimal rewardAmount)
    {
        var start = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-2));
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
            Status = "APPROVED",
            PaymentStatus = ReferralPaymentStatus.NotEligible,
            RewardAmount = rewardAmount,
            CandidateStartDate = start,
            ProductionStartDate = start,
            ApprovedAt = DateTimeOffset.UtcNow.AddMonths(-2),
            EligibleForPaymentAt = DateTimeOffset.UtcNow.AddMonths(4),
            CreatedAt = DateTimeOffset.UtcNow.AddMonths(-3),
        };
        _db.Referrals.Add(e);
        await _db.SaveChangesAsync();
        return e;
    }

    private async Task<ReferralEntity> SeedInTrainingAsync(
        string id,
        string referrerId,
        DateOnly start,
        DateOnly trainingEnd,
        decimal rewardAmount)
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
            Status = "IN_TRAINING",
            PaymentStatus = ReferralPaymentStatus.NotEligible,
            RewardAmount = rewardAmount,
            CandidateStartDate = start,
            TrainingEndDate = trainingEnd,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Referrals.Add(e);
        await _db.SaveChangesAsync();
        return e;
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

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
