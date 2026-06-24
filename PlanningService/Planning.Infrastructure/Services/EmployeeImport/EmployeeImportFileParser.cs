using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace Planning.Infrastructure.Services.EmployeeImport;

public class EmployeeImportFileParser
{
    private const long MaxFileBytes = 5 * 1024 * 1024;

    public ParsedImportFile Parse(IFormFile file)
    {
        if (file.Length == 0)
            throw new InvalidOperationException("Le fichier est vide.");

        if (file.Length > MaxFileBytes)
            throw new InvalidOperationException("Le fichier dépasse la taille maximale de 5 Mo.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        using var stream = new MemoryStream();
        file.CopyTo(stream);
        stream.Position = 0;

        return extension switch
        {
            ".xlsx" or ".xls" => ParseExcel(stream),
            ".csv" => ParseCsv(stream),
            _ => throw new InvalidOperationException("Format non supporté. Utilisez .xlsx ou .csv.")
        };
    }

    private static ParsedImportFile ParseExcel(Stream stream)
    {
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet(1);
        var range = ws.RangeUsed();
        if (range is null)
            throw new InvalidOperationException("Le fichier Excel ne contient aucune donnée.");

        var rows = range.RowsUsed().ToList();
        if (rows.Count < 1)
            throw new InvalidOperationException("Le fichier Excel ne contient aucune ligne.");

        var headers = ReadExcelRow(rows[0]);
        var dataRows = new List<IReadOnlyList<string>>();

        foreach (var row in rows.Skip(1))
            dataRows.Add(ReadExcelRow(row));

        return new ParsedImportFile(headers, dataRows);
    }

    private static IReadOnlyList<string> ReadExcelRow(IXLRangeRow row)
    {
        var lastCol = row.LastCellUsed()?.Address.ColumnNumber ?? 0;
        if (lastCol == 0)
            return Array.Empty<string>();

        var cells = new List<string>();
        for (var col = 1; col <= lastCol; col++)
            cells.Add(row.Cell(col).GetFormattedString().Trim());
        return cells;
    }

    private static ParsedImportFile ParseCsv(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var lines = new List<string>();
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (line is not null)
                lines.Add(line);
        }

        if (lines.Count == 0)
            throw new InvalidOperationException("Le fichier CSV est vide.");

        var delimiter = DetectDelimiter(lines[0]);
        var headers = ParseCsvLine(lines[0], delimiter);
        var dataRows = lines.Skip(1)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => (IReadOnlyList<string>)ParseCsvLine(l, delimiter))
            .ToList();

        return new ParsedImportFile(headers, dataRows);
    }

    private static char DetectDelimiter(string line)
    {
        var semicolons = line.Count(c => c == ';');
        var commas = line.Count(c => c == ',');
        return semicolons > commas ? ';' : ',';
    }

    private static List<string> ParseCsvLine(string line, char delimiter)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
                continue;
            }

            if (c == delimiter && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        result.Add(current.ToString().Trim());
        return result;
    }
}

public sealed record ParsedImportFile(
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows);

public static class EmployeeImportRowMapper
{
    public static Dictionary<string, string?> MapRow(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<int, string> columnToField)
    {
        var mapped = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (columnIndex, fieldKey) in columnToField)
        {
            if (columnIndex < 0 || columnIndex >= row.Count)
                continue;

            var value = row[columnIndex]?.Trim();
            if (string.IsNullOrWhiteSpace(value))
                continue;

            mapped[fieldKey] = value;
        }

        return mapped;
    }

    public static bool TryParseBool(string? value, out bool result)
    {
        result = true;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var v = value.Trim().ToLowerInvariant();
        if (v is "false" or "0" or "non" or "no" or "inactif" or "inactive")
        {
            result = false;
            return true;
        }

        if (v is "true" or "1" or "oui" or "yes" or "actif" or "active")
        {
            result = true;
            return true;
        }

        return bool.TryParse(value, out result);
    }

    public static bool TryParseDate(string? value, out DateTime result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var formats = new[]
        {
            "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "MM/dd/yyyy",
            "dd-MM-yyyy", "d-M-yyyy"
        };

        if (DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result))
            return true;

        return DateTime.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result);
    }
}
