import React, { useEffect, useMemo, useState } from "react";
import api from "../../api";
import { isCqEmbed } from "../../embed.js";
import {
  ResponsiveContainer, BarChart, Bar, LineChart, Line,
  XAxis, YAxis, Tooltip, CartesianGrid, PieChart, Pie,
  Cell as RCell, AreaChart, Area,
} from "recharts";

const pct = (v) => `${Math.round(Number(v || 0) <= 1 ? Number(v) * 100 : Number(v))}%`;
const scoreP = (s) => {
  const v = s?.compliancePercent ?? s?.avgPercent ?? null;
  if (typeof v === "number" && !Number.isNaN(v)) return v <= 1 ? v * 100 : v;
  const v3 = s?.avgScore; if (typeof v3 === "number") return v3 * 20;
  return 0;
};
const fmtD = (d) => { if (!d) return "—"; const dt = new Date(d); return Number.isNaN(dt.getTime()) ? "—" : dt.toLocaleDateString("fr-FR", { day: "2-digit", month: "short" }); };
const fmtFull = (d) => { if (!d) return "—"; const dt = new Date(d); return Number.isNaN(dt.getTime()) ? "—" : dt.toLocaleDateString("fr-FR", { day: "2-digit", month: "short", hour: "2-digit", minute: "2-digit" }); };
const COLORS = ["#4f46e5","#059669","#d97706","#dc2626","#8b5cf6","#0891b2","#be185d","#65a30d"];
const MONTHS = ["Jan","Fév","Mar","Avr","Mai","Juin","Juil","Août","Sep","Oct","Nov","Déc"];

export default function DashboardView() {
  const [loading, setLoading] = useState(true);
  const [stats, setStats] = useState(null);
  const [recent, setRecent] = useState([]);
  const [contested, setContested] = useState(0);
  const [users, setUsers] = useState([]);
  const [allScores, setAllScores] = useState([]);
  const [prevMonthScores, setPrevMonthScores] = useState({});
  const [coachings, setCoachings] = useState([]);
  const [refreshKey, setRefreshKey] = useState(0);

  useEffect(() => {
    let m = true;
    (async () => {
      setLoading(true);
      try {
        const now = new Date();
        const year = String(now.getFullYear());
        const month = String(now.getMonth() + 1);
        const prevM = now.getMonth() === 0 ? "12" : String(now.getMonth());
        const prevY = now.getMonth() === 0 ? String(now.getFullYear() - 1) : year;

        // 6 parallel calls (reduced from 7, lower limits)
        const [statsR, prevStatsR, recentR, usersR, scoresR, coachR] = await Promise.all([
          api.get("/scores/stats", { params: { year, month } }),
          api.get("/scores/stats", { params: { year: prevY, month: prevM } }).catch(() => ({ data: {} })),
          api.get("/scores", { params: { year, month, page: 1, limit: 15 } }),
          api.get("/admin/users").catch(() => ({ data: [] })),
          api.get("/scores", { params: { year, month, page: 1, limit: 500 } }).catch(() => ({ data: { items: [] } })),
          api.get("/coaching", { params: { limit: 100 } }).catch(() => ({ data: { items: [] } })),
        ]);
        if (!m) return;
        setStats(statsR.data || null);
        setPrevMonthScores(prevStatsR.data || {});
        setRecent(Array.isArray(recentR.data?.items) ? recentR.data.items : []);
        setContested(statsR.data?.contestedCount || 0);
        setUsers(Array.isArray(usersR.data) ? usersR.data : []);
        setAllScores(Array.isArray(scoresR.data?.items) ? scoresR.data.items : []);
        setCoachings(Array.isArray(coachR.data?.items) ? coachR.data.items : []);
      } catch {} finally { if (m) setLoading(false); }
    })();
    return () => { m = false; };
  }, [refreshKey]);

  // ---- Derived ----
  const uc = useMemo(() => {
    const u = users;
    return { total: u.length, pilotes: u.filter((x) => x.role === "pilote").length, cq: u.filter((x) => x.role === "cq").length, mgmt: u.filter((x) => x.role === "management").length, active: u.filter((x) => x.active !== false).length, inactive: u.filter((x) => x.active === false).length };
  }, [users]);

  const total = stats?.total || 0;
  const avgP = stats?.avgPercent || 0;
  const cRate = stats?.contestedRate || 0;

  // Previous month comparison — now uses stats object
  const prevAvg = useMemo(() => prevMonthScores?.avgPercent || 0, [prevMonthScores]);
  const deltaAvg = avgP - prevAvg;
  const deltaVolume = total - (prevMonthScores?.total || 0);

  // Score distribution
  const scoreDist = useMemo(() => {
    let h = 0, m = 0, l = 0;
    for (const s of allScores) { const p = scoreP(s); if (p >= 80) h++; else if (p >= 50) m++; else l++; }
    return [{ name: "≥ 80%", value: h, color: "#059669" }, { name: "50-79%", value: m, color: "#d97706" }, { name: "< 50%", value: l, color: "#dc2626" }].filter((d) => d.value > 0);
  }, [allScores]);

  // Per-evaluator
  const evalPerf = useMemo(() => {
    const map = new Map();
    for (const s of allScores) { const n = s.evaluatorName || s.evaluator?.name || "?"; if (!map.has(n)) map.set(n, { name: n, count: 0, sum: 0 }); const e = map.get(n); e.count++; e.sum += scoreP(s); }
    return Array.from(map.values()).map((e) => ({ ...e, avg: Math.round(e.sum / e.count) })).sort((a, b) => b.count - a.count).slice(0, 10);
  }, [allScores]);

  // Per-cell
  const cellPerf = useMemo(() => {
    const map = new Map();
    for (const s of allScores) { const c = s.pilotCell || s.pilot?.cell || "—"; if (!map.has(c)) map.set(c, { cell: c, count: 0, sum: 0 }); const e = map.get(c); e.count++; e.sum += scoreP(s); }
    return Array.from(map.values()).map((e) => ({ ...e, avg: Math.round(e.sum / e.count) })).sort((a, b) => b.count - a.count).slice(0, 10);
  }, [allScores]);

  // Top & Bottom 5 agents
  const agentPerf = useMemo(() => {
    const map = new Map();
    for (const s of allScores) {
      const n = s.pilotName || s.pilot?.name || "?";
      const id = s.pilot?._id || s.pilotId || n;
      const key = String(id);
      if (!map.has(key)) map.set(key, { name: n, cell: s.pilotCell || s.pilot?.cell || "", count: 0, sum: 0 });
      const e = map.get(key); e.count++; e.sum += scoreP(s);
    }
    return Array.from(map.values()).filter((e) => e.count >= 1).map((e) => ({ ...e, avg: Math.round(e.sum / e.count) }));
  }, [allScores]);
  const topAgents = useMemo(() => [...agentPerf].sort((a, b) => b.avg - a.avg).slice(0, 5), [agentPerf]);
  const bottomAgents = useMemo(() => [...agentPerf].sort((a, b) => a.avg - b.avg).slice(0, 5), [agentPerf]);

  // Daily volume trend (current month)
  const dailyTrend = useMemo(() => {
    const map = new Map();
    for (const s of allScores) {
      const d = s.createdAt ? new Date(s.createdAt) : null;
      if (!d) continue;
      const key = `${d.getDate()}`;
      if (!map.has(key)) map.set(key, { day: d.getDate(), label: `${d.getDate()}/${d.getMonth()+1}`, count: 0, sum: 0 });
      const e = map.get(key); e.count++; e.sum += scoreP(s);
    }
    return Array.from(map.values()).sort((a, b) => a.day - b.day).map((e) => ({ ...e, avg: Math.round(e.sum / e.count) }));
  }, [allScores]);

  // Coaching pipeline
  const coachPipeline = useMemo(() => ({
    open: coachings.filter((c) => c.status === "open").length,
    in_progress: coachings.filter((c) => c.status === "in_progress").length,
    done: coachings.filter((c) => c.status === "done").length,
    overdue: coachings.filter((c) => c.status !== "done" && c.followUpDate && new Date(c.followUpDate) < new Date(new Date().toDateString())).length,
    total: coachings.length,
  }), [coachings]);

  // CQ vs Management split
  const roleSplit = useMemo(() => {
    let cq = 0, mgmt = 0;
    for (const s of allScores) { const r = s.evaluatorRole || s.evaluator?.role || ""; if (r === "cq") cq++; else if (r === "management") mgmt++; }
    return { cq, mgmt };
  }, [allScores]);

  const Delta = ({ value, suffix = "" }) => {
    if (!value || value === 0) return <span style={{ fontSize: "0.75rem", color: "var(--muted)" }}>—</span>;
    const pos = value > 0;
    return <span style={{ fontSize: "0.75rem", fontWeight: 700, color: pos ? "var(--success)" : "var(--danger)" }}>{pos ? "▲" : "▼"} {Math.abs(value).toFixed(1)}{suffix}</span>;
  };

  return (
    <div className="page">
      {/* Header */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 16 }}>
        <div>
          {!isCqEmbed() && (
            <div style={{ fontSize: "1.25rem", fontWeight: 800, letterSpacing: "-0.02em" }}>Cockpit de supervision</div>
          )}
          <div style={{ color: "var(--muted)", fontSize: "0.875rem", marginTop: isCqEmbed() ? 0 : 2 }}>
            {new Date().toLocaleDateString("fr-FR", { weekday: "long", day: "numeric", month: "long", year: "numeric" })}
          </div>
        </div>
        <button className="btn btn--ghost btn--sm" onClick={() => setRefreshKey((k) => k + 1)} disabled={loading}>
          {loading ? "Chargement…" : "↻ Rafraîchir"}
        </button>
      </div>

      {/* ===== KPI ROW ===== */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(6, 1fr)", gap: 10, marginBottom: 16 }}>
        {[
          { label: "Note QA", value: loading ? "…" : pct(avgP), color: avgP >= 80 ? "var(--success)" : avgP >= 50 ? "var(--warning)" : "var(--danger)", delta: <Delta value={deltaAvg} suffix="%" /> },
          { label: "Volume (mois)", value: loading ? "…" : String(total), color: "var(--primary)", delta: <Delta value={deltaVolume} /> },
          { label: "Contestations", value: loading ? "…" : String(contested), color: contested > 0 ? "var(--warning)" : "var(--success)", delta: null },
          { label: "Taux contestation", value: loading ? "…" : pct(cRate), color: cRate > 5 ? "var(--danger)" : "var(--success)", delta: null },
          { label: "Coachings actifs", value: loading ? "…" : String(coachPipeline.open + coachPipeline.in_progress), color: "var(--primary)", delta: coachPipeline.overdue > 0 ? <span style={{ fontSize: "0.75rem", fontWeight: 700, color: "var(--danger)" }}>⏰ {coachPipeline.overdue} en retard</span> : null },
          { label: "Agents actifs", value: loading ? "…" : String(uc.pilotes), color: "var(--text-secondary)", delta: null },
        ].map((kpi) => (
          <div key={kpi.label} className="card" style={{ padding: "12px 14px" }}>
            <div style={{ fontSize: "0.68rem", fontWeight: 700, color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>{kpi.label}</div>
            <div style={{ fontSize: "1.5rem", fontWeight: 800, marginTop: 2, color: kpi.color, letterSpacing: "-0.02em" }}>{kpi.value}</div>
            {kpi.delta && <div style={{ marginTop: 2 }}>{kpi.delta}</div>}
          </div>
        ))}
      </div>

      {/* ===== ROW 2: Daily trend + Score dist + CQ/Mgmt split ===== */}
      <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr 1fr", gap: 12, marginBottom: 16 }}>
        {/* Daily volume & avg */}
        <div className="card" style={{ padding: 16 }}>
          <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 10 }}>Volume quotidien & score moyen</div>
          {dailyTrend.length > 0 ? (
            <ResponsiveContainer width="100%" height={200}>
              <AreaChart data={dailyTrend}>
                <defs>
                  <linearGradient id="gVol" x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor="#4f46e5" stopOpacity={0.2} /><stop offset="100%" stopColor="#4f46e5" stopOpacity={0} /></linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
                <XAxis dataKey="label" tick={{ fontSize: 10 }} />
                <YAxis yAxisId="left" tick={{ fontSize: 10 }} />
                <YAxis yAxisId="right" orientation="right" domain={[0, 100]} tick={{ fontSize: 10 }} />
                <Tooltip contentStyle={{ borderRadius: 10, border: "1px solid var(--border)", fontSize: "0.8rem" }} />
                <Area yAxisId="left" type="monotone" dataKey="count" stroke="#4f46e5" fill="url(#gVol)" name="Évaluations" />
                <Line yAxisId="right" type="monotone" dataKey="avg" stroke="#059669" strokeWidth={2} dot={{ r: 3 }} name="Score moyen %" />
              </AreaChart>
            </ResponsiveContainer>
          ) : <div style={{ color: "var(--muted)", padding: 30, textAlign: "center" }}>Aucune donnée ce mois.</div>}
        </div>

        {/* Score distribution donut */}
        <div className="card" style={{ padding: 16, display: "flex", flexDirection: "column" }}>
          <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 10 }}>Répartition qualité</div>
          {scoreDist.length > 0 ? (
            <div style={{ flex: 1, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", gap: 10 }}>
              <ResponsiveContainer width={130} height={130}>
                <PieChart><Pie data={scoreDist} cx="50%" cy="50%" innerRadius={35} outerRadius={55} dataKey="value" paddingAngle={3}>
                  {scoreDist.map((d, i) => <RCell key={i} fill={d.color} />)}
                </Pie></PieChart>
              </ResponsiveContainer>
              {scoreDist.map((d) => (
                <div key={d.name} style={{ display: "flex", alignItems: "center", gap: 6, width: "100%" }}>
                  <div style={{ width: 8, height: 8, borderRadius: 2, background: d.color, flexShrink: 0 }} />
                  <span style={{ fontSize: "0.78rem", fontWeight: 600, flex: 1 }}>{d.name}</span>
                  <span style={{ fontSize: "0.78rem", fontWeight: 800, color: d.color }}>{d.value}</span>
                </div>
              ))}
            </div>
          ) : <div style={{ color: "var(--muted)", flex: 1, display: "grid", placeItems: "center" }}>—</div>}
        </div>

        {/* CQ vs Management + Coaching pipeline */}
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <div className="card" style={{ padding: 14 }}>
            <div style={{ fontWeight: 700, fontSize: "0.85rem", marginBottom: 8 }}>Évaluations par type</div>
            <div style={{ display: "flex", gap: 8 }}>
              <div style={{ flex: 1, padding: "8px 10px", borderRadius: 8, background: "var(--primary-bg)", textAlign: "center" }}>
                <div style={{ fontSize: "0.68rem", fontWeight: 700, color: "var(--muted)", textTransform: "uppercase" }}>CQ</div>
                <div style={{ fontSize: "1.2rem", fontWeight: 800, color: "var(--primary)" }}>{roleSplit.cq}</div>
              </div>
              <div style={{ flex: 1, padding: "8px 10px", borderRadius: 8, background: "var(--warning-bg)", textAlign: "center" }}>
                <div style={{ fontSize: "0.68rem", fontWeight: 700, color: "var(--muted)", textTransform: "uppercase" }}>Management</div>
                <div style={{ fontSize: "1.2rem", fontWeight: 800, color: "var(--warning)" }}>{roleSplit.mgmt}</div>
              </div>
            </div>
          </div>
          <div className="card" style={{ padding: 14, flex: 1 }}>
            <div style={{ fontWeight: 700, fontSize: "0.85rem", marginBottom: 8 }}>Pipeline coaching</div>
            {[
              { l: "Ouverts", v: coachPipeline.open, c: "var(--warning)" },
              { l: "En cours", v: coachPipeline.in_progress, c: "var(--primary)" },
              { l: "Terminés", v: coachPipeline.done, c: "var(--success)" },
              { l: "En retard", v: coachPipeline.overdue, c: "var(--danger)" },
            ].map((r) => (
              <div key={r.l} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "4px 0" }}>
                <span style={{ fontSize: "0.8rem", fontWeight: 600 }}>{r.l}</span>
                <span style={{ fontSize: "0.85rem", fontWeight: 800, color: r.c }}>{r.v}</span>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* ===== ROW 3: Evaluator perf + Cell perf ===== */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, marginBottom: 16 }}>
        <div className="card" style={{ padding: 16 }}>
          <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 10 }}>Performance par évaluateur</div>
          {evalPerf.length > 0 ? (
            <ResponsiveContainer width="100%" height={Math.max(180, evalPerf.length * 28)}>
              <BarChart data={evalPerf} layout="vertical" margin={{ left: 0, right: 10 }}>
                <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
                <XAxis type="number" domain={[0, 100]} tick={{ fontSize: 10 }} />
                <YAxis type="category" dataKey="name" width={100} tick={{ fontSize: 10 }} />
                <Tooltip formatter={(v) => `${v}%`} contentStyle={{ borderRadius: 10, border: "1px solid var(--border)", fontSize: "0.8rem" }} />
                <Bar dataKey="avg" fill="#4f46e5" radius={[0, 4, 4, 0]} barSize={14}>
                  {evalPerf.map((e, i) => <RCell key={i} fill={e.avg >= 80 ? "#059669" : e.avg >= 50 ? "#d97706" : "#dc2626"} />)}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          ) : <div style={{ color: "var(--muted)", padding: 20, textAlign: "center" }}>—</div>}
        </div>

        <div className="card" style={{ padding: 16 }}>
          <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 10 }}>Performance par cellule</div>
          {cellPerf.length > 0 ? (
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {cellPerf.map((c, i) => (
                <div key={c.cell} style={{ display: "flex", alignItems: "center", gap: 8 }}>
                  <div style={{ width: 80, fontSize: "0.8rem", fontWeight: 700, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{c.cell}</div>
                  <div style={{ flex: 1, background: "var(--chip)", borderRadius: 4, height: 18, overflow: "hidden", position: "relative" }}>
                    <div style={{ height: "100%", width: `${Math.min(c.avg, 100)}%`, background: c.avg >= 80 ? "#059669" : c.avg >= 50 ? "#d97706" : "#dc2626", borderRadius: 4, transition: "width 0.5s" }} />
                  </div>
                  <span style={{ fontWeight: 800, fontSize: "0.78rem", minWidth: 40, textAlign: "right" }}>{c.avg}%</span>
                  <span className="badge badge--muted" style={{ fontSize: "0.68rem" }}>{c.count}</span>
                </div>
              ))}
            </div>
          ) : <div style={{ color: "var(--muted)", padding: 20, textAlign: "center" }}>—</div>}
        </div>
      </div>

      {/* ===== ROW 4: Top/Bottom agents + Recent evals ===== */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12, marginBottom: 16 }}>
        {/* Top agents */}
        <div className="card" style={{ padding: 16 }}>
          <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 10, color: "var(--success)" }}>🏆 Top 5 agents</div>
          {topAgents.map((a, i) => (
            <div key={a.name + i} style={{ display: "flex", alignItems: "center", gap: 8, padding: "6px 0", borderBottom: i < 4 ? "1px solid var(--border)" : "none" }}>
              <span style={{ width: 20, fontWeight: 800, fontSize: "0.85rem", color: "var(--muted)" }}>{i + 1}</span>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontWeight: 700, fontSize: "0.85rem", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{a.name}</div>
                <div style={{ fontSize: "0.72rem", color: "var(--muted)" }}>{a.cell || "—"} • {a.count} éval.</div>
              </div>
              <span className="badge badge--success">{a.avg}%</span>
            </div>
          ))}
          {topAgents.length === 0 && <div style={{ color: "var(--muted)", textAlign: "center", padding: 16, fontSize: "0.85rem" }}>—</div>}
        </div>

        {/* Bottom agents */}
        <div className="card" style={{ padding: 16 }}>
          <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 10, color: "var(--danger)" }}>⚠ Bottom 5 agents</div>
          {bottomAgents.map((a, i) => (
            <div key={a.name + i} style={{ display: "flex", alignItems: "center", gap: 8, padding: "6px 0", borderBottom: i < 4 ? "1px solid var(--border)" : "none" }}>
              <span style={{ width: 20, fontWeight: 800, fontSize: "0.85rem", color: "var(--muted)" }}>{i + 1}</span>
              <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ fontWeight: 700, fontSize: "0.85rem", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{a.name}</div>
                <div style={{ fontSize: "0.72rem", color: "var(--muted)" }}>{a.cell || "—"} • {a.count} éval.</div>
              </div>
              <span className={`badge ${a.avg >= 50 ? "badge--warning" : "badge--danger"}`}>{a.avg}%</span>
            </div>
          ))}
          {bottomAgents.length === 0 && <div style={{ color: "var(--muted)", textAlign: "center", padding: 16, fontSize: "0.85rem" }}>—</div>}
        </div>

        {/* Recent evaluations */}
        <div className="card" style={{ padding: 0, overflow: "hidden" }}>
          <div style={{ padding: "12px 14px", borderBottom: "1px solid var(--border)", fontWeight: 700, fontSize: "0.9rem" }}>Activité récente</div>
          <div style={{ maxHeight: 320, overflow: "auto" }}>
            {recent.map((s) => {
              const sc = scoreP(s);
              return (
                <div key={String(s._id)} style={{ padding: "8px 14px", borderBottom: "1px solid var(--border)", display: "flex", alignItems: "center", gap: 8 }}>
                  <div style={{ width: 6, height: 6, borderRadius: "50%", background: sc >= 80 ? "#059669" : sc >= 50 ? "#d97706" : "#dc2626", flexShrink: 0 }} />
                  <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontSize: "0.82rem", fontWeight: 600, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                      {s.pilotName || s.pilot?.name || "—"} <span style={{ color: "var(--muted)", fontWeight: 500 }}>par {s.evaluatorName || "—"}</span>
                    </div>
                    <div style={{ fontSize: "0.72rem", color: "var(--muted)" }}>{fmtFull(s.createdAt)}</div>
                  </div>
                  <span className={`badge ${sc >= 80 ? "badge--success" : sc >= 50 ? "badge--warning" : "badge--danger"}`} style={{ fontSize: "0.72rem" }}>{Math.round(sc)}%</span>
                  {s.contested && <span className="badge badge--warning" style={{ fontSize: "0.68rem" }}>!</span>}
                </div>
              );
            })}
            {recent.length === 0 && <div style={{ padding: 20, textAlign: "center", color: "var(--muted)" }}>Aucune évaluation ce mois.</div>}
          </div>
        </div>
      </div>

      {/* ===== ROW 5: Team overview ===== */}
      <div className="card" style={{ padding: 16 }}>
        <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 10 }}>Vue d'ensemble équipe</div>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(130px, 1fr))", gap: 10 }}>
          {[
            { label: "CQ", count: uc.cq, icon: "🎧", color: "var(--primary)" },
            { label: "Management", count: uc.mgmt, icon: "📊", color: "var(--warning)" },
            { label: "Pilotes", count: uc.pilotes, icon: "👤", color: "var(--success)" },
            { label: "Total actifs", count: uc.active, icon: "✅", color: "var(--text)" },
            { label: "Inactifs", count: uc.inactive, icon: "⏸", color: "var(--danger)" },
          ].map((t) => (
            <div key={t.label} style={{ padding: "10px 12px", borderRadius: 10, border: "1px solid var(--border)", background: "var(--panel-2)" }}>
              <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                <span>{t.icon}</span>
                <span style={{ fontSize: "0.78rem", fontWeight: 600 }}>{t.label}</span>
              </div>
              <div style={{ fontSize: "1.2rem", fontWeight: 800, marginTop: 3, color: t.color }}>{t.count}</div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
