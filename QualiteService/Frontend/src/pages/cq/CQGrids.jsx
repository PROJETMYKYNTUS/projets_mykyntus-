import React, { useEffect, useState, useMemo } from "react";
import api from "../../api";
import PageHeader from "../../components/PageHeader.jsx";

const uid = () => Math.random().toString(36).slice(2);

export default function CQGrids() {
  const [grids, setGrids] = useState([]);
  const [proposals, setProposals] = useState([]);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState("");
  const [ok, setOk] = useState("");

  // Creation form
  const [creating, setCreating] = useState(false);
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [gridType, setGridType] = useState("classic");
  const [items, setItems] = useState([
    { _lid: uid(), type: "group", title: "Phase 1", hardFail: false, malusPercent: 0 },
    { _lid: uid(), type: "item", label: "Critère 1", pointsConforme: 1, pointsNonConforme: 0, malusPercent: 0 },
  ]);
  const [saving, setSaving] = useState(false);

  async function loadData() {
    setLoading(true);
    try {
      const [gridsR, proposalsR] = await Promise.all([
        api.get("/grids/my"),
        api.get("/grids/my-proposals").catch(() => ({ data: [] })),
      ]);
      setGrids(Array.isArray(gridsR.data) ? gridsR.data : []);
      setProposals(Array.isArray(proposalsR.data) ? proposalsR.data : []);
    } catch {} finally { setLoading(false); }
  }

  useEffect(() => { loadData(); }, []);

  function addGroup() { setItems((prev) => [...prev, { _lid: uid(), type: "group", title: "", hardFail: false, malusPercent: 0 }]); }
  function addItem() { setItems((prev) => [...prev, { _lid: uid(), type: "item", label: "", pointsConforme: 1, pointsNonConforme: 0, malusPercent: 0 }]); }
  function removeItem(lid) { setItems((prev) => prev.filter((it) => it._lid !== lid)); }
  function updateItem(lid, key, value) { setItems((prev) => prev.map((it) => it._lid === lid ? { ...it, [key]: value } : it)); }

  async function handlePropose() {
    setErr(""); setOk("");
    if (!name.trim()) return setErr("Le nom est obligatoire.");
    const realItems = items.filter((it) => it.type === "group" ? it.title?.trim() : it.label?.trim());
    if (!realItems.some((it) => it.type === "item")) return setErr("Ajoutez au moins un critère.");
    setSaving(true);
    try {
      await api.post("/grids/propose", {
        name: name.trim(),
        description: description.trim(),
        gridType,
        items: realItems.map((it, idx) => ({
          type: it.type,
          title: it.title || "",
          label: it.label || "",
          hardFail: !!it.hardFail,
          malusPercent: Number(it.malusPercent) || 0,
          pointsConforme: Number(it.pointsConforme) || 1,
          pointsNonConforme: Number(it.pointsNonConforme) || 0,
          order: idx,
        })),
      }, { toast: false });
      setOk("Grille proposée avec succès. Elle sera visible après validation par le management.");
      setCreating(false);
      setName(""); setDescription(""); setGridType("classic");
      setItems([
        { _lid: uid(), type: "group", title: "Phase 1", hardFail: false, malusPercent: 0 },
        { _lid: uid(), type: "item", label: "Critère 1", pointsConforme: 1, pointsNonConforme: 0, malusPercent: 0 },
      ]);
      loadData();
    } catch (e) { setErr(e?.response?.data?.message || "Erreur lors de la proposition."); }
    finally { setSaving(false); }
  }

  return (
    <div className="page">
      <PageHeader title="Grilles d'évaluation" subtitle="Consultez vos grilles actives et proposez-en de nouvelles." />

      <div style={{ display: "flex", justifyContent: "flex-end", marginBottom: 12 }}>
        <button className="btn" onClick={() => { setCreating(true); setErr(""); setOk(""); }}>+ Proposer une grille</button>
      </div>

      {err && <div className="card" style={{ padding: 12, color: "var(--danger)", background: "var(--danger-bg)", borderColor: "rgba(220,38,38,0.2)", marginBottom: 12, fontWeight: 600, fontSize: "0.875rem" }}>{err}</div>}
      {ok && <div className="card" style={{ padding: 12, color: "var(--success)", background: "var(--success-bg)", borderColor: "rgba(5,150,105,0.2)", marginBottom: 12, fontWeight: 600, fontSize: "0.875rem" }}>{ok}</div>}

      {/* Pending proposals */}
      {proposals.length > 0 && (
        <div className="card" style={{ padding: 16, marginBottom: 12 }}>
          <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 10, display: "flex", alignItems: "center", gap: 8 }}>
            <span>⏳</span> Grilles en attente de validation
          </div>
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {proposals.map((g) => (
              <div key={g._id} style={{ padding: "10px 14px", borderRadius: 10, border: "1px solid var(--warning-bg)", background: "var(--warning-bg)", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <div>
                  <div style={{ fontWeight: 700, fontSize: "0.875rem" }}>{g.name}</div>
                  <div style={{ fontSize: "0.78rem", color: "var(--muted)" }}>
                    {g.gridType === "presence" ? "Présence" : "Classique"} • {(g.items || []).filter((i) => i.type === "item").length} critères • {new Date(g.createdAt).toLocaleDateString("fr-FR")}
                  </div>
                </div>
                <span className="badge badge--warning">En attente</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Active grids */}
      <div className="card" style={{ padding: 16 }}>
        <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 10 }}>Grilles actives</div>
        {loading ? (
          <div style={{ color: "var(--muted)", padding: 20, textAlign: "center" }}>Chargement…</div>
        ) : grids.length === 0 ? (
          <div style={{ color: "var(--muted)", padding: 20, textAlign: "center" }}>Aucune grille active.</div>
        ) : (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))", gap: 10 }}>
            {grids.map((g) => (
              <div key={g._id} style={{ padding: "12px 14px", borderRadius: 10, border: "1px solid var(--border)", background: "var(--panel-2)" }}>
                <div style={{ fontWeight: 700, fontSize: "0.9rem" }}>{g.name}</div>
                <div style={{ fontSize: "0.78rem", color: "var(--muted)", marginTop: 4 }}>
                  <span className="badge badge--muted" style={{ marginRight: 6 }}>{g.gridType === "presence" ? "Présence" : "Classique"}</span>
                  {(g.items || []).filter((i) => i.type === "item").length} critères • {(g.items || []).filter((i) => i.type === "group").length} phases
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Creation modal */}
      {creating && (
        <div className="modal-overlay" onClick={() => setCreating(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()} style={{ maxWidth: 720, maxHeight: "90vh", overflow: "auto" }}>
            <div style={{ fontWeight: 800, fontSize: "1.05rem", marginBottom: 16 }}>Proposer une nouvelle grille</div>

            <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: 10, marginBottom: 12 }}>
              <div>
                <div className="label" style={{ marginBottom: 4 }}>Nom de la grille</div>
                <input className="input" value={name} onChange={(e) => setName(e.target.value)} placeholder="Ex: Grille Appels Sortants v2" />
              </div>
              <div>
                <div className="label" style={{ marginBottom: 4 }}>Type</div>
                <select className="input" value={gridType} onChange={(e) => setGridType(e.target.value)}>
                  <option value="classic">Classique (C/NC/NA)</option>
                  <option value="presence">Présence (PC/PNC/NP/NA)</option>
                </select>
              </div>
            </div>

            <div style={{ marginBottom: 12 }}>
              <div className="label" style={{ marginBottom: 4 }}>Description</div>
              <textarea className="input" rows={2} value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Objectif de cette grille…" />
            </div>

            {/* Items builder */}
            <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 8 }}>Critères & Phases</div>
            <div style={{ display: "flex", gap: 8, marginBottom: 10 }}>
              <button className="btn btn--ghost btn--sm" onClick={addGroup}>+ Phase</button>
              <button className="btn btn--ghost btn--sm" onClick={addItem}>+ Critère</button>
            </div>

            <div style={{ display: "flex", flexDirection: "column", gap: 6, maxHeight: 350, overflow: "auto", border: "1px solid var(--border)", borderRadius: 10, padding: 10 }}>
              {items.map((it) => (
                <div key={it._lid} style={{ display: "flex", gap: 8, alignItems: "center", padding: "6px 8px", borderRadius: 8, background: it.type === "group" ? "var(--primary-bg)" : "var(--panel-2)" }}>
                  {it.type === "group" ? (
                    <>
                      <span style={{ fontSize: "0.75rem", fontWeight: 700, color: "var(--primary)", width: 50 }}>PHASE</span>
                      <input className="input" style={{ flex: 1, fontSize: "0.85rem", padding: "4px 8px" }} value={it.title || ""} onChange={(e) => updateItem(it._lid, "title", e.target.value)} placeholder="Nom de la phase" />
                      <label style={{ display: "flex", alignItems: "center", gap: 4, fontSize: "0.75rem", whiteSpace: "nowrap" }}>
                        <input type="checkbox" checked={!!it.hardFail} onChange={(e) => updateItem(it._lid, "hardFail", e.target.checked)} /> Hard fail
                      </label>
                      <input className="input" style={{ width: 60, fontSize: "0.8rem", padding: "4px 6px", textAlign: "center" }} type="number" value={it.malusPercent || 0} onChange={(e) => updateItem(it._lid, "malusPercent", Number(e.target.value))} title="Malus %" />
                    </>
                  ) : (
                    <>
                      <span style={{ fontSize: "0.75rem", fontWeight: 600, color: "var(--muted)", width: 50 }}>Item</span>
                      <input className="input" style={{ flex: 1, fontSize: "0.85rem", padding: "4px 8px" }} value={it.label || ""} onChange={(e) => updateItem(it._lid, "label", e.target.value)} placeholder="Libellé du critère" />
                      <input className="input" style={{ width: 45, fontSize: "0.8rem", padding: "4px 4px", textAlign: "center" }} type="number" value={it.pointsConforme ?? 1} onChange={(e) => updateItem(it._lid, "pointsConforme", Number(e.target.value))} title="Pts C" />
                      <input className="input" style={{ width: 45, fontSize: "0.8rem", padding: "4px 4px", textAlign: "center" }} type="number" value={it.pointsNonConforme ?? 0} onChange={(e) => updateItem(it._lid, "pointsNonConforme", Number(e.target.value))} title="Pts NC" />
                    </>
                  )}
                  <button className="btn btn--ghost btn--sm" style={{ padding: "2px 6px", fontSize: "0.8rem" }} onClick={() => removeItem(it._lid)}>✕</button>
                </div>
              ))}
              {items.length === 0 && <div style={{ color: "var(--muted)", textAlign: "center", padding: 16 }}>Ajoutez des phases et critères.</div>}
            </div>

            <div style={{ marginTop: 12, padding: "10px 12px", borderRadius: 10, background: "var(--warning-bg)", border: "1px solid rgba(217,119,6,0.15)", fontSize: "0.82rem", color: "var(--text-secondary)" }}>
              ⚠ Cette grille sera soumise à validation. Elle ne sera utilisable qu'après approbation par le management ou l'administration.
            </div>

            <div style={{ display: "flex", justifyContent: "flex-end", gap: 8, marginTop: 16 }}>
              <button className="btn btn--ghost" onClick={() => setCreating(false)}>Annuler</button>
              <button className="btn" onClick={handlePropose} disabled={saving}>{saving ? "Envoi…" : "Soumettre la grille"}</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
