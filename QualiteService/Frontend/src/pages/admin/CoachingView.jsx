import React, { useEffect, useMemo, useState } from "react";
import api from "../../api";
import { exportToXlsx } from "./components/exportXlsx.js";

const fmtDate = (d) => { if (!d) return "—"; const dt = new Date(d); return isNaN(dt.getTime()) ? "—" : dt.toLocaleDateString("fr-FR"); };
const fmtFull = (d) => { if (!d) return "—"; const dt = new Date(d); return isNaN(dt.getTime()) ? "—" : dt.toLocaleString("fr-FR"); };
const statusLabel = (s) => s === "open" ? "Ouvert" : s === "in_progress" ? "En cours" : s === "done" ? "Clôturé" : s || "";
const isOverdue = (d, status) => d && status !== "done" && new Date(d) < new Date(new Date().toDateString());

export default function CoachingView() {
  const [items, setItems] = useState([]);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [filterStatus, setFilterStatus] = useState("");
  const [loading, setLoading] = useState(true);
  const [detail, setDetail] = useState(null);
  const pageSize = 50;
  const maxPage = useMemo(() => Math.max(1, Math.ceil(total / pageSize)), [total]);

  async function fetchAll(p = 1) {
    setLoading(true);
    try {
      const res = await api.get("/coaching", { params: { page: p, limit: pageSize, status: filterStatus || undefined } });
      setItems(res.data?.items || []);
      setTotal(Number(res.data?.total || 0));
      setPage(Number(res.data?.page || p));
    } catch { setItems([]); setTotal(0); }
    finally { setLoading(false); }
  }

  useEffect(() => { fetchAll(1); }, [filterStatus]);

  async function handleDelete(id) {
    if (!window.confirm("Supprimer ce coaching ?")) return;
    try { await api.delete(`/coaching/${id}`); fetchAll(page); } catch {}
  }

  function doExport() {
    exportToXlsx(`Coachings_${new Date().toISOString().slice(0, 10)}.xlsx`, items.map((c) => ({
      Date: fmtFull(c.createdAt),
      Coach: c.coach?.name || "",
      Agent: c.pilot?.name || c.score?.pilot?.name || "",
      EPS: c.score?.eps || "",
      Statut: statusLabel(c.status),
      Relance: fmtDate(c.followUpDate),
      Notes: c.notes || "",
      "Plan d'action": c.actionPlan || "",
      "Validé par agent": c.pilotAcknowledged ? "Oui" : "Non",
      "Date validation": c.pilotAcknowledgedAt ? fmtFull(c.pilotAcknowledgedAt) : "",
      "Commentaire agent": c.pilotComment || "",
    })));
  }

  // KPIs
  const kpis = useMemo(() => ({
    total: items.length,
    open: items.filter((c) => c.status === "open").length,
    inProgress: items.filter((c) => c.status === "in_progress").length,
    done: items.filter((c) => c.status === "done").length,
    overdue: items.filter((c) => isOverdue(c.followUpDate, c.status)).length,
    ackPending: items.filter((c) => !c.pilotAcknowledged).length,
    ackDone: items.filter((c) => c.pilotAcknowledged).length,
  }), [items]);

  return (
    <div className="page">
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 16 }}>
        <div>
          <div className="cq-dup-title">
          <div style={{ fontSize: "1.25rem", fontWeight: 800 }}>Coaching — Supervision</div>
          <div style={{ color: "var(--muted)", fontSize: "0.875rem", marginTop: 2 }}>Vue complète des coachings, validations agents et plans d'action</div>
          </div>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <select className="input" value={filterStatus} onChange={(e) => setFilterStatus(e.target.value)} style={{ maxWidth: 160 }}>
            <option value="">Tous les statuts</option>
            <option value="open">Ouvert</option>
            <option value="in_progress">En cours</option>
            <option value="done">Clôturé</option>
          </select>
          <button className="btn" onClick={doExport} disabled={!items.length}>📥 Excel</button>
          <button className="btn btn--ghost" onClick={() => fetchAll(page)} disabled={loading}>↻</button>
        </div>
      </div>

      {/* KPIs */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(130px, 1fr))", gap: 10, marginBottom: 16 }}>
        {[
          { l: "Total", v: total, c: "var(--text)" },
          { l: "Ouverts", v: kpis.open, c: "var(--warning)" },
          { l: "En cours", v: kpis.inProgress, c: "var(--primary)" },
          { l: "Clôturés", v: kpis.done, c: "var(--success)" },
          { l: "En retard", v: kpis.overdue, c: "var(--danger)" },
          { l: "Validés agent", v: kpis.ackDone, c: "var(--success)" },
          { l: "En attente validation", v: kpis.ackPending, c: "var(--warning)" },
        ].map((k) => (
          <div key={k.l} className="card" style={{ padding: "10px 12px" }}>
            <div style={{ fontSize: "0.68rem", fontWeight: 700, color: "var(--muted)", textTransform: "uppercase" }}>{k.l}</div>
            <div style={{ fontSize: "1.3rem", fontWeight: 800, marginTop: 2, color: k.c }}>{k.v}</div>
          </div>
        ))}
      </div>

      {/* Table */}
      <div className="card" style={{ padding: 0, overflow: "hidden" }}>
        <div style={{ overflow: "auto" }}>
          <table className="data-table">
            <thead>
              <tr>
                <th>Date</th>
                <th>Coach</th>
                <th>Agent</th>
                <th>EPS</th>
                <th>Statut</th>
                <th>Validation agent</th>
                <th>Relance</th>
                <th>Plan d'action</th>
                <th style={{ textAlign: "right" }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {items.map((c) => {
                const overdue = isOverdue(c.followUpDate, c.status);
                return (
                  <tr key={c._id}>
                    <td style={{ fontSize: "0.82rem" }}>{fmtFull(c.createdAt)}</td>
                    <td style={{ fontWeight: 600 }}>{c.coach?.name || "—"}</td>
                    <td style={{ fontWeight: 600 }}>{c.pilot?.name || c.score?.pilot?.name || "—"}</td>
                    <td style={{ fontFamily: "monospace", fontSize: "0.82rem" }}>{c.score?.eps || "—"}</td>
                    <td>
                      <span className={`badge ${c.status === "done" ? "badge--success" : c.status === "in_progress" ? "badge--primary" : "badge--warning"}`}>
                        {statusLabel(c.status)}
                      </span>
                    </td>
                    <td>
                      {c.pilotAcknowledged ? (
                        <div>
                          <span className="badge badge--success" style={{ fontSize: "0.72rem" }}>✅ Validé</span>
                          {c.pilotComment && <div style={{ fontSize: "0.72rem", color: "var(--text-secondary)", marginTop: 2, maxWidth: 140, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }} title={c.pilotComment}>💬 {c.pilotComment}</div>}
                        </div>
                      ) : (
                        <span className="badge badge--warning" style={{ fontSize: "0.72rem" }}>⏳ En attente</span>
                      )}
                    </td>
                    <td>
                      {c.followUpDate ? (
                        <span className={`badge ${overdue ? "badge--danger" : "badge--muted"}`} style={{ fontSize: "0.72rem" }}>
                          {overdue ? "⏰ " : ""}{fmtDate(c.followUpDate)}
                        </span>
                      ) : "—"}
                    </td>
                    <td style={{ maxWidth: 160, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", fontSize: "0.82rem" }} title={c.actionPlan}>{c.actionPlan || "—"}</td>
                    <td style={{ textAlign: "right" }}>
                      <div style={{ display: "flex", gap: 4, justifyContent: "flex-end" }}>
                        <button className="btn btn--ghost btn--sm" onClick={() => setDetail(c)}>Détail</button>
                        <button className="btn btn--ghost btn--sm" style={{ color: "var(--danger)" }} onClick={() => handleDelete(c._id)}>Suppr.</button>
                      </div>
                    </td>
                  </tr>
                );
              })}
              {items.length === 0 && <tr><td colSpan={9} style={{ textAlign: "center", color: "var(--muted)", padding: 24 }}>Aucun coaching.</td></tr>}
            </tbody>
          </table>
        </div>

        <div style={{ padding: "10px 16px", borderTop: "1px solid var(--border)", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <button className="btn btn--ghost btn--sm" disabled={page <= 1} onClick={() => fetchAll(page - 1)}>← Préc.</button>
          <span style={{ fontSize: "0.82rem", color: "var(--muted)" }}>Page {page} / {maxPage} — {total} coachings</span>
          <button className="btn btn--ghost btn--sm" disabled={page >= maxPage} onClick={() => fetchAll(page + 1)}>Suiv. →</button>
        </div>
      </div>

      {/* Detail Modal */}
      {detail && (
        <div className="modal-overlay" onClick={() => setDetail(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()} style={{ maxWidth: 640, maxHeight: "90vh", overflow: "auto" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 16 }}>
              <div style={{ fontWeight: 800, fontSize: "1.05rem" }}>Détail coaching</div>
              <button className="btn btn--ghost btn--sm" onClick={() => setDetail(null)}>Fermer</button>
            </div>

            {/* Info grid */}
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 10, marginBottom: 16 }}>
              {[
                { k: "Coach", v: detail.coach?.name || "—" },
                { k: "Agent", v: detail.pilot?.name || detail.score?.pilot?.name || "—" },
                { k: "EPS", v: detail.score?.eps || "—" },
                { k: "Statut", v: statusLabel(detail.status) },
                { k: "Créé le", v: fmtFull(detail.createdAt) },
                { k: "Relance", v: fmtDate(detail.followUpDate) },
              ].map((x) => (
                <div key={x.k} style={{ padding: "8px 10px", borderRadius: 8, border: "1px solid var(--border)", background: "var(--panel-2)" }}>
                  <div style={{ fontSize: "0.7rem", fontWeight: 700, color: "var(--muted)", textTransform: "uppercase" }}>{x.k}</div>
                  <div style={{ fontWeight: 700, fontSize: "0.875rem", marginTop: 2 }}>{x.v}</div>
                </div>
              ))}
            </div>

            {/* Notes */}
            {detail.notes && (
              <div style={{ marginBottom: 12 }}>
                <div style={{ fontWeight: 700, fontSize: "0.85rem", marginBottom: 4 }}>📋 Observations du coach</div>
                <div style={{ padding: "10px 12px", borderRadius: 10, border: "1px solid var(--border)", background: "var(--panel-2)", fontSize: "0.85rem", whiteSpace: "pre-wrap", lineHeight: 1.6 }}>{detail.notes}</div>
              </div>
            )}

            {/* Action plan */}
            {detail.actionPlan && (
              <div style={{ marginBottom: 12 }}>
                <div style={{ fontWeight: 700, fontSize: "0.85rem", marginBottom: 4 }}>🎯 Plan d'action</div>
                <div style={{ padding: "10px 12px", borderRadius: 10, border: "1px solid var(--primary-border)", background: "var(--primary-bg)", fontSize: "0.85rem", whiteSpace: "pre-wrap", lineHeight: 1.6 }}>{detail.actionPlan}</div>
              </div>
            )}

            {/* Pilot acknowledgment */}
            <div style={{ padding: "14px 16px", borderRadius: 12, border: "1px solid var(--border)", background: detail.pilotAcknowledged ? "var(--success-bg)" : "var(--warning-bg)" }}>
              <div style={{ fontWeight: 800, fontSize: "0.9rem", marginBottom: 6 }}>
                {detail.pilotAcknowledged ? "✅ Validé par l'agent" : "⏳ En attente de validation agent"}
              </div>
              {detail.pilotAcknowledged ? (
                <>
                  <div style={{ fontSize: "0.82rem", color: "var(--muted)", marginBottom: 6 }}>
                    Validé le {fmtFull(detail.pilotAcknowledgedAt)}
                  </div>
                  {detail.pilotComment ? (
                    <div style={{ padding: "10px 12px", borderRadius: 8, background: "var(--panel)", border: "1px solid var(--border)", fontSize: "0.875rem", whiteSpace: "pre-wrap", lineHeight: 1.6 }}>
                      <div style={{ fontWeight: 700, fontSize: "0.78rem", color: "var(--muted)", textTransform: "uppercase", marginBottom: 4 }}>Commentaire de l'agent</div>
                      {detail.pilotComment}
                    </div>
                  ) : (
                    <div style={{ fontSize: "0.82rem", color: "var(--muted)", fontStyle: "italic" }}>Aucun commentaire.</div>
                  )}
                </>
              ) : (
                <div style={{ fontSize: "0.82rem", color: "var(--muted)" }}>L'agent n'a pas encore pris connaissance et validé ce coaching.</div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
