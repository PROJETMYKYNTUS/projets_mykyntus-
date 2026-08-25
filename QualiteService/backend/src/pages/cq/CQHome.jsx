import React, { useEffect, useMemo, useState } from "react";
import api from "../../api";
import PageHeader from "../../components/PageHeader.jsx";
import { StatCards } from "../../components/StatCards.jsx";
import MultiSelect from "../../components/MultiSelect.jsx";
import { ResponsiveContainer, LineChart, Line, XAxis, YAxis, Tooltip, CartesianGrid } from "recharts";
import useRealtimeScores from "../../hooks/useRealtimeScores.js";

function ymOptions() {
  const y = new Date().getFullYear();
  const years = [y, y - 1, y - 2].map((v) => ({ value: String(v), label: String(v) }));
  const months = Array.from({ length: 12 }).map((_, i) => {
    const v = String(i + 1).padStart(2, "0");
    return { value: v, label: v };
  });
  const now = new Date();
  return { years, months, defaultYear: String(now.getFullYear()), defaultMonth: String(now.getMonth() + 1).padStart(2, "0") };
}

function pct(v) {
  const n = Number(v || 0);
  const p = n <= 1 ? n * 100 : n;
  return `${Math.round(p)}%`;
}

function scoreToPercent(s) {
  const v1 = s?.compliancePercent;
  if (typeof v1 === "number" && !Number.isNaN(v1)) return v1;
  const v2 = s?.avgPercent;
  if (typeof v2 === "number" && !Number.isNaN(v2)) return v2;
  const v3 = s?.avgScore;
  if (typeof v3 === "number" && !Number.isNaN(v3)) return v3 * 20;
  const raw = s?.score ?? s?.value ?? 0;
  const n = Number(raw) || 0;
  return n <= 1 ? n * 100 : n;
}

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
  const [refreshTick, setRefreshTick] = useState(0);

  // Realtime refresh for dashboard stats/graphs
  useRealtimeScores(() => setRefreshTick((x) => x + 1));

  useEffect(() => {
    let mounted = true;
    (async () => {
      try {
        const res = await api.get("/cq/pilots/search", { params: { limit: 200 } });
        if (!mounted) return;
        const opts = (res.data || []).map((u) => ({
          value: String(u._id || u.id),
          label: `${u.name || ""}${u.cell ? " — " + u.cell : ""}`.trim(),
        }));
        setPilotOptions(opts);
      } catch {
        // ignore
      }
    })();
    return () => { mounted = false; };
  }, []);

  useEffect(() => {
    let mounted = true;
    (async () => {
      setLoading(true);
      setErr("");
      try {
        const params = {
          year: yearSel[0] || "",
          month: monthSel[0] || "",
          pilotId: pilotSel.join(","),
        };
        const res = await api.get("/scores/stats", { params });
        if (!mounted) return;
        setStats(res.data || null);
      } catch (e) {
        if (!mounted) return;
        setErr(e?.response?.data?.message || "Erreur lors du chargement des statistiques.");
      } finally {
        if (mounted) setLoading(false);
      }
    })();
    return () => { mounted = false; };
  }, [yearSel, monthSel, pilotSel, refreshTick]);

  useEffect(() => {
    let mounted = true;
    (async () => {
      try {
        const params = {
          year: yearSel[0] || "",
          month: monthSel[0] || "",
          page: 1,
          limit: 500,
        };
        const res = await api.get("/scores/mine", { params });
        const list = res.data?.items || [];

        // Trend by day (avg %)
        const map = new Map();
        for (const s of list) {
          const d = new Date(s.interactionDate || s.callDate || s.listeningDate || s.createdAt);
          if (Number.isNaN(d.getTime())) continue;
          const key = `${String(d.getDate()).padStart(2, "0")}/${String(d.getMonth() + 1).padStart(2, "0")}`;
          const pctVal = scoreToPercent(s);
          if (!map.has(key)) map.set(key, { day: key, sum: 0, n: 0 });
          const x = map.get(key);
          x.sum += pctVal;
          x.n += 1;
        }
        const data = Array.from(map.values())
          .map((x) => ({ day: x.day, avg: x.n ? Math.round((x.sum / x.n) * 10) / 10 : 0, count: x.n }))
          .sort((a, b) => {
            const [da, ma] = a.day.split("/").map(Number);
            const [db, mb] = b.day.split("/").map(Number);
            return ma === mb ? da - db : ma - mb;
          });

        if (!mounted) return;
        setTrend(data);
        setLatest(list.slice(0, 10));
      } catch {
        if (!mounted) return;
        setTrend([]);
        setLatest([]);
      }
    })();
    return () => { mounted = false; };
  }, [yearSel, monthSel, refreshTick]);

  const cards = useMemo(() => {
    const total = stats?.total || 0;
    const avgPercent = stats?.avgPercent || 0;
    const contestedRate = stats?.contestedRate || 0;
    return [
      { label: "Note moyenne QA", value: loading ? "…" : pct(avgPercent) },
      { label: "Volume d'évaluations", value: loading ? "…" : String(total) },
      { label: "Taux de contestation", value: loading ? "…" : pct(contestedRate) },
    ];
  }, [stats, loading]);

  return (
    <div className="page">
      <PageHeader
        title="Tableau de bord"
        subtitle="Synthèse filtrable (par défaut : mois courant)."
      />

      <div className="card" style={{ padding: 14, marginBottom: 12 }}>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: 12 }}>
          <div>
            <div className="label">Année</div>
            <MultiSelect options={years} value={yearSel} onChange={setYearSel} placeholder="Année" />
          </div>
          <div>
            <div className="label">Mois</div>
            <MultiSelect options={months} value={monthSel} onChange={setMonthSel} placeholder="Mois" />
          </div>
          <div>
            <div className="label">Agent</div>
            <MultiSelect options={pilotOptions} value={pilotSel} onChange={setPilotSel} placeholder="Agents" />
          </div>
        </div>
        <div style={{ marginTop: 8, color: "rgba(30,41,59,0.65)", fontSize: 12 }}>
          {loading ? "Chargement…" : "Filtres appliqués sur les KPI uniquement."}
        </div>
      </div>

      {err ? <div className="card" style={{ border: "1px solid rgba(239,68,68,0.35)", padding: 12, color: "#b91c1c" }}>{err}</div> : null}

      <StatCards stats={cards} />

      <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: 12, marginTop: 12 }}>
        <div className="card" style={{ padding: 14 }}>
          <div style={{ fontWeight: 800, marginBottom: 10 }}>Tendance des notes</div>
          <div style={{ height: 280 }}>
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={trend} margin={{ top: 10, right: 20, bottom: 0, left: 0 }}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="day" tick={{ fontSize: 12 }} />
                <YAxis domain={[0, 100]} tick={{ fontSize: 12 }} />
                <Tooltip />
                <Line type="monotone" dataKey="avg" strokeWidth={2} dot={false} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </div>

        <div className="card" style={{ padding: 14 }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 10 }}>
            <div style={{ fontWeight: 800 }}>Dernières évaluations</div>
            <div style={{ fontSize: 12, color: "rgba(30,41,59,0.65)" }}>{latest.length}</div>
          </div>
          <div style={{ overflow: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse" }}>
              <thead>
                <tr style={{ textAlign: "left" }}>
                  <th style={{ padding: "8px 6px", borderBottom: "1px solid var(--border)" }}>EPS</th>
                  <th style={{ padding: "8px 6px", borderBottom: "1px solid var(--border)" }}>%</th>
                </tr>
              </thead>
              <tbody>
                {latest.map((s) => (
                  <tr key={String(s._id || s.id)}>
                    <td style={{ padding: "8px 6px", borderBottom: "1px solid rgba(148,163,184,0.25)" }}>{s.eps || ""}</td>
                    <td style={{ padding: "8px 6px", borderBottom: "1px solid rgba(148,163,184,0.25)" }}>{pct(s.compliancePercent || 0)}</td>
                  </tr>
                ))}
                {latest.length === 0 ? (
                  <tr><td colSpan={2} style={{ padding: 10, color: "rgba(30,41,59,0.65)" }}>Aucune donnée.</td></tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>
  );
}
