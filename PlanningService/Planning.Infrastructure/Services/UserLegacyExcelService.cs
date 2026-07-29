using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Planning.Application.Abstractions;
using Planning.Application.DTOs;
using Planning.Domain.Entities;
using Planning.Infrastructure.Persistence;

namespace Planning.Infrastructure.Services;

public sealed class UserLegacyExcelService(AppDbContext context) : IUserLegacyExcelService
{
    public byte[] BuildImportTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Employés");

        var headers = new[]
        {
            "FirstName *", "LastName *", "Email *", "Password *",
            "RoleId *", "SubServiceId", "HireDate", "IsActive"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        ws.Cell(2, 1).Value = "Mohammed";
        ws.Cell(2, 2).Value = "Alami";
        ws.Cell(2, 3).Value = "m.alami@kyntus.ma";
        ws.Cell(2, 4).Value = "Kyntus-Import-99!";
        ws.Cell(2, 5).Value = 2;
        ws.Cell(2, 6).Value = 1;
        ws.Cell(2, 7).Value = "2024-01-15";
        ws.Cell(2, 8).Value = true;

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<ImportResultDto> ImportUsersAsync(Stream excelStream, CancellationToken ct = default)
    {
        var result = new ImportResultDto();

        using var wb = new XLWorkbook(excelStream);
        var ws = wb.Worksheet(1);
        var rows = ws.RangeUsed()?.RowsUsed().Skip(1).ToList() ?? [];

        result.TotalLignes = rows.Count;

        foreach (var row in rows)
        {
            var lineNum = row.RowNumber();
            try
            {
                var firstName = row.Cell(1).GetString().Trim();
                var lastName = row.Cell(2).GetString().Trim();
                var email = row.Cell(3).GetString().Trim().ToLower();
                var password = row.Cell(4).GetString().Trim();
                var roleIdStr = row.Cell(5).GetString().Trim();
                var subSvcStr = row.Cell(6).GetString().Trim();
                var hireDateStr = row.Cell(7).GetString().Trim();
                var isActiveStr = row.Cell(8).GetString().Trim();

                if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) ||
                    string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) ||
                    string.IsNullOrEmpty(roleIdStr))
                {
                    result.Erreurs++;
                    result.Details.Add($"Ligne {lineNum} : champs obligatoires manquants.");
                    continue;
                }

                if (!int.TryParse(roleIdStr, out var roleId))
                {
                    result.Erreurs++;
                    result.Details.Add($"Ligne {lineNum} : RoleId invalide ({roleIdStr}).");
                    continue;
                }

                if (await context.Users.AnyAsync(u => u.Email == email, ct))
                {
                    result.Erreurs++;
                    result.Details.Add($"Ligne {lineNum} : email '{email}' déjà existant.");
                    continue;
                }

                int? subServiceId = int.TryParse(subSvcStr, out var sid) ? sid : null;
                var hireDate = DateTime.TryParse(hireDateStr, out var hd) ? hd : DateTime.UtcNow;
                var isActive = isActiveStr.ToLower() != "false";

                var user = new User
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    RoleId = roleId,
                    SubServiceId = subServiceId,
                    HireDate = hireDate,
                    IsActive = isActive,
                    CreatedAt = DateTime.UtcNow
                };

                context.Users.Add(user);
                result.Importes++;
            }
            catch (Exception ex)
            {
                result.Erreurs++;
                result.Details.Add($"Ligne {lineNum} : erreur inattendue — {ex.Message}");
            }
        }

        await context.SaveChangesAsync(ct);
        return result;
    }

    public async Task<byte[]> ExportUsersAsync(CancellationToken ct = default)
    {
        var users = await context.Users
            .Include(u => u.Role)
            .Include(u => u.SubService)
            .OrderBy(u => u.LastName)
            .ToListAsync(ct);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Employés");

        var headers = new[]
        {
            "Id", "Prénom", "Nom", "Email", "Rôle",
            "Sous-service", "Date embauche", "Actif", "Créé le"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        for (var i = 0; i < users.Count; i++)
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

            if (i % 2 == 0)
                ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F4FF");
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }
}
