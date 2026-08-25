import React, { useEffect, useMemo, useState } from "react";
import api from "../../api";
import AsyncSelect from "react-select/async";
import PageHeader from "../../components/PageHeader.jsx";
import MultiSelect from "../../components/MultiSelect.jsx";

function toISODate(d) {
  if (!d) return "";
  const x = new Date(d);
  if (isNaN(x.getTime())) return "";
  const yyyy = x.getFullYear();
  const mm = String(x.getMonth() + 1).padStart(2, "0");
  const dd = String(x.getDate()).padStart(2, "0");
  return `${yyyy}-${mm}-${dd}`;
}

export default function CQCoaching() {
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState("");
  const [items, setItems] = useState([]);

  // filters (separate from dashboard)
  const [pilotSel, setPilotSel] = useState([]);
  const [evaluatorSel, setEvaluatorSel] = useState([]);
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");

  // create
  const [creating, setCreating] = useState(false);
  const [scoreId, setScoreId] = useState("");
  const [createPilot, setCreatePilot] = useState(null);

  const [notes, setNotes] = useState("");
  const [actionPlan, setActionPlan] = useState("");
  const [status, setStatus] = useState("open");
  const [followUpDate, setFollowUpDate] = useState("");

  const [pilots, setPilots] = useState([]);
  const [evaluators, setEvaluators] = useState([]);
  const [scoreOptions, setScoreOptions] = useState([]);
  const [scoresLoading, setScoresLoading] = useState(false);

  async function updateStatus(coachingId, nextStatus) {
    try {
      const { data } = await api.patch(`/coaching/${coachingId}`, { status: nextStatus });
      setItems((arr) =>
        (arr || []).map((x) => (String(x._id || x.id) === String(coachingId) ? (data || x) : x))
      );
    } catch (e) {
      setErr(e?.response?.data?.message || "Erreur serveur lors de la mise à jour du coaching.");
    }
  }

  useEffect(() => {
    let mounted = true;
    (async () => {
      try {
        // CQ/Management do not have access to /admin/users.
        // Use dedicated endpoints.
        const [pRes, eRes] = await Promise.all([
          api.get("/cq/pilots/search", { params: { limit: 200 } }),
          api.get("/scores/evaluators"),
        ]);
        if (!mounted) return;
        setPilots(Array.isArray(pRes.data) ? pRes.data : []);
        setEvaluators(Array.isArray(eRes.data) ? eRes.data : []);
      } catch {
        // ignore
      }
    })();

    return () => {
      mounted = false;
    };
  }, []);

  const pilotOptions = useMemo(
    () => pilots.map((p) => ({ value: String(p._id), label: `${p.name || ""}${p.cell ? " — " + p.cell : ""}`.trim() })),
    [pilots]
  );
  const evaluatorOptions = useMemo(
    () => evaluators.map((u) => ({ value: String(u.id || u._id), label: `${u.name || ""}`.trim() })),
    [evaluators]
  );

  async function fetchCoachings() {
    setLoading(true);
    setErr("");
    try {
      const params = {
        pilotId: pilotSel.join(","),
        evaluatorId: evaluatorSel.join(","),
        dateFrom: dateFrom || undefined,
        dateTo: dateTo || undefined,
        page: 1,
        limit: 100,
      };
      const res = await api.get("/coaching/mine", { params });
      setItems(res.data?.items || res.data || []);
    } catch (e) {
      setErr(e?.response?.data?.message || "Erreur lors du chargement.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { fetchCoachings(); /* eslint-disable-next-line */ }, []);

  useEffect(() => {
    // refresh on filters change
    fetchCoachings();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pilotSel.join(","), evaluatorSel.join(","), dateFrom, dateTo]);

  
  const loadPilotOptions = async (input) => {
    const q = (input || "").trim();
    const res = await api.get("/cq/pilots/search", { params: { q, limit: 200 } });
    const arr = Array.isArray(res.data) ? res.data : [];
    return arr.map((p) => ({ value: String(p._id || p.id), label: p.name || p.fullName || p.email || "Agent" }));
  };

async function fetchScoresForCreate() {
    setScoresLoading(true);
    try {
      const now = new Date();
      const y = String(now.getFullYear());
      const m = String(now.getMonth() + 1).padStart(2, "0");
      const res = await api.get("/scores/mine", { params: { year: y, month: m, page: 1, limit: 200, pilotId: createPilot ? String(createPilot.value) : "" } });
      const list = (res.data?.items || []).map((s) => ({
        id: String(s._id || s.id),
        label: `${s.pilotName || "Agent"} — ${s.eps || ""} — ${s.createdAt ? new Date(s.createdAt).toLocaleDateString() : ""}`,
      }));
      setScoreOptions(list);
    } catch {
      setScoreOptions([]);
    } finally {
      setScoresLoading(false);
    }
  }

  useEffect(() => { fetchScoresForCreate(); }, [createPilot?.value]);

  async function onCreate(e) {
    e.preventDefault();
    setCreating(true);
    setErr("");
    try {
      await api.post("/coaching", {
        scoreId,
        pilotId: createPilot?.value || undefined,
        notes,
        actionPlan,
        status,
        followUpDate: followUpDate || null,
      });
      setScoreId("");
      setNotes("");
      setActionPlan("");
      setStatus("open");
      setFollowUpDate("");
      await fetchCoachings();
    } catch (e2) {
      setErr(e2?.response?.data?.message || "Erreur lors de la création.");
    } finally {
      setCreating(false);
    }
  }

  return (
    <div className="page">
      <PageHeader title="Coaching" subtitle="" />
      <div className="card" style={{ padding: 14, marginBottom: 12 }}>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))", gap: 12 }}>
          <div>
            <div className="label">Agent</div>
            <MultiSelect options={pilotOptions} value={pilotSel} onChange={setPilotSel} placeholder="Agents" />
          </div>
          <div>
            <div className="label">Évaluateur</div>
            <MultiSelect options={evaluatorOptions} value={evaluatorSel} onChange={setEvaluatorSel} placeholder="Évaluateurs" />
          </div>
          <div>
            <div className="label">Date début</div>
            <input className="input" type="date" value={dateFrom} onChange={(e) => setDateFrom(e.target.value)} />
          </div>
          <div>
            <div className="label">Date fin</div>
            <input className="input" type="date" value={dateTo} onChange={(e) => setDateTo(e.target.value)} />
          </div>
        </div>
      </div>

      <div className="card" style={{ padding: 14, marginBottom: 12 }}>
        <div style={{ fontWeight: 800, marginBottom: 10 }}>Créer un coaching</div>
        <form onSubmit={onCreate} style={{ display: "grid", gridTemplateColumns: "repeat(2, minmax(0, 1fr))", gap: 12 }}>
          <div style={{ gridColumn: "1 / -1" }}>
            <div className="label">Agent</div>
            <AsyncSelect
              cacheOptions
              defaultOptions={pilotOptions}
              loadOptions={loadPilotOptions}
              value={createPilot}
              onChange={(opt) => { setCreatePilot(opt || null); setScoreId(""); }}
              placeholder="Rechercher un agent…"
              styles={{
                control: (base) => ({ ...base, minHeight: 42, borderRadius: 10 }),
                menu: (base) => ({ ...base, zIndex: 60 }),
              }}
            />
          </div>
          <div style={{ gridColumn: "1 / -1" }}>
            <div className="label">Évaluation</div>
            <select className="input" value={scoreId} onChange={(e) => setScoreId(e.target.value)} disabled={scoresLoading}>
              <option value="">{scoresLoading ? "Chargement…" : "Choisir une évaluation"}</option>
              {scoreOptions.map((o) => <option key={o.id} value={o.id}>{o.label}</option>)}
            </select>
          </div>

          <div>
            <div className="label">Notes</div>
            <textarea className="input" style={{ minHeight: 110, resize: "vertical" }} value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>

          <div>
            <div className="label">Plan d’action</div>
            <textarea className="input" style={{ minHeight: 110, resize: "vertical" }} value={actionPlan} onChange={(e) => setActionPlan(e.target.value)} />
          </div>

          <div>
            <div className="label">Statut</div>
            <select className="input" value={status} onChange={(e) => setStatus(e.target.value)}>
              <option value="open">Ouvert</option>
              <option value="in_progress">En cours</option>
              <option value="done">Terminé</option>
            </select>
          </div>

          <div>
            <div className="label">Suivi (date)</div>
            <input className="input" type="date" value={followUpDate} onChange={(e) => setFollowUpDate(e.target.value)} />
          </div>

          <div style={{ gridColumn: "1 / -1", display: "flex", justifyContent: "flex-end" }}>
            <button className="btn" type="submit" disabled={!scoreId || creating}>
              {creating ? "Création…" : "Créer"}
            </button>
          </div>
        </form>
        {err ? <div style={{ marginTop: 10, color: "#b91c1c" }}>{err}</div> : null}
      </div>

      <div className="card" style={{ padding: 14 }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 10, marginBottom: 10 }}>
          <div style={{ fontWeight: 800 }}>Coachings</div>
          <div style={{ color: "rgba(30,41,59,0.7)", fontSize: 12 }}>{loading ? "Chargement…" : `${items.length} éléments`}</div>
        </div>

        <div style={{ overflow: "auto" }}>
          <table style={{ width: "100%", borderCollapse: "collapse" }}>
            <thead>
              <tr style={{ textAlign: "left" }}>
                <th style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.35)" }}>Date</th>
                <th style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.35)" }}>Agent</th>
                <th style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.35)" }}>Notes</th>
                <th style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.35)" }}>Statut</th>
                <th style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.35)" }}>Suivi</th>
              </tr>
            </thead>
            <tbody>
              {(items || []).map((c) => (
                <tr key={String(c._id || c.id)}>
                  <td style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.25)" }}>
                    {c.createdAt ? new Date(c.createdAt).toLocaleDateString() : ""}
                  </td>
                  <td style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.25)" }}>
                    {c.pilotName || c.pilot?.name || ""}
                  </td>
                  <td style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.25)", whiteSpace: "pre-wrap" }}>
                    {c.notes || ""}
                  </td>
                  <td style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.25)" }}>
                    <select className="input" style={{ height: 34 }} value={c.status || "open"} onChange={(e) => updateStatus(c._id || c.id, e.target.value)}>
                      <option value="open">Ouvert</option>
                      <option value="in_progress">En cours</option>
                      <option value="done">Terminé</option>
                    </select>
                  </td>
                  <td style={{ padding: "10px 8px", borderBottom: "1px solid rgba(148,163,184,0.25)" }}>
                    {c.followUpDate ? new Date(c.followUpDate).toLocaleDateString() : ""}
                  </td>
                </tr>
              ))}
              {!loading && (!items || items.length === 0) ? (
                <tr><td colSpan={5} style={{ padding: 12, color: "rgba(30,41,59,0.7)" }}>Aucun coaching.</td></tr>
              ) : null}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}