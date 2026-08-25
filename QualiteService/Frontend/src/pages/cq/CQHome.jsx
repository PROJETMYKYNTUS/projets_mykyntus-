import React, { useEffect, useMemo, useState, useCallback } from "react";
import api from "../../api";
import PageHeader from "../../components/PageHeader.jsx";
import { StatCards } from "../../components/StatCards.jsx";
import MultiSelect from "../../components/MultiSelect.jsx";
import { ResponsiveContainer, LineChart, Line, XAxis, YAxis, Tooltip, CartesianGrid } from "recharts";
import useRealtimeScores from "../../hooks/useRealtimeScores.js";
import { exportToXlsx } from "../admin/components/exportXlsx.js";

const ymOptions = () => {
  const y = new Date().getFullYear();
  return {
    years: [y, y - 1, y - 2].map((v) => ({ value: String(v), label: String(v) })),
    months: Array.from({ length: 12 }, (_, i) => ({ value: String(i + 1).padStart(2, "0"), label: String(i + 1).padStart(2, "0") })),
    defaultYear: String(y),
    defaultMonth: String(new Date().getMonth() + 1).padStart(2, "0"),
  };
};

const pct = (v) => `${Math.round(Number(v || 0) <= 1 ? Number(v) * 100 : Number(v))}%`;
const scoreToPercent = (s) => {
  const v1 = s?.compliancePercent; if (typeof v1 === "number" && !isNaN(v1)) return v1;
  const v2 = s?.avgPercent; if (typeof v2 === "number" && !isNaN(v2)) return v2;
  const v3 = s?.avgScore; if (typeof v3 === "number" && !isNaN(v3)) return v3 * 20;
  return 0;
};

export default function CQHome() {
  const { years, months, defaultYear, defaultMonth } = useMemo(ymOptions, []);
  const [yearSel, setYearSel] = useState([defaultYear]);
  const [monthSel, setMonthSel] = useState([defaultMonth]);
  const [pilotSel, setPilotSel] = useState([]);
  const [pilotOptions, setPilotOptions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [stats, setStats] = useState(null);
  const [err, setErr] = useState("");
  const [trend, setTrend] = useState([]);
  const [latest, setLatest] = useState([]);
  const [contestedList, setContestedList] = useState([]);
  const [topAgents, setTopAgents] = useState([]);
  const [refreshTick, setRefreshTick] = useState(0);

  useRealtimeScores(() => setRefreshTick((x) => x + 1));

  // Load pilots once
  useEffect(() => {
    let m = true;
    api.get("/cq/pilots/search", { params: { limit: 200 } }).then((r) => {
      if (m) setPilotOptions((r.data || []).map((u) => ({ value: String(u._id || u.id), label: `${u.name || ""}${u.cell ? " — " + u.cell : ""}`.trim() })));
    }).catch(() => {});
    return () => { m = false; };
  }, []);

  // Single consolidated data fetch
  const loadData = useCallback(async () => {
    setLoading(true); setErr("");
    try {
      const params = { year: yearSel[0] || "", month: monthSel[0] || "", pilotId: pilotSel.join(",") };

      // 3 parallel calls instead of 6
      const [statsR, scoresR, contestedR] = await Promise.all([
        api.get("/scores/stats", { params }),
        api.get("/scores/mine", { params: { ...params, page: 1, limit: 200 } }),
        api.get("/scores/mine", { params: { ...params, contested: "yes", page: 1, limit: 10 } }),
      ]);

      setStats(statsR.data || null);
      const list = scoresR.data?.items || [];
      setContestedList(contestedR.data?.items || []);

      // Build trend from scores (no extra API call)
      const map = new Map();
      for (const s of list) {
        const d = new Date(s.interactionDate || s.callDate || s.listeningDate || s.createdAt);
        if (isNaN(d.getTime())) continue;
        const key = `${String(d.getDate()).padStart(2, "0")}/${String(d.getMonth() + 1).padStart(2, "0")}`;
        if (!map.has(key)) map.set(key, { day: key, sum: 0, n: 0 });
        const x = map.get(key); x.sum += scoreToPercent(s); x.n++;
      }
      setTrend(Array.from(map.values()).map((x) => ({ day: x.day, avg: x.n ? Math.round((x.sum / x.n) * 10) / 10 : 0 })).sort((a, b) => {
        const [da, ma] = a.day.split("/").map(Number);
        const [db, mb] = b.day.split("/").map(Number);
        return ma === mb ? da - db : ma - mb;
      }));
      setLatest(list.slice(0, 10));

      // Top agents from loaded data
      const agentMap = new Map();
      for (const s of list) {
        const name = s.pilotName || s.pilot?.name || "?";
        if (!agentMap.has(name)) agentMap.set(name, { name, sum: 0, n: 0 });
        const e = agentMap.get(name); e.sum += scoreToPercent(s); e.n++;
      }
      setTopAgents(Array.from(agentMap.values()).map((e) => ({ ...e, avg: Math.round(e.sum / e.n) })).sort((a, b) => b.avg - a.avg).slice(0, 5));
    } catch (e) { setErr(e?.response?.data?.message || "Erreur chargement."); }
    finally { setLoading(false); }
  }, [yearSel, monthSel, pilotSel]);

  useEffect(() => { loadData(); }, [loadData, refreshTick]);

  const cards = useMemo(() => [
    { label: "Note moyenne QA", value: loading ? "…" : pct(stats?.avgPercent || 0) },
    { label: "Volume d'évaluations", value: loading ? "…" : String(stats?.total || 0) },
    { label: "Taux de contestation", value: loading ? "…" : pct(stats?.contestedRate || 0) },
    { label: "Contestées", value: loading ? "…" : String(stats?.contestedCount || 0) },
  ], [stats, loading]);

  const go = (view) => window.dispatchEvent(new CustomEvent("kcq:navigate", { detail: { role: "cq", view } }));
  const goEdit = (id) => window.dispatchEvent(new CustomEvent("kcq:navigate", { detail: { role: "cq", view: "new", editScoreId: String(id || "") } }));

  const exportMonth = async () => {
    const params = { year: yearSel[0] || "", month: monthSel[0] || "", pilotId: pilotSel.join(","), page: 1, limit: 2000 };
    const res = await api.get("/scores/mine", { params });
    const rows = (res.data?.items || []).map((s) => ({
      Date: new Date(s.interactionDate || s.callDate || s.createdAt).toLocaleDateString(),
      EPS: s.eps || "", Pilote: s.pilotName || "", Cellule: s.pilotCell || "",
      "Conformité (%)": Math.round(scoreToPercent(s) * 10) / 10, Contestée: s.contested ? "Oui" : "Non",
    }));
    exportToXlsx(`Mes_evaluations_${yearSel[0]}-${monthSel[0]}.xlsx`, rows);
  };

  return (
    <div className="page">
      <PageHeader title="Tableau de bord" subtitle="Vue synthèse" />

      <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: 12, marginBottom: 12 }}>
        <div className="card filters-card">
          <div className="filters-grid" style={{ gridTemplateColumns: "1fr 1fr 1fr" }}>
            <div><div className="label">Année</div><MultiSelect options={years} value={yearSel} onChange={setYearSel} placeholder="Année" /></div>
            <div><div className="label">Mois</div><MultiSelect options={months} value={monthSel} onChange={setMonthSel} placeholder="Mois" /></div>
            <div><div className="label">Agent</div><MultiSelect options={pilotOptions} value={pilotSel} onChange={setPilotSel} placeholder="Agents" /></div>
          </div>
        </div>
        <div className="card" style={{ padding: 14, display: "flex", flexDirection: "column", gap: 8 }}>
          <div style={{ fontWeight: 700, fontSize: "0.9rem" }}>Actions</div>
          <button className="btn" type="button" onClick={() => go("new")}>Nouvelle évaluation</button>
          <button className="btn btn--ghost" type="button" onClick={() => go("list")}>Mes évaluations</button>
          <button className="btn btn--ghost" type="button" onClick={() => go("coaching")}>Coaching</button>
          <button className="btn btn--ghost" type="button" onClick={exportMonth}>Exporter (période)</button>
        </div>
      </div>

      {err && <div className="card" style={{ borderColor: "rgba(220,38,38,0.25)", padding: 12, color: "var(--danger)", background: "var(--danger-bg)", marginBottom: 12 }}>{err}</div>}

      <StatCards stats={cards} />

      <div style={{ display: "grid", gridTemplateColumns: "1.5fr 1fr", gap: 12, marginTop: 12 }}>
        {/* Trend chart */}
        <div className="card" style={{ padding: 14 }}>
          <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 10 }}>Tendance des notes</div>
          <div style={{ height: 260 }}>
            {trend.length > 0 ? (
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={trend} margin={{ top: 10, right: 20, bottom: 0, left: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" stroke="var(--border)" />
                  <XAxis dataKey="day" tick={{ fontSize: 11 }} />
                  <YAxis domain={[0, 100]} tick={{ fontSize: 11 }} />
                  <Tooltip formatter={(v) => `${v}%`} contentStyle={{ borderRadius: 10, border: "1px solid var(--border)" }} />
                  <Line type="monotone" dataKey="avg" stroke="#4f46e5" strokeWidth={2.5} dot={{ r: 3 }} />
                </LineChart>
              </ResponsiveContainer>
            ) : (
              <div style={{ height: "100%", display: "grid", placeItems: "center", color: "var(--muted)" }}>Aucune donnée.</div>
            )}
          </div>
        </div>

        {/* Right column: contested + top agents + latest */}
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          {/* Contested */}
          <div className="card" style={{ padding: 14 }}>
            <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 8 }}>Contestations à traiter</div>
            {contestedList.length === 0 ? (
              <div style={{ color: "var(--muted)", fontSize: "0.85rem", padding: "8px 0" }}>Aucune contestation.</div>
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                {contestedList.map((s) => (
                  <div key={String(s._id)} style={{ display: "flex", alignItems: "center", justifyContent: "space-between", padding: "6px 8px", borderRadius: 8, border: "1px solid var(--warning-bg)", background: "var(--warning-bg)" }}>
                    <div>
                      <span style={{ fontWeight: 700, fontSize: "0.85rem" }}>{s.eps || "—"}</span>
                      <span style={{ color: "var(--muted)", fontSize: "0.78rem", marginLeft: 8 }}>{pct(scoreToPercent(s))}</span>
                    </div>
                    <button className="btn btn--sm btn--ghost" onClick={() => goEdit(s._id)}>Réévaluer</button>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Top agents */}
          <div className="card" style={{ padding: 14 }}>
            <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 8 }}>Top agents</div>
            {topAgents.length === 0 ? (
              <div style={{ color: "var(--muted)", fontSize: "0.85rem" }}>—</div>
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                {topAgents.map((a, i) => (
                  <div key={a.name} style={{ display: "flex", alignItems: "center", justifyContent: "space-between", padding: "4px 0" }}>
                    <span style={{ fontSize: "0.85rem", fontWeight: 600 }}>{a.name}</span>
                    <span className={`badge ${a.avg >= 80 ? "badge--success" : a.avg >= 50 ? "badge--warning" : "badge--danger"}`}>{a.avg}%</span>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Latest evals */}
          <div className="card" style={{ padding: 14, flex: 1 }}>
            <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 8 }}>Dernières évaluations</div>
            <div style={{ display: "flex", flexDirection: "column", gap: 3 }}>
              {latest.slice(0, 6).map((s) => (
                <div key={String(s._id)} style={{ display: "flex", justifyContent: "space-between", fontSize: "0.82rem", padding: "3px 0" }}>
                  <span style={{ fontWeight: 600 }}>{s.eps || "—"}</span>
                  <span className={`badge ${scoreToPercent(s) >= 80 ? "badge--success" : scoreToPercent(s) >= 50 ? "badge--warning" : "badge--danger"}`}>{pct(scoreToPercent(s))}</span>
                </div>
              ))}
              {latest.length === 0 && <div style={{ color: "var(--muted)", fontSize: "0.85rem" }}>Aucune donnée.</div>}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
