using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Planning.Application.Abstractions.EmployeeImport;
using Planning.Infrastructure.Persistence;
using Planning.Application.DTOs;
using Planning.Domain.Entities;

namespace Planning.Infrastructure.Services.EmployeeImport;

public partial class EmployeeFieldService(AppDbContext db) : IEmployeeFieldService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly HashSet<string> AllowedDataTypes =
        new(StringComparer.OrdinalIgnoreCase) { "text", "date", "number", "boolean" };

    public async Task<List<EmployeeImportFieldConfigDto>> GetAllAsync(bool enabledOnly = false, CancellationToken ct = default)
    {
        await EnsureSeedAsync(ct);
        var query = db.EmployeeImportFieldConfigs.AsNoTracking().OrderBy(f => f.SortOrder);
        var rows = enabledOnly
            ? await query.Where(f => f.IsEnabled).ToListAsync(ct)
            : await query.ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<EmployeeImportFieldConfigDto> CreateCustomFieldAsync(
        CreateEmployeeFieldRequest request,
        CancellationToken ct = default)
    {
        await EnsureSeedAsync(ct);

        var label = request.Label.Trim();
        if (string.IsNullOrWhiteSpace(label))
            throw new InvalidOperationException("Le libellé du champ est obligatoire.");

        var dataType = NormalizeDataType(request.DataType);
        var fieldKey = string.IsNullOrWhiteSpace(request.FieldKey)
            ? Slugify(label)
            : Slugify(request.FieldKey!);

        if (string.IsNullOrWhiteSpace(fieldKey))
            throw new InvalidOperationException("Impossible de générer une clé pour ce champ.");

        var exists = await db.EmployeeImportFieldConfigs
            .AnyAsync(f => f.FieldKey.ToLower() == fieldKey.ToLower(), ct);
        if (exists)
            throw new InvalidOperationException($"La clé « {fieldKey} » existe déjà.");

        var maxSort = await db.EmployeeImportFieldConfigs.MaxAsync(f => (int?)f.SortOrder, ct) ?? 0;
        var row = new EmployeeImportFieldConfig
        {
            FieldKey = fieldKey,
            Label = label,
            IsEnabled = request.IsEnabled,
            IsRequiredOnCreate = request.IsRequiredOnCreate,
            IsSystemField = false,
            DataType = dataType,
            SortOrder = request.SortOrder > 0 ? request.SortOrder : maxSort + 1,
            AliasesJson = JsonSerializer.Serialize(NormalizeAliases(request.Aliases), JsonOpts),
            CreatedAt = DateTime.UtcNow
        };

        db.EmployeeImportFieldConfigs.Add(row);
        await db.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task<EmployeeImportFieldConfigDto?> UpdateFieldAsync(
        string fieldKey,
        UpdateEmployeeFieldRequest request,
        CancellationToken ct = default)
    {
        await EnsureSeedAsync(ct);
        var row = await db.EmployeeImportFieldConfigs
            .FirstOrDefaultAsync(f => f.FieldKey.ToLower() == fieldKey.ToLower(), ct);
        if (row is null)
            return null;

        if (!string.IsNullOrWhiteSpace(request.Label))
            row.Label = request.Label.Trim();

        row.DataType = NormalizeDataType(request.DataType);
        var isRequired = request.IsRequiredOnCreate;
        var isEnabled = request.IsEnabled;
        EmployeeImportFieldRegistry.EnforceFieldLockConstraints(row.FieldKey, ref isEnabled, ref isRequired);
        row.IsRequiredOnCreate = isRequired;
        row.IsEnabled = isEnabled;
        row.SortOrder = request.SortOrder;

        if (request.Aliases.Count > 0)
            row.AliasesJson = JsonSerializer.Serialize(NormalizeAliases(request.Aliases), JsonOpts);

        await db.SaveChangesAsync(ct);
        return Map(row);
    }

    public async Task<bool> DeleteCustomFieldAsync(string fieldKey, CancellationToken ct = default)
    {
        var row = await db.EmployeeImportFieldConfigs
            .FirstOrDefaultAsync(f => f.FieldKey.ToLower() == fieldKey.ToLower(), ct);
        if (row is null || row.IsSystemField)
            return false;

        var values = db.UserCustomFieldValues.Where(v => v.FieldKey == row.FieldKey);
        db.UserCustomFieldValues.RemoveRange(values);
        db.EmployeeImportFieldConfigs.Remove(row);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<EmployeeImportMappingItemDto>> ResolveImportMappingsAsync(
        IReadOnlyList<EmployeeImportMappingItemDto> mappings,
        IReadOnlyList<string> headers,
        CancellationToken ct = default)
    {
        await EnsureSeedAsync(ct);
        var resolved = mappings.Select(CloneMapping).ToList();

        foreach (var mapping in resolved)
        {
            if (!string.Equals(mapping.Disposition, "keepAsNewField", StringComparison.OrdinalIgnoreCase))
                continue;

            var def = mapping.NewFieldDefinition
                ?? throw new InvalidOperationException(
                    $"Définition manquante pour la colonne {mapping.ColumnIndex + 1}.");

            var header = mapping.ColumnIndex >= 0 && mapping.ColumnIndex < headers.Count
                ? headers[mapping.ColumnIndex]
                : string.Empty;

            var label = string.IsNullOrWhiteSpace(def.Label)
                ? (string.IsNullOrWhiteSpace(header) ? $"Colonne {mapping.ColumnIndex + 1}" : header.Trim())
                : def.Label.Trim();

            var aliases = new List<string>();
            if (!string.IsNullOrWhiteSpace(header))
                aliases.Add(header.Trim());

            var fieldKey = Slugify(label);
            var existing = await db.EmployeeImportFieldConfigs
                .FirstOrDefaultAsync(f => f.FieldKey.ToLower() == fieldKey.ToLower(), ct);

            if (existing is null)
            {
                var created = await CreateCustomFieldAsync(new CreateEmployeeFieldRequest
                {
                    Label = label,
                    FieldKey = fieldKey,
                    DataType = def.DataType,
                    IsRequiredOnCreate = def.IsRequiredOnCreate,
                    IsEnabled = true,
                    Aliases = aliases
                }, ct);
                mapping.FieldKey = created.FieldKey;
            }
            else
            {
                mapping.FieldKey = existing.FieldKey;
                if (aliases.Count > 0)
                {
                    var mapped = Map(existing);
                    var merged = mapped.Aliases.Concat(aliases)
                        .Select(a => a.Trim())
                        .Where(a => a.Length > 0)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    await UpdateFieldAsync(existing.FieldKey, new UpdateEmployeeFieldRequest
                    {
                        Label = existing.Label,
                        DataType = existing.DataType,
                        IsRequiredOnCreate = existing.IsRequiredOnCreate,
                        IsEnabled = existing.IsEnabled,
                        SortOrder = existing.SortOrder,
                        Aliases = merged
                    }, ct);
                }
            }

            mapping.Disposition = "map";
        }

        return resolved;
    }

    public async Task UpsertCustomFieldsAsync(
        int userId,
        Dictionary<string, string?> values,
        bool isCreate,
        CancellationToken ct = default)
    {
        if (values.Count == 0)
            return;

        await EnsureSeedAsync(ct);
        var customFields = await db.EmployeeImportFieldConfigs
            .AsNoTracking()
            .Where(f => f.IsEnabled && !f.IsSystemField)
            .ToListAsync(ct);

        var customKeys = customFields.Select(f => f.FieldKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = await db.UserCustomFieldValues
            .Where(v => v.UserId == userId)
            .ToListAsync(ct);

        foreach (var (fieldKey, rawValue) in values)
        {
            if (!customKeys.Contains(fieldKey))
                continue;

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                if (isCreate)
                    continue;

                var row = existing.FirstOrDefault(v =>
                    string.Equals(v.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase));
                if (row is not null)
                    db.UserCustomFieldValues.Remove(row);
                continue;
            }

            var canonicalKey = customFields
                .First(f => string.Equals(f.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase))
                .FieldKey;

            var current = existing.FirstOrDefault(v =>
                string.Equals(v.FieldKey, canonicalKey, StringComparison.OrdinalIgnoreCase));

            if (current is null)
            {
                db.UserCustomFieldValues.Add(new UserCustomFieldValue
                {
                    UserId = userId,
                    FieldKey = canonicalKey,
                    Value = rawValue.Trim(),
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                current.Value = rawValue.Trim();
                current.UpdatedAt = DateTime.UtcNow;
            }
        }

        foreach (var entry in db.ChangeTracker.Entries<User>().ToList())
        {
            if (entry.State == EntityState.Added)
                entry.State = EntityState.Detached;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<Dictionary<int, Dictionary<string, string?>>> LoadCustomFieldsForUsersAsync(
        IReadOnlyList<int> userIds,
        CancellationToken ct = default)
    {
        if (userIds.Count == 0)
            return new Dictionary<int, Dictionary<string, string?>>();

        var rows = await db.UserCustomFieldValues
            .AsNoTracking()
            .Where(v => userIds.Contains(v.UserId))
            .ToListAsync(ct);

        return rows
            .GroupBy(v => v.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(x => x.FieldKey, x => x.Value, StringComparer.OrdinalIgnoreCase));
    }

    public async Task ValidateCustomFieldsForCreateAsync(
        Dictionary<string, string?> values,
        CancellationToken ct = default)
    {
        var fields = await GetAllAsync(enabledOnly: true, ct);
        EmployeeFieldValidator.ValidateRequiredCustomFieldsOnCreate(values, fields);
    }

    public async Task EnsureSeedAsync(CancellationToken ct = default)
    {
        var existing = await db.EmployeeImportFieldConfigs.ToListAsync(ct);

        foreach (var def in EmployeeImportFieldRegistry.DefaultFields)
        {
            var row = existing.FirstOrDefault(e =>
                string.Equals(e.FieldKey, def.FieldKey, StringComparison.OrdinalIgnoreCase));

            var dataType = ResolveSystemDataType(def.FieldKey);

            if (row is null)
            {
                db.EmployeeImportFieldConfigs.Add(new EmployeeImportFieldConfig
                {
                    FieldKey = def.FieldKey,
                    Label = def.Label,
                    IsEnabled = def.IsEnabledByDefault,
                    IsRequiredOnCreate = def.IsRequiredOnCreate,
                    SortOrder = def.SortOrder,
                    IsSystemField = EmployeeImportFieldRegistry.IsSystemFieldKey(def.FieldKey),
                    DataType = dataType,
                    AliasesJson = JsonSerializer.Serialize(def.Aliases, JsonOpts),
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                row.Label = def.Label;
                row.SortOrder = def.SortOrder;
                row.IsSystemField = EmployeeImportFieldRegistry.IsSystemFieldKey(def.FieldKey);
                row.DataType = dataType;
                row.AliasesJson = JsonSerializer.Serialize(def.Aliases, JsonOpts);
                if (!def.IsEnabledByDefault)
                    row.IsEnabled = false;

                var enabled = row.IsEnabled;
                var required = row.IsRequiredOnCreate;
                EmployeeImportFieldRegistry.ApplyFieldLockDefaults(row.FieldKey, ref enabled, ref required);
                row.IsEnabled = enabled;
                row.IsRequiredOnCreate = required;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static EmployeeImportMappingItemDto CloneMapping(EmployeeImportMappingItemDto m) => new()
    {
        ColumnIndex = m.ColumnIndex,
        FieldKey = m.FieldKey,
        Disposition = string.IsNullOrWhiteSpace(m.Disposition) ? "map" : m.Disposition,
        NewFieldDefinition = m.NewFieldDefinition is null ? null : new EmployeeImportNewFieldDefinitionDto
        {
            Label = m.NewFieldDefinition.Label,
            DataType = m.NewFieldDefinition.DataType,
            IsRequiredOnCreate = m.NewFieldDefinition.IsRequiredOnCreate
        }
    };

    private static string NormalizeDataType(string? dataType)
    {
        var normalized = (dataType ?? "text").Trim().ToLowerInvariant();
        return AllowedDataTypes.Contains(normalized) ? normalized : "text";
    }

    private static string ResolveSystemDataType(string fieldKey) => fieldKey switch
    {
        "hireDate" or "dateNaissance" or "dateEntree" or "dateAnciennete" or "dateSortie"
            or "dateEvolutionPoste" or "dateDebutFormation" or "dateFinFormationPrevue"
            or "contractStartDate" or "contractEndDate" => "date",
        "isActive" or "enFormation" => "boolean",
        "level" or "niveauExpertiseMetier" or "nombreEnfants" or "contractProbationDays"
            or "contractAlertThresholdDays" or "contractStatus" => "number",
        _ => "text"
    };

    private static List<string> NormalizeAliases(IEnumerable<string> aliases) =>
        aliases.Select(a => a.Trim()).Where(a => a.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static List<string> DeserializeAliases(List<string> aliases) => aliases;

    internal static EmployeeImportFieldConfigDto Map(EmployeeImportFieldConfig row)
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
            SortOrder = row.SortOrder,
            IsSystemField = row.IsSystemField,
            DataType = row.DataType,
            CreatedAt = row.CreatedAt
        };
    }

    public static string Slugify(string input)
    {
        var normalized = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else if (c is ' ' or '-' or '_' && sb.Length > 0 && sb[^1] != '_')
                sb.Append('_');
        }

        var slug = SlugCleanup().Replace(sb.ToString().Trim('_'), "_");
        return slug.Length > 64 ? slug[..64].Trim('_') : slug;
    }

    [GeneratedRegex("_+")]
    private static partial Regex SlugCleanup();
}

public static class EmployeeFieldValidator
{
    public static void ValidateRequiredCustomFieldsOnCreate(
        Dictionary<string, string?> values,
        IReadOnlyList<EmployeeImportFieldConfigDto> activeFields,
        bool onlyMappedKeys = false)
    {
        foreach (var field in activeFields.Where(f => f.IsEnabled && !f.IsSystemField && f.IsRequiredOnCreate))
        {
            if (onlyMappedKeys && !values.ContainsKey(field.FieldKey))
                continue;

            if (!values.TryGetValue(field.FieldKey, out var value) || string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Champ obligatoire manquant : {field.Label}.");
        }
    }

    public static Dictionary<string, string?> ExtractCustomFieldValues(
        Dictionary<string, string?> mapped,
        IReadOnlyList<EmployeeImportFieldConfigDto> activeFields)
    {
        var customKeys = activeFields
            .Where(f => f.IsEnabled && !f.IsSystemField)
            .Select(f => f.FieldKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return mapped
            .Where(kv => customKeys.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
    }
}
