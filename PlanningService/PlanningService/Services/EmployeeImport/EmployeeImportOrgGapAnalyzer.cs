using PlanningService.DTOs;
using PlanningService.Models;

namespace PlanningService.Services.EmployeeImport;

public interface IEmployeeImportOrgGapAnalyzer
{
    EmployeeImportAnalysisResult AnalyzeFile(
        ParsedImportFile parsed,
        Dictionary<int, string> columnToField,
        EmployeeImportOrgSnapshot orgSnapshot,
        IReadOnlyList<Role> roles);
}

public sealed class EmployeeImportAnalysisResult
{
    public List<EmployeeImportResolvedRowDto> ResolvedRows { get; init; } = [];
    public List<PendingOrgCreationDto> PendingOrgCreations { get; init; } = [];
    public List<EmployeeImportOrgLineIssueDto> OrgLineIssues { get; init; } = [];
}

public class EmployeeImportOrgGapAnalyzer(IEmployeeImportOrgResolver orgResolver) : IEmployeeImportOrgGapAnalyzer
{
    public EmployeeImportAnalysisResult AnalyzeFile(
        ParsedImportFile parsed,
        Dictionary<int, string> columnToField,
        EmployeeImportOrgSnapshot orgSnapshot,
        IReadOnlyList<Role> roles)
    {
        var resolvedRows = new List<EmployeeImportResolvedRowDto>();
        var lineIssues = new List<EmployeeImportOrgLineIssueDto>();
        var pending = new Dictionary<string, PendingOrgCreationDto>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < parsed.Rows.Count; i++)
        {
            var lineNumber = i + 2;
            var row = parsed.Rows[i];
            if (row.All(string.IsNullOrWhiteSpace))
                continue;

            var mapped = EmployeeImportRowMapper.MapRow(row, columnToField);
            AnalyzeLine(lineNumber, mapped, orgSnapshot, roles, resolvedRows, lineIssues, pending);
        }

        return new EmployeeImportAnalysisResult
        {
            ResolvedRows = resolvedRows,
            PendingOrgCreations = pending.Values.OrderBy(p => p.Type).ThenBy(p => p.Pole).ToList(),
            OrgLineIssues = lineIssues
        };
    }

    private void AnalyzeLine(
        int lineNumber,
        Dictionary<string, string?> mapped,
        EmployeeImportOrgSnapshot orgSnapshot,
        IReadOnlyList<Role> roles,
        List<EmployeeImportResolvedRowDto> resolvedRows,
        List<EmployeeImportOrgLineIssueDto> lineIssues,
        Dictionary<string, PendingOrgCreationDto> pending)
    {
        mapped.TryGetValue("email", out var email);
        mapped.TryGetValue("role", out var roleRaw);
        mapped.TryGetValue("pole", out var poleRaw);
        mapped.TryGetValue("cellule", out var celluleRaw);
        mapped.TryGetValue("service", out var serviceRaw);

        var roleResult = EmployeeImportRoleResolver.Resolve(roleRaw, roles);
        if (roleResult.ErrorMessage is not null)
        {
            lineIssues.Add(new EmployeeImportOrgLineIssueDto
            {
                LineNumber = lineNumber,
                Email = email,
                Severity = roleResult.IsForbidden ? "error" : "error",
                Message = roleResult.ErrorMessage
            });
        }

        var orgResolution = EmployeeImportOrgFuzzyMatcher.ResolveOrgNames(
            orgSnapshot, poleRaw, celluleRaw, serviceRaw);

        var effectiveMapped = EmployeeImportOrgFuzzyMatcher.ApplyToMapped(mapped, orgResolution);
        var depth = EmployeeImportRoleSynonymRegistry.GetOrgDepth(roleResult.CanonicalRoleName);

        if (depth != EmployeeImportOrgDepth.None && !EmployeeImportRoleSynonymRegistry.HasRequiredOrgColumns(effectiveMapped, depth))
        {
            lineIssues.Add(new EmployeeImportOrgLineIssueDto
            {
                LineNumber = lineNumber,
                Email = email,
                Severity = "error",
                Message = EmployeeImportRoleSynonymRegistry.RequiredOrgColumnsMessage(depth)
            });
        }

        foreach (var hint in orgResolution.Hints.Where(h => h.Confidence == "medium"))
        {
            lineIssues.Add(new EmployeeImportOrgLineIssueDto
            {
                LineNumber = lineNumber,
                Email = email,
                Severity = "warning",
                Message = $"Correspondance probable {hint.FieldKey} : « {hint.SourceValue} » → « {hint.MatchedValue} »."
            });
        }

        foreach (var hint in orgResolution.Hints.Where(h => h.IsNewName))
        {
            lineIssues.Add(new EmployeeImportOrgLineIssueDto
            {
                LineNumber = lineNumber,
                Email = email,
                Severity = "warning",
                Message = $"Nouveau {OrgLevelLabel(hint.FieldKey)} à créer : « {hint.SourceValue} »."
            });
        }

        TryResolveExistingOrg(orgSnapshot, effectiveMapped, depth, out var orgError);
        if (orgError is not null)
        {
            lineIssues.Add(new EmployeeImportOrgLineIssueDto
            {
                LineNumber = lineNumber,
                Email = email,
                Severity = "error",
                Message = orgError
            });
        }

        if (depth != EmployeeImportOrgDepth.None &&
            EmployeeImportRoleSynonymRegistry.HasRequiredOrgColumns(effectiveMapped, depth))
        {
            foreach (var creation in BuildPendingCreations(depth, orgResolution, orgSnapshot, effectiveMapped))
            {
                if (TryBlockDuplicateOrgCreation(orgSnapshot, orgResolution, creation, lineNumber, email, lineIssues))
                    continue;

                if (creation.Type == "pole" && string.IsNullOrWhiteSpace(creation.OperationalDepartment))
                {
                    lineIssues.Add(new EmployeeImportOrgLineIssueDto
                    {
                        LineNumber = lineNumber,
                        Email = email,
                        Severity = "error",
                        Message =
                            $"Département opérationnel requis pour créer le pôle « {creation.Pole} » — mappez la colonne ou utilisez un pôle existant."
                    });
                }

                var key = PendingKey(creation);
                if (pending.TryGetValue(key, out var existing))
                {
                    existing.AffectedLineNumbers.Add(lineNumber);
                }
                else
                {
                    creation.AffectedLineNumbers.Add(lineNumber);
                    pending[key] = creation;
                }
            }
        }

        resolvedRows.Add(new EmployeeImportResolvedRowDto
        {
            LineNumber = lineNumber,
            Email = email,
            RoleName = roleResult.CanonicalRoleName,
            RoleConfidence = roleResult.Confidence,
            Pole = orgResolution.Pole,
            Cellule = orgResolution.Cellule,
            Service = orgResolution.Service,
            OrgHints = orgResolution.Hints.Select(h => new EmployeeImportOrgHintDto
            {
                FieldKey = h.FieldKey,
                SourceValue = h.SourceValue,
                MatchedValue = h.MatchedValue,
                Confidence = h.Confidence,
                IsNewName = h.IsNewName
            }).ToList()
        });
    }

    private bool TryResolveExistingOrg(
        EmployeeImportOrgSnapshot snapshot,
        Dictionary<string, string?> mapped,
        EmployeeImportOrgDepth depth,
        out string? error)
    {
        error = null;
        if (depth == EmployeeImportOrgDepth.None)
            return true;

        try
        {
            if (depth == EmployeeImportOrgDepth.Service)
            {
                orgResolver.ResolveSubServiceId(snapshot, mapped);
                return true;
            }

            if (depth == EmployeeImportOrgDepth.Cellule)
            {
                mapped.TryGetValue("pole", out var pole);
                mapped.TryGetValue("cellule", out var cellule);
                orgResolver.EnsureCelluleExists(snapshot, pole, cellule);
                return true;
            }

            if (depth == EmployeeImportOrgDepth.Pole)
            {
                mapped.TryGetValue("pole", out var pole);
                orgResolver.EnsurePoleExists(snapshot, pole);
                return true;
            }
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("ambigu", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("Plusieurs", StringComparison.OrdinalIgnoreCase))
            {
                error = ex.Message;
                return false;
            }

            return false;
        }

        return true;
    }

    private static bool TryBlockDuplicateOrgCreation(
        EmployeeImportOrgSnapshot snapshot,
        OrgFuzzyResolution orgResolution,
        PendingOrgCreationDto creation,
        int lineNumber,
        string? email,
        List<EmployeeImportOrgLineIssueDto> lineIssues)
    {
        var fieldKey = creation.Type switch
        {
            "pole" => "pole",
            "cellule" => "cellule",
            "service" => "service",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(fieldKey))
            return false;

        var raw = fieldKey switch
        {
            "pole" => creation.Pole,
            "cellule" => creation.Cellule,
            "service" => creation.Service,
            _ => null
        };

        var hint = orgResolution.Hints.LastOrDefault(h =>
            string.Equals(h.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase));
        if (hint is not null && !hint.IsNewName)
            return false;

        var candidates = GetCandidatesForField(snapshot, orgResolution, fieldKey);
        var likely = EmployeeImportFuzzyMatcher.FindBestOrgMatch(fieldKey, raw, candidates);
        if (likely is null)
            return false;

        lineIssues.Add(new EmployeeImportOrgLineIssueDto
        {
            LineNumber = lineNumber,
            Email = email,
            Severity = "error",
            Message =
                $"Un {OrgLevelLabel(fieldKey)} très proche existe déjà : « {likely.Value} » " +
                $"(fichier : « {raw} »). Utilisez le nom existant ou validez la correspondance à l'étape Organisation."
        });

        return true;
    }

    private static List<string> GetCandidatesForField(
        EmployeeImportOrgSnapshot snapshot,
        OrgFuzzyResolution orgResolution,
        string fieldKey)
    {
        IEnumerable<OrgHierarchyRow> rows = snapshot.Rows;

        if (!string.IsNullOrWhiteSpace(orgResolution.Pole))
        {
            rows = rows.Where(r =>
                EmployeeImportColumnMatcher.Normalize(r.FloorName) ==
                EmployeeImportColumnMatcher.Normalize(orgResolution.Pole));
        }

        if (fieldKey is "cellule" or "service" && !string.IsNullOrWhiteSpace(orgResolution.Cellule))
        {
            rows = rows.Where(r =>
                EmployeeImportColumnMatcher.Normalize(r.ServiceName) ==
                EmployeeImportColumnMatcher.Normalize(orgResolution.Cellule));
        }

        return fieldKey switch
        {
            "pole" => rows.Select(r => r.FloorName).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            "cellule" => rows.Select(r => r.ServiceName).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            "service" => rows.Select(r => r.SubServiceName).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            _ => []
        };
    }

    private static string OrgLevelLabel(string fieldKey) => fieldKey switch
    {
        "pole" => "pôle",
        "cellule" => "cellule",
        _ => "service"
    };

    private static IEnumerable<PendingOrgCreationDto> BuildPendingCreations(
        EmployeeImportOrgDepth depth,
        OrgFuzzyResolution orgResolution,
        EmployeeImportOrgSnapshot snapshot,
        Dictionary<string, string?> effectiveMapped)
    {
        var poleSource = SourceOrgName(orgResolution, "pole", effectiveMapped);
        var celluleSource = SourceOrgName(orgResolution, "cellule", effectiveMapped);
        var serviceSource = SourceOrgName(orgResolution, "service", effectiveMapped);
        effectiveMapped.TryGetValue("operationalDepartment", out var operationalDepartment);
        var operationalDepartmentSource = string.IsNullOrWhiteSpace(operationalDepartment)
            ? null
            : operationalDepartment.Trim();

        var poleCheck = orgResolution.Pole ?? poleSource;
        var celluleCheck = orgResolution.Cellule ?? celluleSource;
        var serviceCheck = orgResolution.Service ?? serviceSource;

        if (depth == EmployeeImportOrgDepth.Pole && !string.IsNullOrWhiteSpace(poleSource) && !PoleExists(snapshot, poleCheck!))
        {
            yield return new PendingOrgCreationDto
            {
                Type = "pole",
                Pole = poleSource,
                OperationalDepartment = operationalDepartmentSource,
                ConfirmationLabel = $"Créer le pôle « {poleSource} »"
            };
            yield break;
        }

        if (depth == EmployeeImportOrgDepth.Cellule &&
            !string.IsNullOrWhiteSpace(poleSource) &&
            !string.IsNullOrWhiteSpace(celluleSource))
        {
            if (!PoleExists(snapshot, poleCheck!))
            {
                yield return new PendingOrgCreationDto
                {
                    Type = "pole",
                    Pole = poleSource,
                    OperationalDepartment = operationalDepartmentSource,
                    ConfirmationLabel = $"Créer le pôle « {poleSource} »"
                };
            }

            if (!CelluleExists(snapshot, poleCheck!, celluleCheck!))
            {
                yield return new PendingOrgCreationDto
                {
                    Type = "cellule",
                    Pole = poleSource,
                    Cellule = celluleSource,
                    ConfirmationLabel = $"Créer la cellule « {celluleSource} » sous le pôle « {poleSource} »"
                };
            }

            yield break;
        }

        if (depth == EmployeeImportOrgDepth.Service &&
            !string.IsNullOrWhiteSpace(poleSource) &&
            !string.IsNullOrWhiteSpace(celluleSource) &&
            !string.IsNullOrWhiteSpace(serviceSource))
        {
            if (!PoleExists(snapshot, poleCheck!))
            {
                yield return new PendingOrgCreationDto
                {
                    Type = "pole",
                    Pole = poleSource,
                    OperationalDepartment = operationalDepartmentSource,
                    ConfirmationLabel = $"Créer le pôle « {poleSource} »"
                };
            }

            if (!CelluleExists(snapshot, poleCheck!, celluleCheck!))
            {
                yield return new PendingOrgCreationDto
                {
                    Type = "cellule",
                    Pole = poleSource,
                    Cellule = celluleSource,
                    ConfirmationLabel = $"Créer la cellule « {celluleSource} » sous le pôle « {poleSource} »"
                };
            }

            if (!ServiceExists(snapshot, poleCheck!, celluleCheck!, serviceCheck!))
            {
                yield return new PendingOrgCreationDto
                {
                    Type = "service",
                    Pole = poleSource,
                    Cellule = celluleSource,
                    Service = serviceSource,
                    ConfirmationLabel =
                        $"Créer le service « {serviceSource} » (pôle « {poleSource} », cellule « {celluleSource} »)"
                };
            }
        }
    }

    private static string? SourceOrgName(
        OrgFuzzyResolution orgResolution,
        string fieldKey,
        Dictionary<string, string?> effectiveMapped)
    {
        var hint = orgResolution.Hints.LastOrDefault(h =>
            string.Equals(h.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(hint?.SourceValue))
            return hint.SourceValue.Trim();

        return effectiveMapped.TryGetValue(fieldKey, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    private static bool PoleExists(EmployeeImportOrgSnapshot snapshot, string pole) =>
        snapshot.Rows.Any(r =>
            EmployeeImportColumnMatcher.Normalize(r.FloorName) ==
            EmployeeImportColumnMatcher.Normalize(pole));

    private static bool CelluleExists(EmployeeImportOrgSnapshot snapshot, string pole, string cellule) =>
        snapshot.Rows.Any(r =>
            EmployeeImportColumnMatcher.Normalize(r.FloorName) ==
            EmployeeImportColumnMatcher.Normalize(pole)
            && EmployeeImportColumnMatcher.Normalize(r.ServiceName) ==
            EmployeeImportColumnMatcher.Normalize(cellule));

    private static bool ServiceExists(
        EmployeeImportOrgSnapshot snapshot,
        string pole,
        string cellule,
        string service) =>
        snapshot.Rows.Any(r =>
            EmployeeImportColumnMatcher.Normalize(r.FloorName) ==
            EmployeeImportColumnMatcher.Normalize(pole)
            && EmployeeImportColumnMatcher.Normalize(r.ServiceName) ==
            EmployeeImportColumnMatcher.Normalize(cellule)
            && EmployeeImportColumnMatcher.Normalize(r.SubServiceName) ==
            EmployeeImportColumnMatcher.Normalize(service));

    private static string PendingKey(PendingOrgCreationDto dto) =>
        $"{dto.Type}|{dto.Pole}|{dto.Cellule}|{dto.Service}".ToLowerInvariant();
}
