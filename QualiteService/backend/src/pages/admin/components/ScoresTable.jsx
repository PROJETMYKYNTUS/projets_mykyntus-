import React from "react";

const thStyle = {
  textAlign: "left",
  padding: "0.65rem",
  fontSize: "0.85rem",
  opacity: 0.85,
  whiteSpace: "nowrap",
};

const tdStyle = { padding: "0.6rem", borderTop: "1px solid #e5e7eb", verticalAlign: "top" };

const fmtDate = (d) => {
  if (!d) return "";
  const dt = new Date(d);
  if (Number.isNaN(dt.getTime())) return String(d);
  return dt.toLocaleString("fr-FR");
};

export default function ScoresTable({ rows, onEdit }) {
  return (
    <div style={{ overflowX: "auto" }}>
      <table style={{ width: "100%", borderCollapse: "collapse" }}>
        <thead>
          <tr>
            {["Date", "Pilote", "Cellule", "Évaluateur", "EPS", "Picking prime", "Score", "Statut", "Commentaire", "Actions"].map(
              (h) => (
                <th key={h} style={thStyle}>
                  {h}
                </th>
              )
            )}
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r._id || r.__raw?._id || `${r.kind}-${r.date}-${r.pilot}-${Math.random()}`}>
              <td style={tdStyle}>{fmtDate(r.date)}</td>
              <td style={tdStyle}>{r.pilot}</td>
              <td style={tdStyle}>{r.cell}</td>
              <td style={tdStyle}>{r.evaluator}</td>
              <td style={tdStyle}>{r.eps || "—"}</td>
              <td style={tdStyle}>{r.pickingPrime ? "Vrai" : "Faux"}</td>
              <td style={{ ...tdStyle, fontWeight: 900 }}>{Number(r.score || 0)}%</td>
              <td style={tdStyle}>{r.status || ""}</td>
              <td style={tdStyle}>{r.comment || "-"}</td>
              <td style={tdStyle}>
                <button
                  className="btn-outline"
                  onClick={() => onEdit?.(r.__raw)}
                  style={{ padding: "0.35rem 0.7rem" }}
                >
                  Éditer notation
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
