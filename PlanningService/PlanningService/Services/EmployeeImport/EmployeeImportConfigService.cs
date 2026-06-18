using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlanningService.Data;
using PlanningService.DTOs;
using PlanningService.Models;

namespace PlanningService.Services.EmployeeImport;

public interface IEmployeeImportConfigService
{
    Task<List<EmployeeImportFieldConfigDto>> GetConfigAsync(CancellationToken ct = default);
    Task<List<EmployeeImportFieldConfigDto>> UpdateConfigAsync(UpdateEmployeeImportConfigRequest request, CancellationToken ct = default);
    Task EnsureSeedAsync(CancellationToken ct = default);
    Task<List<FieldMatchTarget>> GetActiveFieldTargetsAsync(CancellationToken ct = default);
}

public class EmployeeImportConfigService(AppDbContext db) : IEmployeeImportConfigService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task EnsureSeedAsync(CancellationToken ct = default)
    {
        var existing = await db.EmployeeImportFieldConfigs.ToListAsync(ct);

        foreach (var def in EmployeeImportFieldRegistry.DefaultFields)
        {
            var row = existing.FirstOrDefault(e =>
                string.Equals(e.FieldKey, def.FieldKey, StringComparison.OrdinalIgnoreCase));

            if (row is null)
            {
                db.EmployeeImportFieldConfigs.Add(new EmployeeImportFieldConfig
                {
                    FieldKey = def.FieldKey,
                    Label = def.Label,
                    IsEnabled = def.IsEnabledByDefault,
                    IsRequiredOnCreate = def.IsRequiredOnCreate,
                    SortOrder = def.SortOrder,
                    AliasesJson = JsonSerializer.Serialize(def.Aliases, JsonOpts)
                });
            }
            else
            {
                row.Label = def.Label;
                row.SortOrder = def.SortOrder;
                row.AliasesJson = JsonSerializer.Serialize(def.Aliases, JsonOpts);
                if (!def.IsEnabledByDefault)
                    row.IsEnabled = false;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<List<EmployeeImportFieldConfigDto>> GetConfigAsync(CancellationToken ct = default)
    {
        await EnsureSeedAsync(ct);
        var rows = await db.EmployeeImportFieldConfigs
            .OrderBy(f => f.SortOrder)
            .ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<List<EmployeeImportFieldConfigDto>> UpdateConfigAsync(
        UpdateEmployeeImportConfigRequest request,
        CancellationToken ct = default)
    {
        await EnsureSeedAsync(ct);
        var existing = await db.EmployeeImportFieldConfigs.ToListAsync(ct);

        foreach (var item in request.Fields)
        {
            var row = existing.FirstOrDefault(e =>
                string.Equals(e.FieldKey, item.FieldKey, StringComparison.OrdinalIgnoreCase));
            if (row is null)
                continue;

            row.IsEnabled = item.IsEnabled;
            row.Label = string.IsNullOrWhiteSpace(item.Label) ? row.Label : item.Label.Trim();
            row.IsRequiredOnCreate = item.IsRequiredOnCreate;
            row.SortOrder = item.SortOrder;
            if (item.Aliases.Count > 0)
                row.AliasesJson = JsonSerializer.Serialize(item.Aliases, JsonOpts);
        }

        await db.SaveChangesAsync(ct);
        return await GetConfigAsync(ct);
    }

    public async Task<List<FieldMatchTarget>> GetActiveFieldTargetsAsync(CancellationToken ct = default)
    {
        var config = await GetConfigAsync(ct);
        return config
            .Where(f => f.IsEnabled)
            .Select(f => new FieldMatchTarget(f.FieldKey, f.Label, f.Aliases))
            .ToList();
    }

    private static EmployeeImportFieldConfigDto Map(EmployeeImportFieldConfig row)
    {
        List<string> aliases;
        try
        {
            aliases = JsonSerializer.Deserialize<List<string>>(row.AliasesJson, JsonOpts) ?? [];
        }
        catch
        {
            aliases = [];
        }

        return new EmployeeImportFieldConfigDto
        {
            FieldKey = row.FieldKey,
            Label = row.Label,
            IsEnabled = row.IsEnabled,
            IsRequiredOnCreate = row.IsRequiredOnCreate,
            Aliases = aliases,
            SortOrder = row.SortOrder
        };
    }
}
