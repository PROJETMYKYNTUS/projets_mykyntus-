import React, { useEffect, useMemo, useState } from "react";
import api from "../../api";
import AsyncSelect from "react-select/async";

/* -------------------- Helpers (robust & backward compatible) -------------------- */

function normalizeStatus(raw) {
  const s = (raw || "").toString().trim().toLowerCase();
  if (["c", "conforme", "ok", "oui", "yes", "true", "1"].includes(s)) return "C";
  if (["nc", "non conforme", "non_conforme", "ko", "non", "no", "false", "0"].includes(s)) return "NC";
  if (["na", "n/a", "non applicable", "non_applicable"].includes(s)) return "NA";
  if (["pc", "présent conforme", "present conforme", "présent / conforme", "present / conforme"].includes(s)) return "PC";
  if (["pnc", "présent non conforme", "present non conforme", "présent / non conforme", "present / non conforme"].includes(s)) return "PNC";
  if (["np", "non présent", "non present", "absent"].includes(s)) return "NP";
  return "";
}

function isTrue(v) {
  return v === true || v === 1 || v === "1" || v === "true" || v === "on";
}

function isoDate(v) {
  const d = v ? new Date(v) : new Date();
  if (Number.isNaN(d.getTime())) return "";
  const yyyy = d.getFullYear();
  const mm = String(d.getMonth() + 1).padStart(2, "0");
  const dd = String(d.getDate()).padStart(2, "0");
  return `${yyyy}-${mm}-${dd}`;
}

/**
 * Compute compliance % from selectedGrid + statuses.
 * Keeps compatibility with:
 * - classic: C/NC/NA + hardFail/malus + per-item points
 * - presence: PC/PNC/NP/NA + hardFail/malus (phase)
 */
function computeCompliancePercent(items, gridDoc) {
  const its = Array.isArray(items) ? items : [];
  const gridItems = Array.isArray(gridDoc?.items) ? gridDoc.items : [];
  const gridType = (gridDoc?.gridType || "classic").toString();

  // Map grid item label => current group meta (hardFail/malus)
  const groupByLabel = new Map();
  let currentGroup = null;
  for (const gi of gridItems) {
    if (!gi) continue;
    if (gi.type === "group") {
      currentGroup = {
        hardFail: isTrue(gi.hardFail),
        malusPercent: Number.isFinite(Number(gi.malusPercent)) ? Number(gi.malusPercent) : 0,
      };
      continue;
    }
    const lbl = (gi.label || "").toString().trim();
    if (!lbl) continue;
    groupByLabel.set(lbl, currentGroup);
  }

  if (gridType === "presence") {
    let obtained = 0;
    let maxApplicable = 0;
    let totalMalus = 0;

    for (const it of its) {
      const label = (it?.label || "").toString().trim();
      if (!label) continue;
      const status = normalizeStatus(it.status);
      if (status === "NA") continue;

      const group = groupByLabel.get(label) || null;
      const isNonCompliant = status === "PNC" || status === "NP" || status === "NC";
      if (isNonCompliant && group && group.hardFail) return 0;

      if (isNonCompliant) {
        const phaseMalus = group && group.malusPercent > 0 ? group.malusPercent : 0;
        if (phaseMalus > 0) totalMalus += phaseMalus;
      }

      maxApplicable += 1;
      if (status === "PC" || status === "C") obtained += 1;
      else if (status === "PNC") obtained += 0.5;
    }

    if (maxApplicable <= 0) return 0;
    let base = (obtained / maxApplicable) * 100;
    base = Math.max(0, Math.min(100, base));
    const finalPct = Math.max(0, base - totalMalus);
    return Math.round(finalPct * 10) / 10;
  }

  // classic points
  const pointsByLabel = new Map();
  const itemMalusByLabel = new Map();
  for (const gi of gridItems) {
    if (!gi || gi.type === "group") continue;
    const label = (gi.label || "").toString().trim();
    if (!label) continue;
    const pC = typeof gi.pointsConforme === "number" ? gi.pointsConforme : 1;
    const pNC = typeof gi.pointsNonConforme === "number" ? gi.pointsNonConforme : 0;
    pointsByLabel.set(label, { pC, pNC });
    const mp = Number.isFinite(Number(gi.malusPercent)) ? Number(gi.malusPercent) : 0;
    itemMalusByLabel.set(label, mp);
  }

  let obtained = 0;
  let maxApplicable = 0;
  let totalMalus = 0;

  for (const it of its) {
    const label = (it?.label || "").toString().trim();
    if (!label) continue;

    const status = normalizeStatus(it.status);
    if (status === "NA") continue;

    const group = groupByLabel.get(label) || null;
    if (status === "NC" && group && group.hardFail) return 0;

    if (status === "NC") {
      const itemMalus = Number(itemMalusByLabel.get(label)) || 0;
      const phaseMalus = group && group.malusPercent > 0 ? group.malusPercent : 0;
      const applied = itemMalus > 0 ? itemMalus : phaseMalus;
      if (applied > 0) totalMalus += applied;
    }

    const pts = pointsByLabel.get(label);
    const pC = pts ? pts.pC : 1;
    const pNC = pts ? pts.pNC : 0;

    maxApplicable += pC;
    if (status === "C") obtained += pC;
    else if (status === "NC") obtained += pNC;
  }

  if (maxApplicable <= 0) return 0;
  let base = (obtained / maxApplicable) * 100;
  base = Math.max(0, Math.min(100, base));
  const finalPct = Math.max(0, base - totalMalus);
  return Math.round(finalPct * 10) / 10;
}

const STATUS_CLASSIC = [
  { value: "C", label: "Conforme (C)" },
  { value: "NC", label: "Non conforme (NC)" },
  { value: "NA", label: "Non applicable (NA)" },
];

const STATUS_PRESENCE = [
  { value: "PC", label: "Présent / Conforme (PC)" },
  { value: "PNC", label: "Présent / Non conforme (PNC)" },
  { value: "NP", label: "Non présent (NP)" },
  { value: "NA", label: "Non applicable (NA)" },
];

/* -------------------- Component -------------------- */

export default function NewEvaluationPage({ title = "Nouvelle évaluation", editScoreId = "" }) {
  const [loading, setLoading] = useState(false);
  const [err, setErr] = useState("");
  const [okMsg, setOkMsg] = useState("");

  const [pilots, setPilots] = useState([]);
  const [grids, setGrids] = useState([]);

  const [pilotId, setPilotId] = useState("");
  const [gridId, setGridId] = useState("");
  const [eps, setEps] = useState("");
  const [callDate, setCallDate] = useState(isoDate(new Date()));
  // Backward compatibility (some screens / backend still rely on interactionDate)
  const [interactionDate, setInteractionDate] = useState(isoDate(new Date()));
  const [pickingPrime, setPickingPrime] = useState(false);
  const [comment, setComment] = useState("");
  const [callDuration, setCallDuration] = useState("");

  // key = grid item index (stable for selected grid), value = status code
  const [statusByKey, setStatusByKey] = useState({});

  /* -------------------- Data loading -------------------- */

  useEffect(() => {
    let mounted = true;
    (async () => {
      try {
        // scalable for 200+ agents
        const res = await api.get("/cq/pilots/search", { params: { limit: 200 } });
        if (!mounted) return;
        const list = Array.isArray(res.data) ? res.data : [];
        setPilots(list);
      } catch {
        // silent: user can still type search
      }
    })();
    return () => {
      mounted = false;
    };
  }, []);

  useEffect(() => {
    let mounted = true;
    (async () => {
      try {
        const res = await api.get("/grids/my");
        if (!mounted) return;
        setGrids(Array.isArray(res.data) ? res.data : []);
      } catch (e) {
        if (!mounted) return;
        setErr(e?.response?.data?.message || "Impossible de charger les grilles.");
      }
    })();
    return () => {
      mounted = false;
    };
  }, []);

  // Mode édition : charger l'évaluation et pré-remplir
  useEffect(() => {
    let mounted = true;
    (async () => {
      if (!editScoreId) return;
      try {
        setErr("");
        setOkMsg("");
        const { data } = await api.get(`/scores/${editScoreId}`);
        if (!mounted) return;

        // champs simples
        if (data?.pilot?._id) setPilotId(String(data.pilot._id));
        else if (data?.pilotId) setPilotId(String(data.pilotId));
        else if (data?.pilot) setPilotId(String(data.pilot));

        if (data?.grid?._id) setGridId(String(data.grid._id));
        else if (data?.gridId) setGridId(String(data.gridId));
        else if (data?.grid) setGridId(String(data.grid));

        if (data?.eps !== undefined) setEps(String(data.eps || ""));
        if (data?.pickingPrime !== undefined) setPickingPrime(!!data.pickingPrime);
        if (data?.comment !== undefined) setComment(String(data.comment || ""));
        if (data?.callDuration !== undefined) setCallDuration(String(data.callDuration || ""));

        if (data?.callDate) {
          const cd = isoDate(new Date(data.callDate));
          setCallDate(cd);
          setInteractionDate(cd);
        } else if (data?.interactionDate) {
          const id = isoDate(new Date(data.interactionDate));
          setInteractionDate(id);
          setCallDate(id);
        }

        // items statuses will be applied after grid load (see effect below)
        // store raw items in stateByKey by label later
        const savedItems = Array.isArray(data?.items) ? data.items : [];
        // temporary stash on window? no. We'll compute when selectedGrid is available
        // We'll attach to data via a ref-like state:
        setStatusByKey((prev) => ({ ...prev, __savedItems: savedItems }));
      } catch (e) {
        if (!mounted) return;
        setErr(e?.response?.data?.message || "Erreur lors du chargement de l'évaluation.");
      }
    })();
    return () => {
      mounted = false;
    };
  }, [editScoreId]);

  const pilotOptions = useMemo(
    () =>
      (pilots || []).map((p) => ({
        value: String(p._id),
        label: `${p.name || ""}${p.cell ? " — " + p.cell : ""}`.trim(),
      })),
    [pilots]
  );

  async function loadPilots(inputValue) {
    const q = String(inputValue || "").trim();
    try {
      const res = await api.get("/cq/pilots/search", { params: { q, limit: 200 } });
      const list = Array.isArray(res.data) ? res.data : [];
      return list.map((p) => ({
        value: String(p._id),
        label: `${p.name || ""}${p.cell ? " — " + p.cell : ""}`.trim(),
      }));
    } catch {
      return pilotOptions;
    }
  }

  const selectedGrid = useMemo(
    () => (grids || []).find((g) => String(g._id) === String(gridId)),
    [grids, gridId]
  );

  const gridType = (selectedGrid?.gridType || "classic").toString();
  const statusOptions = gridType === "presence" ? STATUS_PRESENCE : STATUS_CLASSIC;

  const renderRows = useMemo(() => {
    const items = Array.isArray(selectedGrid?.items) ? selectedGrid.items : [];
    return items
      .map((it, idx) => ({ ...it, __idx: idx }))
      .filter((it) => it && (it.type === "group" || String(it.label || "").trim()));
  }, [selectedGrid]);

  // Init statuses when grid changes (and apply saved statuses in edit mode)
  useEffect(() => {
    if (!selectedGrid) {
      setStatusByKey({});
      return;
    }

    // Build saved status map by label (from edit loaded score items)
    const saved = statusByKey.__savedItems;
    const savedByLabel = new Map();
    if (Array.isArray(saved)) {
      for (const it of saved) {
        const label = (it?.label || "").toString().trim();
        const st = normalizeStatus(it?.status);
        if (label && st) savedByLabel.set(label, st);
      }
    }

    const next = {};
    for (const r of renderRows) {
      if (r.type === "group") continue;
      const key = String(r.__idx);
      const label = String(r.label || "").trim();
      const defaultStatus = gridType === "presence" ? "PC" : "C";
      const fromSaved = label ? savedByLabel.get(label) : "";
      next[key] = fromSaved || defaultStatus;
    }
    setStatusByKey(next);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [gridId, selectedGrid?._id]);

  const scoreItemsForCalc = useMemo(() => {
    return renderRows
      .filter((r) => r.type !== "group")
      .map((r) => ({
        label: String(r.label || "").trim(),
        status: statusByKey[String(r.__idx)] || (gridType === "presence" ? "PC" : "C"),
      }))
      .filter((x) => x.label);
  }, [renderRows, statusByKey, gridType]);

  const realtimeScore = useMemo(() => {
    if (!selectedGrid) return null;
    return computeCompliancePercent(scoreItemsForCalc, selectedGrid);
  }, [scoreItemsForCalc, selectedGrid]);

  /* -------------------- Validations -------------------- */

  async function checkEpsDuplicate(v) {
    const val = (v || "").toString().trim();
    if (!val) return;
    try {
      const res = await api.get("/scores/check-eps", { params: { eps: val } });
      if (res.data?.duplicate) {
        setErr("EPS doublon : une évaluation existe déjà avec cet EPS.");
      }
    } catch {
      // ignore
    }
  }

  /* -------------------- Submit -------------------- */

  async function onSubmit(e) {
    e.preventDefault();
    setErr("");
    setOkMsg("");

    if (!pilotId) return setErr("Veuillez choisir un agent.");
    if (!gridId) return setErr("Veuillez choisir une grille.");
    if (!String(eps || "").trim()) return setErr("Veuillez renseigner l'EPS.");

    const items = renderRows
      .filter((r) => r.type !== "group")
      .map((r) => ({
        label: String(r.label || "").trim(),
        status: statusByKey[String(r.__idx)] || (gridType === "presence" ? "PC" : "C"),
      }))
      .filter((it) => it.label);

    if (!items.length) return setErr("Cette grille ne contient aucun item évaluable.");

    const payload = {
      pilotId,
      gridId,
      eps: String(eps || "").trim(),
      pickingPrime: !!pickingPrime,
      callDate: callDate ? new Date(callDate).toISOString() : null,
      interactionDate: interactionDate
        ? new Date(interactionDate).toISOString()
        : callDate
          ? new Date(callDate).toISOString()
          : null,
      callDuration: callDuration || "",
      comment: comment || "",
      items,
      // NOTE: score is computed server-side. We keep client realtimeScore only for UX.
    };

    setLoading(true);
    try {
      if (editScoreId) {
        await api.patch(`/scores/${editScoreId}`, payload);
        setOkMsg("Évaluation mise à jour avec succès.");
      } else {
        await api.post("/scores", payload);
        setOkMsg("Évaluation créée avec succès.");
        // reset some fields for quick successive input
        setEps("");
        setComment("");
        setPickingPrime(false);
      }
    } catch (e2) {
      setErr(e2?.response?.data?.message || "Erreur lors de l'enregistrement de l'évaluation.");
    } finally {
      setLoading(false);
    }
  }

  /* -------------------- UI -------------------- */

  return (
    <div className="page">
      <div className="pageHeader">
        <div>
          <div className="pageTitle">{title}</div>
          <div className="pageSubtitle" />
        </div>
        <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
          {selectedGrid ? (
            <div className="chip" title="Score temps réel">
              Score: <b>{typeof realtimeScore === "number" ? `${realtimeScore.toFixed(1)}%` : "—"}</b>
            </div>
          ) : null}
        </div>
      </div>

      <form className="card" style={{ padding: 16 }} onSubmit={onSubmit}>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(12, minmax(0, 1fr))", gap: 12 }}>
          <div style={{ gridColumn: "span 6" }}>
            <div className="label">Agent</div>
            <AsyncSelect
              cacheOptions
              defaultOptions={pilotOptions}
              loadOptions={loadPilots}
              value={pilotOptions.find((o) => String(o.value) === String(pilotId)) || null}
              onChange={(opt) => setPilotId(opt?.value || "")}
              placeholder="Rechercher un agent…"
              styles={{
                control: (base) => ({ ...base, minHeight: 42, borderRadius: 10 }),
                menu: (base) => ({ ...base, zIndex: 50 }),
              }}
            />
          </div>

          <div style={{ gridColumn: "span 6" }}>
            <div className="label">Grille</div>
            <select className="input" value={gridId} onChange={(e) => setGridId(e.target.value)}>
              <option value="">Choisir une grille</option>
              {(grids || []).map((g) => (
                <option key={String(g._id)} value={String(g._id)}>
                  {g.name || g.title || "Grille"}
                </option>
              ))}
            </select>
          </div>

          <div style={{ gridColumn: "span 4" }}>
            <div className="label">EPS</div>
            <input
              className="input"
              value={eps}
              onChange={(e) => setEps(e.target.value)}
              onBlur={() => checkEpsDuplicate(eps)}
              placeholder="EPS-123…"
            />
          </div>

          <div style={{ gridColumn: "span 4" }}>
            <div className="label">Date de l’appel</div>
            <input
              className="input"
              type="date"
              value={callDate}
              onChange={(e) => {
                setCallDate(e.target.value);
                setInteractionDate(e.target.value);
              }}
            />
          </div>

          <div style={{ gridColumn: "span 2" }}>
            <div className="label">Durée d’appel</div>
            <input className="input" value={callDuration} onChange={(e) => setCallDuration(e.target.value)} placeholder="00:00" />
          </div>

          <div style={{ gridColumn: "span 2" }}>
            <div className="label">Picking prime</div>
            <div style={{ display: "flex", alignItems: "center", gap: 10, height: 42 }}>
              <input id="pp" type="checkbox" checked={pickingPrime} onChange={(e) => setPickingPrime(e.target.checked)} />
              <label htmlFor="pp" style={{ fontSize: 14 }}>Activer</label>
            </div>
          </div>
        </div>

        <div style={{ marginTop: 12 }}>
          <div className="label">Commentaire</div>
          <textarea className="input" rows={3} value={comment} onChange={(e) => setComment(e.target.value)} placeholder="Remarques…" />
        </div>

        <div style={{ marginTop: 12 }}>
          <div className="label" style={{ marginBottom: 8 }}>Items de la grille</div>
          {!gridId ? (
            <div style={{ color: "rgba(30,41,59,0.7)", fontSize: 13 }}>Choisissez une grille pour voir ses items.</div>
          ) : (
            <div style={{ border: "1px solid var(--border)", borderRadius: 12, overflow: "hidden" }}>
              <table style={{ width: "100%", borderCollapse: "collapse" }}>
                <thead>
                  <tr style={{ textAlign: "left", background: "rgba(148,163,184,0.12)" }}>
                    <th style={{ padding: "10px 10px", borderBottom: "1px solid var(--border)" }}>Critère</th>
                    <th style={{ padding: "10px 10px", borderBottom: "1px solid var(--border)", width: 260 }}>Statut</th>
                  </tr>
                </thead>
                <tbody>
                  {renderRows.map((r) => {
                    if (r.type === "group") {
                      const hf = r.hardFail ? "Hard fail" : "";
                      const mal = r.malusPercent ? `Malus ${r.malusPercent}%` : "";
                      const meta = [hf, mal].filter(Boolean).join(" • ");
                      return (
                        <tr key={`g-${r.__idx}`}>
                          <td
                            colSpan={2}
                            style={{
                              padding: "10px 10px",
                              borderBottom: "1px solid rgba(148,163,184,0.25)",
                              fontWeight: 800,
                              background: "rgba(99,102,241,0.08)",
                            }}
                          >
                            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 10 }}>
                              <div style={{ whiteSpace: "pre-wrap" }}>{String(r.title || r.label || "").trim() || "Phase"}</div>
                              {meta ? <div style={{ fontSize: 12, color: "rgba(30,41,59,0.75)" }}>{meta}</div> : null}
                            </div>
                          </td>
                        </tr>
                      );
                    }

                    const key = String(r.__idx);
                    return (
                      <tr key={`i-${r.__idx}`}>
                        <td style={{ padding: "10px 10px", borderBottom: "1px solid rgba(148,163,184,0.25)", whiteSpace: "pre-wrap" }}>
                          {String(r.label || "").trim()}
                        </td>
                        <td style={{ padding: "10px 10px", borderBottom: "1px solid rgba(148,163,184,0.25)" }}>
                          <select
                            className="input"
                            value={statusByKey[key] || (gridType === "presence" ? "PC" : "C")}
                            onChange={(e) => setStatusByKey((m) => ({ ...m, [key]: e.target.value }))}
                          >
                            {statusOptions.map((o) => (
                              <option key={o.value} value={o.value}>{o.label}</option>
                            ))}
                          </select>
                        </td>
                      </tr>
                    );
                  })}
                  {renderRows.filter((r) => r.type !== "group").length === 0 ? (
                    <tr><td colSpan={2} style={{ padding: 12, color: "rgba(30,41,59,0.7)" }}>Aucun item.</td></tr>
                  ) : null}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {err ? (
          <div className="card" style={{ marginTop: 12, border: "1px solid rgba(239,68,68,0.35)", padding: 10, color: "#b91c1c" }}>
            {err}
          </div>
        ) : null}

        {okMsg ? (
          <div className="card" style={{ marginTop: 12, border: "1px solid rgba(34,197,94,0.35)", padding: 10, color: "#166534" }}>
            {okMsg}
          </div>
        ) : null}

        <div style={{ display: "flex", justifyContent: "flex-end", gap: 10, marginTop: 12 }}>
          <button type="submit" className="btn" disabled={loading}>
            {loading ? (editScoreId ? "Enregistrement…" : "Création…") : (editScoreId ? "Enregistrer" : "Créer")}
          </button>
        </div>
      </form>
    </div>
  );
}
