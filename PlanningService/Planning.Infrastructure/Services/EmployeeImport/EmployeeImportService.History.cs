using Microsoft.EntityFrameworkCore;
using Planning.Application.DTOs;

namespace Planning.Infrastructure.Services.EmployeeImport;

public partial class EmployeeImportService
{
    private async Task<List<EmployeeImportJobSummaryDto>> GetHistoryInternalAsync(int take = 50, CancellationToken ct = default)
    {
        return await _db.EmployeeImportJobs
            .AsNoTracking()
            .OrderByDescending(j => j.StartedAt)
            .Take(take)
            .Select(j => new EmployeeImportJobSummaryDto
            {
                Id = j.Id,
                FileName = j.FileName,
                TotalLignes = j.TotalLignes,
                Crees = j.Crees,
                MisAJour = j.MisAJour,
                Ignores = j.Ignores,
                Erreurs = j.Erreurs,
                StartedByEmail = j.StartedByEmail,
                StartedAt = j.StartedAt,
                CompletedAt = j.CompletedAt
            })
            .ToListAsync(ct);
    }

    private async Task<EmployeeImportReportDto?> GetJobReportInternalAsync(Guid jobId, CancellationToken ct = default)
    {
        var job = await _db.EmployeeImportJobs
            .AsNoTracking()
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.Id == jobId, ct);

        if (job is null)
            return null;

        return new EmployeeImportReportDto
        {
            ImportJobId = job.Id,
            TotalLignes = job.TotalLignes,
            Crees = job.Crees,
            MisAJour = job.MisAJour,
            Ignores = job.Ignores,
            Erreurs = job.Erreurs,
            CompletedAt = job.CompletedAt ?? job.StartedAt,
            Lignes = job.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new EmployeeImportRowResultDto
                {
                    LineNumber = l.LineNumber,
                    Email = l.Email,
                    Action = l.Action,
                    Message = l.Message
                })
                .ToList()
        };
    }
}
