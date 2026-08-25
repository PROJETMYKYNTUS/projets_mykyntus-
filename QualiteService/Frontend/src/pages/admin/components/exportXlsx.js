import * as XLSX from "xlsx";
import { toast } from "../../../toast/toastBus.js";

/**
 * exportToXlsx(filename, data)
 * - data can be:
 *   - Array<object>  -> single sheet named "Export"
 *   - Object<string, Array<object>> -> multiple sheets (key = sheet name)
 */
export function exportToXlsx(filename, data) {
  try {
    toast.info("Export en cours…", { durationMs: 1600 });
  } catch {
    // no-op if toast isn't ready
  }

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
  try {
    toast.success("Export terminé");
  } catch {
    // no-op
  }
}
