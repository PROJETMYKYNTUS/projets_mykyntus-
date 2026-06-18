using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PlanningService.Data;

namespace PlanningService.Services.EmployeeImport;

public class EmployeeImportTemplateBuilder(AppDbContext db)
{
    private static readonly XLColor HeaderBg = XLColor.FromHtml("#1E3A5F");

    public async Task<byte[]> BuildAsync(CancellationToken ct = default)
    {
        var orgSnapshot = await LoadOrgDataAsync(ct);
        var roles = await db.Roles.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => r.Name)
            .ToListAsync(ct);
        roles = roles.Where(r => !EmployeeImportFieldRegistry.IsImportForbiddenRoleName(r)).ToList();

        using var wb = new XLWorkbook();
        BuildEmployeesSheet(wb, orgSnapshot, roles);
        BuildReferentialsSheet(wb, roles, orgSnapshot);
        BuildNoticeSheet(wb);

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }

    private void BuildEmployeesSheet(XLWorkbook wb, OrgTemplateSample sample, List<string> roles)
    {
        var ws = wb.Worksheets.Add("Employés");
        var fields = EmployeeImportFieldRegistry.TemplateFields;

        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            var header = field.IsRequiredOnCreate ? $"{field.Label} *" : field.Label;
            StyleHeader(ws.Cell(1, i + 1), header);
        }

        var example = new Dictionary<string, string>
        {
            ["email"] = "employe@exemple.fr",
            ["firstName"] = "Mohammed",
            ["lastName"] = "Alami",
            ["password"] = "",
            ["role"] = roles.FirstOrDefault(r => r.Equals("Pilote", StringComparison.OrdinalIgnoreCase))
                       ?? roles.FirstOrDefault() ?? "Pilote",
            ["pole"] = sample.PoleName,
            ["cellule"] = sample.CelluleName,
            ["service"] = sample.ServiceName,
            ["hireDate"] = "15/01/2024",
            ["isActive"] = "Oui",
            ["level"] = "Débutant",
        };

        for (var i = 0; i < fields.Count; i++)
        {
            if (example.TryGetValue(fields[i].FieldKey, out var val))
                ws.Cell(2, i + 1).Value = val;
        }

        ws.Row(1).Height = 22;
        ws.Columns().AdjustToContents();
    }

    private void BuildReferentialsSheet(XLWorkbook wb, List<string> roles, OrgTemplateSample sample)
    {
        var ws = wb.Worksheets.Add("Référentiels");

        ws.Cell(1, 1).Value = "Rôles disponibles";
        StyleHeader(ws.Cell(1, 1), "Rôles disponibles");
        for (var i = 0; i < roles.Count; i++)
            ws.Cell(i + 2, 1).Value = roles[i];

        var startCol = 3;
        StyleHeader(ws.Cell(1, startCol), "Pôle");
        StyleHeader(ws.Cell(1, startCol + 1), "Cellule");
        StyleHeader(ws.Cell(1, startCol + 2), "Service");

        var row = 2;
        foreach (var line in sample.HierarchyLines)
        {
            ws.Cell(row, startCol).Value = line.Pole;
            ws.Cell(row, startCol + 1).Value = line.Cellule;
            ws.Cell(row, startCol + 2).Value = line.Service;
            row++;
        }

        var levelCol = startCol + 3;
        StyleHeader(ws.Cell(1, levelCol), "Niveau");
        for (var i = 0; i < EmployeeImportLevelResolver.Labels.Count; i++)
            ws.Cell(i + 2, levelCol).Value = EmployeeImportLevelResolver.Labels[i];

        ws.Columns().AdjustToContents();
    }

    private static void BuildNoticeSheet(XLWorkbook wb)
    {
        var ws = wb.Worksheets.Add("Notice");
        var lines = new[]
        {
            "Règles d'import employés",
            "",
            "• Email = identifiant unique (création si nouveau, mise à jour si existant).",
            "• Champs marqués * obligatoires à la création.",
            "• Cellule vide = la valeur en base n'est pas effacée.",
            "• Mot de passe vide = mot de passe système par défaut (Azerty@123).",
            "• Pôle / Cellule / Service : utilisez les noms exacts de la feuille Référentiels.",
            "• Niveau : Débutant, Intermédiaire ou Expert (voir feuille Référentiels).",
            "• Les rôles Admin et Manager ne peuvent pas être attribués via l'import employés.",
            "• Une erreur sur une ligne n'arrête pas le reste du fichier.",
            "",
            "Après dépôt du fichier : vérifiez le mapping des colonnes puis la prévisualisation avant de lancer l'import.",
        };

        for (var i = 0; i < lines.Length; i++)
            ws.Cell(i + 1, 1).Value = lines[i];

        ws.Column(1).Width = 90;
    }

    private static void StyleHeader(IXLCell cell, string text)
    {
        cell.Value = text;
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontColor = XLColor.White;
        cell.Style.Fill.BackgroundColor = HeaderBg;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private async Task<OrgTemplateSample> LoadOrgDataAsync(CancellationToken ct)
    {
        var floors = await db.Floors
            .AsNoTracking()
            .Include(f => f.Services)
            .ThenInclude(s => s.SubServices)
            .OrderBy(f => f.Name)
            .ToListAsync(ct);

        var hierarchy = new List<OrgHierarchyLine>();
        OrgHierarchyLine? first = null;

        foreach (var floor in floors)
        {
            foreach (var service in floor.Services.OrderBy(s => s.Name))
            {
                foreach (var sub in service.SubServices.OrderBy(s => s.Name))
                {
                    var line = new OrgHierarchyLine(floor.Name, service.Name, sub.Name);
                    hierarchy.Add(line);
                    first ??= line;
                }
            }
        }

        first ??= new OrgHierarchyLine("Mon Pôle", "Ma Cellule", "Mon Service");

        return new OrgTemplateSample(
            first.Pole,
            first.Cellule,
            first.Service,
            hierarchy);
    }

    private sealed record OrgHierarchyLine(string Pole, string Cellule, string Service);

    private sealed record OrgTemplateSample(
        string PoleName,
        string CelluleName,
        string ServiceName,
        IReadOnlyList<OrgHierarchyLine> HierarchyLines);
}
