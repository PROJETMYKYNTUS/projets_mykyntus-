using Microsoft.AspNetCore.Http;
using Planning.Application.Abstractions;
using Planning.Application.DTOs;
using Planning.Infrastructure.Services;

namespace Planning.Infrastructure.Services.EmployeeImport;

public interface IImportExecutionJournal
{
    void RecordOrgCreated(OrgNodeCreatedReportDto node);

    void RecordUserCreated(int planningUserId, Guid employeeGuid, int? authUserId);

    void RecordUserUpdated(
        int planningUserId,
        UpdateUserDto previousState,
        Dictionary<string, string?> previousCustomFields);

    Task RollbackLastUserChangeAsync(CancellationToken ct = default);

    Task CompensateAsync(CancellationToken ct = default);

    void Commit();
}

public sealed class ImportExecutionJournal(
    IUserService userService,
    IDirectoryOrgWriteClient directoryOrg,
    IPlanningOrgMirrorService orgMirror,
    IHttpContextAccessor httpContextAccessor,
    ILogger<ImportExecutionJournal> logger) : IImportExecutionJournal
{
    private readonly List<IJournalEntry> _entries = [];
    private readonly Dictionary<string, string> _poleDirectoryIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _celluleDirectoryIds = new(StringComparer.OrdinalIgnoreCase);

    public void RecordOrgCreated(OrgNodeCreatedReportDto node)
    {
        if (string.IsNullOrWhiteSpace(node.DirectoryNodeId))
            return;

        string? parentDirectoryId = null;
        switch (node.NodeType.ToLowerInvariant())
        {
            case "pole":
                _poleDirectoryIds[node.Name] = node.DirectoryNodeId;
                break;
            case "cellule":
                if (!string.IsNullOrWhiteSpace(node.Pole))
                    _poleDirectoryIds.TryGetValue(node.Pole, out parentDirectoryId);
                if (!string.IsNullOrWhiteSpace(node.Pole) && !string.IsNullOrWhiteSpace(node.Name))
                    _celluleDirectoryIds[$"{node.Pole}|{node.Name}"] = node.DirectoryNodeId;
                break;
            case "service":
                if (!string.IsNullOrWhiteSpace(node.Pole) && !string.IsNullOrWhiteSpace(node.Cellule))
                    _celluleDirectoryIds.TryGetValue($"{node.Pole}|{node.Cellule}", out parentDirectoryId);
                break;
        }

        _entries.Add(new OrgCreatedEntry(node.NodeType, node.DirectoryNodeId, parentDirectoryId));
    }

    public void RecordUserCreated(int planningUserId, Guid employeeGuid, int? authUserId) =>
        _entries.Add(new UserCreatedEntry(planningUserId, employeeGuid, authUserId));

    public void RecordUserUpdated(
        int planningUserId,
        UpdateUserDto previousState,
        Dictionary<string, string?> previousCustomFields) =>
        _entries.Add(new UserUpdatedEntry(planningUserId, previousState, previousCustomFields));

    public async Task RollbackLastUserChangeAsync(CancellationToken ct = default)
    {
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i] is not UserCreatedEntry and not UserUpdatedEntry)
                continue;

            var entry = _entries[i];
            _entries.RemoveAt(i);
            await CompensateEntryAsync(entry, ct);
            return;
        }
    }

    public async Task CompensateAsync(CancellationToken ct = default)
    {
        var hadOrgEntries = _entries.Any(e => e is OrgCreatedEntry);

        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            try
            {
                await CompensateEntryAsync(_entries[i], ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Compensation import employé : entrée {Index} en échec (best-effort)", i);
            }
        }

        _entries.Clear();
        _poleDirectoryIds.Clear();
        _celluleDirectoryIds.Clear();

        if (hadOrgEntries)
            await SyncOrgMirrorAsync(ct);
    }

    public void Commit()
    {
        _entries.Clear();
        _poleDirectoryIds.Clear();
        _celluleDirectoryIds.Clear();
    }

    private async Task CompensateEntryAsync(IJournalEntry entry, CancellationToken ct)
    {
        switch (entry)
        {
            case UserCreatedEntry created:
                await userService.RollbackImportCreatedUserAsync(created.PlanningUserId, ct);
                break;
            case UserUpdatedEntry updated:
                await userService.RollbackImportUpdatedUserAsync(
                    updated.PlanningUserId,
                    updated.PreviousState,
                    updated.PreviousCustomFields,
                    ct);
                break;
            case OrgCreatedEntry org:
                await CompensateOrgAsync(org, ct);
                break;
        }
    }

    private async Task CompensateOrgAsync(OrgCreatedEntry org, CancellationToken ct)
    {
        switch (org.NodeType.ToLowerInvariant())
        {
            case "service":
                if (!string.IsNullOrWhiteSpace(org.ParentDirectoryId))
                {
                    await directoryOrg.DeleteServiceAsync(org.ParentDirectoryId, org.DirectoryNodeId, ct);
                }
                break;
            case "cellule":
                if (!string.IsNullOrWhiteSpace(org.ParentDirectoryId))
                {
                    await directoryOrg.DeleteCelluleAsync(org.ParentDirectoryId, org.DirectoryNodeId, ct);
                }
                break;
            case "pole":
                await directoryOrg.DeletePoleAsync(org.DirectoryNodeId, ct);
                break;
        }
    }

    private async Task SyncOrgMirrorAsync(CancellationToken ct)
    {
        try
        {
            var auth = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
            await orgMirror.SyncFromDirectoryOverviewAsync(
                string.IsNullOrWhiteSpace(auth) ? null : auth, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Resynchronisation miroir org après rollback import (best-effort)");
        }
    }

    private interface IJournalEntry;

    private sealed record OrgCreatedEntry(string NodeType, string DirectoryNodeId, string? ParentDirectoryId)
        : IJournalEntry;

    private sealed record UserCreatedEntry(int PlanningUserId, Guid EmployeeGuid, int? AuthUserId)
        : IJournalEntry;

    private sealed record UserUpdatedEntry(
        int PlanningUserId,
        UpdateUserDto PreviousState,
        Dictionary<string, string?> PreviousCustomFields) : IJournalEntry;
}
