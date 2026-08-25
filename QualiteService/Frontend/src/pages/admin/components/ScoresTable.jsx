import React from "react";

const fmtDate = (d) => {
  if (!d) return "";
  const dt = new Date(d);
  if (Number.isNaN(dt.getTime())) return String(d);
  return dt.toLocaleDateString("fr-FR");
};

function scoreBadge(score) {
  const n = Number(score || 0);
  if (n >= 80) return "badge badge--success";
  if (n >= 50) return "badge badge--warning";
  return "badge badge--danger";
}

export default function ScoresTable({ rows, onEdit, onDelete }) {
  return (
    <div style={{ overflowX: "auto" }}>
      <table className="data-table">
        <thead>
          <tr>
            {["Date", "Pilote", "Cellule", "Évaluateur", "EPS", "Prime", "Score", "Statut", "Commentaire", "Actions"].map((h) => (
              <th key={h}>{h}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r._id || r.__raw?._id || `${r.kind}-${r.date}-${r.pilot}-${Math.random()}`}>
              <td>{fmtDate(r.date)}</td>
              <td style={{ fontWeight: 600 }}>{r.pilot}</td>
              <td><span className="badge badge--muted">{r.cell || "—"}</span></td>
              <td>{r.evaluator}</td>
              <td style={{ fontFamily: "monospace", fontSize: "0.8rem" }}>{r.eps || "—"}</td>
              <td>{r.pickingPrime ? <span className="badge badge--primary">Oui</span> : <span style={{ color: "var(--muted)", fontSize: "0.8rem" }}>Non</span>}</td>
              <td><span className={scoreBadge(r.score)}>{Number(r.score || 0)}%</span></td>
              <td>{r.status ? <span className="badge badge--warning">{r.status}</span> : ""}</td>
              <td style={{ maxWidth: 200, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{r.comment || "—"}</td>
              <td>
                <div style={{ display: "flex", gap: 6 }}>
                  <button className="btn-outline btn--sm" onClick={() => onEdit?.(r.__raw)} style={{ padding: "0.3rem 0.6rem", fontSize: "0.8rem" }}>
                    Éditer
                  </button>
                  <button className="btn-danger btn--sm" onClick={() => onDelete?.(r.__raw)} style={{ padding: "0.3rem 0.6rem", fontSize: "0.8rem" }}>
                    Suppr.
                  </button>
                </div>
              </td>
            </tr>
          ))}
          {rows.length === 0 && (
            <tr><td colSpan={10} className="empty-state"><div className="empty-state__text">Aucun résultat.</div></td></tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
