using Planning.Application.Abstractions.EmployeeImport;
using Planning.Application.DTOs;

namespace Planning.Infrastructure.Services.EmployeeImport;

public class EmployeeImportConfigService(IEmployeeFieldService fieldService) : IEmployeeImportConfigService
{
    public Task<List<EmployeeImportFieldConfigDto>> GetConfigAsync(CancellationToken ct = default) =>
        fieldService.GetAllAsync(enabledOnly: false, ct);

    public async Task<List<EmployeeImportFieldConfigDto>> UpdateConfigAsync(
        UpdateEmployeeImportConfigRequest request,
        CancellationToken ct = default)
    {
        foreach (var item in request.Fields)
        {
            await fieldService.UpdateFieldAsync(item.FieldKey, new UpdateEmployeeFieldRequest
            {
                Label = item.Label,
                DataType = item.DataType,
                IsRequiredOnCreate = item.IsRequiredOnCreate,
                IsEnabled = item.IsEnabled,
                SortOrder = item.SortOrder,
                Aliases = item.Aliases
            }, ct);
        }

        return await GetConfigAsync(ct);
    }

    public Task EnsureSeedAsync(CancellationToken ct = default) =>
        fieldService.EnsureSeedAsync(ct);

    public async Task<List<FieldMatchTarget>> GetActiveFieldTargetsAsync(CancellationToken ct = default)
    {
        var config = await fieldService.GetAllAsync(enabledOnly: true, ct);
        return config
            .Select(f => new FieldMatchTarget(f.FieldKey, f.Label, f.Aliases))
            .ToList();
    }
}
