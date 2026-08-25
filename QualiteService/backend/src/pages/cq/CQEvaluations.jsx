import React, { useEffect, useMemo, useState } from "react";
import api from "../../api";
import PageHeader from "../../components/PageHeader.jsx";
import MultiSelect from "../../components/MultiSelect.jsx";
import { fetchAllPaged } from "../../utils/fetchAllPaged.js";
import { exportToXlsx } from "../../pages/admin/components/exportXlsx.js";

function isCurrentMonth(d) {
  if (!d) return false;
  const x = new Date(d);
  const now = new Date();
  return x.getFullYear() === now.getFullYear() && x.getMonth() === now.getMonth();
}

function openEdit(scoreId) {
  window.dispatchEvent(
    new CustomEvent("kcq:navigate", {
      detail: { role: "cq", view: "new", editScoreId: String(scoreId || "") },
    })
  );
}

function scoreToPercent(s) {
  const v1 = s?.compliancePercent;
  if (typeof v1 === "number" && !Number.isNaN(v1)) return v1;

  const v2 = s?.avgPercent;
  if (typeof v2 === "number" && !Number.isNaN(v2)) return v2;

  const v3 = s?.avgScore;
  if (typeof v3 === "number" && !Number.isNaN(v3)) return v3 * 20;

  // legacy decimals (0..1)
  const v4 = s?.score ?? s?.value ?? s?.avg ?? null;
  if (typeof v4 === "number" && !Number.isNaN(v4)) {
    if (v4 <= 1) return v4 * 100;
    if (v4 <= 5) return (v4 / 5) * 100;
    return v4;
  }
  return 0;
}

function formatScorePct(s) {
  const pct = scoreToPercent(s);
  return `${Number(pct || 0).toFixed(1)}%`;
}

const months = [
  { value: "01", label: "Jan" }, { value: "02", label: "Fév" }, { value: "03", label: "Mar" }, { value: "04", label: "Avr" },
  { value: "05", label: "Mai" }, { value: "06", label: "Juin" }, { value: "07", label: "Juil" }, { value: "08", label: "Août" },
  { value: "09", label: "Sep" }, { value: "10", label: "Oct" }, { value: "11", label: "Nov" }, { value: "12", label: "Déc" },
];

function buildYearOptions(span = 6) {
  const now = new Date();
  const y = now.getFullYear();
  const out = [];
  for (let i = 0; i < span; i++) {
    const yy = String(y - i);
    out.push({ value: yy, label: yy });
  }
  return out;
}

export default function CQEvaluations() {
  const [loading, setLoading] = useState(false);
  const [err, setErr] = useState("");
  const [items, setItems] = useState([]);
  const [total, setTotal] = useState(0);

  const limit = 20;
  const [page, setPage] = useState(1);

  const years = useMemo(() => buildYearOptions(8), []);
  const now = new Date();
  const [yearSel, setYearSel] = useState([String(now.getFullYear())]);
  const [monthSel, setMonthSel] = useState([String(now.getMonth() + 1).padStart(2, "0")]);
  const [pilotSel, setPilotSel] = useState([]);

  const [pilots, setPilots] = useState([]);
  const pilotOptions = useMemo(
    () => (pilots || []).map((p) => ({ value: String(p._id), label: `${p.name || ""}${p.cell ? " — " + p.cell : ""}`.trim() })),
    [pilots]
  );

  useEffect(() => {
    let mounted = true;
    (async () => {
      try {
        const res = await api.get("/cq/pilots/search", { params: { limit: 200 } });
        if (!mounted) return;
        setPilots(Array.isArray(res.data) ? res.data : []);
      } catch {
        // ignore
      }
    })();
    return () => { mounted = false; };
  }, []);

  useEffect(() => { setPage(1); }, [yearSel.join(","), monthSel.join(","), pilotSel.join(",")]);

  useEffect(() => {
    let mounted = true;
    (async () => {
      setLoading(true);
      setErr("");
      try {
        const params = {
          page,
          limit,
          year: yearSel.join(","),
          month: monthSel.join(","),
          pilotId: pilotSel.join(","),
        };
        const res = await api.get("/scores/mine", { params });
        if (!mounted) return;
        setItems(res.data?.items || []);
        setTotal(res.data?.total || 0);
      } catch (e) {
        if (!mounted) return;
        setErr(e?.response?.data?.message || "Erreur lors du chargement.");
      } finally {
        if (mounted) setLoading(false);
      }
    })();
    return () => { mounted = false; };
  }, [page, limit, yearSel, monthSel, pilotSel]);

  const totalPages = Math.max(1, Math.ceil((total || 0) / limit));

  const onExport = async () => {
    try {
      setErr("");
      const all = await fetchAllPaged(async ({ page: p, limit: l }) => {
        const params = {
          page: p,
          limit: l,
          year: yearSel.join(","),
          month: monthSel.join(","),
          pilotId: pilotSel.join(","),
        };
        const res = await api.get("/scores/mine", { params });
        return { items: res.data?.items || [], total: res.data?.total || 0 };
      }, { pageSize: 200 });

      const rows = (all || []).map((s) => ({
        Date: s.createdAt ? new Date(s.createdAt).toLocaleDateString() : "",
        Agent: s.pilotName || s.pilot?.name || "",
        Cellule: s.pilotCell || s.pilot?.cell || "",
        EPS: s.eps || "",
        "Évaluateur": s.evaluatorName || s.evaluator?.name || s.evaluator || "",
        Score: formatScorePct(s),
        Commentaire: s.comment || "",
        "Picking prime": s.pickingPrime ? "Vrai" : "Faux",
      }));

      exportToXlsx(`evaluations_${new Date().toISOString().slice(0, 10)}.xlsx`, rows);
    } catch (e) {
      console.error(e);
      setErr("Impossible d’exporter en Excel.");
    }
  };

  return (
    <div className="page">
      <PageHeader title="Évaluations" subtitle="Filtrez et consultez vos évaluations (optimisé gros volume)." />

      <div style={{ display: "flex", justifyContent: "flex-end", margin: "8px 0 12px" }}>
        <button className="btn" type="button" onClick={onExport}>Exporter Excel</button>
      </div>

      <div className="card" style={{ padding: 14, marginBottom: 12 }}>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))", gap: 12 }}>
          <div>
            <div className="label">Année</div>
            <MultiSelect options={years} value={yearSel} onChange={setYearSel} placeholder="Années" />
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
      </div>

      {err ? (
        <div className="card" style={{ border: "1px solid rgba(239,68,68,0.35)", padding: 12, color: "#b91c1c" }}>
          {err}
        </div>
      ) : null}

      <div className="card" style={{ padding: 14 }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 10, marginBottom: 10 }}>
          <div style={{ fontWeight: 800 }}>Résultats</div>
          <div style={{ color: "rgba(30,41,59,0.7)", fontSize: 12 }}>
            {loading ? "Chargement…" : `${total} évaluations`}
          </div>
        </div>

        <div style={{ overflow: "auto" }}>
          <table style={{ width: "100%", borderCollapse: "collapse" }}>
            <thead>
              <tr style={{ textAlign: "left" }}>
                <th style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.35)" }}>Date</th>
                <th style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.35)" }}>Agent</th>
                <th style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.35)" }}>Cellule</th>
                <th style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.35)" }}>EPS</th>
                <th style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.35)" }}>Évaluateur</th>
                <th style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.35)" }}>Score</th>
                <th style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.35)" }}>Picking prime</th>
                <th style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.35)" }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {(items || []).map((s) => (
                <tr key={String(s._id || s.id)}>
                  <td style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.25)" }}>
                    {s.createdAt ? new Date(s.createdAt).toLocaleDateString() : ""}
                  </td>
                  <td style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.25)" }}>{s.pilotName || s.pilot?.name || ""}</td>
                  <td style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.25)" }}>{s.pilotCell || s.pilot?.cell || ""}</td>
                  <td style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.25)" }}>{s.eps || ""}</td>
                  <td style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.25)" }}>{s.evaluatorName || s.evaluator?.name || ""}</td>
                  <td style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.25)" }}>{formatScorePct(s)}</td>
                  <td style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.25)" }}>{s.pickingPrime ? "Vrai" : "Faux"}</td>
                  <td style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.25)" }}>
                    {isCurrentMonth(s.createdAt) ? (
                      <button className="btn" type="button" onClick={() => openEdit(s._id || s.id)}>Modifier</button>
                    ) : (
                      <span style={{ color: "rgba(30,41,59,0.55)", fontSize: 12 }}>—</span>
                    )}
                  </td>
                </tr>
              ))}
              {!loading && (!items || items.length === 0) ? (
                <tr><td colSpan={8} style={{ padding: 12, color: "rgba(30,41,59,0.7)" }}>Aucun résultat.</td></tr>
              ) : null}
            </tbody>
          </table>
        </div>

        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: 12 }}>
          <button className="btn" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>Précédent</button>
          <div style={{ color: "rgba(30,41,59,0.75)", fontSize: 12 }}>
            Page {page} / {totalPages}
          </div>
          <button className="btn" disabled={page >= totalPages} onClick={() => setPage((p) => Math.min(totalPages, p + 1))}>Suivant</button>
        </div>
      </div>
    </div>
  );
}
