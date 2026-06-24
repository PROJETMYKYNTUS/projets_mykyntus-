namespace Planning.Application.DTOs;

public sealed class PrimeOrgPoleMirrorDto
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public List<PrimeOrgCelluleMirrorDto> Cellules { get; init; } = [];
}

public sealed class PrimeOrgCelluleMirrorDto
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public List<PrimeOrgLeafServiceMirrorDto> Services { get; init; } = [];
}

public sealed class PrimeOrgLeafServiceMirrorDto
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
}

public sealed class PrimeOrgBackfillRequest
{
    public List<PrimePoleBackfillDto> Poles { get; init; } = [];
}

public sealed class PrimePoleBackfillDto
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public List<PrimeCelluleBackfillDto> Cellules { get; init; } = [];
}

public sealed class PrimeCelluleBackfillDto
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public List<PrimeServiceBackfillDto> Services { get; init; } = [];
}

public sealed class PrimeServiceBackfillDto
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
}

public sealed record OrgReconciliationVerifyDto(
    int FloorsWithoutPrimeId,
    int ServicesWithoutPrimeCelluleId,
    int SubServicesWithoutPrimeServiceId,
    int DuplicateSubServiceNames,
    int ActiveUsers,
    bool Ok);
