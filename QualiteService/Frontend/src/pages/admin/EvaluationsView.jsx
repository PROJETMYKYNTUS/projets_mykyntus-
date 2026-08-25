import React, { useEffect, useMemo, useState } from "react";
import axios from "../../api";
import Card from "./components/Card.jsx";
import Pager from "./components/Pager.jsx";
import { exportToXlsx } from "./components/exportXlsx.js";
import FiltersBar from "./components/FiltersBar.jsx";
import ScoresTable from "./components/ScoresTable.jsx";
import StatsCards from "./components/StatsCards.jsx";
import EditScoreModal from "./components/EditScoreModal.jsx";
import { useToast } from "../../toast/ToastProvider.jsx";

const PAGE_SIZE = 25;

const safeText = (v) => {
  if (v === null || v === undefined) return "";
  if (typeof v === "string" || typeof v === "number" || typeof v === "boolean") return String(v);
  if (typeof v === "object") return String(v.name || v.label || v.title || v.email || v._id || v.id || "");
  return String(v);
};

const getId = (v) => {
  if (!v) return "";
  if (typeof v === "string") return v;
  if (typeof v === "object") return String(v._id || v.id || "");
  return String(v);
};

const get = (obj, keys, fallback = "") => {
  for (const k of keys) {
    const v = obj?.[k];
    if (v !== undefined && v !== null && String(v).length > 0) return v;
  }
  return fallback;
};

// Minimal, robust percent computation:
// Prefer backend compliancePercent when present; otherwise fallback to stored percent/percentage.
const normalizeRow = (r, kind, gridsById) => {
  const date = get(r, ["createdAt"], "");
  const pilot = safeText(get(r, ["pilotName", "pilot", "pilotUserName", "agent"], ""));
  const cell = safeText(get(r, ["cell", "pilotCell"], ""));
  const evaluator = safeText(get(r, ["evaluatorName", "cqName", "managerName", "evaluator"], ""));

  const rawGridId = get(r, ["gridId", "grid", "grid_id"], "");
  const gridId = getId(rawGridId);
  void gridsById; // reserved for future use (kept to avoid reintroducing large computed scoring here)

  const score = Number(get(r, ["compliancePercent", "percent", "percentage"], 0)) || 0;
  const status = safeText(get(r, ["status", "result", "state"], "")) || (r?.contested ? "Contestée" : "");
  const comment = safeText(get(r, ["comment", "commentaire", "notes"], ""));
  const eps = safeText(get(r, ["eps"], ""));
  const pickingPrime = !!get(r, ["pickingPrime"], false);

  return {
    kind,
    date,
    pilot,
    cell,
    evaluator,
    eps,
    pickingPrime,
    score: Math.round(score * 10) / 10,
    status,
    comment,
    gridId,
    __raw: r,
    _id: r?._id,
  };
};

export default function EvaluationsView() {
  const toast = useToast();
  const [scoresRaw, setScoresRaw] = useState([]);
  const [gridsRaw, setGridsRaw] = useState([]);

  const [q, setQ] = useState("");
  const [cell, setCell] = useState([]);
  const [grid, setGrid] = useState([]);
  const [year, setYear] = useState(() => [String(new Date().getFullYear())]);
  const [month, setMonth] = useState(() => [String(new Date().getMonth() + 1)]);
  const [pickingPrime, setPickingPrime] = useState([]);

  const [isTruncated, setIsTruncated] = useState(false);

  const [pageCq, setPageCq] = useState(1);
  const [pageMg, setPageMg] = useState(1);

  const [editingScore, setEditingScore] = useState(null);
  const [isSavingEdit, setIsSavingEdit] = useState(false);

  const fetchScoresAllPages = async (params) => {
    const HARD_CAP = 2000;
    const acc = [];
    let page = 1;
    let total = 0;
    let safety = 0;
    setIsTruncated(false);

    while (safety < 200) {
      safety += 1;
      const res = await axios.get("/scores", { params: { ...params, page, limit: 200 } });
      const payload = res?.data || {};
      const items = Array.isArray(payload.items) ? payload.items : [];
      total = Number(payload.total || 0);
      acc.push(...items);

      if (acc.length >= total) break;
      if (acc.length >= HARD_CAP) {
        setIsTruncated(true);
        break;
      }
      page += 1;
    }

    return acc.slice(0, HARD_CAP);
  };

  useEffect(() => {
    axios
      .get("/admin/grids")
      .then((r) => setGridsRaw(Array.isArray(r.data) ? r.data : []))
      .catch(() => setGridsRaw([]));
  }, []);

  useEffect(() => {
    (async () => {
      try {
        const params = {
          year: Array.isArray(year) && year.length ? year.join(",") : undefined,
          month: Array.isArray(month) && month.length ? month.join(",") : undefined,
          pickingPrime:
            Array.isArray(pickingPrime) && pickingPrime.length ? pickingPrime[0] : undefined,
        };
        const items = await fetchScoresAllPages(params);
        setScoresRaw(items);
      } catch (_) {
        setScoresRaw([]);
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [year, month, pickingPrime]);

  const gridsById = useMemo(() => {
    const m = new Map();
    for (const g of Array.isArray(gridsRaw) ? gridsRaw : []) {
      const id = getId(g);
      if (id) m.set(String(id), g);
    }
    return m;
  }, [gridsRaw]);

  const cq = useMemo(() => {
    const arr = scoresRaw.filter((s) => String(s?.evaluatorRole || s?.role || "") === "cq");
    return arr.map((x) => normalizeRow(x, "cq", gridsById));
  }, [scoresRaw, gridsById]);

  const mg = useMemo(() => {
    const arr = scoresRaw.filter((s) => String(s?.evaluatorRole || s?.role || "") === "management");
    return arr.map((x) => normalizeRow(x, "management", gridsById));
  }, [scoresRaw, gridsById]);

  const allCells = useMemo(() => {
    const s = new Set();
    [...cq, ...mg].forEach((r) => {
      if (r.cell) s.add(r.cell);
    });
    return Array.from(s).sort();
  }, [cq, mg]);

  const allYears = useMemo(() => {
    const s = new Set();
    scoresRaw.forEach((r) => {
      const d = r?.createdAt ? new Date(r.createdAt) : null;
      if (d && !Number.isNaN(d.getTime())) s.add(String(d.getFullYear()));
    });
    const arr = Array.from(s);
    arr.sort((a, b) => Number(b) - Number(a));
    const cy = String(new Date().getFullYear());
    if (!arr.includes(cy)) arr.unshift(cy);
    return arr;
  }, [scoresRaw]);

  const allMonths = useMemo(() => Array.from({ length: 12 }, (_, i) => String(i + 1)), []);

  const allGrids = useMemo(() => {
    const s = new Map();
    for (const g of Array.isArray(gridsRaw) ? gridsRaw : []) {
      const id = getId(g);
      if (!id) continue;
      s.set(String(id), safeText(g.name || g.title || id));
    }
    const arr = Array.from(s.entries()).map(([id, name]) => ({ id, name }));
    arr.sort((a, b) => a.name.localeCompare(b.name));
    return arr;
  }, [gridsRaw]);

  const filterFn = (r) => {
    if (Array.isArray(cell) && cell.length > 0) {
      if (!cell.includes(String(r.cell || ""))) return false;
    }
    if (Array.isArray(grid) && grid.length > 0) {
      if (!grid.includes(String(r.gridId || ""))) return false;
    }
    if (q.trim()) {
      const t = `${r.date || ""} ${r.pilot || ""} ${r.cell || ""} ${r.evaluator || ""} ${r.eps || ""} ${
        r.pickingPrime ? "picking prime" : ""
      } ${r.status || ""} ${r.comment || ""}`.toLowerCase();
      if (!t.includes(q.trim().toLowerCase())) return false;
    }
    return true;
  };

  const filteredCq = useMemo(() => cq.filter(filterFn), [cq, cell, grid, q]);
  const filteredMg = useMemo(() => mg.filter(filterFn), [mg, cell, grid, q]);

  useEffect(() => setPageCq(1), [cell, grid, q, year, month, pickingPrime]);
  useEffect(() => setPageMg(1), [cell, grid, q, year, month, pickingPrime]);

  const pageSlice = (rows, page) => rows.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);
  const cqPageRows = useMemo(() => pageSlice(filteredCq, pageCq), [filteredCq, pageCq]);
  const mgPageRows = useMemo(() => pageSlice(filteredMg, pageMg), [filteredMg, pageMg]);

  const toExportRow = (r) => {
    const raw = r.__raw || {};
    const fmtDate = (d) => {
      if (!d) return "";
      const dt = new Date(d);
      return Number.isNaN(dt.getTime()) ? "" : dt.toLocaleDateString("fr-FR");
    };
    const fmtItems = (items) => {
      if (!Array.isArray(items) || items.length === 0) return "";
      return items.map((it) => `${it.label || ""}:${it.status || ""}`).join(" | ");
    };
    return {
      "Date évaluation": fmtDate(r.date || raw.createdAt),
      "Date appel": fmtDate(raw.callDate || raw.interactionDate),
      "Date d'écoute": fmtDate(raw.listeningDate),
      Agent: r.pilot,
      Email_agent: safeText(get(raw, ["pilotEmail", "pilot"], "")),
      Cellule: r.cell,
      EPS: r.eps,
      Évaluateur: r.evaluator,
      Rôle_évaluateur: safeText(get(raw, ["evaluatorRole"], "")),
      "Picking prime": r.pickingPrime ? "Oui" : "Non",
      "Score (%)": `${Number(r.score || 0)}%`,
      Statut: r.status,
      Commentaire: r.comment,
      "Durée appel": safeText(get(raw, ["callDuration"], "")),
      Contestée: raw.contested ? "Oui" : "Non",
      "Commentaire contestation": safeText(get(raw, ["contestComment"], "")),
      "Date contestation": fmtDate(raw.contestedAt),
      "Date réévaluation": fmtDate(raw.reevaluatedAt),
      "Détail items": fmtItems(raw.items),
    };
  };

  const onExportAll = () => {
    exportToXlsx("Dashboard_export.xlsx", {
      "Écoutes CQ": filteredCq.map(toExportRow),
      "Écoutes Management": filteredMg.map(toExportRow),
    });
  };

  const openEditNotation = (rawScore) => {
    if (!rawScore) return;
    setEditingScore(rawScore);
  };

  
  const refreshScores = async () => {
    const params = {
      year: Array.isArray(year) && year.length ? year.join(",") : undefined,
      month: Array.isArray(month) && month.length ? month.join(",") : undefined,
      pickingPrime:
        Array.isArray(pickingPrime) && pickingPrime.length ? pickingPrime[0] : undefined,
    };
    const items = await fetchScoresAllPages(params);
    setScoresRaw(items);
  };

  const hardDeleteEvaluation = async (rawScore) => {
    const id = rawScore?._id || rawScore?.id;
    if (!id) return;
    const ok = window.confirm(
      "Suppression définitive : cette évaluation sera supprimée de la base de données. Continuer ?"
    );
    if (!ok) return;

    try {
      await axios.delete(`/admin/evaluations/${id}`);
      await refreshScores();
    } catch (e) {
      alert(e?.response?.data?.message || "Erreur lors de la suppression.");
    }
  };

const closeEditNotation = () => setEditingScore(null);

  const saveEditNotation = async (items) => {
    if (!editingScore?._id) return;
    try {
      setIsSavingEdit(true);
      const res = await axios.patch(`/scores/${editingScore._id}`, { items });
      const updated = res?.data;
      setScoresRaw((prev) =>
        (Array.isArray(prev) ? prev : []).map((s) => (String(s?._id) === String(editingScore._id) ? { ...s, ...updated } : s))
      );
      closeEditNotation();
    } catch (e) {
      // eslint-disable-next-line no-alert
      alert(e?.response?.data?.message || "Erreur lors de l'enregistrement.");
    } finally {
      setIsSavingEdit(false);
    }
  };

  return (
    <>
      <Card title="Synthèse" right={null}>
        <StatsCards cqRows={filteredCq} mgRows={filteredMg} />
      </Card>

      <Card title="Filtres Dashboard" right={null}>
        <FiltersBar
          q={q}
          setQ={setQ}
          year={year}
          setYear={setYear}
          month={month}
          setMonth={setMonth}
          cell={cell}
          setCell={setCell}
          grid={grid}
          setGrid={setGrid}
          pickingPrime={pickingPrime}
          setPickingPrime={setPickingPrime}
          allYears={allYears}
          allMonths={allMonths}
          allCells={allCells}
          allGrids={allGrids}
          onExportAll={onExportAll}
          isTruncated={isTruncated}
        />
      </Card>

      <Card
        title="Tableau CQ des écoutes"
        right={
          <button className="btn-outline" onClick={() => exportToXlsx("Ecoutes_CQ.xlsx", filteredCq.map(toExportRow))}>
            Export Excel
          </button>
        }
      >
        <ScoresTable rows={cqPageRows} onEdit={openEditNotation} onDelete={hardDeleteEvaluation} />
        <Pager
          page={pageCq}
          pageSize={PAGE_SIZE}
          total={filteredCq.length}
          onPrev={() => setPageCq((p) => Math.max(1, p - 1))}
          onNext={() => setPageCq((p) => p + 1)}
        />
      </Card>

      <Card
        title="Tableau Management des écoutes"
        right={
          <button className="btn-outline" onClick={() => exportToXlsx("Ecoutes_Management.xlsx", filteredMg.map(toExportRow))}>
            Export Excel
          </button>
        }
      >
        <ScoresTable rows={mgPageRows} onEdit={openEditNotation} onDelete={hardDeleteEvaluation} />
        <Pager
          page={pageMg}
          pageSize={PAGE_SIZE}
          total={filteredMg.length}
          onPrev={() => setPageMg((p) => Math.max(1, p - 1))}
          onNext={() => setPageMg((p) => p + 1)}
        />
      </Card>

      <EditScoreModal key={editingScore?._id||"none"} score={editingScore} onClose={closeEditNotation} onSave={saveEditNotation} isSaving={isSavingEdit} />
    </>
  );
}