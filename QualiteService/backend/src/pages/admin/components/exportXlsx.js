import * as XLSX from "xlsx";

/**
 * exportToXlsx(filename, data)
 * - data can be:
 *   - Array<object>  -> single sheet named "Export"
 *   - Object<string, Array<object>> -> multiple sheets (key = sheet name)
 */
export function exportToXlsx(filename, data) {
  const wb = XLSX.utils.book_new();

  const addSheet = (name, rows) => {
    const safeRows = Array.isArray(rows) ? rows : [];
    const ws = XLSX.utils.json_to_sheet(safeRows);
    XLSX.utils.book_append_sheet(wb, ws, String(name || "Sheet"));
  };

  if (Array.isArray(data)) {
    addSheet("Export", data);
  } else if (data && typeof data === "object") {
    const entries = Object.entries(data);
    if (entries.length === 0) addSheet("Export", []);
    else entries.forEach(([sheetName, rows]) => addSheet(sheetName, rows));
  } else {
    addSheet("Export", []);
  }

  XLSX.writeFile(wb, filename);
}
