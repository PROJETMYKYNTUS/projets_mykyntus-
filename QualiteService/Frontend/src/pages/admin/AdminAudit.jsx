import React, { useEffect, useMemo, useState } from "react";
import api from "../../api";
import { exportToXlsx } from "./components/exportXlsx.js";
import { fetchAllPaged } from "../../utils/fetchAllPaged.js";

const PAGE_SIZE = 25;

const ACTION_LABELS = {
  CONTEST: { label: "Contestation", icon: "⚠", color: "var(--warning)" },
  REEVALUATE: { label: "Réévaluation", icon: "🔄", color: "var(--primary)" },
  DELETE_EVALUATION: { label: "Suppression", icon: "🗑", color: "var(--danger)" },
  UPDATE_EVALUATION: { label: "Modification", icon: "✏", color: "var(--text-secondary)" },
};

function fmtAction(a) { return ACTION_LABELS[a] || { label: a, icon: "📝", color: "var(--muted)" }; }

function fmtDetails(meta) {
  if (!meta || !Object.keys(meta).length) return "—";
  const parts = [];
  if (meta.pilotName) parts.push(`Agent: ${meta.pilotName}`);
  if (meta.eps) parts.push(`EPS: ${meta.eps}`);
  if (meta.by === "cq") parts.push("Par: CQ");
  if (meta.by === "management") parts.push("Par: Management");
  if (meta.contestComment) parts.push(`Motif: ${meta.contestComment}`);
  if (meta.pilotId && !meta.pilotName) parts.push(`Pilote ID: …${String(meta.pilotId).slice(-6)}`);
  return parts.length ? parts.join(" • ") : "—";
}

export default function AdminAudit() {
  const [items, setItems] = useState([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [action, setAction] = useState("");
  const [loading, setLoading] = useState(true);

  async function load() {
    setLoading(true);
    try {
      const res = await api.get("/admin/audit", { params: { page, limit: PAGE_SIZE, action: action || undefined } });
      setItems(res.data.items || []);
      setTotal(Number(res.data.total || 0));
    } catch {} finally { setLoading(false); }
  }

  useEffect(() => { load(); }, [page, action]);

  const pages = useMemo(() => Math.max(1, Math.ceil(total / PAGE_SIZE)), [total]);

  async function doExport() {
    const all = await fetchAllPaged("/admin/audit", { action: action || undefined }, { pageSize: 200, maxPages: 200 });
    exportToXlsx(`audit_${new Date().toISOString().slice(0, 10)}.xlsx`, (all || []).map((it) => ({
      Date: it.createdAt ? new Date(it.createdAt).toLocaleString("fr-FR") : "",
      Action: fmtAction(it.action).label,
      Acteur: it.actor?.name || "",
      "Rôle acteur": it.actor?.role || "",
      Agent: it.metadata?.pilotName || "",
      EPS: it.metadata?.eps || "",
      Détails: fmtDetails(it.metadata),
    })));
  }

  return (
    <div className="page">
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 16 }}>
        <div>
          <div className="cq-dup-title">
          <div style={{ fontSize: "1.25rem", fontWeight: 800 }}>Journal d'audit</div>
          <div style={{ color: "var(--muted)", fontSize: "0.875rem", marginTop: 2 }}>Traçabilité des actions : contestations, modifications, suppressions</div>
          </div>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <select className="input" value={action} onChange={(e) => { setPage(1); setAction(e.target.value); }} style={{ maxWidth: 200 }}>
            <option value="">Toutes les actions</option>
            <option value="CONTEST">Contestation</option>
            <option value="REEVALUATE">Réévaluation</option>
            <option value="DELETE_EVALUATION">Suppression</option>
            <option value="UPDATE_EVALUATION">Modification</option>
          </select>
          <button className="btn btn--ghost" onClick={doExport} disabled={loading}>Exporter Excel</button>
          <button className="btn btn--ghost" onClick={load} disabled={loading}>↻</button>
        </div>
      </div>

      <div className="card" style={{ padding: 0, overflow: "hidden" }}>
        <div style={{ padding: "12px 16px", borderBottom: "1px solid var(--border)", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <span style={{ fontWeight: 700, fontSize: "0.9rem" }}>Événements</span>
          <span style={{ color: "var(--muted)", fontSize: "0.8rem", fontWeight: 600 }}>{total} entrée{total > 1 ? "s" : ""}</span>
        </div>
        <div style={{ overflow: "auto" }}>
          <table className="data-table">
            <thead>
              <tr>
                <th style={{ width: 150 }}>Date</th>
                <th style={{ width: 140 }}>Action</th>
                <th>Acteur</th>
                <th>Agent concerné</th>
                <th>EPS</th>
                <th>Détails</th>
              </tr>
            </thead>
            <tbody>
              {items.map((it) => {
                const act = fmtAction(it.action);
                return (
                  <tr key={it._id}>
                    <td style={{ fontSize: "0.82rem" }}>{it.createdAt ? new Date(it.createdAt).toLocaleString("fr-FR") : "—"}</td>
                    <td>
                      <span style={{ display: "inline-flex", alignItems: "center", gap: 6, fontWeight: 700, fontSize: "0.85rem", color: act.color }}>
                        <span>{act.icon}</span> {act.label}
                      </span>
                    </td>
                    <td>
                      <div style={{ fontWeight: 600, fontSize: "0.85rem" }}>{it.actor?.name || "—"}</div>
                      <div style={{ fontSize: "0.72rem", color: "var(--muted)" }}>{it.actor?.role || ""}</div>
                    </td>
                    <td style={{ fontWeight: 600, fontSize: "0.85rem" }}>{it.metadata?.pilotName || "—"}</td>
                    <td style={{ fontFamily: "monospace", fontSize: "0.82rem" }}>{it.metadata?.eps || "—"}</td>
                    <td style={{ fontSize: "0.82rem", color: "var(--text-secondary)", maxWidth: 250 }}>
                      {fmtDetails(it.metadata)}
                    </td>
                  </tr>
                );
              })}
              {items.length === 0 && <tr><td colSpan={6} style={{ textAlign: "center", color: "var(--muted)", padding: 24 }}>Aucun événement.</td></tr>}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        <div style={{ padding: "10px 16px", borderTop: "1px solid var(--border)", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <button className="btn btn--ghost btn--sm" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>← Précédent</button>
          <span style={{ fontSize: "0.82rem", color: "var(--muted)" }}>Page {page} / {pages}</span>
          <button className="btn btn--ghost btn--sm" disabled={page >= pages} onClick={() => setPage((p) => p + 1)}>Suivant →</button>
        </div>
      </div>
    </div>
  );
}
