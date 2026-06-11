using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;
using PrimeBackend.Services;
using Xunit;

namespace PrimeBackend.Tests;

public sealed class PrimeGlobalSynthesisPaymentServiceTests
{
    private static (PrimeDbContext Db, SqliteConnection Conn, PrimeGlobalSynthesisPaymentService Payment) Create()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        var opts = new DbContextOptionsBuilder<PrimeDbContext>().UseSqlite(conn).Options;
        var db = new PrimeDbContext(opts);
        db.Database.EnsureCreated();
        return (db, conn, new PrimeGlobalSynthesisPaymentService(db));
    }

    private static GlobalPoolSynthesisLineEntity Line(Guid scopeId, string lineStatus) => new()
    {
        Id = Guid.NewGuid(),
        ScopeSynthesisId = scopeId,
        FicheId = Guid.NewGuid(),
        EmployeeId = "e1",
        ServiceId = "s1",
        LineStatus = lineStatus,
        RhDecision = lineStatus == "Approved" ? "Approved" : "Pending",
        ManagerDecision = lineStatus == "Approved" ? "Approved" : "Pending",
        PaymentStatus = "Unpaid",
    };

    [Fact]
    public async Task SetLinePayment_allows_approved_line_even_if_scope_not_fully_decided()
    {
        var (db, conn, payment) = Create();
        using (conn)
        {
            var scopeId = Guid.NewGuid();
            db.GlobalPoolScopeSyntheses.Add(new GlobalPoolScopeSynthesisEntity
            {
                Id = scopeId,
                Period = "2026-05",
                ScopeType = "Service",
                ScopeId = "s1",
                ScopeDisplayName = "Svc",
                UpdatedAt = DateTimeOffset.UtcNow,
                // Volontairement non débloqué au niveau périmètre.
                ManagerApprovedAt = null,
                RhApprovedAt = null,
            });
            var approved = Line(scopeId, "Approved");
            var pending = Line(scopeId, "PendingReview");
            db.GlobalPoolSynthesisLines.AddRange(approved, pending);
            await db.SaveChangesAsync();

            var (ok, err) = await payment.SetLinePaymentAsync(
                approved.Id, "compta1", "Comptabilité", paid: true, paidAt: null, reference: "VIR-1");
            Assert.True(ok, err);

            var (ok2, err2) = await payment.SetLinePaymentAsync(
                pending.Id, "compta1", "Comptabilité", paid: true, paidAt: null, reference: null);
            Assert.False(ok2);
            Assert.NotNull(err2);
        }
    }

    [Fact]
    public async Task PayAll_pays_only_approved_lines()
    {
        var (db, conn, payment) = Create();
        using (conn)
        {
            var scopeId = Guid.NewGuid();
            db.GlobalPoolScopeSyntheses.Add(new GlobalPoolScopeSynthesisEntity
            {
                Id = scopeId,
                Period = "2026-05",
                ScopeType = "Service",
                ScopeId = "s1",
                ScopeDisplayName = "Svc",
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            var a1 = Line(scopeId, "Approved");
            var a2 = Line(scopeId, "Approved");
            var rejected = Line(scopeId, "LineRejected");
            var pending = Line(scopeId, "PendingReview");
            db.GlobalPoolSynthesisLines.AddRange(a1, a2, rejected, pending);
            await db.SaveChangesAsync();

            var (ok, err) = await payment.PayAllAsync(scopeId, "compta1", "Comptabilité", paidAt: null, reference: null);
            Assert.True(ok, err);

            var paidCount = await db.GlobalPoolSynthesisLines
                .CountAsync(l => l.ScopeSynthesisId == scopeId && l.PaymentStatus == "Paid");
            Assert.Equal(2, paidCount);
        }
    }
}
