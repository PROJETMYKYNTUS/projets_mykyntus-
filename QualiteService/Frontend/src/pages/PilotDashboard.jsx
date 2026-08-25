import React, { useEffect, useState, useMemo } from "react";
import api from "../api.js";
import { ResponsiveContainer, AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip } from "recharts";

function getRefDate(s) { const d = s.listeningDate || s.interactionDate || s.callDate || s.createdAt; return d ? new Date(d) : null; }
const MONTHS = ["Jan","Fév","Mar","Avr","Mai","Juin","Juil","Août","Sep","Oct","Nov","Déc"];

function PilotDashboard() {
  const [loading, setLoading] = useState(true);
  const [scores, setScores] = useState([]);
  const [globalAverage, setGlobalAverage] = useState(0);
  const [totalCount, setTotalCount] = useState(0);
  const [selectedEval, setSelectedEval] = useState(null);
  const [filterYear, setFilterYear] = useState("all");
  const [filterMonth, setFilterMonth] = useState("all");

  const user = useMemo(() => { try { return JSON.parse(localStorage.getItem("user") || "null"); } catch { return null; } }, []);

  useEffect(() => {
    (async () => {
      try { const r = await api.get("/scores/me"); setGlobalAverage(r.data?.average || 0); setTotalCount(r.data?.count || 0); setScores(r.data?.scores || []); }
      catch {} finally { setLoading(false); }
    })();
  }, []);

  const years = useMemo(() => [...new Set(scores.map((s) => getRefDate(s)?.getFullYear()).filter(Boolean))].sort((a, b) => b - a), [scores]);

  const filteredScores = useMemo(() => scores.filter((s) => {
    const d = getRefDate(s); if (!d) return false;
    if (filterYear !== "all" && String(d.getFullYear()) !== filterYear) return false;
    if (filterMonth !== "all" && String(d.getMonth() + 1) !== filterMonth) return false;
    return true;
  }), [scores, filterYear, filterMonth]);

  const now = new Date();
  const currentMonthCount = useMemo(() => scores.filter((s) => { const d = getRefDate(s); return d && d.getFullYear() === now.getFullYear() && d.getMonth() === now.getMonth(); }).length, [scores]);
  const contestedCount = useMemo(() => scores.filter((s) => s.contested).length, [scores]);

  const itemsStats = useMemo(() => {
    const map = new Map();
    scores.forEach((s) => (s.items || []).forEach((it) => {
      if (it.type === "group") return;
      const key = it.label || "Item";
      const status = (it.status || "").toUpperCase();
      if (!map.has(key)) map.set(key, { label: key, c: 0, nc: 0, total: 0 });
      const o = map.get(key);
      if (status === "NA") return;
      if (["C", "PC"].includes(status)) { o.c++; o.total++; }
      else if (["NC", "PNC", "NP"].includes(status)) { o.nc++; o.total++; }
    }));
    return Array.from(map.values()).map((o) => ({ ...o, pct: o.total > 0 ? Math.round((o.c / o.total) * 100) : 0 })).sort((a, b) => a.pct - b.pct);
  }, [scores]);

  const bestItem = useMemo(() => itemsStats.length ? [...itemsStats].sort((a, b) => b.pct - a.pct)[0] : null, [itemsStats]);
  const worstItem = useMemo(() => itemsStats.length ? itemsStats[0] : null, [itemsStats]);

  const trendData = useMemo(() => {
    const m = new Map();
    scores.forEach((s) => { const d = getRefDate(s); if (!d) return; const k = `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,"0")}`; if (!m.has(k)) m.set(k, { month: `${MONTHS[d.getMonth()]} ${String(d.getFullYear()).slice(2)}`, sum: 0, n: 0 }); const e = m.get(k); e.sum += Number(s.total || 0); e.n++; });
    return Array.from(m.entries()).sort(([a],[b]) => a.localeCompare(b)).map(([,v]) => ({ ...v, avg: Math.round(v.sum / v.n) }));
  }, [scores]);

  // Score color helper
  const sc = (v) => v >= 80 ? "var(--success)" : v >= 50 ? "var(--warning)" : "var(--danger)";
  const scBg = (v) => v >= 80 ? "var(--success-bg)" : v >= 50 ? "var(--warning-bg)" : "var(--danger-bg)";
  const scBadge = (v) => v >= 80 ? "badge--success" : v >= 50 ? "badge--warning" : "badge--danger";

  if (loading) return <div className="page"><div className="card" style={{ padding: 40, textAlign: "center", color: "var(--muted)" }}>Chargement…</div></div>;

  return (
    <div className="page">
      {/* ===== Welcome + Main Score ===== */}
      <div className="cq-pilot-welcome" style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 16, marginBottom: 20 }}>
        <div className="cq-dup-title">
          <div style={{ fontSize: "1.4rem", fontWeight: 800, letterSpacing: "-0.02em" }}>Bonjour {user?.name || "Pilote"} 👋</div>
          <div style={{ color: "var(--muted)", fontSize: "0.9rem", marginTop: 4, lineHeight: 1.5 }}>
            Mon tableau de bord — suivez vos performances et vos axes de progrès.
          </div>
        </div>

        {/* Big score gauge */}
        <div className="card" style={{ padding: 20, display: "flex", alignItems: "center", gap: 20, background: scBg(globalAverage) }}>
          <div style={{ position: "relative", width: 80, height: 80, flexShrink: 0 }}>
            <svg viewBox="0 0 36 36" style={{ width: 80, height: 80, transform: "rotate(-90deg)" }}>
              <circle cx="18" cy="18" r="15.5" fill="none" stroke="var(--chip)" strokeWidth="3" />
              <circle cx="18" cy="18" r="15.5" fill="none" stroke={sc(globalAverage)} strokeWidth="3" strokeDasharray={`${globalAverage * 0.974} 100`} strokeLinecap="round" />
            </svg>
            <div style={{ position: "absolute", inset: 0, display: "grid", placeItems: "center", fontWeight: 900, fontSize: "1.1rem", color: sc(globalAverage) }}>{Math.round(globalAverage)}%</div>
          </div>
          <div>
            <div style={{ fontWeight: 800, fontSize: "1rem" }}>Votre score qualité</div>
            <div style={{ fontSize: "0.85rem", color: "var(--text-secondary)", marginTop: 4 }}>
              {globalAverage >= 80 ? "Excellent travail ! Continuez comme ça." : globalAverage >= 50 ? "Des progrès à faire sur certains critères." : "Attention, des actions correctives sont nécessaires."}
            </div>
          </div>
        </div>
      </div>

      {/* ===== KPIs row ===== */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: 10, marginBottom: 20 }}>
        <div className="card" style={{ padding: "14px 16px", textAlign: "center" }}>
          <div style={{ fontSize: "2rem", fontWeight: 900, color: "var(--primary)" }}>{totalCount}</div>
          <div style={{ fontSize: "0.78rem", fontWeight: 600, color: "var(--muted)", marginTop: 2 }}>Évaluations totales</div>
        </div>
        <div className="card" style={{ padding: "14px 16px", textAlign: "center" }}>
          <div style={{ fontSize: "2rem", fontWeight: 900 }}>{currentMonthCount}</div>
          <div style={{ fontSize: "0.78rem", fontWeight: 600, color: "var(--muted)", marginTop: 2 }}>Ce mois-ci</div>
        </div>
        <div className="card" style={{ padding: "14px 16px", textAlign: "center" }}>
          <div style={{ fontSize: "2rem", fontWeight: 900, color: contestedCount > 0 ? "var(--warning)" : "var(--success)" }}>{contestedCount}</div>
          <div style={{ fontSize: "0.78rem", fontWeight: 600, color: "var(--muted)", marginTop: 2 }}>Contestées</div>
        </div>
        <div className="card" style={{ padding: "14px 16px", textAlign: "center" }}>
          <div style={{ fontSize: "2rem", fontWeight: 900, color: "var(--success)" }}>{itemsStats.filter((i) => i.pct >= 80).length}<span style={{ fontSize: "1rem", color: "var(--muted)" }}>/{itemsStats.length}</span></div>
          <div style={{ fontSize: "0.78rem", fontWeight: 600, color: "var(--muted)", marginTop: 2 }}>Critères maîtrisés</div>
        </div>
      </div>

      {/* ===== Trend Chart ===== */}
      <div className="card" style={{ padding: 16, marginBottom: 20 }}>
        <div style={{ fontWeight: 800, fontSize: "1rem", marginBottom: 12 }}>📈 Évolution de votre score</div>
        {trendData.length > 1 ? (
          <ResponsiveContainer width="100%" height={200}>
            <AreaChart data={trendData}>
              <defs><linearGradient id="gP" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor="#4f46e5" stopOpacity={0.2}/><stop offset="100%" stopColor="#4f46e5" stopOpacity={0}/></linearGradient></defs>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
              <XAxis dataKey="month" tick={{ fontSize: 11 }} />
              <YAxis domain={[0, 100]} tick={{ fontSize: 11 }} />
              <Tooltip formatter={(v) => `${v}%`} contentStyle={{ borderRadius: 10, border: "1px solid var(--border)", fontSize: "0.85rem" }} />
              <Area type="monotone" dataKey="avg" stroke="#4f46e5" strokeWidth={2.5} fill="url(#gP)" dot={{ r: 4, fill: "#4f46e5" }} />
            </AreaChart>
          </ResponsiveContainer>
        ) : trendData.length === 1 ? (
          <div style={{ textAlign: "center", padding: 20, color: "var(--muted)", fontSize: "0.9rem" }}>Score de {trendData[0].avg}% sur {trendData[0].month}. Les tendances apparaîtront avec plus de données.</div>
        ) : (
          <div style={{ textAlign: "center", padding: 20, color: "var(--muted)" }}>Pas encore de données.</div>
        )}
      </div>

      {/* ===== Criteria breakdown ===== */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 16, marginBottom: 20 }}>
        {/* Per-criteria bars */}
        <div className="card" style={{ padding: 16 }}>
          <div style={{ fontWeight: 800, fontSize: "1rem", marginBottom: 12 }}>📊 Vos résultats par critère</div>
          {itemsStats.length > 0 ? (
            <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
              {[...itemsStats].sort((a, b) => b.pct - a.pct).map((it) => (
                <div key={it.label}>
                  <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 4 }}>
                    <span style={{ fontSize: "0.85rem", fontWeight: 600 }}>{it.label}</span>
                    <span style={{ fontSize: "0.85rem", fontWeight: 800, color: sc(it.pct) }}>{it.pct}%</span>
                  </div>
                  <div style={{ height: 8, borderRadius: 4, background: "var(--chip)", overflow: "hidden" }}>
                    <div style={{ height: "100%", width: `${it.pct}%`, borderRadius: 4, background: sc(it.pct), transition: "width 0.5s" }} />
                  </div>
                  <div style={{ fontSize: "0.72rem", color: "var(--muted)", marginTop: 2 }}>{it.c} conforme{it.c > 1 ? "s" : ""} / {it.total} évalué{it.total > 1 ? "s" : ""}</div>
                </div>
              ))}
            </div>
          ) : <div style={{ color: "var(--muted)", textAlign: "center", padding: 20 }}>Pas de données.</div>}
        </div>

        {/* Strengths + Improvements */}
        <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          {/* Best */}
          <div className="card" style={{ padding: 16, background: "var(--success-bg)", flex: 1 }}>
            <div style={{ fontWeight: 800, fontSize: "1rem", marginBottom: 8, color: "var(--success)" }}>💪 Votre point fort</div>
            {bestItem ? (
              <div>
                <div style={{ fontSize: "1.1rem", fontWeight: 800 }}>{bestItem.label}</div>
                <div style={{ fontSize: "0.85rem", color: "var(--text-secondary)", marginTop: 4 }}>Taux de conformité de {bestItem.pct}% — continuez ainsi !</div>
              </div>
            ) : <div style={{ color: "var(--muted)" }}>En attente de données.</div>}
          </div>

          {/* Worst / Improvement */}
          <div className="card" style={{ padding: 16, background: worstItem && worstItem.pct < 70 ? "var(--danger-bg)" : "var(--panel-2)", flex: 1 }}>
            <div style={{ fontWeight: 800, fontSize: "1rem", marginBottom: 8, color: worstItem && worstItem.pct < 70 ? "var(--danger)" : "var(--text-secondary)" }}>
              {worstItem && worstItem.pct < 70 ? "⚠ Axe d'amélioration prioritaire" : "🎯 Critère à surveiller"}
            </div>
            {worstItem ? (
              <div>
                <div style={{ fontSize: "1.1rem", fontWeight: 800 }}>{worstItem.label}</div>
                <div style={{ fontSize: "0.85rem", color: "var(--text-secondary)", marginTop: 4 }}>
                  Taux de {worstItem.pct}% — {worstItem.pct < 50 ? "Concentrez-vous sur ce point lors de vos prochains appels." : worstItem.pct < 70 ? "Des progrès sont attendus, pensez à revoir les bonnes pratiques." : "Bon niveau, restez vigilant."}
                </div>
              </div>
            ) : <div style={{ color: "var(--muted)" }}>En attente de données.</div>}
          </div>

          {/* All below 70% */}
          {itemsStats.filter((i) => i.pct < 70).length > 0 && (
            <div className="card" style={{ padding: 16 }}>
              <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 8 }}>📋 Critères sous 70%</div>
              {itemsStats.filter((i) => i.pct < 70).map((it) => (
                <div key={it.label} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "6px 0", borderBottom: "1px solid var(--border)" }}>
                  <span style={{ fontSize: "0.85rem", fontWeight: 600 }}>{it.label}</span>
                  <span className="badge badge--danger">{it.pct}%</span>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

export default PilotDashboard;
