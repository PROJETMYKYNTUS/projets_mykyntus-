import React, { useEffect, useMemo, useState } from "react";
import api from "../api.js";

/**
 * NewEvaluationModal
 * Minimal, robust evaluation creator for CQ & Management.
 *
 * - Fetches grids from /grids/my
 * - Expects pilots list via props (array of {value,label})
 */
function normISODate(v) {
  if (!v) return "";
  const d = new Date(v);
  if (isNaN(d.getTime())) return "";
  const yyyy = d.getFullYear();
  const mm = String(d.getMonth() + 1).padStart(2, "0");
  const dd = String(d.getDate()).padStart(2, "0");
  return `${yyyy}-${mm}-${dd}`;
}

const STATUS_OPTIONS = [
  { value: "C", label: "Conforme (C)" },
  { value: "NC", label: "Non conforme (NC)" },
  { value: "NA", label: "Non applicable (NA)" },
];

export default function NewEvaluationModal({ open, onClose, pilotOptions }) {
  const [loading, setLoading] = useState(false);
  const [err, setErr] = useState("");

  const [grids, setGrids] = useState([]);
  const [gridId, setGridId] = useState("");
  const [pilotId, setPilotId] = useState("");
  const [eps, setEps] = useState("");
  const [interactionDate, setInteractionDate] = useState(normISODate(new Date()));
  const [pickingPrime, setPickingPrime] = useState(false);

  // statusByLabel: { [label]: "C"|"NC"|"NA" }
  const [statusByLabel, setStatusByLabel] = useState({});

  useEffect(() => {
    if (!open) return;
    let mounted = true;
    (async () => {
      try {
        const res = await api.get("/grids/my");
        if (!mounted) return;
        setGrids(res.data || []);
      } catch (e) {
        if (!mounted) return;
        setErr(e?.response?.data?.message || "Impossible de charger les grilles.");
      }
    })();
    return () => { mounted = false; };
  }, [open]);

  const selectedGrid = useMemo(() => {
    return (grids || []).find((g) => String(g._id) === String(gridId));
  }, [grids, gridId]);

  const gridItems = useMemo(() => {
    const items = Array.isArray(selectedGrid?.items) ? selectedGrid.items : [];
    return items
      .filter((it) => it && it.type !== "group")
      .map((it) => ({ label: (it.label || "").toString().trim() }))
      .filter((it) => it.label);
  }, [selectedGrid]);

  useEffect(() => {
    // Init statuses when grid changes
    if (!open) return;
    const next = {};
    for (const it of gridItems) {
      next[it.label] = statusByLabel[it.label] || "C";
    }
    setStatusByLabel(next);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [gridId]);

  async function onSubmit(e) {
    e.preventDefault();
    setErr("");

    if (!pilotId) return setErr("Veuillez choisir un agent.");
    if (!gridId) return setErr("Veuillez choisir une grille.");
    if (!eps.trim()) return setErr("Veuillez renseigner l'EPS.");

    const items = gridItems.map((it) => ({
      label: it.label,
      status: statusByLabel[it.label] || "C",
    }));

    if (!items.length) return setErr("Cette grille ne contient aucun item évaluable.");

    setLoading(true);
    try {
      await api.post("/scores", {
        pilotId,
        gridId,
        eps: eps.trim(),
        pickingPrime: !!pickingPrime,
        interactionDate: interactionDate ? new Date(interactionDate).toISOString() : null,
        items,
      });
      onClose?.(true);
      // reset minimal
      setEps("");
      setPickingPrime(false);
    } catch (e2) {
      setErr(e2?.response?.data?.message || "Erreur lors de la création de l'évaluation.");
    } finally {
      setLoading(false);
    }
  }

  if (!open) return null;

  return (
    <div
      role="dialog"
      aria-modal="true"
      onClick={() => onClose?.(false)}
      style={{
        position: "fixed",
        inset: 0,
        background: "rgba(15,23,42,0.45)",
        zIndex: 9999,
        display: "grid",
        placeItems: "center",
        padding: 18,
      }}
    >
      <div
        className="card"
        onClick={(e) => e.stopPropagation()}
        style={{
          width: "min(980px, 96vw)",
          maxHeight: "92vh",
          overflow: "auto",
          padding: 16,
        }}
      >
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 12, marginBottom: 10 }}>
          <div style={{ fontWeight: 900, fontSize: 16 }}>Nouvelle évaluation</div>
          <button type="button" className="btn btn--ghost" onClick={() => onClose?.(false)}>
            Fermer
          </button>
        </div>

        <form onSubmit={onSubmit} style={{ display: "grid", gridTemplateColumns: "repeat(2, minmax(0, 1fr))", gap: 12 }}>
          <div>
            <div className="label">Agent</div>
            <select className="input" value={pilotId} onChange={(e) => setPilotId(e.target.value)}>
              <option value="">Choisir un agent</option>
              {(pilotOptions || []).map((p) => (
                <option key={p.value} value={p.value}>{p.label}</option>
              ))}
            </select>
          </div>

          <div>
            <div className="label">Grille</div>
            <select className="input" value={gridId} onChange={(e) => setGridId(e.target.value)}>
              <option value="">Choisir une grille</option>
              {(grids || []).map((g) => (
                <option key={String(g._id)} value={String(g._id)}>{g.name || g.title || "Grille"}</option>
              ))}
            </select>
          </div>

          <div>
            <div className="label">EPS</div>
            <input className="input" value={eps} onChange={(e) => setEps(e.target.value)} placeholder="EPS-123..." />
          </div>

          <div>
            <div className="label">Date</div>
            <input className="input" type="date" value={interactionDate} onChange={(e) => setInteractionDate(e.target.value)} />
          </div>

          <div style={{ gridColumn: "1 / -1", display: "flex", alignItems: "center", gap: 10 }}>
            <input id="pp" type="checkbox" checked={pickingPrime} onChange={(e) => setPickingPrime(e.target.checked)} />
            <label htmlFor="pp" style={{ fontSize: 14 }}>Picking prime</label>
          </div>

          <div style={{ gridColumn: "1 / -1" }}>
            <div className="label" style={{ marginBottom: 8 }}>Items</div>
            {!gridId ? (
              <div style={{ color: "rgba(30,41,59,0.7)", fontSize: 13 }}>Choisissez une grille pour voir ses items.</div>
            ) : (
              <div style={{ border: "1px solid var(--border)", borderRadius: 12, overflow: "hidden" }}>
                <table style={{ width: "100%", borderCollapse: "collapse" }}>
                  <thead>
                    <tr style={{ textAlign: "left", background: "rgba(148,163,184,0.12)" }}>
                      <th style={{ padding: "10px 10px", borderBottom: "1px solid var(--border)" }}>Critère</th>
                      <th style={{ padding: "10px 10px", borderBottom: "1px solid var(--border)", width: 220 }}>Statut</th>
                    </tr>
                  </thead>
                  <tbody>
                    {gridItems.map((it) => (
                      <tr key={it.label}>
                        <td style={{ padding: "10px 10px", borderBottom: "1px solid rgba(148,163,184,0.25)" }}>{it.label}</td>
                        <td style={{ padding: "10px 10px", borderBottom: "1px solid rgba(148,163,184,0.25)" }}>
                          <select
                            className="input"
                            value={statusByLabel[it.label] || "C"}
                            onChange={(e) => setStatusByLabel((m) => ({ ...m, [it.label]: e.target.value }))}
                          >
                            {STATUS_OPTIONS.map((o) => (
                              <option key={o.value} value={o.value}>{o.label}</option>
                            ))}
                          </select>
                        </td>
                      </tr>
                    ))}
                    {gridItems.length === 0 ? (
                      <tr><td colSpan={2} style={{ padding: 12, color: "rgba(30,41,59,0.7)" }}>Aucun item.</td></tr>
                    ) : null}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {err ? (
            <div className="card" style={{ gridColumn: "1 / -1", border: "1px solid rgba(239,68,68,0.35)", padding: 10, color: "#b91c1c" }}>
              {err}
            </div>
          ) : null}

          <div style={{ gridColumn: "1 / -1", display: "flex", justifyContent: "flex-end", gap: 10 }}>
            <button type="button" className="btn btn--ghost" onClick={() => onClose?.(false)} disabled={loading}>Annuler</button>
            <button type="submit" className="btn" disabled={loading}>
              {loading ? "Création…" : "Créer l'évaluation"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
