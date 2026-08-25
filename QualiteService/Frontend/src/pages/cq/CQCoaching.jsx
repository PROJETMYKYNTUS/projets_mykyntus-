import React, { useEffect, useMemo, useState } from "react";
import api from "../../api";
import PageHeader from "../../components/PageHeader.jsx";
import MultiSelect from "../../components/MultiSelect.jsx";

function toISO(d) { if (!d) return ""; const x = new Date(d); return isNaN(x.getTime()) ? "" : `${x.getFullYear()}-${String(x.getMonth()+1).padStart(2,"0")}-${String(x.getDate()).padStart(2,"0")}`; }
function fmtDate(d) { if (!d) return "—"; const x = new Date(d); return isNaN(x.getTime()) ? "—" : x.toLocaleDateString("fr-FR"); }
function isOverdue(d) { if (!d) return false; return new Date(d) < new Date(new Date().toDateString()); }

const STATUS_LABELS = { open: "Ouvert", in_progress: "En cours", done: "Terminé" };
const STATUS_COLORS = { open: "badge--warning", in_progress: "badge--primary", done: "badge--success" };

export default function CQCoaching() {
  const [loading, setLoading] = useState(true);
  const [items, setItems] = useState([]);
  const [err, setErr] = useState("");
  const [statusFilter, setStatusFilter] = useState(["open", "in_progress"]);
  const [pilotSel, setPilotSel] = useState([]);
  const [pilots, setPilots] = useState([]);

  // Create form
  const [creating, setCreating] = useState(false);
  const [scoreId, setScoreId] = useState("");
  const [notes, setNotes] = useState("");
  const [actionPlan, setActionPlan] = useState("");
  const [status, setStatus] = useState("open");
  const [followUpDate, setFollowUpDate] = useState("");
  const [scoresLoading, setScoresLoading] = useState(false);
  const [scoreOptions, setScoreOptions] = useState([]);

  // Edit modal
  const [editing, setEditing] = useState(null);
  const [editNotes, setEditNotes] = useState("");
  const [editActionPlan, setEditActionPlan] = useState("");
  const [editStatus, setEditStatus] = useState("open");
  const [editFollowUp, setEditFollowUp] = useState("");

  const pilotOptions = useMemo(() => (pilots || []).map((p) => ({ value: String(p._id), label: `${p.name || ""}${p.cell ? " — " + p.cell : ""}` })), [pilots]);

  useEffect(() => {
    api.get("/cq/pilots/search", { params: { limit: 200 } }).then((r) => setPilots(Array.isArray(r.data) ? r.data : [])).catch(() => {});
  }, []);

  async function loadCoachings() {
    setLoading(true); setErr("");
    try {
      const params = { limit: 200 };
      if (pilotSel.length) params.pilotId = pilotSel.join(",");
      const res = await api.get("/coaching/mine", { params });
      setItems(res.data?.items || []);
    } catch (e) { setErr(e?.response?.data?.message || "Erreur chargement coachings."); }
    finally { setLoading(false); }
  }

  useEffect(() => { loadCoachings(); }, [pilotSel]);

  // Create form — pilot selection
  const [createPilotId, setCreatePilotId] = useState("");

  // Load scores for creation — filtered by pilot
  useEffect(() => {
    if (!creating) return;
    setScoresLoading(true);
    const params = { limit: 100, page: 1 };
    if (createPilotId) params.pilotId = createPilotId;
    api.get("/scores/mine", { params }).then((r) => {
      setScoreOptions((r.data?.items || []).map((s) => ({
        value: String(s._id),
        label: `${s.pilotName || s.pilot?.name || "Agent"} — ${s.eps || "—"} — ${fmtDate(s.createdAt)}`,
        pilotName: s.pilotName || s.pilot?.name || "",
        pilotId: String(s.pilot?._id || s.pilotId || ""),
      })));
    }).catch(() => {}).finally(() => setScoresLoading(false));
  }, [creating, createPilotId]);

  async function handleCreate() {
    setErr("");
    if (!scoreId) return setErr("Sélectionnez une évaluation.");
    try {
      await api.post("/coaching", { scoreId, notes, actionPlan, status, followUpDate: followUpDate || null });
      setCreating(false); setScoreId(""); setCreatePilotId(""); setNotes(""); setActionPlan(""); setStatus("open"); setFollowUpDate("");
      loadCoachings();
    } catch (e) { setErr(e?.response?.data?.message || "Erreur création coaching."); }
  }

  async function handleStatusChange(id, newStatus) {
    try {
      const { data } = await api.patch(`/coaching/${id}`, { status: newStatus });
      setItems((arr) => arr.map((x) => String(x._id) === String(id) ? data : x));
    } catch (e) { setErr(e?.response?.data?.message || "Erreur mise à jour."); }
  }

  function openEdit(c) {
    setEditing(c);
    setEditNotes(c.notes || "");
    setEditActionPlan(c.actionPlan || "");
    setEditStatus(c.status || "open");
    setEditFollowUp(toISO(c.followUpDate));
  }

  async function saveEdit() {
    if (!editing) return;
    try {
      const { data } = await api.patch(`/coaching/${editing._id}`, {
        notes: editNotes, actionPlan: editActionPlan, status: editStatus, followUpDate: editFollowUp || null,
      });
      setItems((arr) => arr.map((x) => String(x._id) === String(editing._id) ? data : x));
      setEditing(null);
    } catch (e) { setErr(e?.response?.data?.message || "Erreur sauvegarde."); }
  }

  const filtered = useMemo(() => items.filter((c) => {
    if (statusFilter.length && !statusFilter.includes(c.status)) return false;
    return true;
  }), [items, statusFilter]);

  // Pipeline counts
  const counts = useMemo(() => ({
    open: items.filter((c) => c.status === "open").length,
    in_progress: items.filter((c) => c.status === "in_progress").length,
    done: items.filter((c) => c.status === "done").length,
    overdue: items.filter((c) => c.status !== "done" && isOverdue(c.followUpDate)).length,
  }), [items]);

  return (
    <div className="page">
      <PageHeader title="Coaching" subtitle="Suivi des plans d'action, relances et progression des agents." />

      {/* Pipeline KPIs */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: 10, marginBottom: 16 }}>
        {[
          { label: "Ouverts", count: counts.open, color: "var(--warning)", icon: "📋" },
          { label: "En cours", count: counts.in_progress, color: "var(--primary)", icon: "🔄" },
          { label: "Terminés", count: counts.done, color: "var(--success)", icon: "✅" },
          { label: "En retard", count: counts.overdue, color: "var(--danger)", icon: "⏰" },
        ].map((k) => (
          <div key={k.label} className="card" style={{ padding: "12px 14px", cursor: "pointer" }} onClick={() => k.label === "En retard" ? setStatusFilter(["open", "in_progress"]) : setStatusFilter([k.label === "Ouverts" ? "open" : k.label === "En cours" ? "in_progress" : "done"])}>
            <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
              <span>{k.icon}</span>
              <span style={{ fontSize: "0.75rem", fontWeight: 700, color: "var(--muted)", textTransform: "uppercase" }}>{k.label}</span>
            </div>
            <div style={{ fontSize: "1.4rem", fontWeight: 800, color: k.color, marginTop: 4 }}>{k.count}</div>
          </div>
        ))}
      </div>

      {/* Filters + Create */}
      <div className="card filters-card" style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
        <div style={{ display: "flex", gap: 10, flex: 1, minWidth: 300 }}>
          <div style={{ flex: 1 }}>
            <div className="label">Agent</div>
            <MultiSelect options={pilotOptions} value={pilotSel} onChange={setPilotSel} placeholder="Tous agents" />
          </div>
          <div style={{ flex: 1 }}>
            <div className="label">Statut</div>
            <MultiSelect
              options={[{ value: "open", label: "Ouvert" }, { value: "in_progress", label: "En cours" }, { value: "done", label: "Terminé" }]}
              value={statusFilter} onChange={setStatusFilter} placeholder="Tous"
            />
          </div>
        </div>
        <button className="btn" onClick={() => setCreating(true)}>+ Nouveau coaching</button>
      </div>

      {err && <div className="card" style={{ padding: 12, color: "var(--danger)", background: "var(--danger-bg)", borderColor: "rgba(220,38,38,0.2)", marginBottom: 12 }}>{err}</div>}

      {/* Coaching List */}
      <div className="card" style={{ padding: 0, overflow: "hidden" }}>
        <div style={{ padding: "12px 16px", borderBottom: "1px solid var(--border)", display: "flex", justifyContent: "space-between" }}>
          <span style={{ fontWeight: 700, fontSize: "0.9rem" }}>Coachings</span>
          <span style={{ color: "var(--muted)", fontSize: "0.8rem", fontWeight: 600 }}>{filtered.length} résultat{filtered.length > 1 ? "s" : ""}</span>
        </div>
        <div style={{ overflow: "auto" }}>
          <table className="data-table">
            <thead><tr><th>Date</th><th>Agent</th><th>Évaluation</th><th>Statut</th><th>Validation agent</th><th>Relance</th><th>Actions</th></tr></thead>
            <tbody>
              {filtered.map((c) => {
                const pilot = c.pilot?.name || c.score?.pilot?.name || "—";
                const evalEps = c.score?.eps || "—";
                const evalDate = fmtDate(c.score?.createdAt);
                const overdueFlag = c.status !== "done" && isOverdue(c.followUpDate);
                return (
                  <tr key={c._id}>
                    <td style={{ fontSize: "0.85rem" }}>{fmtDate(c.createdAt)}</td>
                    <td style={{ fontWeight: 600 }}>{pilot}</td>
                    <td style={{ fontSize: "0.8rem" }}><span style={{ fontFamily: "monospace" }}>{evalEps}</span> <span style={{ color: "var(--muted)" }}>({evalDate})</span></td>
                    <td>
                      <select className="input" value={c.status} onChange={(e) => handleStatusChange(c._id, e.target.value)} style={{ padding: "3px 8px", fontSize: "0.8rem", width: 120 }}>
                        <option value="open">Ouvert</option>
                        <option value="in_progress">En cours</option>
                        <option value="done">Terminé</option>
                      </select>
                    </td>
                    <td>
                      {c.pilotAcknowledged ? (
                        <div>
                          <span className="badge badge--success" style={{ fontSize: "0.72rem" }}>✅ Validé</span>
                          {c.pilotComment && <div style={{ fontSize: "0.75rem", color: "var(--text-secondary)", marginTop: 2, maxWidth: 160, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }} title={c.pilotComment}>💬 {c.pilotComment}</div>}
                        </div>
                      ) : (
                        <span className="badge badge--warning" style={{ fontSize: "0.72rem" }}>⏳ En attente</span>
                      )}
                    </td>
                    <td>
                      {c.followUpDate ? (
                        <span className={`badge ${overdueFlag ? "badge--danger" : "badge--muted"}`}>
                          {overdueFlag ? "⏰ " : ""}{fmtDate(c.followUpDate)}
                        </span>
                      ) : <span style={{ color: "var(--muted)", fontSize: "0.8rem" }}>—</span>}
                    </td>
                    <td><button className="btn btn--ghost btn--sm" onClick={() => openEdit(c)}>Détail</button></td>
                  </tr>
                );
              })}
              {filtered.length === 0 && <tr><td colSpan={7} style={{ textAlign: "center", color: "var(--muted)", padding: 24 }}>Aucun coaching trouvé.</td></tr>}
            </tbody>
          </table>
        </div>
      </div>

      {/* Create Modal */}
      {creating && (
        <div className="modal-overlay" onClick={() => setCreating(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()} style={{ maxWidth: 580 }}>
            <div style={{ fontWeight: 800, fontSize: "1.05rem", marginBottom: 16 }}>Nouveau coaching</div>
            <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
              <div>
                <div className="label" style={{ marginBottom: 4 }}>Agent (pilote)</div>
                <select className="input" value={createPilotId} onChange={(e) => { setCreatePilotId(e.target.value); setScoreId(""); }}>
                  <option value="">Tous les agents</option>
                  {pilotOptions.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                </select>
              </div>
              <div>
                <div className="label" style={{ marginBottom: 4 }}>Évaluation liée</div>
                <select className="input" value={scoreId} onChange={(e) => setScoreId(e.target.value)}>
                  <option value="">{scoresLoading ? "Chargement…" : "Sélectionner une évaluation"}</option>
                  {scoreOptions.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                </select>
                {createPilotId && scoreOptions.length === 0 && !scoresLoading && (
                  <div style={{ fontSize: "0.78rem", color: "var(--warning)", marginTop: 4 }}>Aucune évaluation trouvée pour cet agent.</div>
                )}
              </div>
              <div>
                <div className="label" style={{ marginBottom: 4 }}>Notes / Observations</div>
                <textarea className="input" rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} placeholder="Points observés durant l'écoute…" />
              </div>
              <div>
                <div className="label" style={{ marginBottom: 4 }}>Plan d'action</div>
                <textarea className="input" rows={3} value={actionPlan} onChange={(e) => setActionPlan(e.target.value)} placeholder="Actions correctives à mettre en place…" />
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 }}>
                <div>
                  <div className="label" style={{ marginBottom: 4 }}>Statut</div>
                  <select className="input" value={status} onChange={(e) => setStatus(e.target.value)}>
                    <option value="open">Ouvert</option>
                    <option value="in_progress">En cours</option>
                    <option value="done">Terminé</option>
                  </select>
                </div>
                <div>
                  <div className="label" style={{ marginBottom: 4 }}>Date de relance</div>
                  <input className="input" type="date" value={followUpDate} onChange={(e) => setFollowUpDate(e.target.value)} />
                </div>
              </div>
            </div>
            <div style={{ display: "flex", justifyContent: "flex-end", gap: 8, marginTop: 16 }}>
              <button className="btn btn--ghost" onClick={() => setCreating(false)}>Annuler</button>
              <button className="btn" onClick={handleCreate}>Créer</button>
            </div>
          </div>
        </div>
      )}

      {/* Edit Modal */}
      {editing && (
        <div className="modal-overlay" onClick={() => setEditing(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()} style={{ maxWidth: 600 }}>
            <div style={{ fontWeight: 800, fontSize: "1.05rem", marginBottom: 4 }}>Coaching — {editing.pilot?.name || "Agent"}</div>
            <div style={{ color: "var(--muted)", fontSize: "0.8rem", marginBottom: 16 }}>
              Évaluation : {editing.score?.eps || "—"} • {fmtDate(editing.score?.createdAt)} • Score : {Number(editing.score?.compliancePercent || editing.score?.total || 0).toFixed(1)}%
            </div>
            <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
              <div>
                <div className="label" style={{ marginBottom: 4 }}>Notes / Observations</div>
                <textarea className="input" rows={4} value={editNotes} onChange={(e) => setEditNotes(e.target.value)} />
              </div>
              <div>
                <div className="label" style={{ marginBottom: 4 }}>Plan d'action</div>
                <textarea className="input" rows={4} value={editActionPlan} onChange={(e) => setEditActionPlan(e.target.value)} />
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 }}>
                <div>
                  <div className="label" style={{ marginBottom: 4 }}>Statut</div>
                  <select className="input" value={editStatus} onChange={(e) => setEditStatus(e.target.value)}>
                    <option value="open">Ouvert</option>
                    <option value="in_progress">En cours</option>
                    <option value="done">Terminé</option>
                  </select>
                </div>
                <div>
                  <div className="label" style={{ marginBottom: 4 }}>Date de relance</div>
                  <input className="input" type="date" value={editFollowUp} onChange={(e) => setEditFollowUp(e.target.value)} />
                </div>
              </div>
            </div>
            {/* Pilot acknowledgment */}
            <div style={{ marginTop: 12, padding: "12px 14px", borderRadius: 10, border: "1px solid var(--border)", background: editing.pilotAcknowledged ? "var(--success-bg)" : "var(--warning-bg)" }}>
              <div style={{ fontWeight: 700, fontSize: "0.85rem", marginBottom: 4 }}>
                {editing.pilotAcknowledged ? "✅ Validé par l'agent" : "⏳ En attente de validation agent"}
              </div>
              {editing.pilotAcknowledged ? (
                <>
                  <div style={{ fontSize: "0.82rem", color: "var(--muted)" }}>Validé le {editing.pilotAcknowledgedAt ? new Date(editing.pilotAcknowledgedAt).toLocaleString("fr-FR") : "—"}</div>
                  {editing.pilotComment && (
                    <div style={{ marginTop: 6, padding: "8px 10px", borderRadius: 8, background: "var(--panel)", border: "1px solid var(--border)", fontSize: "0.85rem", whiteSpace: "pre-wrap" }}>
                      <span style={{ fontWeight: 700 }}>Commentaire agent :</span> {editing.pilotComment}
                    </div>
                  )}
                </>
              ) : (
                <div style={{ fontSize: "0.82rem", color: "var(--muted)" }}>L'agent n'a pas encore validé ce coaching.</div>
              )}
            </div>

            <div style={{ display: "flex", justifyContent: "flex-end", gap: 8, marginTop: 16 }}>
              <button className="btn btn--ghost" onClick={() => setEditing(null)}>Annuler</button>
              <button className="btn" onClick={saveEdit}>Enregistrer</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
