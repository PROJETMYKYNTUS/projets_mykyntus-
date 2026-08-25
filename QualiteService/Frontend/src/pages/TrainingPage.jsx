import React, { useEffect, useState, useMemo, useCallback } from "react";
import api from "../api.js";
import { exportToXlsx } from "./admin/components/exportXlsx.js";

const getUser = () => { try { return JSON.parse(localStorage.getItem("user") || "null"); } catch { return null; } };
const canManage = () => ["admin", "formateur"].includes(getUser()?.role);
const fmtDate = (d) => { if (!d) return "—"; const dt = new Date(d); return isNaN(dt.getTime()) ? "—" : dt.toLocaleDateString("fr-FR"); };
const fmtFull = (d) => { if (!d) return "—"; const dt = new Date(d); return isNaN(dt.getTime()) ? "—" : dt.toLocaleString("fr-FR"); };
const uid = () => Math.random().toString(36).slice(2);

export default function TrainingPage() {
  const [tab, setTab] = useState("list"); // list, history, stats
  const [trainings, setTrainings] = useState([]);
  const [loading, setLoading] = useState(true);
  const [selected, setSelected] = useState(null);
  const [quizMode, setQuizMode] = useState(false);
  const [quizAnswers, setQuizAnswers] = useState([]);
  const [quizResult, setQuizResult] = useState(null);
  const [submitting, setSubmitting] = useState(false);
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState(null); // training to edit
  const [form, setForm] = useState({ title: "", description: "", pdfUrl: "", videoUrl: "", category: "", roles: [], targetCells: [], targetUsers: [], questions: [], allowMultipleAttempts: true, passThreshold: 80 });
  const [pdfFile, setPdfFile] = useState(null);
  const [saving, setSaving] = useState(false);
  const [msg, setMsg] = useState("");
  const [err, setErr] = useState("");
  const [resultsFor, setResultsFor] = useState(null);
  const [results, setResults] = useState(null);
  const [history, setHistory] = useState([]);
  const [stats, setStats] = useState(null);
  const [cells, setCells] = useState([]);
  const [users, setUsers] = useState([]);

  const loadList = useCallback(async () => {
    setLoading(true);
    try { setTrainings((await api.get(canManage() ? "/training/admin" : "/training")).data || []); } catch {} finally { setLoading(false); }
  }, []);
  useEffect(() => { loadList(); }, [loadList]);

  // Load cells + users for targeting (admin/formateur only)
  useEffect(() => {
    if (!canManage()) return;
    api.get("/admin/cells").then((r) => setCells(Array.isArray(r.data) ? r.data : [])).catch(() => {});
    api.get("/cq/pilots/search", { params: { limit: 500 } }).then((r) => setUsers(Array.isArray(r.data) ? r.data : [])).catch(() => {});
  }, []);

  async function loadHistory() {
    try { setHistory((await api.get("/training/history/me")).data || []); } catch { setHistory([]); }
  }
  async function loadStats() {
    try { setStats((await api.get("/training/stats/global")).data || null); } catch { setStats(null); }
  }

  useEffect(() => { if (tab === "history") loadHistory(); if (tab === "stats") loadStats(); }, [tab]);

  async function openTraining(id) {
    try { const r = await api.get(`/training/${id}`); setSelected(r.data); setQuizMode(false); setQuizResult(null); setQuizAnswers((r.data?.questions || []).map(() => -1)); } catch {}
  }
  async function submitQuiz() {
    if (!selected?._id) return; setSubmitting(true); setErr("");
    try {
      const res = await api.post(`/training/${selected._id}/attempt`, { answers: quizAnswers });
      setQuizResult(res.data);
    } catch (e) {
      const msg = e?.response?.data?.message || "Erreur.";
      if (e?.response?.status === 403) {
        setQuizResult({ score: 0, total: 0, percent: 0, passed: false, threshold: selected.passThreshold || 80, blocked: true, blockedMsg: msg });
        setQuizMode(false);
      } else { setErr(msg); }
    } finally { setSubmitting(false); }
  }
  async function loadResults(id) {
    try { setResults((await api.get(`/training/${id}/results`)).data); setResultsFor(id); } catch {}
  }

  // Form helpers
  function resetForm() { setForm({ title: "", description: "", pdfUrl: "", videoUrl: "", category: "", roles: [], targetCells: [], targetUsers: [], questions: [], allowMultipleAttempts: true, passThreshold: 80 }); setPdfFile(null); }
  function openCreate() { resetForm(); setEditing(null); setCreating(true); setMsg(""); setErr(""); }
  function openEdit(t) {
    setForm({ title: t.title || "", description: t.description || "", pdfUrl: t.pdfUrl || "", videoUrl: t.videoUrl || "", category: t.category || "", roles: t.roles || [], targetCells: t.targetCells || [], targetUsers: (t.targetUsers || []).map((u) => typeof u === "string" ? u : u._id || u), questions: (t.questions || []).map((q) => ({ ...q, _lid: uid() })), allowMultipleAttempts: t.allowMultipleAttempts !== false, passThreshold: Number.isFinite(t.passThreshold) ? t.passThreshold : 80 });
    setEditing(t); setCreating(true); setMsg(""); setErr("");
  }

  async function handleSave() {
    setMsg(""); setErr(""); setSaving(true);
    try {
      let pdfData = "";
      if (pdfFile) { pdfData = await new Promise((r) => { const rd = new FileReader(); rd.onload = () => r(rd.result); rd.readAsDataURL(pdfFile); }); }
      const payload = { ...form };
      if (pdfData) payload.pdfData = pdfData;
      if (editing?._id) { await api.patch(`/training/${editing._id}`, payload); setMsg("Formation mise à jour."); }
      else { await api.post("/training", payload); setMsg("Formation créée."); }
      setCreating(false); resetForm(); setEditing(null); loadList();
    } catch (e) { setErr(e?.response?.data?.message || "Erreur."); } finally { setSaving(false); }
  }

  // Quiz editor helpers
  function addQ() { setForm((f) => ({ ...f, questions: [...f.questions, { _lid: uid(), question: "", imageData: "", options: ["", ""], correctIndex: 0 }] })); }
  function rmQ(i) { setForm((f) => ({ ...f, questions: f.questions.filter((_, j) => j !== i) })); }
  function upQ(i, k, v) { setForm((f) => ({ ...f, questions: f.questions.map((q, j) => j === i ? { ...q, [k]: v } : q) })); }
  function upOpt(qi, oi, v) { setForm((f) => ({ ...f, questions: f.questions.map((q, j) => j === qi ? { ...q, options: q.options.map((o, k) => k === oi ? v : o) } : q) })); }
  function addOpt(qi) { setForm((f) => ({ ...f, questions: f.questions.map((q, j) => j === qi ? { ...q, options: [...q.options, ""] } : q) })); }
  function rmOpt(qi, oi) { setForm((f) => ({ ...f, questions: f.questions.map((q, j) => { if (j !== qi) return q; const opts = q.options.filter((_, k) => k !== oi); return { ...q, options: opts, correctIndex: Math.min(q.correctIndex, opts.length - 1) }; }) })); }
  function imgQ(qi, file) { if (!file) return; const r = new FileReader(); r.onload = () => upQ(qi, "imageData", r.result); r.readAsDataURL(file); }

  // Export results as PDF-like XLSX
  function exportResultsPdf() {
    if (!results) return;
    const rows = (results.attempts || []).map((a) => ({
      Collaborateur: a.user?.name || "", Email: a.user?.email || "", Rôle: a.user?.role || "", Cellule: a.user?.cell || "",
      "Score (%)": a.percent, "Bonnes réponses": `${a.score}/${a.total}`, Date: fmtFull(a.completedAt),
    }));
    exportToXlsx(`Resultats_${(results.title || "quiz").replace(/\s+/g, "_")}.xlsx`, rows);
  }

  // ==================== VIEWER ====================
  if (selected) {
    return (
      <div className="page">
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 16 }}>
          <div>
            <div style={{ fontSize: "1.25rem", fontWeight: 800 }}>{selected.title}</div>
            <div style={{ color: "var(--muted)", fontSize: "0.875rem", marginTop: 2 }}>{selected.description || ""}{selected.category ? ` • ${selected.category}` : ""}</div>
          </div>
          <div style={{ display: "flex", gap: 8 }}>
            {(selected.questions || []).length > 0 && !quizMode && !quizResult && <button className="btn" onClick={() => { setQuizMode(true); setQuizResult(null); setQuizAnswers((selected.questions || []).map(() => -1)); }}>📝 Quiz ({selected.questions.length})</button>}
            {canManage() && !quizMode && !quizResult && <button className="btn btn--ghost" onClick={() => { setSelected(null); openEdit(selected); }}>✏ Modifier</button>}
            <button className="btn btn--ghost" onClick={() => setSelected(null)}>← Retour</button>
          </div>
        </div>
        {!quizMode && !quizResult && (
          <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
            {/* Video player */}
            {selected.videoUrl && (() => {
              const vUrl = selected.videoUrl.startsWith("/uploads/") ? `${api.defaults.baseURL?.replace("/api", "") || "http://my-backend:5000"}${selected.videoUrl}` : selected.videoUrl;
              return (
              <div className="card" style={{ padding: 16 }}>
                <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 10 }}>🎬 Vidéo</div>
                {/youtube\.com|youtu\.be/.test(vUrl) ? (
                  <iframe
                    src={vUrl.replace("watch?v=", "embed/").replace("youtu.be/", "youtube.com/embed/").split("&")[0]}
                    style={{ width: "100%", height: "45vh", border: "none", borderRadius: 12 }}
                    allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
                    allowFullScreen title="Vidéo"
                  />
                ) : /vimeo\.com/.test(vUrl) ? (
                  <iframe
                    src={vUrl.replace("vimeo.com/", "player.vimeo.com/video/")}
                    style={{ width: "100%", height: "45vh", border: "none", borderRadius: 12 }}
                    allow="autoplay; fullscreen; picture-in-picture" allowFullScreen title="Vidéo"
                  />
                ) : (
                  <video controls src={vUrl} style={{ width: "100%", maxHeight: "50vh", borderRadius: 12, background: "#000" }}>
                    Votre navigateur ne supporte pas la lecture vidéo.
                  </video>
                )}
              </div>
              );
            })()}
            {/* PDF viewer */}
            {(selected.pdfData || selected.pdfUrl) && (
              <div className="card" style={{ padding: 0, overflow: "hidden" }}>
                <iframe src={selected.pdfData || selected.pdfUrl} style={{ width: "100%", height: "70vh", border: "none" }} title="PDF" />
              </div>
            )}
            {!selected.videoUrl && !selected.pdfData && !selected.pdfUrl && (
              <div className="card" style={{ padding: 40, textAlign: "center", color: "var(--muted)" }}>Aucun document associé.</div>
            )}
          </div>
        )}
        {quizMode && !quizResult && (
          <div className="card" style={{ padding: 20 }}>
            <div style={{ fontWeight: 700, fontSize: "0.95rem", marginBottom: 16 }}>📝 Quiz — {selected.title}</div>
            {(selected.questions || []).map((q, qi) => (
              <div key={qi} style={{ marginBottom: 20, padding: "14px 16px", borderRadius: 12, border: "1px solid var(--border)", background: "var(--panel-2)" }}>
                <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 10 }}>{qi + 1}. {q.question}</div>
                {q.imageData && <img src={q.imageData} alt="" style={{ maxWidth: "100%", maxHeight: 280, borderRadius: 10, marginBottom: 12, objectFit: "contain" }} />}
                <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                  {(q.options || []).map((opt, oi) => (
                    <label key={oi} style={{ display: "flex", alignItems: "center", gap: 10, padding: "8px 12px", borderRadius: 8, border: quizAnswers[qi] === oi ? "2px solid var(--primary)" : "1px solid var(--border)", background: quizAnswers[qi] === oi ? "var(--primary-bg)" : "transparent", cursor: "pointer" }}>
                      <input type="radio" name={`q${qi}`} checked={quizAnswers[qi] === oi} onChange={() => setQuizAnswers((a) => a.map((v, i) => i === qi ? oi : v))} style={{ accentColor: "var(--primary)" }} />
                      <span style={{ fontSize: "0.875rem", fontWeight: quizAnswers[qi] === oi ? 700 : 500 }}>{opt}</span>
                    </label>
                  ))}
                </div>
              </div>
            ))}
            <div style={{ display: "flex", justifyContent: "flex-end", gap: 8 }}>
              <button className="btn btn--ghost" onClick={() => setQuizMode(false)}>Annuler</button>
              <button className="btn" onClick={submitQuiz} disabled={submitting || quizAnswers.includes(-1)}>{submitting ? "Envoi…" : "Soumettre"}</button>
            </div>
          </div>
        )}
        {quizResult && (
          <div className="card" style={{ padding: 24, textAlign: "center" }}>
            {quizResult.blocked ? (
              <>
                <div style={{ fontSize: "2.5rem", marginBottom: 8 }}>🚫</div>
                <div style={{ fontSize: "1.1rem", fontWeight: 800, color: "var(--warning)" }}>Quiz déjà passé</div>
                <div style={{ fontSize: "0.9rem", color: "var(--muted)", marginTop: 8, maxWidth: 400, margin: "8px auto 0" }}>{quizResult.blockedMsg}</div>
                <div style={{ marginTop: 16 }}><button className="btn" onClick={() => setSelected(null)}>Retour</button></div>
              </>
            ) : (
              <>
                <div style={{ fontSize: "2.5rem", marginBottom: 8 }}>{quizResult.passed ? "🏆" : quizResult.percent >= 50 ? "👍" : "📚"}</div>
                <div style={{ fontSize: "1.5rem", fontWeight: 800, color: quizResult.passed ? "var(--success)" : quizResult.percent >= 50 ? "var(--warning)" : "var(--danger)" }}>{quizResult.percent}%</div>
                <div style={{ fontSize: "0.9rem", color: "var(--muted)", marginTop: 4 }}>{quizResult.score} / {quizResult.total} bonnes réponses</div>
                <div style={{ marginTop: 8, fontSize: "0.875rem", fontWeight: 700, color: quizResult.passed ? "var(--success)" : "var(--danger)" }}>
                  {quizResult.passed ? `✅ Réussi (seuil: ${quizResult.threshold}%)` : `❌ Non réussi (seuil: ${quizResult.threshold}%)`}
                </div>
                <div style={{ marginTop: 16, display: "flex", justifyContent: "center", gap: 8 }}>
                  {selected.allowMultipleAttempts !== false && (
                    <button className="btn btn--ghost" onClick={() => { setQuizMode(true); setQuizResult(null); setQuizAnswers((selected.questions || []).map(() => -1)); }}>Recommencer</button>
                  )}
                  {selected.allowMultipleAttempts === false && (
                    <div style={{ fontSize: "0.82rem", color: "var(--muted)", padding: "6px 0" }}>Une seule tentative autorisée.</div>
                  )}
                  <button className="btn" onClick={() => setSelected(null)}>Retour</button>
                </div>
              </>
            )}
          </div>
        )}
      </div>
    );
  }

  // ==================== RESULTS ====================
  if (results && resultsFor) {
    const attempts = results.attempts || [];
    const avgS = attempts.length ? Math.round(attempts.reduce((a, x) => a + x.percent, 0) / attempts.length) : 0;
    return (
      <div className="page">
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 16 }}>
          <div>
            <div style={{ fontSize: "1.25rem", fontWeight: 800 }}>Résultats — {results.title}</div>
            <div style={{ color: "var(--muted)", fontSize: "0.875rem" }}>{results.questionCount} questions • {attempts.length} tentatives</div>
          </div>
          <div style={{ display: "flex", gap: 8 }}>
            <button className="btn" onClick={exportResultsPdf} disabled={!attempts.length}>📥 Exporter Excel</button>
            <button className="btn btn--ghost" onClick={() => { setResults(null); setResultsFor(null); }}>← Retour</button>
          </div>
        </div>
        {attempts.length > 0 && (
          <div style={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: 10, marginBottom: 16 }}>
            {[
              { l: "Tentatives", v: attempts.length, c: "var(--primary)" },
              { l: "Score moyen", v: `${avgS}%`, c: avgS >= 80 ? "var(--success)" : avgS >= 50 ? "var(--warning)" : "var(--danger)" },
              { l: "Meilleur", v: `${Math.max(...attempts.map((a) => a.percent))}%`, c: "var(--success)" },
              { l: "Réussite ≥80%", v: `${Math.round(attempts.filter((a) => a.percent >= 80).length / attempts.length * 100)}%`, c: "var(--text)" },
            ].map((k) => <div key={k.l} className="card" style={{ padding: "12px 14px" }}><div style={{ fontSize: "0.7rem", fontWeight: 700, color: "var(--muted)", textTransform: "uppercase" }}>{k.l}</div><div style={{ fontSize: "1.4rem", fontWeight: 800, marginTop: 2, color: k.c }}>{k.v}</div></div>)}
          </div>
        )}
        <div className="card" style={{ padding: 0, overflow: "hidden" }}>
          <table className="data-table"><thead><tr><th>Collaborateur</th><th>Rôle</th><th>Cellule</th><th>Score</th><th>Rép.</th><th>Date</th></tr></thead>
            <tbody>{attempts.map((a, i) => <tr key={i}><td style={{ fontWeight: 600 }}>{a.user?.name || "—"}</td><td><span className="badge badge--muted">{a.user?.role || "—"}</span></td><td>{a.user?.cell || "—"}</td><td><span className={`badge ${a.percent >= 80 ? "badge--success" : a.percent >= 50 ? "badge--warning" : "badge--danger"}`}>{a.percent}%</span></td><td>{a.score}/{a.total}</td><td style={{ fontSize: "0.82rem" }}>{fmtDate(a.completedAt)}</td></tr>)}
              {!attempts.length && <tr><td colSpan={6} style={{ textAlign: "center", color: "var(--muted)", padding: 20 }}>Aucune tentative.</td></tr>}</tbody>
          </table>
        </div>
      </div>
    );
  }

  // ==================== MAIN VIEW WITH TABS ====================
  return (
    <div className="page">
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 16 }}>
        <div>
          <div style={{ fontSize: "1.25rem", fontWeight: 800 }}>📚 Formations</div>
          <div style={{ color: "var(--muted)", fontSize: "0.875rem", marginTop: 2 }}>Documents, quiz et suivi des connaissances</div>
        </div>
        {canManage() && <button className="btn" onClick={openCreate}>+ Créer</button>}
      </div>

      {/* Tabs */}
      <div style={{ display: "flex", gap: 4, marginBottom: 16, borderBottom: "2px solid var(--border)", paddingBottom: 0 }}>
        {[
          { key: "list", label: "Formations" },
          { key: "history", label: "Mon historique" },
          ...(canManage() ? [{ key: "stats", label: "Statistiques" }] : []),
        ].map((t) => (
          <button key={t.key} onClick={() => setTab(t.key)} style={{ padding: "8px 16px", fontWeight: 700, fontSize: "0.875rem", border: "none", background: "none", cursor: "pointer", color: tab === t.key ? "var(--primary)" : "var(--muted)", borderBottom: tab === t.key ? "2px solid var(--primary)" : "2px solid transparent", marginBottom: -2, transition: "all 150ms" }}>
            {t.label}
          </button>
        ))}
      </div>

      {msg && <div className="card" style={{ padding: 12, color: "var(--success)", background: "var(--success-bg)", marginBottom: 12, fontWeight: 600, fontSize: "0.85rem" }}>{msg}</div>}
      {err && <div className="card" style={{ padding: 12, color: "var(--danger)", background: "var(--danger-bg)", marginBottom: 12, fontWeight: 600, fontSize: "0.85rem" }}>{err}</div>}

      {/* TAB: LIST */}
      {tab === "list" && (
        loading ? <div className="card" style={{ padding: 40, textAlign: "center", color: "var(--muted)" }}>Chargement…</div>
          : !trainings.length ? <div className="card" style={{ padding: 40, textAlign: "center" }}><div style={{ fontSize: "2.5rem", marginBottom: 8 }}>📚</div><div style={{ fontWeight: 700 }}>Aucune formation</div></div>
            : <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(300px, 1fr))", gap: 12 }}>
              {trainings.map((t) => (
                <div key={t._id} className="card" style={{ padding: 16, display: "flex", flexDirection: "column", gap: 8 }}>
                  <div style={{ display: "flex", justifyContent: "space-between", gap: 8 }}>
                    <div style={{ flex: 1 }}><div style={{ fontWeight: 800, fontSize: "0.95rem" }}>{t.title}</div>{t.description && <div style={{ color: "var(--muted)", fontSize: "0.82rem", marginTop: 2 }}>{t.description.slice(0, 100)}</div>}</div>
                    {t.category && <span className="badge badge--muted" style={{ fontSize: "0.7rem", flexShrink: 0 }}>{t.category}</span>}
                  </div>
                  <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                    {(t.questions || []).length > 0 && <span className="badge badge--primary" style={{ fontSize: "0.72rem" }}>📝 {t.questions.length} Q</span>}
                    {(t.questions || []).length > 0 && <span className="badge badge--muted" style={{ fontSize: "0.72rem" }}>Seuil: {t.passThreshold ?? 80}%</span>}
                    {(t.questions || []).length > 0 && t.allowMultipleAttempts === false && <span className="badge badge--warning" style={{ fontSize: "0.72rem" }}>1 tentative</span>}
                    {(t.pdfUrl || t.pdfData) && <span className="badge badge--muted" style={{ fontSize: "0.72rem" }}>📄 PDF</span>}
                    {t.videoUrl && <span className="badge badge--primary" style={{ fontSize: "0.72rem" }}>🎬 Vidéo</span>}
                    <span style={{ fontSize: "0.72rem", color: "var(--muted)" }}>{fmtDate(t.createdAt)}</span>
                  </div>
                  {canManage() && t.attemptCount !== undefined && <div style={{ fontSize: "0.78rem", color: "var(--muted)" }}>{t.attemptCount} tentatives • Moy: {t.avgScore}%</div>}
                  {canManage() && ((t.targetCells || []).length > 0 || (t.targetUsers || []).length > 0) && (
                    <div style={{ display: "flex", gap: 4, flexWrap: "wrap" }}>
                      {(t.targetCells || []).map((c) => <span key={c} className="badge badge--muted" style={{ fontSize: "0.68rem" }}>🏢 {c}</span>)}
                      {(t.targetUsers || []).length > 0 && <span className="badge badge--primary" style={{ fontSize: "0.68rem" }}>👤 {t.targetUsers.length} pers.</span>}
                    </div>
                  )}
                  <div style={{ display: "flex", gap: 6, marginTop: "auto" }}>
                    <button className="btn btn--sm" onClick={() => openTraining(t._id)}>Consulter</button>
                    {canManage() && <button className="btn btn--ghost btn--sm" onClick={() => openEdit(t)}>✏ Modifier</button>}
                    {canManage() && (t.questions || []).length > 0 && <button className="btn btn--ghost btn--sm" onClick={() => loadResults(t._id)}>Résultats</button>}
                    {getUser()?.role === "admin" && <button className="btn btn--ghost btn--sm" style={{ color: "var(--danger)" }} onClick={async () => { if (!window.confirm(`Supprimer la formation "${t.title}" ?`)) return; try { await api.delete(`/training/${t._id}`); loadList(); } catch {} }}>🗑</button>}
                  </div>
                </div>
              ))}
            </div>
      )}

      {/* TAB: HISTORY */}
      {tab === "history" && (
        <div className="card" style={{ padding: 0, overflow: "hidden" }}>
          <div style={{ padding: "12px 16px", borderBottom: "1px solid var(--border)", fontWeight: 700, fontSize: "0.9rem" }}>Mes tentatives de quiz</div>
          <table className="data-table"><thead><tr><th>Formation</th><th>Catégorie</th><th>Score</th><th>Rép.</th><th>Date</th></tr></thead>
            <tbody>
              {history.map((h, i) => <tr key={i}><td style={{ fontWeight: 600 }}>{h.trainingTitle}</td><td>{h.category || "—"}</td><td><span className={`badge ${h.percent >= 80 ? "badge--success" : h.percent >= 50 ? "badge--warning" : "badge--danger"}`}>{h.percent}%</span></td><td>{h.score}/{h.total}</td><td style={{ fontSize: "0.82rem" }}>{fmtFull(h.completedAt)}</td></tr>)}
              {!history.length && <tr><td colSpan={5} style={{ textAlign: "center", color: "var(--muted)", padding: 24 }}>Aucun quiz passé.</td></tr>}
            </tbody>
          </table>
        </div>
      )}

      {/* TAB: STATS (admin/formateur) */}
      {tab === "stats" && canManage() && stats && (
        <>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: 10, marginBottom: 16 }}>
            {[
              { l: "Formations", v: stats.totalTrainings, c: "var(--primary)" },
              { l: "Questions totales", v: stats.totalQuestions, c: "var(--text-secondary)" },
              { l: "Tentatives", v: stats.totalAttempts, c: "var(--primary)" },
              { l: "Score moyen global", v: `${stats.avgScore}%`, c: stats.avgScore >= 80 ? "var(--success)" : stats.avgScore >= 50 ? "var(--warning)" : "var(--danger)" },
            ].map((k) => <div key={k.l} className="card" style={{ padding: "12px 14px" }}><div style={{ fontSize: "0.7rem", fontWeight: 700, color: "var(--muted)", textTransform: "uppercase" }}>{k.l}</div><div style={{ fontSize: "1.4rem", fontWeight: 800, marginTop: 2, color: k.c }}>{k.v}</div></div>)}
          </div>
          <div className="card" style={{ padding: 0, overflow: "hidden" }}>
            <div style={{ padding: "12px 16px", borderBottom: "1px solid var(--border)", fontWeight: 700, fontSize: "0.9rem" }}>Détail par formation</div>
            <table className="data-table"><thead><tr><th>Formation</th><th>Catégorie</th><th>Questions</th><th>Tentatives</th><th>Score moyen</th><th>Actions</th></tr></thead>
              <tbody>
                {(stats.byTraining || []).map((t) => <tr key={t.id}><td style={{ fontWeight: 600 }}>{t.title}</td><td>{t.category || "—"}</td><td>{t.questions}</td><td>{t.attempts}</td><td><span className={`badge ${t.avgScore >= 80 ? "badge--success" : t.avgScore >= 50 ? "badge--warning" : "badge--danger"}`}>{t.avgScore}%</span></td><td>{t.attempts > 0 && <button className="btn btn--ghost btn--sm" onClick={() => loadResults(t.id)}>Détails</button>}</td></tr>)}
              </tbody>
            </table>
          </div>
        </>
      )}

      {/* ==================== CREATE/EDIT MODAL ==================== */}
      {creating && (
        <div className="modal-overlay" onClick={() => setCreating(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()} style={{ maxWidth: 740, maxHeight: "92vh", overflow: "auto" }}>
            <div style={{ fontWeight: 800, fontSize: "1.05rem", marginBottom: 16 }}>{editing ? "Modifier la formation" : "Créer une formation"}</div>
            <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
              <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: 10 }}>
                <div><div className="label" style={{ marginBottom: 4 }}>Titre</div><input className="input" value={form.title} onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))} placeholder="Ex: Formation accueil client" /></div>
                <div><div className="label" style={{ marginBottom: 4 }}>Catégorie</div><input className="input" value={form.category} onChange={(e) => setForm((f) => ({ ...f, category: e.target.value }))} placeholder="Ex: Accueil, Technique…" /></div>
              </div>
              <div><div className="label" style={{ marginBottom: 4 }}>Description</div><textarea className="input" rows={2} value={form.description} onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))} /></div>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 }}>
                <div><div className="label" style={{ marginBottom: 4 }}>PDF (fichier)</div><input type="file" accept=".pdf" onChange={(e) => setPdfFile(e.target.files?.[0] || null)} style={{ fontSize: "0.85rem" }} /></div>
                <div><div className="label" style={{ marginBottom: 4 }}>ou URL du PDF</div><input className="input" value={form.pdfUrl} onChange={(e) => setForm((f) => ({ ...f, pdfUrl: e.target.value }))} placeholder="https://…" /></div>
              </div>
              <div>
                <div className="label" style={{ marginBottom: 4 }}>🎬 Vidéo</div>
                <div style={{ display: "flex", gap: 10, alignItems: "flex-start" }}>
                  <div style={{ flex: 1 }}>
                    <div style={{ display: "flex", gap: 8, alignItems: "center", marginBottom: 6 }}>
                      <input type="file" accept="video/mp4,video/webm,video/quicktime,.mp4,.webm,.mov" onChange={async (e) => {
                        const file = e.target.files?.[0]; if (!file) return;
                        setMsg(""); setErr("");
                        const fd = new FormData(); fd.append("file", file);
                        try {
                          const r = await api.post("/training/upload", fd, { headers: { "Content-Type": "multipart/form-data" } });
                          const url = r.data?.url || "";
                          setForm((f) => ({ ...f, videoUrl: url }));
                          setMsg(`Vidéo uploadée (${(file.size / 1024 / 1024).toFixed(1)} MB)`);
                        } catch (ex) { setErr(ex?.response?.data?.message || "Erreur upload vidéo."); }
                      }} style={{ fontSize: "0.85rem" }} />
                    </div>
                    <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                      <span style={{ fontSize: "0.78rem", color: "var(--muted)" }}>ou URL :</span>
                      <input className="input" value={form.videoUrl} onChange={(e) => setForm((f) => ({ ...f, videoUrl: e.target.value }))} placeholder="https://youtube.com/… ou lien direct" style={{ flex: 1, fontSize: "0.85rem" }} />
                    </div>
                  </div>
                  {form.videoUrl && (
                    <div style={{ flexShrink: 0, display: "flex", alignItems: "center", gap: 6 }}>
                      <span className="badge badge--success" style={{ fontSize: "0.72rem" }}>✅ Vidéo prête</span>
                      <button className="btn btn--ghost btn--sm" style={{ fontSize: "0.75rem" }} onClick={() => setForm((f) => ({ ...f, videoUrl: "" }))}>✕</button>
                    </div>
                  )}
                </div>
              </div>
              <div>
                <div className="label" style={{ marginBottom: 4 }}>Rôles cibles <span style={{ fontWeight: 400, color: "var(--muted)" }}>(vide = tous)</span></div>
                <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
                  {["pilote", "cq", "management", "formateur", "admin"].map((r) => (
                    <label key={r} style={{ display: "flex", alignItems: "center", gap: 4, fontSize: "0.85rem" }}>
                      <input type="checkbox" checked={form.roles.includes(r)} onChange={(e) => setForm((f) => ({ ...f, roles: e.target.checked ? [...f.roles, r] : f.roles.filter((x) => x !== r) }))} /> {r}
                    </label>
                  ))}
                </div>
              </div>

              {/* Targeting: cells */}
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 }}>
                <div>
                  <div className="label" style={{ marginBottom: 4 }}>Cellules ciblées <span style={{ fontWeight: 400, color: "var(--muted)" }}>(vide = toutes)</span></div>
                  <div style={{ display: "flex", flexDirection: "column", gap: 4, maxHeight: 120, overflow: "auto", border: "1px solid var(--border)", borderRadius: 8, padding: 8 }}>
                    {cells.length === 0 ? <div style={{ color: "var(--muted)", fontSize: "0.82rem" }}>Aucune cellule.</div> : cells.map((c) => (
                      <label key={c._id} style={{ display: "flex", alignItems: "center", gap: 6, fontSize: "0.85rem" }}>
                        <input type="checkbox" checked={form.targetCells.includes(c.name)} onChange={(e) => setForm((f) => ({ ...f, targetCells: e.target.checked ? [...f.targetCells, c.name] : f.targetCells.filter((x) => x !== c.name) }))} />
                        {c.name}
                      </label>
                    ))}
                  </div>
                </div>
                <div>
                  <div className="label" style={{ marginBottom: 4 }}>Collaborateurs ciblés <span style={{ fontWeight: 400, color: "var(--muted)" }}>(vide = tous)</span></div>
                  <div style={{ display: "flex", flexDirection: "column", gap: 4, maxHeight: 120, overflow: "auto", border: "1px solid var(--border)", borderRadius: 8, padding: 8 }}>
                    {users.length === 0 ? <div style={{ color: "var(--muted)", fontSize: "0.82rem" }}>Aucun utilisateur.</div> : users.map((u) => (
                      <label key={u._id} style={{ display: "flex", alignItems: "center", gap: 6, fontSize: "0.85rem" }}>
                        <input type="checkbox" checked={form.targetUsers.includes(String(u._id))} onChange={(e) => setForm((f) => ({ ...f, targetUsers: e.target.checked ? [...f.targetUsers, String(u._id)] : f.targetUsers.filter((x) => x !== String(u._id)) }))} />
                        {u.name}{u.cell ? <span style={{ color: "var(--muted)", fontSize: "0.78rem" }}> — {u.cell}</span> : ""}
                      </label>
                    ))}
                  </div>
                </div>
              </div>

              {/* Quiz settings */}
              <div style={{ borderTop: "1px solid var(--border)", paddingTop: 14 }}>
                <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 10 }}>⚙ Paramètres du quiz</div>
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
                  <div>
                    <div className="label" style={{ marginBottom: 4 }}>Tentatives autorisées</div>
                    <div style={{ display: "flex", gap: 8 }}>
                      <label style={{ display: "flex", alignItems: "center", gap: 6, padding: "8px 14px", borderRadius: 8, border: form.allowMultipleAttempts ? "1px solid var(--border)" : "2px solid var(--primary)", background: !form.allowMultipleAttempts ? "var(--primary-bg)" : "transparent", cursor: "pointer", fontSize: "0.85rem", fontWeight: !form.allowMultipleAttempts ? 700 : 500 }}>
                        <input type="radio" name="attempts" checked={!form.allowMultipleAttempts} onChange={() => setForm((f) => ({ ...f, allowMultipleAttempts: false }))} style={{ accentColor: "var(--primary)" }} />
                        Une seule
                      </label>
                      <label style={{ display: "flex", alignItems: "center", gap: 6, padding: "8px 14px", borderRadius: 8, border: !form.allowMultipleAttempts ? "1px solid var(--border)" : "2px solid var(--primary)", background: form.allowMultipleAttempts ? "var(--primary-bg)" : "transparent", cursor: "pointer", fontSize: "0.85rem", fontWeight: form.allowMultipleAttempts ? 700 : 500 }}>
                        <input type="radio" name="attempts" checked={form.allowMultipleAttempts} onChange={() => setForm((f) => ({ ...f, allowMultipleAttempts: true }))} style={{ accentColor: "var(--primary)" }} />
                        Plusieurs
                      </label>
                    </div>
                  </div>
                  <div>
                    <div className="label" style={{ marginBottom: 4 }}>Seuil de réussite (%)</div>
                    <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                      <input className="input" type="number" min="0" max="100" value={form.passThreshold} onChange={(e) => setForm((f) => ({ ...f, passThreshold: Math.max(0, Math.min(100, Number(e.target.value) || 0)) }))} style={{ width: 80, textAlign: "center" }} />
                      <span style={{ fontSize: "0.82rem", color: "var(--muted)" }}>%</span>
                      <div style={{ flex: 1, height: 6, borderRadius: 3, background: "var(--chip)", overflow: "hidden" }}>
                        <div style={{ height: "100%", width: `${form.passThreshold}%`, background: "var(--primary)", borderRadius: 3, transition: "width 200ms" }} />
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              {/* Quiz builder */}
              <div style={{ borderTop: "1px solid var(--border)", paddingTop: 14 }}>
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 10 }}>
                  <div style={{ fontWeight: 700, fontSize: "0.9rem" }}>📝 Quiz ({form.questions.length})</div>
                  <button className="btn btn--sm" onClick={addQ}>+ Question</button>
                </div>
                {form.questions.map((q, qi) => (
                  <div key={q._lid || qi} style={{ padding: 14, borderRadius: 12, border: "1px solid var(--border)", background: "var(--panel-2)", marginBottom: 10 }}>
                    <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 8 }}>
                      <span style={{ fontWeight: 800, fontSize: "0.85rem", color: "var(--primary)" }}>Q{qi + 1}</span>
                      <button className="btn btn--ghost btn--sm" style={{ color: "var(--danger)" }} onClick={() => rmQ(qi)}>Supprimer</button>
                    </div>
                    <input className="input" value={q.question} onChange={(e) => upQ(qi, "question", e.target.value)} placeholder="Intitulé…" style={{ marginBottom: 8 }} />
                    <div style={{ marginBottom: 10 }}>
                      <div className="label" style={{ marginBottom: 4, fontSize: "0.78rem" }}>Image (optionnelle)</div>
                      <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
                        <input type="file" accept="image/*" onChange={(e) => imgQ(qi, e.target.files?.[0])} style={{ fontSize: "0.8rem" }} />
                        {q.imageData && <><img src={q.imageData} alt="" style={{ width: 48, height: 48, borderRadius: 8, objectFit: "cover", border: "1px solid var(--border)" }} /><button className="btn btn--ghost btn--sm" onClick={() => upQ(qi, "imageData", "")}>✕</button></>}
                      </div>
                    </div>
                    <div className="label" style={{ marginBottom: 4, fontSize: "0.78rem" }}>Choix ({q.options.length})</div>
                    {(q.options || []).map((opt, oi) => (
                      <div key={oi} style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 4 }}>
                        <input type="radio" name={`eq${qi}`} checked={q.correctIndex === oi} onChange={() => upQ(qi, "correctIndex", oi)} style={{ accentColor: "var(--success)" }} />
                        <input className="input" value={opt} onChange={(e) => upOpt(qi, oi, e.target.value)} placeholder={`Choix ${oi + 1}`} style={{ flex: 1, fontSize: "0.85rem", padding: "4px 8px" }} />
                        {q.options.length > 2 && <button className="btn btn--ghost btn--sm" style={{ color: "var(--danger)", padding: "2px 6px" }} onClick={() => rmOpt(qi, oi)}>✕</button>}
                      </div>
                    ))}
                    <button className="btn btn--ghost btn--sm" style={{ marginTop: 4, fontSize: "0.78rem" }} onClick={() => addOpt(qi)}>+ Choix</button>
                  </div>
                ))}
                {!form.questions.length && <div style={{ color: "var(--muted)", textAlign: "center", padding: 16 }}>Aucune question.</div>}
              </div>
            </div>
            <div style={{ display: "flex", justifyContent: "flex-end", gap: 8, marginTop: 16 }}>
              <button className="btn btn--ghost" onClick={() => setCreating(false)}>Annuler</button>
              <button className="btn" onClick={handleSave} disabled={saving}>{saving ? "Sauvegarde…" : editing ? "Enregistrer" : "Créer"}</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
