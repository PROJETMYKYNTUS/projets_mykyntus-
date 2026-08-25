import React, { useEffect, useMemo, useState, useRef } from "react";
import api from "../../api";
import AsyncSelect from "react-select/async";
import { useToast } from "../../toast/ToastProvider.jsx";
import HelpTip from "../../components/HelpTip.jsx";
import { isCqEmbed } from "../../embed.js";
import "./NewEvaluationPage.css";

/* ---- Helpers ---- */
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
function isTrue(v) { return v === true || v === 1 || v === "1" || v === "true" || v === "on"; }
function isoDate(v) {
  const d = v ? new Date(v) : new Date();
  if (Number.isNaN(d.getTime())) return "";
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}
function isValidDuration(v) {
  const s = (v || "").toString().trim();
  if (!s) return true;
  return /^\d{1,3}:\d{2}$/.test(s);
}

function parseDuration(v) {
  const s = (v || "").toString().trim();
  if (!s) return { min: "", sec: "" };
  const parts = s.split(":");
  if (parts.length >= 2) {
    return { min: String(parseInt(parts[0], 10) || 0), sec: String(parseInt(parts[1], 10) || 0) };
  }
  return { min: String(parseInt(s, 10) || 0), sec: "0" };
}

function formatDuration(min, sec) {
  const m = parseInt(String(min).replace(/\D/g, ""), 10);
  const s = parseInt(String(sec).replace(/\D/g, ""), 10);
  if (isNaN(m) && isNaN(s)) return "";
  return `${String(m || 0).padStart(2, "0")}:${String(Math.min(s || 0, 59)).padStart(2, "0")}`;
}

function formatPlayerTime(seconds) {
  if (!Number.isFinite(seconds)) return "00:00";
  const mins = Math.floor(seconds / 60);
  const secs = Math.floor(seconds % 60);
  return `${String(mins).padStart(2, "0")}:${String(secs).padStart(2, "0")}`;
}

function computeCompliancePercent(items, gridDoc) {
  const its = Array.isArray(items) ? items : [];
  const gridItems = Array.isArray(gridDoc?.items) ? gridDoc.items : [];
  const gridType = (gridDoc?.gridType || "classic").toString();
  const groupByLabel = new Map();
  let currentGroup = null;
  for (const gi of gridItems) {
    if (!gi) continue;
    if (gi.type === "group") { currentGroup = { hardFail: isTrue(gi.hardFail), malusPercent: Number.isFinite(Number(gi.malusPercent)) ? Number(gi.malusPercent) : 0 }; continue; }
    const lbl = (gi.label || "").toString().trim();
    if (lbl) groupByLabel.set(lbl, currentGroup);
  }
  if (gridType === "presence") {
    let obtained = 0, maxApplicable = 0, totalMalus = 0;
    for (const it of its) {
      const label = (it?.label || "").toString().trim(); if (!label) continue;
      const status = normalizeStatus(it.status); if (status === "NA") continue;
      const group = groupByLabel.get(label) || null;
      const isNC = status === "PNC" || status === "NP" || status === "NC";
      if (isNC && group?.hardFail) return 0;
      if (isNC && group?.malusPercent > 0) totalMalus += group.malusPercent;
      maxApplicable += 1;
      if (status === "PC" || status === "C") obtained += 1;
      else if (status === "PNC") obtained += 0.5;
    }
    if (maxApplicable <= 0) return 0;
    return Math.round(Math.max(0, Math.min(100, (obtained / maxApplicable) * 100) - totalMalus) * 10) / 10;
  }
  const pointsByLabel = new Map();
  const itemMalusByLabel = new Map();
  for (const gi of gridItems) {
    if (!gi || gi.type === "group") continue;
    const label = (gi.label || "").toString().trim(); if (!label) continue;
    pointsByLabel.set(label, { pC: typeof gi.pointsConforme === "number" ? gi.pointsConforme : 1, pNC: typeof gi.pointsNonConforme === "number" ? gi.pointsNonConforme : 0 });
    itemMalusByLabel.set(label, Number.isFinite(Number(gi.malusPercent)) ? Number(gi.malusPercent) : 0);
  }
  let obtained = 0, maxApplicable = 0, totalMalus = 0;
  for (const it of its) {
    const label = (it?.label || "").toString().trim(); if (!label) continue;
    const status = normalizeStatus(it.status); if (status === "NA") continue;
    const group = groupByLabel.get(label) || null;
    if (status === "NC" && group?.hardFail) return 0;
    if (status === "NC") { const im = Number(itemMalusByLabel.get(label)) || 0; const pm = group?.malusPercent > 0 ? group.malusPercent : 0; if ((im || pm) > 0) totalMalus += (im > 0 ? im : pm); }
    const pts = pointsByLabel.get(label); const pC = pts ? pts.pC : 1; const pNC = pts ? pts.pNC : 0;
    maxApplicable += pC;
    if (status === "C") obtained += pC; else if (status === "NC") obtained += pNC;
  }
  if (maxApplicable <= 0) return 0;
  return Math.round(Math.max(0, Math.min(100, (obtained / maxApplicable) * 100) - totalMalus) * 10) / 10;
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

/* ==================== COMPONENT ==================== */

/**
 * Props:
 * - title: string
 * - editScoreId: string (edit mode)
 * - pickingCall: object | null (when coming from picking list — pre-fills EPS, date, duration, audio)
 * - onBack: fn (optional — go back to picking list)
 */
export default function NewEvaluationPage({ title = "Nouvelle évaluation", editScoreId = "", pickingCall = null, onBack }) {
  const embed = isCqEmbed();
  const toast = useToast();
  const user = useMemo(() => { try { return JSON.parse(localStorage.getItem("user") || "null"); } catch { return null; } }, []);
  const draftKey = useMemo(() => `draft_eval_${user?._id || user?.id || "anon"}`, [user]);
  const isPicking = !!pickingCall;
  
  const [pendingCall, setPendingCall] = useState(null);

  const effectivePickingCall = useMemo(() => {
    if (pickingCall) return pickingCall;
    return pendingCall;
  }, [pickingCall, pendingCall]);

  const effectiveIsPicking = !!effectivePickingCall;

  const [loading, setLoading] = useState(false);
  const [err, setErr] = useState("");
  const [okMsg, setOkMsg] = useState("");

  const [pilots, setPilots] = useState([]);
  const [grids, setGrids] = useState([]);

  const [pilotId, setPilotId] = useState("");
  const [gridId, setGridId] = useState("");
  const [eps, setEps] = useState("");
  const [callDate, setCallDate] = useState(isoDate(new Date()));
  const [interactionDate, setInteractionDate] = useState(isoDate(new Date()));
  const [pickingPrime, setPickingPrime] = useState(false);
  const [comment, setComment] = useState("");
  const [callDuration, setCallDuration] = useState("");
  const [statusByKey, setStatusByKey] = useState({});
  const [savedScoreItems, setSavedScoreItems] = useState([]);

  // Audio
  const [audioSource, setAudioSource] = useState("");
  const [audioFile, setAudioFile] = useState(null);
  const audioRef = useRef(null);

  const [playerReady, setPlayerReady] = useState(false);
  const [isPlaying, setIsPlaying] = useState(false);
  const [playerCurrentTime, setPlayerCurrentTime] = useState(0);
  const [playerDuration, setPlayerDuration] = useState(0);

  function navigateToEvaluations() {
    const role = user?.role;
    if (!role) return;

    const view = role === "management" ? "form" : "list";
    window.dispatchEvent(
      new CustomEvent("kcq:navigate", {
        detail: { role, view },
      })
    );
  }

  /* ---- Load pending call from sessionStorage ---- */
  useEffect(() => {
    if (editScoreId) return;

    try {
      const raw = sessionStorage.getItem("pending_call_evaluation");
      if (!raw) return;
      const parsed = JSON.parse(raw);
      if (parsed && typeof parsed === "object") {
        setPendingCall(parsed);
      }
    } catch {}
  }, [editScoreId]);

  /* ---- Apply picking call data on mount ---- */
  useEffect(() => {
    if (!effectivePickingCall || editScoreId) return;

    if (effectivePickingCall.eps) setEps(String(effectivePickingCall.eps));

    if (effectivePickingCall.callDate) {
      const d = isoDate(new Date(effectivePickingCall.callDate));
      setCallDate(d);
      setInteractionDate(isoDate(new Date()));
    }

    if (effectivePickingCall.callDuration) setCallDuration(String(effectivePickingCall.callDuration));
    if (effectivePickingCall.pilotId) setPilotId(String(effectivePickingCall.pilotId));
    if (effectivePickingCall.comment) setComment(String(effectivePickingCall.comment || ""));
  }, [effectivePickingCall, editScoreId]);

  /* ---- Load protected audio with authentication ---- */
  useEffect(() => {
    let revokedUrl = null;

    async function loadProtectedAudio() {
      if (!effectivePickingCall || editScoreId) return;

      const remoteAudio =
        effectivePickingCall.audioUrl ||
        effectivePickingCall.recordingUrl ||
        effectivePickingCall.audio ||
        "";

      if (!remoteAudio) {
        setAudioSource("");
        return;
      }

      try {
        const res = await api.get(remoteAudio, { responseType: "blob", toast: false });
        const blobUrl = URL.createObjectURL(res.data);
        revokedUrl = blobUrl;
        setAudioSource(blobUrl);
      } catch (e) {
        setAudioSource("");
      }
    }

    loadProtectedAudio();

    return () => {
      if (revokedUrl) {
        URL.revokeObjectURL(revokedUrl);
      }
    };
  }, [effectivePickingCall, editScoreId]);

  /* ---- Draft restore (only for empty new eval, not picking) ---- */
  useEffect(() => {
    if (editScoreId || effectiveIsPicking) return;
    try {
      const raw = localStorage.getItem(draftKey);
      if (!raw) return;
      const d = JSON.parse(raw);
      if (!d || typeof d !== "object") return;
      if (d.pilotId) setPilotId(String(d.pilotId));
      if (d.gridId) setGridId(String(d.gridId));
      if (d.eps) setEps(String(d.eps));
      if (d.callDate) setCallDate(String(d.callDate));
      if (d.interactionDate) setInteractionDate(String(d.interactionDate));
      if (d.callDuration) setCallDuration(String(d.callDuration));
      if (d.comment) setComment(String(d.comment));
      if (d.pickingPrime !== undefined) setPickingPrime(!!d.pickingPrime);
      if (d.statusByKey && typeof d.statusByKey === "object") setStatusByKey(d.statusByKey);
    } catch {}
  }, []);

  // Auto-save draft (only empty new eval)
  useEffect(() => {
    if (editScoreId || effectiveIsPicking) return;
    try {
      localStorage.setItem(draftKey, JSON.stringify({ pilotId, gridId, eps, callDate, interactionDate, callDuration, comment, pickingPrime, statusByKey, updatedAt: Date.now() }));
    } catch {}
  }, [editScoreId, effectiveIsPicking, pilotId, gridId, eps, callDate, interactionDate, callDuration, comment, pickingPrime, statusByKey, draftKey]);

  /* ---- Load pilots ---- */
  useEffect(() => {
    let m = true;
    api.get("/cq/pilots/search", { params: { limit: 200 } }).then((r) => m && setPilots(Array.isArray(r.data) ? r.data : [])).catch(() => {});
    return () => { m = false; };
  }, []);

  /* ---- Load grids ---- */
  useEffect(() => {
    let m = true;
    api.get("/grids/my").then((r) => m && setGrids(Array.isArray(r.data) ? r.data : [])).catch((e) => m && setErr(e?.response?.data?.message || "Impossible de charger les grilles."));
    return () => { m = false; };
  }, []);

  /* ---- Edit mode: load evaluation ---- */
  useEffect(() => {
    if (!editScoreId) return;
    let m = true;
    (async () => {
      try {
        setErr(""); setOkMsg("");
        const { data } = await api.get(`/scores/${editScoreId}`);
        if (!m) return;
        if (data?.pilot?._id) setPilotId(String(data.pilot._id));
        else if (data?.pilotId) setPilotId(String(data.pilotId));
        else if (data?.pilot) setPilotId(String(data.pilot));
        if (data?.gridId?._id) setGridId(String(data.gridId._id));
        else if (data?.grid?._id) setGridId(String(data.grid._id));
        else if (data?.gridId) setGridId(String(data.gridId));
        if (data?.eps !== undefined) setEps(String(data.eps || ""));
        if (data?.pickingPrime !== undefined) setPickingPrime(!!data.pickingPrime);
        if (data?.comment !== undefined) setComment(String(data.comment || ""));
        if (data?.callDuration !== undefined) setCallDuration(String(data.callDuration || ""));
        if (data?.callDate) setCallDate(isoDate(new Date(data.callDate)));
        if (data?.interactionDate) setInteractionDate(isoDate(new Date(data.interactionDate)));
        else if (data?.callDate) setInteractionDate(isoDate(new Date(data.callDate)));
        setSavedScoreItems(Array.isArray(data?.items) ? data.items : []);
      } catch (e) { if (m) setErr(e?.response?.data?.message || "Erreur lors du chargement."); }
    })();
    return () => { m = false; };
  }, [editScoreId]);

  // Fetch soft-deleted grid in edit mode
  useEffect(() => {
    if (!editScoreId) return;
    const id = String(gridId || "").trim(); if (!id) return;
    if ((grids || []).some((g) => String(g?._id) === id)) return;
    let m = true;
    api.get(`/grids/${id}`, { params: { includeDeleted: 1 } }).then((r) => {
      if (!m || !r?.data?._id) return;
      setGrids((prev) => { const p = Array.isArray(prev) ? prev : []; return p.some((g) => String(g?._id) === String(r.data._id)) ? p : [r.data, ...p]; });
    }).catch(() => {});
    return () => { m = false; };
  }, [editScoreId, gridId, grids]);

  const pilotOptions = useMemo(() => (pilots || []).map((p) => ({ value: String(p._id), label: `${p.name || ""}${p.cell ? " — " + p.cell : ""}`.trim() })), [pilots]);
  async function loadPilots(v) {
    try {
      const r = await api.get("/cq/pilots/search", { params: { q: String(v || "").trim(), limit: 200 } });
      return (r.data || []).map((p) => ({ value: String(p._id), label: `${p.name || ""}${p.cell ? " — " + p.cell : ""}`.trim() }));
    } catch { return pilotOptions; }
  }

  const selectedGrid = useMemo(() => (grids || []).find((g) => String(g._id) === String(gridId)), [grids, gridId]);
  const gridType = (selectedGrid?.gridType || "classic").toString();
  const statusOptions = gridType === "presence" ? STATUS_PRESENCE : STATUS_CLASSIC;
  const renderRows = useMemo(() => {
    const items = Array.isArray(selectedGrid?.items) ? selectedGrid.items : [];
    return items.map((it, idx) => ({ ...it, __idx: idx })).filter((it) => it && (it.type === "group" || String(it.label || "").trim()));
  }, [selectedGrid]);

  // Init statuses when grid changes
  useEffect(() => {
    if (!selectedGrid) { setStatusByKey({}); return; }
    const savedByLabel = new Map();
    if (Array.isArray(savedScoreItems)) for (const it of savedScoreItems) { const l = (it?.label || "").trim(); const st = normalizeStatus(it?.status); if (l && st) savedByLabel.set(l, st); }
    const next = {};
    for (const r of renderRows) { if (r.type === "group") continue; const key = String(r.__idx); const label = String(r.label || "").trim(); next[key] = (label ? savedByLabel.get(label) : "") || (gridType === "presence" ? "PC" : "C"); }
    setStatusByKey(next);
  }, [gridId, selectedGrid?._id]);

  const scoreItemsForCalc = useMemo(() => renderRows.filter((r) => r.type !== "group").map((r) => ({ label: String(r.label || "").trim(), status: statusByKey[String(r.__idx)] || (gridType === "presence" ? "PC" : "C") })).filter((x) => x.label), [renderRows, statusByKey, gridType]);
  const realtimeScore = useMemo(() => selectedGrid ? computeCompliancePercent(scoreItemsForCalc, selectedGrid) : null, [scoreItemsForCalc, selectedGrid]);
  const realtimeStats = useMemo(() => {
    const st = { total: 0, C: 0, NC: 0, NA: 0, PC: 0, PNC: 0 };
    for (const it of scoreItemsForCalc) { st.total += 1; const s = normalizeStatus(it.status); if (st[s] != null) st[s] += 1; }
    return { ...st, conform: (st.C || 0) + (st.PC || 0), nonConform: (st.NC || 0) + (st.PNC || 0) };
  }, [scoreItemsForCalc]);

  function handleAudioFile(e) { const f = e.target.files?.[0]; if (!f) return; setAudioFile(f); setAudioSource(URL.createObjectURL(f)); }

  async function checkEpsDuplicate(v) {
    const val = (v || "").trim(); if (!val) return;
    try { const r = await api.get("/scores/check-eps", { params: { eps: val } }); if (r.data?.duplicate) setErr("EPS doublon : une évaluation existe déjà avec cet EPS."); } catch {}
  }

  async function onSubmit(e) {
    e.preventDefault(); setErr(""); setOkMsg("");
    if (!pilotId) return setErr("Veuillez choisir un agent.");
    if (!gridId) return setErr("Veuillez choisir une grille.");
    if (!String(eps || "").trim()) return setErr("Veuillez renseigner l'EPS.");
    if (!isValidDuration(callDuration)) return setErr("Durée invalide.");
    const items = renderRows.filter((r) => r.type !== "group").map((r) => ({ label: String(r.label || "").trim(), status: statusByKey[String(r.__idx)] || (gridType === "presence" ? "PC" : "C") })).filter((it) => it.label);
    if (!items.length) return setErr("Cette grille ne contient aucun item évaluable.");
    const payload = { pilotId, gridId, eps: String(eps || "").trim(), pickingPrime: !!pickingPrime, callDate: callDate ? new Date(callDate).toISOString() : null, interactionDate: interactionDate ? new Date(interactionDate).toISOString() : callDate ? new Date(callDate).toISOString() : null, callDuration: callDuration || "", comment: comment || "", items };
    setLoading(true);
    try {
      if (editScoreId) {
        await api.patch(`/scores/${editScoreId}`, payload);
        setOkMsg("Évaluation mise à jour avec succès.");
        setTimeout(() => navigateToEvaluations(), 700);
      } else {
        await api.post("/scores", payload);
        setOkMsg("Évaluation créée avec succès.");
        try { localStorage.removeItem(draftKey); } catch {}
        try { sessionStorage.removeItem("pending_call_evaluation"); } catch {}

        setTimeout(() => navigateToEvaluations(), 700);
      }
    } catch (e2) { setErr(e2?.response?.data?.message || "Erreur lors de l'enregistrement."); }
    finally { setLoading(false); }
  }

  useEffect(() => {
    const audio = audioRef.current;
    if (!audio) return;

    const onLoadedMetadata = () => {
      setPlayerReady(true);
      setPlayerDuration(audio.duration || 0);
    };
    const onTimeUpdate = () => setPlayerCurrentTime(audio.currentTime || 0);
    const onPlay = () => setIsPlaying(true);
    const onPause = () => setIsPlaying(false);
    const onEnded = () => setIsPlaying(false);

    audio.addEventListener("loadedmetadata", onLoadedMetadata);
    audio.addEventListener("timeupdate", onTimeUpdate);
    audio.addEventListener("play", onPlay);
    audio.addEventListener("pause", onPause);
    audio.addEventListener("ended", onEnded);

    return () => {
      audio.removeEventListener("loadedmetadata", onLoadedMetadata);
      audio.removeEventListener("timeupdate", onTimeUpdate);
      audio.removeEventListener("play", onPlay);
      audio.removeEventListener("pause", onPause);
      audio.removeEventListener("ended", onEnded);
    };
  }, [audioSource]);

  useEffect(() => {
    const audio = audioRef.current;
    if (!audio) return;
    setPlayerReady(false);
    setIsPlaying(false);
    setPlayerCurrentTime(0);
    setPlayerDuration(0);
    if (audioSource) {
      audio.load();
    }
  }, [audioSource]);

  function togglePlayPause() {
    const audio = audioRef.current;
    if (!audio || !audioSource) return;

    if (audio.paused) {
      audio.play().catch(() => {});
    } else {
      audio.pause();
    }
  }

  function skipPlayer(seconds) {
    const audio = audioRef.current;
    if (!audio) return;

    const next = Math.max(0, Math.min((audio.currentTime || 0) + seconds, playerDuration || 0));
    audio.currentTime = next;
    setPlayerCurrentTime(next);
  }

  function handlePlayerSeek(e) {
    const audio = audioRef.current;
    if (!audio) return;

    const next = Number(e.target.value || 0);
    audio.currentTime = next;
    setPlayerCurrentTime(next);
  }
  const scoreColor = realtimeScore != null ? (realtimeScore >= 80 ? "var(--success)" : realtimeScore >= 50 ? "var(--warning)" : "var(--danger)") : "var(--muted)";

  /* ==================== RENDER ==================== */
  return (
    <div className="page nep">
      <div className="nep__header">
        <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
          {onBack && (
            <button type="button" className="btn btn--ghost btn--sm" onClick={onBack} style={{ flexShrink: 0 }}>
              ← Retour
            </button>
          )}
          {!embed && (
            <div>
              <div className="nep__title">{title}</div>
              <div className="nep__subtitle">
                {effectiveIsPicking
                  ? <>Appel <b style={{ color: "var(--primary)" }}>{effectivePickingCall?.eps || ""}</b> — remplissez la grille d'évaluation.</>
                  : editScoreId ? "Modifier les informations de l'évaluation." : "Renseigne les informations, puis évalue chaque critère."}
              </div>
            </div>
          )}
        </div>
        <div className="nep__headerRight">
          {selectedGrid ? (
            <div className="nep__scorePill"><span>Score</span><b style={{ color: scoreColor }}>{typeof realtimeScore === "number" ? `${realtimeScore.toFixed(1)}%` : "—"}</b></div>
          ) : (
            <div className="nep__scorePill nep__scorePill--muted"><span>Score</span><b>—</b></div>
          )}
          <button type="submit" form="nep-form" className="btn nep__primaryBtn" disabled={loading}>
            {loading ? (editScoreId ? "Enregistrement…" : "Création…") : (editScoreId ? "Enregistrer" : "Créer l'évaluation")}
          </button>
        </div>
      </div>

      {/* Picking info banner */}
      {effectiveIsPicking && (
        <div className="card" style={{ padding: "10px 14px", marginBottom: 12, display: "flex", alignItems: "center", gap: 12, background: "var(--primary-bg)", borderColor: "var(--primary-border)" }}>
          <span style={{ fontSize: "1.2rem" }}>📋</span>
          <div style={{ flex: 1 }}>
            <div style={{ fontWeight: 700, fontSize: "0.875rem", color: "var(--primary)" }}>Appel sélectionné</div>
            <div style={{ fontSize: "0.8rem", color: "var(--text-secondary)" }}>
              TAG : <b>{effectivePickingCall?.eps || "—"}</b> • N° Cellule : <b>{effectivePickingCall?.cell || "—"}</b> • N° Client : <b>{effectivePickingCall?.phone || "—"}</b> • Date : <b>{effectivePickingCall?.callDate ? new Date(effectivePickingCall.callDate).toLocaleDateString("fr-FR") : "—"}</b> • Durée : <b>{effectivePickingCall?.callDuration || "—"}</b>
            </div>
          </div>
        </div>
      )}

      <form id="nep-form" className="card nep__card" onSubmit={onSubmit}>
        <div className="nep__layout">
          {/* Left column - Summary */}
          <div className="nep__col">
            <div className="nep__section nep__summary">
              <div className="nep__sectionTitle">Résumé</div>

              <div className="nep__rt">
                <div className="nep__rtMain">
                  <div className="nep__rtLabel">Conformité</div>
                  <div className="nep__rtValue" style={{ color: scoreColor }}>
                    {typeof realtimeScore === "number" ? `${realtimeScore.toFixed(1)}%` : "—"}
                  </div>
                </div>
                <div className="nep__rtMeta">
                  <span>{realtimeStats.conform} C</span>
                  <span>•</span>
                  <span>{realtimeStats.nonConform} NC</span>
                  <span>•</span>
                  <span>{realtimeStats.NA} NA</span>
                  <span>•</span>
                  <span>{realtimeStats.total} items</span>
                </div>
              </div>

              <div className="nep__summaryGrid">
                <div className="nep__kv">
                  <div className="nep__k">Agent</div>
                  <div className="nep__v">{pilotOptions.find((o) => String(o.value) === String(pilotId))?.label || "—"}</div>
                </div>
                <div className="nep__kv">
                  <div className="nep__k">Grille</div>
                  <div className="nep__v">{selectedGrid?.name || selectedGrid?.title || "—"}</div>
                </div>
                <div className="nep__kv">
                  <div className="nep__k">EPS</div>
                  <div className="nep__v">{eps || "—"}</div>
                </div>
                <div className="nep__kv">
                  <div className="nep__k">Date appel</div>
                  <div className="nep__v">{callDate || "—"}</div>
                </div>
                <div className="nep__kv">
                  <div className="nep__k">Durée</div>
                  <div className="nep__v">{callDuration || "—"}</div>
                </div>
                <div className="nep__kv">
                  <div className="nep__k">Picking prime</div>
                  <div className="nep__v">{pickingPrime ? "Oui" : "Non"}</div>
                </div>
              </div>

              {effectiveIsPicking && (
                <div className="nep__stickyAudio">
                  <div className="nep__sectionTitle" style={{ marginBottom: 10 }}>
                    <span>🎧 Appel sélectionné</span>
                    {audioSource ? (
                      <span className="badge badge--success" style={{ fontSize: "0.7rem" }}>Audio chargé</span>
                    ) : null}
                  </div>

                  <div className="nep__audioMeta">
                    <div><b>TAG :</b> {effectivePickingCall?.eps || "—"}</div>
                    <div><b>Cellule :</b> {effectivePickingCall?.cell || "—"}</div>
                    <div><b>Client :</b> {effectivePickingCall?.phone || "—"}</div>
                    <div><b>Date :</b> {effectivePickingCall?.callDate ? new Date(effectivePickingCall.callDate).toLocaleString("fr-FR") : "—"}</div>
                  </div>

                  <audio ref={audioRef} preload="metadata" style={{ display: "none" }}>
                    <source src={audioSource} />
                  </audio>

                  {audioSource ? (
                    <>
                      <div className="nep__playerControls">
                        <button type="button" className="btn btn--sm" onClick={() => skipPlayer(-10)}>
                          ⏪ -10s
                        </button>
                        <button type="button" className="btn btn--sm btn--primary" onClick={togglePlayPause}>
                          {isPlaying ? "Pause" : "Play"}
                        </button>
                        <button type="button" className="btn btn--sm" onClick={() => skipPlayer(10)}>
                          +10s ⏩
                        </button>
                      </div>

                      <div className="nep__playerSeek">
                        <span>{formatPlayerTime(playerCurrentTime)}</span>
                        <input
                          type="range"
                          min="0"
                          max={Math.max(playerDuration, 0)}
                          step="0.1"
                          value={Math.min(playerCurrentTime, playerDuration || 0)}
                          onChange={handlePlayerSeek}
                        />
                        <span>{formatPlayerTime(playerDuration)}</span>
                      </div>
                    </>
                  ) : (
                    <div className="nep__audioEmpty">Aucun enregistrement audio disponible pour cet appel.</div>
                  )}
                </div>
              )}

              {err ? <div className="nep__alert nep__alert--err">{err}</div> : null}
              {okMsg ? <div className="nep__alert nep__alert--ok">{okMsg}</div> : null}

              {!embed && (
              <div className="nep__summaryActions">
                <button type="submit" className="btn nep__primaryBtn" disabled={loading}>
                  {loading ? (editScoreId ? "Enregistrement…" : "Création…") : (editScoreId ? "Enregistrer" : "Créer")}
                </button>
              </div>
              )}
            </div>
          </div>

          {/* Right column - Form + Grid */}
          <div className="nep__col">
            <div className="nep__section">
              <div className="nep__sectionTitle">Informations</div>
              <div className="nep__grid">
                <div className="nep__field nep__field--span2">
                  <div className="label">Agent <HelpTip text="Agent évalué." /></div>
                  <AsyncSelect
                    cacheOptions defaultOptions={pilotOptions} loadOptions={loadPilots}
                    value={pilotOptions.find((o) => String(o.value) === String(pilotId)) || null}
                    onChange={(opt) => setPilotId(opt?.value || "")}
                    placeholder="Rechercher un agent…"
                    styles={{
                      control: (base) => ({ ...base, minHeight: 40, borderRadius: 12, borderColor: "var(--border-strong)", background: "var(--panel)", fontSize: "0.875rem" }),
                      menu: (base) => ({ ...base, zIndex: 50, borderRadius: 12, overflow: "hidden" }),
                      option: (base, state) => ({ ...base, fontSize: "0.875rem", background: state.isFocused ? "var(--chip)" : "var(--panel)" }),
                      singleValue: (base) => ({ ...base, color: "var(--text)" }),
                      input: (base) => ({ ...base, color: "var(--text)" }),
                    }}
                  />
                </div>
                <div className="nep__field nep__field--span2">
                  <div className="label">Grille <HelpTip text="Critères & calcul." /></div>
                  <select className="input" value={gridId} onChange={(e) => setGridId(e.target.value)}>
                    <option value="">Choisir une grille</option>
                    {(grids || []).map((g) => <option key={String(g._id)} value={String(g._id)}>{g.name || g.title || "Grille"}</option>)}
                  </select>
                </div>
                <div className="nep__field">
                  <div className="label">EPS <HelpTip text="Identifiant de l'appel." /></div>
                  <input className="input" value={eps} onChange={(e) => setEps(e.target.value)} onBlur={() => checkEpsDuplicate(eps)} placeholder="EPS-123…" readOnly={effectiveIsPicking} style={effectiveIsPicking ? { background: "var(--chip)" } : undefined} />
                </div>
                <div className="nep__field">
                  <div className="label">Date de l'appel <HelpTip text="Date du contact (auto en mode picking)." /></div>
                  <input className="input" type="date" value={callDate} onChange={(e) => setCallDate(e.target.value)} readOnly={effectiveIsPicking} style={effectiveIsPicking ? { background: "var(--chip)" } : undefined} />
                </div>
                <div className="nep__field">
                  <div className="label">Date de l'évaluation <HelpTip text="Date de réalisation." /></div>
                  <input className="input" type="date" value={interactionDate} onChange={(e) => setInteractionDate(e.target.value)} />
                </div>
                <div className="nep__field">
                  <div className="label">Durée <HelpTip text="Minutes et secondes." /></div>
                  <div style={{ display: "flex", gap: 4, alignItems: "center" }}>
                    <input
                      className="input" type="number" min="0" max="999" placeholder="min"
                      value={(() => { const p = parseDuration(callDuration); return p.min; })()}
                      onChange={(e) => {
                        const m = e.target.value.replace(/\D/g, "");
                        const s = parseDuration(callDuration).sec;
                        setCallDuration(m || s ? `${String(m || 0).padStart(2, "0")}:${String(s || 0).padStart(2, "0")}` : "");
                      }}
                      onBlur={() => { if (callDuration) setCallDuration(formatDuration(parseDuration(callDuration).min, parseDuration(callDuration).sec)); }}
                      style={{ width: 70, textAlign: "center", MozAppearance: "textfield" }}
                      readOnly={effectiveIsPicking && !!callDuration}
                    />
                    <span style={{ fontWeight: 800, color: "var(--muted)", fontSize: "1.1rem", userSelect: "none" }}>:</span>
                    <input
                      className="input" type="number" min="0" max="59" placeholder="sec"
                      value={(() => { const p = parseDuration(callDuration); return p.sec; })()}
                      onChange={(e) => {
                        const raw = e.target.value.replace(/\D/g, "");
                        const s = Math.min(59, parseInt(raw, 10) || 0);
                        const m = parseDuration(callDuration).min;
                        setCallDuration(`${String(m || 0).padStart(2, "0")}:${String(s).padStart(2, "0")}`);
                      }}
                      onBlur={() => { if (callDuration) setCallDuration(formatDuration(parseDuration(callDuration).min, parseDuration(callDuration).sec)); }}
                      style={{ width: 70, textAlign: "center", MozAppearance: "textfield" }}
                      readOnly={effectiveIsPicking && !!callDuration}
                    />
                    <span style={{ fontSize: "0.75rem", color: "var(--muted)", marginLeft: 4 }}>min : sec</span>
                  </div>
                </div>
                <div className="nep__field">
                  <div className="label">Picking prime <HelpTip text="Éligibilité prime." /></div>
                  <label className="nep__toggle">
                    <input type="checkbox" checked={pickingPrime} onChange={(e) => setPickingPrime(e.target.checked)} />
                    <span>Activer</span>
                  </label>
                </div>
              </div>
              <div className="nep__field nep__field--full">
                <div className="label">Commentaire <HelpTip text="Notes internes." /></div>
                <textarea className="input" rows={3} value={comment} onChange={(e) => setComment(e.target.value)} placeholder="Remarques…" />
              </div>
            </div>

            {/* Grid Items */}
            <div className="nep__section">
              <div className="nep__sectionTitle">Items de la grille</div>
              {!gridId ? (
                <div className="nep__hint">Choisissez une grille pour voir ses items.</div>
              ) : (
                <div className="nep__tableWrap">
                  <table className="nep__table">
                    <thead>
                      <tr>
                        <th>Critère</th>
                        <th className="nep__thRight">Statut</th>
                      </tr>
                    </thead>
                    <tbody>
                      {renderRows.map((r) => {
                        if (r.type === "group") {
                          const hf = r.hardFail ? "Hard fail" : "";
                          const mal = r.malusPercent ? `Malus ${r.malusPercent}%` : "";
                          const meta = [hf, mal].filter(Boolean).join(" • ");
                          return (
                            <tr key={`g-${r.__idx}`} className="nep__groupRow">
                              <td colSpan={2}><div className="nep__groupRowInner"><div className="nep__groupTitle">{String(r.title || r.label || "").trim() || "Phase"}</div>{meta ? <div className="nep__groupMeta">{meta}</div> : null}</div></td>
                            </tr>
                          );
                        }
                        const key = String(r.__idx);
                        const cur = statusByKey[key] || (gridType === "presence" ? "PC" : "C");
                        const isNC = ["NC", "PNC", "NP"].includes(cur);
                        return (
                          <tr key={`i-${r.__idx}`} style={isNC ? { background: "var(--danger-bg)" } : undefined}>
                            <td className="nep__tdLabel">{String(r.label || "").trim()}</td>
                            <td className="nep__tdStatus">
                              <select className="input nep__statusSelect" value={cur} onChange={(e) => setStatusByKey((m) => ({ ...m, [key]: e.target.value }))}>
                                {statusOptions.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                              </select>
                            </td>
                          </tr>
                        );
                      })}
                      {renderRows.filter((r) => r.type !== "group").length === 0 && (
                        <tr><td colSpan={2} className="nep__empty">Aucun item.</td></tr>
                      )}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </div>
        </div>
      </form>
    </div>
  );
}
