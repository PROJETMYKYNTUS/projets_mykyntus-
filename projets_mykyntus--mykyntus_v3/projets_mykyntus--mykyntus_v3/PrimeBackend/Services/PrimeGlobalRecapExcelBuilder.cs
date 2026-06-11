using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PrimeBackend.Data;

namespace PrimeBackend.Services;

/// <summary>
/// Excel de synthèse pour RH / Manager / Compta : totaux agrégés par pôle et par pilote (service),
/// sans ligne de détail par employé.
/// </summary>
public static class PrimeGlobalRecapExcelBuilder
{
    public static decimal EffectiveLineAmount(EmployeePrimeServiceFicheEntity f)
    {
        if (f.TotalAmount.HasValue) return f.TotalAmount.Value;
        return (f.PrimeAmount ?? 0m) + (f.ChallengeAmount ?? 0m);
    }

    public static async Task<byte[]> BuildAsync(PrimeDbContext db, string period, CancellationToken ct = default)
    {
        var per = period.Trim();

        var raw = await (
            from f in db.EmployeePrimeServiceFiches.AsNoTracking()
            where f.Period == per
            join srv in db.Services.AsNoTracking() on f.ServiceId equals srv.Id
            join cel in db.Cellules.AsNoTracking() on srv.CelluleId equals cel.Id
            join pole in db.Poles.AsNoTracking() on cel.PoleId equals pole.Id
            select new
            {
                f,
                ServiceId = srv.Id,
                ServiceName = srv.Name,
                CelluleName = cel.Name,
                PoleId = pole.Id,
                PoleName = pole.Name,
            }
        ).ToListAsync(ct);

        var byPole = raw
            .GroupBy(x => x.PoleId, StringComparer.Ordinal)
            .Select(g => new
            {
                PoleId = g.Key,
                PoleName = g.First().PoleName,
                Total = g.Sum(x => EffectiveLineAmount(x.f)),
            })
            .OrderBy(x => x.PoleName)
            .ToList();

        var byPilot = raw
            .GroupBy(x => new { x.PoleId, x.ServiceId })
            .Select(g =>
            {
                var first = g.First();
                return new
                {
                    first.PoleId,
                    first.PoleName,
                    first.CelluleName,
                    first.ServiceId,
                    first.ServiceName,
                    Total = g.Sum(x => EffectiveLineAmount(x.f)),
                };
            })
            .OrderBy(x => x.PoleName)
            .ThenBy(x => x.CelluleName)
            .ThenBy(x => x.ServiceName)
            .ToList();

        var grandTotal = raw.Sum(x => EffectiveLineAmount(x.f));

        using var wb = new XLWorkbook();

        var cover = wb.Worksheets.Add("Synthèse générale");
        cover.Cell(1, 1).Value = "PRIME — Synthèse globale (totaux, sans détail employé)";
        cover.Cell(2, 1).Value = "Période";
        cover.Cell(2, 2).Value = per;
        cover.Cell(3, 1).Value = "Généré le (UTC)";
        cover.Cell(3, 2).Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        cover.Cell(5, 1).Value = "Total général primes (tous pôles, tous pilotes)";
        cover.Cell(5, 2).Value = grandTotal;
        cover.Cell(5, 2).Style.NumberFormat.Format = "# ##0.00";

        var wsP = wb.Worksheets.Add("Totaux par pôle");
        wsP.Cell(1, 1).Value = "Identifiant pôle";
        wsP.Cell(1, 2).Value = "Libellé pôle";
        wsP.Cell(1, 3).Value = "Total primes";
        for (var i = 0; i < byPole.Count; i++)
        {
            var r = i + 2;
            wsP.Cell(r, 1).Value = byPole[i].PoleId;
            wsP.Cell(r, 2).Value = byPole[i].PoleName;
            wsP.Cell(r, 3).Value = byPole[i].Total;
            wsP.Cell(r, 3).Style.NumberFormat.Format = "# ##0.00";
        }

        var poleTotalRow = byPole.Count + 3;
        wsP.Cell(poleTotalRow, 2).Value = "TOTAL";
        wsP.Cell(poleTotalRow, 3).Value = grandTotal;
        wsP.Cell(poleTotalRow, 3).Style.NumberFormat.Format = "# ##0.00";

        var wsS = wb.Worksheets.Add("Totaux par pilote");
        wsS.Cell(1, 1).Value = "Pôle";
        wsS.Cell(1, 2).Value = "Cellule";
        wsS.Cell(1, 3).Value = "Pilote (service)";
        wsS.Cell(1, 4).Value = "Id service";
        wsS.Cell(1, 5).Value = "Total primes";
        for (var i = 0; i < byPilot.Count; i++)
        {
            var r = i + 2;
            wsS.Cell(r, 1).Value = byPilot[i].PoleName;
            wsS.Cell(r, 2).Value = byPilot[i].CelluleName;
            wsS.Cell(r, 3).Value = byPilot[i].ServiceName;
            wsS.Cell(r, 4).Value = byPilot[i].ServiceId;
            wsS.Cell(r, 5).Value = byPilot[i].Total;
            wsS.Cell(r, 5).Style.NumberFormat.Format = "# ##0.00";
        }

        var pilotTotalRow = byPilot.Count + 3;
        wsS.Cell(pilotTotalRow, 4).Value = "TOTAL";
        wsS.Cell(pilotTotalRow, 5).Value = grandTotal;
        wsS.Cell(pilotTotalRow, 5).Style.NumberFormat.Format = "# ##0.00";

        cover.Columns(1, 2).AdjustToContents();
        wsP.Columns().AdjustToContents();
        wsS.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
