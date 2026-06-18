using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PlanningService.Data;
using PlanningService.DTOs;
using PlanningService.Services;

namespace PlanningService.Services.EmployeeImport;

public interface IEmployeeImportService
{
    Task<EmployeeImportAnalyzeResponse> AnalyzeAsync(IFormFile file, CancellationToken ct = default);
    Task<EmployeeImportRevalidateOrgResponse> RevalidateOrgAsync(
        EmployeeImportRevalidateOrgRequest request,
        CancellationToken ct = default);
    Task<EmployeeImportReportDto> ExecuteAsync(EmployeeImportExecuteRequest request, string? startedByEmail, CancellationToken ct = default);
    Task<List<EmployeeImportJobSummaryDto>> GetHistoryAsync(int take = 50, CancellationToken ct = default);
    Task<EmployeeImportReportDto?> GetJobReportAsync(Guid jobId, CancellationToken ct = default);
    Task<byte[]> BuildTemplateAsync(CancellationToken ct = default);
}

public partial class EmployeeImportService(
    AppDbContext db,
    EmployeeImportFileParser parser,
    EmployeeImportColumnMatcher matcher,
    IEmployeeImportConfigService configService,
    IEmployeeImportSessionStore sessionStore,
    IEmployeeImportExecutor executor,
    IEmployeeImportOrgResolver orgResolver,
    IEmployeeImportOrgGapAnalyzer orgGapAnalyzer,
    EmployeeImportTemplateBuilder templateBuilder,
    IPlanningOrgMirrorService orgMirror,
    IHttpContextAccessor httpContextAccessor) : IEmployeeImportService
{
    private readonly AppDbContext _db = db;
    public async Task<EmployeeImportAnalyzeResponse> AnalyzeAsync(IFormFile file, CancellationToken ct = default)
    {
        var auth = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        await orgMirror.SyncFromDirectoryOverviewAsync(
            string.IsNullOrWhiteSpace(auth) ? null : auth, ct);

        var parsed = parser.Parse(file);
        if (parsed.Headers.Count == 0)
            throw new InvalidOperationException("Le fichier ne contient pas d'en-têtes.");

        var activeFields = (await configService.GetConfigAsync(ct)).Where(f => f.IsEnabled).ToList();
        var targets = activeFields
            .Select(f => new FieldMatchTarget(f.FieldKey, f.Label, f.Aliases))
            .ToList();

        var matches = matcher.MatchHeaders(parsed.Headers, targets);
        var sessionId = await sessionStore.SaveAsync(file.FileName, parsed, ct);

        var columnMap = matches
            .Where(m => !string.IsNullOrWhiteSpace(m.SuggestedFieldKey))
            .ToDictionary(m => m.ColumnIndex, m => m.SuggestedFieldKey!);

        var preview = parsed.Rows
            .Take(10)
            .Select(row => BuildPreviewRow(row, columnMap))
            .ToList();

        var alerts = new List<string>();
        if (!matches.Any(m => m.SuggestedFieldKey == "email"))
            alerts.Add("Aucune colonne email détectée — les lignes seront ignorées sans identifiant.");
        if (!matches.Any(m => m.SuggestedFieldKey == "role"))
            alerts.Add("Aucune colonne rôle détectée — la création échouera pour les nouveaux employés.");

        var orgSnapshot = await orgResolver.LoadSnapshotAsync(ct);
        var orgAnalysis = orgGapAnalyzer.AnalyzeFile(parsed, columnMap, orgSnapshot, orgSnapshot.Roles);

        foreach (var issue in orgAnalysis.OrgLineIssues.Where(i => i.Severity == "error"))
            alerts.Add($"Ligne {issue.LineNumber} : {issue.Message}");

        return new EmployeeImportAnalyzeResponse
        {
            ImportSessionId = sessionId,
            FileName = file.FileName,
            TotalRows = parsed.Rows.Count,
            Headers = parsed.Headers.ToList(),
            SuggestedMappings = matches.Select(m => new EmployeeImportColumnMappingDto
            {
                ColumnIndex = m.ColumnIndex,
                SourceHeader = m.SourceHeader,
                SuggestedFieldKey = m.SuggestedFieldKey,
                Confidence = m.Confidence
            }).ToList(),
            PreviewRows = preview,
            Alerts = alerts,
            ActiveFields = activeFields,
            PendingOrgCreations = orgAnalysis.PendingOrgCreations,
            ResolvedRows = orgAnalysis.ResolvedRows,
            OrgLineIssues = orgAnalysis.OrgLineIssues
        };
    }

    public Task<EmployeeImportReportDto> ExecuteAsync(
        EmployeeImportExecuteRequest request,
        string? startedByEmail,
        CancellationToken ct = default) =>
        executor.ExecuteAsync(request, startedByEmail, ct);

    public async Task<EmployeeImportRevalidateOrgResponse> RevalidateOrgAsync(
        EmployeeImportRevalidateOrgRequest request,
        CancellationToken ct = default)
    {
        var auth = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        await orgMirror.SyncFromDirectoryOverviewAsync(
            string.IsNullOrWhiteSpace(auth) ? null : auth, ct);

        var parsed = await sessionStore.GetAsync(request.ImportSessionId, ct)
            ?? throw new InvalidOperationException("Session d'import expirée ou introuvable. Re-analysez le fichier.");

        var activeFields = (await configService.GetConfigAsync(ct)).Where(f => f.IsEnabled).ToList();
        var columnMap = EmployeeImportMappingHelper.BuildColumnMap(request.Mappings, activeFields);

        if (!columnMap.Values.Any(v => v is "pole" or "cellule" or "service"))
        {
            throw new InvalidOperationException(
                "Mappez au moins une colonne Pôle, Cellule ou Service avant l'étape Organisation.");
        }

        var orgSnapshot = await orgResolver.LoadSnapshotAsync(ct);
        var orgAnalysis = orgGapAnalyzer.AnalyzeFile(parsed, columnMap, orgSnapshot, orgSnapshot.Roles);

        return new EmployeeImportRevalidateOrgResponse
        {
            PendingOrgCreations = orgAnalysis.PendingOrgCreations,
            ResolvedRows = orgAnalysis.ResolvedRows,
            OrgLineIssues = orgAnalysis.OrgLineIssues
        };
    }

    public async Task<List<EmployeeImportJobSummaryDto>> GetHistoryAsync(int take = 50, CancellationToken ct = default) =>
        await GetHistoryInternalAsync(take, ct);

    public async Task<EmployeeImportReportDto?> GetJobReportAsync(Guid jobId, CancellationToken ct = default) =>
        await GetJobReportInternalAsync(jobId, ct);

    public async Task<byte[]> BuildTemplateAsync(CancellationToken ct = default) =>
        await templateBuilder.BuildAsync(ct);

    private static Dictionary<string, string?> BuildPreviewRow(
        IReadOnlyList<string> row,
        Dictionary<int, string> columnMap)
    {
        var mapped = EmployeeImportRowMapper.MapRow(row, columnMap);
        foreach (var key in mapped.Keys.ToList())
        {
            if (key == "password" && !string.IsNullOrWhiteSpace(mapped[key]))
                mapped[key] = "********";
        }
        return mapped;
    }
}
