using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlanningService.Data;
using PlanningService.Models;

[ApiController]
[Route("api/Users")]
public class UserImportExportController : ControllerBase
{
    private readonly AppDbContext _context;

    public UserImportExportController(AppDbContext context)
    {
        _context = context;
    }

    // ── GET /api/Users/template ──
    // Télécharger le template Excel vide
    [HttpGet("template")]
    public IActionResult DownloadTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Employés");

        // Headers
        var headers = new[] {
            "FirstName *", "LastName *", "Email *", "Password *",
            "RoleId *", "SubServiceId", "HireDate", "IsActive"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Exemple
        ws.Cell(2, 1).Value = "Mohammed";
        ws.Cell(2, 2).Value = "Alami";
        ws.Cell(2, 3).Value = "m.alami@kyntus.ma";
        ws.Cell(2, 4).Value = "Password123!";
        ws.Cell(2, 5).Value = 2;
        ws.Cell(2, 6).Value = 1;
        ws.Cell(2, 7).Value = "2024-01-15";
        ws.Cell(2, 8).Value = true;

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        stream.Position = 0;

        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "template_employes.xlsx");
    }

    // ── POST /api/Users/import ──
    // Importer les employés depuis Excel
    [HttpPost("import")]
    public async Task<ActionResult<ImportResultDto>> ImportUsers(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Fichier manquant.");

        var result = new ImportResultDto();

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        stream.Position = 0;

        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet(1);
        var rows = ws.RangeUsed().RowsUsed().Skip(1).ToList(); // Skip header

        result.TotalLignes = rows.Count;

        foreach (var row in rows)
        {
            var lineNum = row.RowNumber();
            try
            {
                // Lire les valeurs
                var firstName = row.Cell(1).GetString().Trim();
                var lastName = row.Cell(2).GetString().Trim();
                var email = row.Cell(3).GetString().Trim().ToLower();
                var password = row.Cell(4).GetString().Trim();
                var roleIdStr = row.Cell(5).GetString().Trim();
                var subSvcStr = row.Cell(6).GetString().Trim();
                var hireDateStr = row.Cell(7).GetString().Trim();
                var isActiveStr = row.Cell(8).GetString().Trim();

                // Validation champs obligatoires
                if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) ||
                    string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) ||
                    string.IsNullOrEmpty(roleIdStr))
                {
                    result.Erreurs++;
                    result.Details.Add($"Ligne {lineNum} : champs obligatoires manquants.");
                    continue;
                }

                if (!int.TryParse(roleIdStr, out int roleId))
                {
                    result.Erreurs++;
                    result.Details.Add($"Ligne {lineNum} : RoleId invalide ({roleIdStr}).");
                    continue;
                }

                // Email unique
                if (await _context.Users.AnyAsync(u => u.Email == email))
                {
                    result.Erreurs++;
                    result.Details.Add($"Ligne {lineNum} : email '{email}' déjà existant.");
                    continue;
                }

                // Champs optionnels
                int? subServiceId = int.TryParse(subSvcStr, out int sid) ? sid : null;
                DateTime hireDate = DateTime.TryParse(hireDateStr, out DateTime hd) ? hd : DateTime.UtcNow;
                bool isActive = isActiveStr.ToLower() != "false";

                // Hash du mot de passe
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

                var user = new User
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Email = email,
                    PasswordHash = passwordHash,
                    RoleId = roleId,
                    SubServiceId = subServiceId,
                    HireDate = hireDate,
                    IsActive = isActive,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                result.Importes++;
            }
            catch (Exception ex)
            {
                result.Erreurs++;
                result.Details.Add($"Ligne {lineNum} : erreur inattendue — {ex.Message}");
            }
        }

        await _context.SaveChangesAsync();
        return Ok(result);
    }

    // ── GET /api/Users/export ──
    // Exporter tous les employés en Excel
    [HttpGet("export")]
    public async Task<IActionResult> ExportUsers()
    {
        var users = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.SubService)
            .OrderBy(u => u.LastName)
            .ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Employés");

        // Headers
        var headers = new[] {
            "Id", "Prénom", "Nom", "Email", "Rôle",
            "Sous-service", "Date embauche", "Actif", "Créé le"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Data
        for (int i = 0; i < users.Count; i++)
        {
            var u = users[i];
            var row = i + 2;

            ws.Cell(row, 1).Value = u.Id;
            ws.Cell(row, 2).Value = u.FirstName;
            ws.Cell(row, 3).Value = u.LastName;
            ws.Cell(row, 4).Value = u.Email;
            ws.Cell(row, 5).Value = u.Role?.Name ?? "";
            ws.Cell(row, 6).Value = u.SubService?.Name ?? "";
            ws.Cell(row, 7).Value = u.HireDate.ToString("dd/MM/yyyy");
            ws.Cell(row, 8).Value = u.IsActive ? "Oui" : "Non";
            ws.Cell(row, 9).Value = u.CreatedAt.ToString("dd/MM/yyyy");

            // Alternating row colors
            if (i % 2 == 0)
                ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F4FF");
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        stream.Position = 0;

        var fileName = $"employes_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}