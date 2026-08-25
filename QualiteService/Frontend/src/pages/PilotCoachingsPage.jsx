import React, { useEffect, useState } from "react";
import api from "../api.js";

export default function PilotCoachingsPage() {
  const [coachings, setCoachings] = useState([]);
  const [loading, setLoading] = useState(true);
  const [ackId, setAckId] = useState(null);
  const [ackComment, setAckComment] = useState("");
  const [submitting, setSubmitting] = useState(false);

  async function load() { setLoading(true); try { setCoachings((await api.get("/coaching/my-coachings")).data || []); } catch {} finally { setLoading(false); } }
  useEffect(() => { load(); }, []);

  async function acknowledge(id) {
    setSubmitting(true);
    try { await api.post(`/coaching/${id}/acknowledge`, { comment: ackComment }); setAckId(null); setAckComment(""); load(); }
    catch (e) { alert(e?.response?.data?.message || "Erreur."); }
    finally { setSubmitting(false); }
  }

  const pending = coachings.filter((c) => !c.pilotAcknowledged);
  const acknowledged = coachings.filter((c) => c.pilotAcknowledged);

  return (
    <div className="page">
      <div className="cq-dup-title" style={{ marginBottom: 16 }}>
        <div style={{ fontSize: "1.25rem", fontWeight: 800 }}>🎯 Mes coachings</div>
        <div style={{ color: "var(--muted)", fontSize: "0.875rem", marginTop: 2 }}>Prenez connaissance de vos coachings et validez-les avec un commentaire.</div>
      </div>

      {loading && <div className="card" style={{ padding: 30, textAlign: "center", color: "var(--muted)" }}>Chargement…</div>}

      {!loading && !coachings.length && (
        <div className="card" style={{ padding: 40, textAlign: "center" }}>
          <div style={{ fontSize: "2rem", marginBottom: 8 }}>🎯</div>
          <div style={{ fontWeight: 700, fontSize: "1rem" }}>Aucun coaching pour le moment</div>
          <div style={{ color: "var(--muted)", fontSize: "0.85rem", marginTop: 4 }}>Vos coachings apparaîtront ici quand un évaluateur en créera un.</div>
        </div>
      )}

      {/* Pending */}
      {pending.length > 0 && (
        <div style={{ marginBottom: 20 }}>
          <div style={{ fontWeight: 800, fontSize: "0.95rem", marginBottom: 10, color: "var(--warning)" }}>⏳ En attente de votre validation ({pending.length})</div>
          {pending.map((c) => (
            <div key={c._id} className="card" style={{ padding: 16, marginBottom: 12, borderLeft: "4px solid var(--warning)" }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 10 }}>
                <div>
                  <div style={{ fontWeight: 700, fontSize: "0.95rem" }}>👤 Coach : {c.coach?.name || "—"}</div>
                  <div style={{ fontSize: "0.82rem", color: "var(--muted)", marginTop: 2 }}>EPS : {c.score?.eps || "—"} • {c.createdAt ? new Date(c.createdAt).toLocaleDateString("fr-FR") : "—"}</div>
                </div>
                <span className={`badge ${c.status === "done" ? "badge--success" : c.status === "in_progress" ? "badge--primary" : "badge--warning"}`}>
                  {c.status === "done" ? "Terminé" : c.status === "in_progress" ? "En cours" : "Ouvert"}
                </span>
              </div>

              {c.notes && (
                <div style={{ marginBottom: 10 }}>
                  <div style={{ fontSize: "0.78rem", fontWeight: 700, color: "var(--muted)", textTransform: "uppercase", marginBottom: 4 }}>📋 Ce que l'évaluateur a observé</div>
                  <div style={{ fontSize: "0.875rem", whiteSpace: "pre-wrap", lineHeight: 1.6, padding: "10px 12px", borderRadius: 8, background: "var(--panel-2)", border: "1px solid var(--border)" }}>{c.notes}</div>
                </div>
              )}
              {c.actionPlan && (
                <div style={{ marginBottom: 10 }}>
                  <div style={{ fontSize: "0.78rem", fontWeight: 700, color: "var(--muted)", textTransform: "uppercase", marginBottom: 4 }}>🎯 Ce que vous devez faire</div>
                  <div style={{ fontSize: "0.875rem", whiteSpace: "pre-wrap", lineHeight: 1.6, padding: "10px 12px", borderRadius: 8, background: "var(--primary-bg)", border: "1px solid var(--primary-border)" }}>{c.actionPlan}</div>
                </div>
              )}
              {c.followUpDate && (
                <div style={{ fontSize: "0.82rem", color: "var(--muted)", marginBottom: 10 }}>📅 Relance prévue le {new Date(c.followUpDate).toLocaleDateString("fr-FR")}</div>
              )}

              {ackId === c._id ? (
                <div style={{ padding: "12px 14px", borderRadius: 10, border: "2px solid var(--primary)", background: "var(--primary-bg)", marginTop: 8 }}>
                  <div style={{ fontWeight: 700, fontSize: "0.85rem", marginBottom: 6 }}>✍ Votre retour</div>
                  <textarea className="input" rows={3} value={ackComment} onChange={(e) => setAckComment(e.target.value)} placeholder="J'ai pris note des axes d'amélioration. Je m'engage à…" style={{ marginBottom: 8 }} />
                  <div style={{ display: "flex", justifyContent: "flex-end", gap: 8 }}>
                    <button className="btn btn--ghost btn--sm" onClick={() => setAckId(null)}>Annuler</button>
                    <button className="btn btn--sm" onClick={() => acknowledge(c._id)} disabled={submitting}>{submitting ? "Envoi…" : "✓ Je valide ce coaching"}</button>
                  </div>
                </div>
              ) : (
                <button className="btn" onClick={() => { setAckId(c._id); setAckComment(""); }} style={{ width: "100%", marginTop: 4 }}>✓ Valider et ajouter mon commentaire</button>
              )}
            </div>
          ))}
        </div>
      )}

      {/* Acknowledged */}
      {acknowledged.length > 0 && (
        <div>
          <div style={{ fontWeight: 800, fontSize: "0.95rem", marginBottom: 10, color: "var(--success)" }}>✅ Coachings validés ({acknowledged.length})</div>
          {acknowledged.map((c) => (
            <div key={c._id} className="card" style={{ padding: 16, marginBottom: 10, borderLeft: "4px solid var(--success)" }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 8 }}>
                <div>
                  <div style={{ fontWeight: 700, fontSize: "0.9rem" }}>{c.coach?.name || "—"} <span style={{ color: "var(--muted)", fontWeight: 400 }}>— EPS : {c.score?.eps || "—"}</span></div>
                  <div style={{ fontSize: "0.78rem", color: "var(--muted)", marginTop: 2 }}>{c.createdAt ? new Date(c.createdAt).toLocaleDateString("fr-FR") : ""}</div>
                </div>
                <span className="badge badge--success" style={{ fontSize: "0.72rem" }}>Validé {c.pilotAcknowledgedAt ? new Date(c.pilotAcknowledgedAt).toLocaleDateString("fr-FR") : ""}</span>
              </div>

              {c.notes && (
                <div style={{ marginBottom: 8 }}>
                  <div style={{ fontSize: "0.75rem", fontWeight: 700, color: "var(--muted)", textTransform: "uppercase", marginBottom: 2 }}>Observations</div>
                  <div style={{ fontSize: "0.85rem", whiteSpace: "pre-wrap", lineHeight: 1.5, color: "var(--text-secondary)" }}>{c.notes}</div>
                </div>
              )}
              {c.actionPlan && (
                <div style={{ marginBottom: 8 }}>
                  <div style={{ fontSize: "0.75rem", fontWeight: 700, color: "var(--muted)", textTransform: "uppercase", marginBottom: 2 }}>Plan d'action</div>
                  <div style={{ fontSize: "0.85rem", whiteSpace: "pre-wrap", lineHeight: 1.5, padding: "8px 10px", borderRadius: 8, background: "var(--primary-bg)" }}>{c.actionPlan}</div>
                </div>
              )}
              {c.pilotComment && (
                <div style={{ padding: "8px 10px", borderRadius: 8, background: "var(--success-bg)", border: "1px solid rgba(5,150,105,0.15)" }}>
                  <div style={{ fontSize: "0.75rem", fontWeight: 700, color: "var(--muted)", textTransform: "uppercase", marginBottom: 2 }}>Mon commentaire</div>
                  <div style={{ fontSize: "0.85rem", whiteSpace: "pre-wrap" }}>"{c.pilotComment}"</div>
                </div>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
