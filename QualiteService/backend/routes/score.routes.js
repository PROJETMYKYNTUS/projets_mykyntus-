// backend/routes/score.routes.js
const express = require("express");
const mongoose = require("mongoose");

const fs = require("fs");
const path = require("path");
const axios = require("axios");

const Score = require("../models/Score");
const User = require("../models/User");
const Grid = require("../models/Grid");
const Notification = require("../models/Notification");
const { logAudit } = require("../utils/audit");

const {
  RECORDINGS_BASE_URL,
  AUDIO_BASE_PATH,
  isHttpMode,
  resolvePickingSource,
  httpAuth,
  getMysqlPool,
} = require("../config/recordings");

const { applyScorePilotScope } = require("../utils/scope");
const auth = require("../middleware/auth");
const permit = require("../middleware/roles");

const router = express.Router();

// Toutes les routes /scores/* nécessitent une authentification
router.use(auth);

/**
 * Utilitaire : calcul du score moyen d'une évaluation (moyenne /5)
 */
function computeAverageScore(scoreDoc) {
  if (
    !scoreDoc.items ||
    !Array.isArray(scoreDoc.items) ||
    scoreDoc.items.length === 0
  ) {
    return 0;
  }
  const sum = scoreDoc.items.reduce((acc, it) => {
    const v =
      typeof it.value === "number" ? it.value : Number(it.value) || 0;
    return acc + v;
  }, 0);
  return sum / scoreDoc.items.length;
}

/**
 * Utilitaire : calcul du total (/45 si 9 items)
 */
function computeTotalScore(scoreDoc) {
  if (
    !scoreDoc.items ||
    !Array.isArray(scoreDoc.items) ||
    scoreDoc.items.length === 0
  ) {
    return 0;
  }
  return scoreDoc.items.reduce((acc, it) => {
    const v =
      typeof it.value === "number" ? it.value : Number(it.value) || 0;
    return acc + v;
  }, 0);
}


/**
 * Nouveau : normalisation du status (Conforme / Non conforme / Non applicable)
 */
function normalizeStatus(raw) {
  const s = (raw || "").toString().trim().toLowerCase();
  if (["c", "conforme", "ok", "oui", "yes", "true", "1"].includes(s)) return "C";
  if (["nc", "non conforme", "non_conforme", "ko", "non", "no", "false", "0"].includes(s)) return "NC";
  if (["na", "n/a", "non applicable", "non_applicable"].includes(s)) return "NA";

// Presence grid (Grille 2)
if (["pc","présent conforme","present conforme","présent / conforme","present / conforme"].includes(s)) return "PC";
if (["pnc","présent non conforme","present non conforme","présent / non conforme","present / non conforme"].includes(s)) return "PNC";
if (["np","non présent","non present","absent"].includes(s)) return "NP";
  return "";
}

function isTrue(v) {
  return v === true || v === 1 || v === "1" || v === "true" || v === "on";
}


/**
 * Nouveau : calcul du taux de conformité (%) basé sur :
 * - status: C / NC / NA
 * - points définis par la grille (pointsConforme / pointsNonConforme)
 * - NA exclu du dénominateur
 *
 * Fallback si pas de grille:
 * - C = 1, NC = 0, NA exclu
 */
function computeCompliancePercent(scoreDoc, gridDoc) {
  const items = Array.isArray(scoreDoc?.items) ? scoreDoc.items : [];
  const gridItems = Array.isArray(gridDoc?.items) ? gridDoc.items : [];


// Grille 2 (présence) : scoring fixe
// Présent/Conforme = 1
// Présent/Non conforme = 0.5
// Non Présent = 0
// NA exclu du calcul
const gridType = (gridDoc?.gridType || "classic").toString();
if (gridType === "presence") {
  // Map phase rules to each item label (based on nearest previous group in grid order)
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

  let obtained = 0;
  let maxApplicable = 0;
  let totalMalus = 0;

  for (const it of items) {
    const label = (it?.label || "").toString().trim();
    if (!label) continue;

    let status = normalizeStatus(it.status);

    if (!status) {
      // Legacy fallback: derive status from numeric value (old data)
      const v = typeof it.value === "number" ? it.value : Number(it.value) || 0;
      // scale 0..5 or 0..1: treat >=4 as compliant, >=2.5 as partially compliant
      if (v <= 1) status = v >= 0.8 ? "PC" : v >= 0.4 ? "PNC" : "NP";
      else status = v >= 4 ? "PC" : v >= 2.5 ? "PNC" : "NP";
    }
if (status === "NA") continue;

    const group = groupByLabel.get(label) || null;

    const isNonCompliant = status === "PNC" || status === "NP" || status === "NC";

    if (isNonCompliant && group && group.hardFail) {
      return 0;
    }

    if (isNonCompliant) {
      const phaseMalus = group && group.malusPercent > 0 ? group.malusPercent : 0;
      if (phaseMalus > 0) totalMalus += phaseMalus;
    }

    maxApplicable += 1;

    if (status === "PC" || status === "C") obtained += 1;
    else if (status === "PNC") obtained += 0.5;
    else if (status === "NP" || status === "NC") obtained += 0;
    else {
      // legacy numérique
      const v = typeof it.value === "number" ? it.value : Number(it.value) || 0;
      obtained += v >= 4 ? 1 : 0;
    }
  }

  if (maxApplicable <= 0) return 0;
  let base = (obtained / maxApplicable) * 100;
  base = Math.max(0, Math.min(100, base));
  const finalPct = Math.max(0, base - totalMalus);
  return Math.round(finalPct * 10) / 10;
}


  const pointsByLabel = new Map();
  for (const gi of gridItems) {
    if (!gi) continue;
    if (gi.type === "group") continue;
    const label = (gi.label || "").toString().trim();
    if (!label) continue;
    const pC = typeof gi.pointsConforme === "number" ? gi.pointsConforme : 1;
    const pNC = typeof gi.pointsNonConforme === "number" ? gi.pointsNonConforme : 0;
    pointsByLabel.set(label, { pC, pNC });
  }

  // malus par item (si défini). Fallback possible sur le malus de phase.
  const itemMalusByLabel = new Map();
  for (const gi of gridItems) {
    if (!gi || gi.type === "group") continue;
    const label = (gi.label || "").toString().trim();
    if (!label) continue;
    const mp = Number.isFinite(Number(gi.malusPercent)) ? Number(gi.malusPercent) : 0;
    itemMalusByLabel.set(label, mp);
  }


  
  // Map phase rules to each item label (based on nearest previous group in grid order)
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
let obtained = 0;
  let maxApplicable = 0;
  let totalMalus = 0;

  for (const it of items) {
    const label = (it?.label || "").toString().trim();
    if (!label) continue;

    let status = normalizeStatus(it.status);

    // NA => on ignore complètement
    if (!status) {
      // Legacy fallback: derive status from numeric value (old data)
      const v = typeof it.value === "number" ? it.value : Number(it.value) || 0;
      if (v <= 1) status = v >= 0.8 ? "C" : "NC";
      else status = v >= 4 ? "C" : "NC";
    }
if (status === "NA") continue;

    const group = groupByLabel.get(label) || null;

    // Phase rule #1: hard fail (any NC => 0%)
    if (status === "NC" && group && group.hardFail) {
      return 0;
    }

    // Phase rule #2: malus (priorité item, fallback phase)
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
    else {
      // legacy numérique 1..5 → on approxime C si >=4 sinon NC
      const v = typeof it.value === "number" ? it.value : Number(it.value) || 0;
      obtained += v >= 4 ? pC : pNC;
    }
  }

  if (maxApplicable <= 0) return 0;
  let base = (obtained / maxApplicable) * 100;
  // clamp base to avoid >100% when pointsNonConforme > pointsConforme
  base = Math.max(0, Math.min(100, base));
  const finalPct = Math.max(0, base - totalMalus);
  return Math.round(finalPct * 10) / 10; // 1 décimale
}


/* =========================================================================
 *  EPS: DÉTECTION DE DOUBLON (CQ / MANAGEMENT)
 * ========================================================================= */

/**
 * GET /scores/check-eps?eps=XXX&excludeId=...
 * Retour: { duplicate: boolean, existing?: { id, createdAt, listeningDate, pilotName, evaluatorName } }
 */
router.get(
  "/check-eps",
  permit("cq", "management", "admin", "formateur"),
  async (req, res) => {
    try {
      const eps = (req.query.eps || "").toString().trim();
      const excludeId = (req.query.excludeId || "").toString().trim();

      if (!eps) return res.json({ duplicate: false });

      const q = { eps };
      if (excludeId && mongoose.Types.ObjectId.isValid(excludeId)) {
        q._id = { $ne: excludeId };
      }

      const existing = await Score.findOne(q)
        .populate("pilot", "name email")
        .populate("evaluator", "name email role")
        .sort({ createdAt: -1 })
        .lean();

      if (!existing) return res.json({ duplicate: false });

      return res.json({
        duplicate: true,
        existing: {
          id: existing._id,
          createdAt: existing.createdAt,
          listeningDate: existing.listeningDate || existing.interactionDate || existing.callDate || null,
          pilotName: existing.pilot?.name || "",
          evaluatorName: existing.evaluator?.name || "",
        },
      });
    } catch (err) {
      console.error("GET /scores/check-eps error:", err);
      return res.status(500).json({ message: "Erreur serveur lors de la vérification EPS." });
    }
  }
);

/* =========================================================================
 *  CRÉATION D'UNE ÉVALUATION (CQ / MANAGEMENT)
 * ========================================================================= */

/**
 * POST /scores
 * Body :
 * {
 *   pilotId,        // <== envoyé par le front (CQ / Management)
 *   (ou pilot),
 *   listeningDate,
 *   callDate,
 *   interactionDate,
 *   eps,
 *   callDuration,
 *   comment,
 *   gridId,
 *   items: [{ label, value }]
 * }
 */
router.post(
  "/",
  permit("cq", "management", "formateur"),
  async (req, res) => {
    try {
      const evaluatorId = req.user.id || req.user._id;

      const {
        pilot, // compatibilité éventuelle ancienne version
        pilotId, // ce que le front envoie actuellement
        listeningDate,
        callDate,
        interactionDate,
        eps,
        pickingPrime,
        callDuration,
        comment,
        gridId,
        items,
      } = req.body;

      const finalPilotId = pilotId || pilot;

      if (!finalPilotId || !mongoose.Types.ObjectId.isValid(finalPilotId)) {
        return res.status(400).json({ message: "Pilote invalide." });
      }

            // Nettoyage items : on ignore les lignes vides et on accepte status=C/NC/NA
      const cleanedItems = Array.isArray(items)
        ? items
            .map((it) => ({
              label: (it?.label || "").toString().trim(),
              status: normalizeStatus(it?.status),
              value: it?.value,
            }))
            .filter((it) => it.label.length > 0)
        : [];

      if (cleanedItems.length === 0) {
        return res
          .status(400)
          .json({ message: "Aucun item d'évaluation fourni." });
      }


// 🔔 EPS doublon (même comportement CQ)
const epsClean = (eps || "").toString().trim();
if (epsClean) {
  const existing = await Score.findOne({ eps: epsClean }).lean();
  if (existing) {
    return res.status(409).json({
      message: "EPS doublon : une évaluation existe déjà avec cet EPS.",
      code: "EPS_DUPLICATE",
      duplicate: true,
      existingId: existing._id,
    });
  }
}

      // Charger la grille (pour les points par item)
      const grid =
        gridId && mongoose.Types.ObjectId.isValid(gridId)
          ? await Grid.findById(gridId).lean()
          : null;

      
const gridTypeForSave = (grid?.gridType || "classic").toString();

      // Map points par label
      const gridPointsByLabel = new Map();
      if (grid && Array.isArray(grid.items)) {
        for (const gi of grid.items) {
          if (!gi) continue;
          if (gi.type === "group") continue;
          const lbl = (gi.label || "").toString().trim();
          if (!lbl) continue;
          const pC = typeof gi.pointsConforme === "number" ? gi.pointsConforme : 1;
          const pNC = typeof gi.pointsNonConforme === "number" ? gi.pointsNonConforme : 0;
          gridPointsByLabel.set(lbl, { pC, pNC });
        }
      }

const score = await Score.create({
        pilot: finalPilotId,
        evaluator: evaluatorId,
        gridId:
          gridId && mongoose.Types.ObjectId.isValid(gridId) ? gridId : null,

        listeningDate: listeningDate ? new Date(listeningDate) : null,
        callDate: callDate ? new Date(callDate) : null,
        interactionDate: interactionDate ? new Date(interactionDate) : null,

        eps: epsClean || "",
        pickingPrime: isTrue(pickingPrime),
        callDuration: callDuration || "",
        comment: comment || "",

        items: cleanedItems.map((it) => {
          const pts = gridPointsByLabel.get(it.label);
          const pC = pts ? pts.pC : 1;
          const pNC = pts ? pts.pNC : 0;


// Award points according to status.
// - classic: use grid points (C/NC/NA)
// - presence: PC=1, PNC=0.5, NP=0, NA excluded
let awarded = 0;
if (gridTypeForSave === "presence") {
  if (it.status === "PC" || it.status === "C") awarded = 1;
  else if (it.status === "PNC") awarded = 0.5;
  else if (it.status === "NP" || it.status === "NC") awarded = 0;
  else if (it.status === "NA") awarded = 0;
  else {
    const v = typeof it.value === "number" ? it.value : Number(it.value) || 0;
    awarded = v >= 4 ? 1 : 0;
  }
} else {
  if (it.status === "C") awarded = pC;
  else if (it.status === "NC") awarded = pNC;
  else if (it.status === "NA") awarded = 0;
  else {
    // legacy numérique
    const v = typeof it.value === "number" ? it.value : Number(it.value) || 0;
    awarded = v >= 4 ? pC : pNC;
  }
}

          return {
            label: it.label,
            status: it.status,
            value: Number(awarded) || 0, // on garde 'value' pour compat (points obtenus)
          };
        }),
      });

      const populated = await Score.findById(score._id)
        .populate("pilot")
        .populate("evaluator")
        .lean();

      if (populated) {
        populated.compliancePercent = computeCompliancePercent(populated, grid);
      }

      // 🔔 Realtime push for CQ/Management dashboards & lists
      try {
        const io = req.app.get("io");
        if (io) {
          io.emit("scores:changed", { action: "created", id: String(score._id) });
          const cell = populated?.pilot?.cell ? String(populated.pilot.cell).trim() : "";
          if (cell) io.to(`cell:${cell}`).emit("scores:changed", { action: "created", id: String(score._id) });
          const evId = populated?.evaluator?._id ? String(populated.evaluator._id) : evaluatorId;
          if (evId) io.to(`user:${evId}`).emit("scores:changed", { action: "created", id: String(score._id) });
        }

        // Notify pilot of new evaluation
        try {
          const Notification = require("../models/Notification");
          const pilotUserId = populated?.pilot?._id || pilotId;
          const evaluatorName = req.user.name || "Évaluateur";
          const notif = await Notification.create({
            type: "notification",
            title: "Nouvelle évaluation",
            message: `📋 ${evaluatorName} a réalisé une évaluation sur l'appel ${populated?.eps || ""}. Score : ${Math.round(populated?.compliancePercent || populated?.total || 0)}%.`,
            targetUsers: [pilotUserId],
            createdBy: req.user.id || req.user._id,
            meta: { scoreId: String(score._id) },
          });
          if (io) io.emit("notification:new", notif);
        } catch (_) {}
      } catch (_) {
        // ignore realtime failures
      }

      res.status(201).json(populated);
    } catch (err) {
      console.error("POST /scores error:", err);
      res.status(500).json({
        message: "Erreur serveur lors de la création de l'évaluation.",
      });
    }
  }
);

// -------------------- Helpers for large lists --------------------
function parseCsvList(v) {
  if (v === undefined || v === null) return [];
  return String(v)
    .split(",")
    .map((x) => x.trim())
    .filter(Boolean);
}

function parseDateRange(from, to) {
  const out = {};
  const f = from ? new Date(String(from)) : null;
  const t = to ? new Date(String(to)) : null;
  if (f && !Number.isNaN(f.getTime())) out.$gte = f;
  if (t && !Number.isNaN(t.getTime())) {
    t.setHours(23, 59, 59, 999);
    out.$lte = t;
  }
  return Object.keys(out).length ? out : null;
}

function startOfMonth(year, month1to12) {
  const y = Number(year);
  const m = Number(month1to12);
  if (!Number.isFinite(y) || !Number.isFinite(m) || m < 1 || m > 12) return null;
  return new Date(Date.UTC(y, m - 1, 1, 0, 0, 0, 0));
}

function endOfMonth(year, month1to12) {
  const y = Number(year);
  const m = Number(month1to12);
  if (!Number.isFinite(y) || !Number.isFinite(m) || m < 1 || m > 12) return null;
  // last ms of month
  return new Date(Date.UTC(y, m, 0, 23, 59, 59, 999));
}

/* =========================================================================
 *  META: LISTE DES ÉVALUATEURS (pour alimenter les filtres multi-select)
 *  NOTE: important car /scores est filtré côté serveur (perf), donc la liste
 *  des évaluateurs ne doit pas dépendre des résultats déjà filtrés.
 * ========================================================================= */

router.get(
  "/evaluators",
  permit("admin", "management", "manager", "cq", "formateur"),
  async (req, res) => {
    try {
      // CQ: la liste d'évaluateurs est triviale (lui-même)
      if (req.user.role === "cq") {
        return res.json([
          {
            id: String(req.user.id || req.user._id),
            name: req.user.name || "",
            email: req.user.email || "",
          },
        ]);
      }

      const q = {};

      // Optional filters to scope the evaluator list (year/month + pilot)
      const pilotIds = parseCsvList(req.query.pilotId);
      const validPilotIds = pilotIds.filter((x) => mongoose.Types.ObjectId.isValid(x));
      if (validPilotIds.length) q.pilot = { $in: validPilotIds };

      const years = parseCsvList(req.query.year);
      const months = parseCsvList(req.query.month);
      if (years.length && months.length) {
        const ranges = [];
        for (const y of years) {
          for (const m of months) {
            const s = startOfMonth(y, m);
            const e = endOfMonth(y, m);
            if (s && e) ranges.push({ createdAt: { $gte: s, $lte: e } });
          }
        }
        if (ranges.length) q.$or = ranges;
      }

      const rows = await Score.aggregate([
        { $match: q },
        { $group: { _id: "$evaluator", count: { $sum: 1 } } },
        {
          $lookup: {
            from: "users",
            localField: "_id",
            foreignField: "_id",
            as: "u",
          },
        },
        { $unwind: { path: "$u", preserveNullAndEmptyArrays: true } },
        {
          $project: {
            _id: 0,
            id: { $toString: "$_id" },
            name: "$u.name",
            email: "$u.email",
            count: 1,
          },
        },
        { $sort: { name: 1 } },
      ]);

      res.json((rows || []).filter((r) => r.id && r.name));
    } catch (err) {
      console.error("GET /scores/evaluators error:", err);
      res.status(500).json({ message: "Erreur serveur." });
    }
  }
);

/* =========================================================================
 *  LECTURE : MES ÉVALUATIONS (CQ / MANAGEMENT)
 * ========================================================================= */

/**
 * GET /scores/mine
 * Renvoie toutes les évaluations réalisées par l'utilisateur connecté
 * (CQ ou Management).
 */


/**
 * GET /scores/stats
 * KPI agrégés (léger) pour dashboards, avec filtres.
 * Query:
 * - year=YYYY (optionnel) ; month=MM (optionnel)
 * - pilotId=... (csv)
 * - evaluatorId=... (csv)
 * - dateFrom=YYYY-MM-DD ; dateTo=YYYY-MM-DD (optionnel)
 *
 * Retour:
 * { total, avgScore, avgPercent, contestedCount, contestedRate }
 */
router.get(
  "/stats",
  permit("admin", "management", "cq", "formateur"),
  async (req, res) => {
    try {
      const match = applyScorePilotScope({}, req);
      // Scope role CQ -> evaluator = current user
      if (req.user?.role === "cq") {
        const uid = req.user.id || req.user._id;
        if (!uid || !mongoose.Types.ObjectId.isValid(String(uid))) {
          return res.status(401).json({ message: "Utilisateur non authentifié." });
        }
        match.evaluator = new mongoose.Types.ObjectId(String(uid));
      }

      const { year, month, pilotId, evaluatorId, dateFrom, dateTo } = req.query || {};

      // year/month => createdAt range
      if (year) {
        const y = parseInt(year, 10);
        if (!Number.isNaN(y)) {
          const m = month ? parseInt(month, 10) : null;
          const start = new Date(y, m ? m - 1 : 0, 1);
          const end = m ? new Date(y, m, 1) : new Date(y + 1, 0, 1);
          match.createdAt = { $gte: start, $lt: end };
        }
      }

      // explicit date range overrides/extends createdAt match
      if (dateFrom || dateTo) {
        const from = dateFrom ? new Date(dateFrom) : null;
        const to = dateTo ? new Date(dateTo) : null;
        const range = {};
        if (from && !isNaN(from.getTime())) range.$gte = from;
        if (to && !isNaN(to.getTime())) {
          // include end day
          const t2 = new Date(to);
          t2.setDate(t2.getDate() + 1);
          range.$lt = t2;
        }
        if (Object.keys(range).length) match.createdAt = range;
      }

      if (pilotId) {
        const ids = String(pilotId)
          .split(",")
          .map((s) => s.trim())
          .filter(Boolean)
          .filter((id) => mongoose.Types.ObjectId.isValid(id))
          .map((id) => new mongoose.Types.ObjectId(id));
        if (ids.length) match.pilot = { $in: ids };
      }

      // For admin/management only; CQ already scoped
      if (evaluatorId && req.user?.role !== "cq") {
        const ids = String(evaluatorId)
          .split(",")
          .map((s) => s.trim())
          .filter(Boolean)
          .filter((id) => mongoose.Types.ObjectId.isValid(id))
          .map((id) => new mongoose.Types.ObjectId(id));
        if (ids.length) match.evaluator = { $in: ids };
      }

      // Compute percent with the same rules as evaluation scoring
      // (classic grids + presence + hardFail + malus).
      const scores = await Score.find(match).lean()
        .select("items gridId contested")
        .lean();

      const gridIds = Array.from(
        new Set(
          (scores || [])
            .map((s) => (s.gridId ? String(s.gridId) : ""))
            .filter(Boolean)
        )
      );

      const grids = gridIds.length
        ? await Grid.find({ _id: { $in: gridIds } }).lean()
        : [];
      const gridById = new Map((grids || []).map((g) => [String(g._id), g]));

      let total = 0;
      let sumPercent = 0;
      let contestedCount = 0;

      for (const s of scores || []) {
        total += 1;
        if (s.contested) contestedCount += 1;
        const g = s.gridId ? gridById.get(String(s.gridId)) : null;
        const pct = computeCompliancePercent(s, g);
        sumPercent += Number(pct) || 0;
      }

      const avgPercent = total > 0 ? sumPercent / total : 0;
      const contestedRate = total > 0 ? (contestedCount / total) * 100 : 0;

      // avgScore kept for legacy screens (0..5 scale)
      const avgScore = avgPercent / 20;

      res.json({
        total,
        avgScore,
        avgPercent: Math.round(avgPercent * 10) / 10,
        contestedCount,
        contestedRate: Math.round(contestedRate * 10) / 10,
      });
    } catch (err) {
      console.error("GET /scores/stats error:", err);
      res.status(500).json({ message: "Erreur serveur lors du calcul des statistiques." });
    }
  }
);

router.get(
  "/mine",
  permit("cq", "management", "formateur"),
  async (req, res) => {
    try {
      const evaluatorId = req.user.id || req.user._id;

      // Pagination + filtering for performance (thousands of evaluations)
      const page = Math.max(1, parseInt(req.query.page || "1", 10));
      const limit = Math.min(500, Math.max(10, parseInt(req.query.limit || "50", 10)));
      const skip = (page - 1) * limit;

      const q = applyScorePilotScope({ evaluator: evaluatorId }, req);

      // Le profil CQ voit les évaluations réalisées par tous les CQ
      // ainsi que celles réalisées par le Management.
      if (req.user?.role === "cq") {
        const visibleEvaluatorIds = await User.distinct("_id", {
          role: { $in: ["cq", "management"] },
        });

        q.evaluator = { $in: visibleEvaluatorIds };
      }

      // Filter by pilot (supports multi: pilotId=a,b,c)
      const pilotIds = parseCsvList(req.query.pilotId);
      const validPilotIds = pilotIds.filter((x) => mongoose.Types.ObjectId.isValid(x));
      if (validPilotIds.length) q.pilot = { $in: validPilotIds };

      // Filter by createdAt month/year (supports multi)
      const years = parseCsvList(req.query.year);
      const months = parseCsvList(req.query.month);
      if (years.length && months.length) {
        // If multiple, build OR ranges
        const ranges = [];
        for (const y of years) {
          for (const m of months) {
            const s = startOfMonth(y, m);
            const e = endOfMonth(y, m);
            if (s && e) ranges.push({ createdAt: { $gte: s, $lte: e } });
          }
        }
        if (ranges.length) q.$or = ranges;
      }

      // Direct date range override (YYYY-MM-DD)
      if (req.query.startDate || req.query.endDate) {
        const sd = req.query.startDate ? new Date(String(req.query.startDate)) : null;
        const ed = req.query.endDate ? new Date(String(req.query.endDate)) : null;
        if (sd && !isNaN(sd.getTime())) q.createdAt = { ...(q.createdAt || {}), $gte: sd };
        if (ed && !isNaN(ed.getTime())) q.createdAt = { ...(q.createdAt || {}), $lte: ed };
      }

      // Contested filter (yes/no)
      const contested = parseCsvList(req.query.contested);
      if (contested.length) {
        const wantYes = contested.includes("yes") || contested.includes("true") || contested.includes("1");
        const wantNo = contested.includes("no") || contested.includes("false") || contested.includes("0");
        if (wantYes && !wantNo) q.contested = true;
        else if (!wantYes && wantNo) q.contested = false;
      }

      const [scores, total] = await Promise.all([
        Score.find(q)
          .populate("pilot")
          .populate("evaluator")
          .sort({ createdAt: -1 })
          .skip(skip)
          .limit(limit)
          .lean(),
        Score.countDocuments(q),
      ]);


      // Load grids once to apply phase rules (hardFail/malus) correctly
      const gridIds = [
        ...new Set(
          (scores || [])
            .map((s) => (s && s.gridId ? String(s.gridId) : ""))
            .filter(Boolean)
        ),
      ];
      const grids = gridIds.length
        ? await Grid.find({ _id: { $in: gridIds } }).lean()
        : [];
      const gridsById = new Map((grids || []).map((g) => [String(g._id), g]));


      const mapped = scores.map((s) => ({
        ...s,
        pilotName: s.pilot?.name || "",
        pilotEmail: s.pilot?.email || "",
        pilotCell: s.pilot?.cell || "",
        pilotId: s.pilot?._id || s.pilot,
        evaluatorName: s.evaluator?.name || "",
        evaluatorRole: s.evaluator?.role || "",
        avgScore: computeAverageScore(s),
        compliancePercent: computeCompliancePercent(s, gridsById.get(String(s.gridId)) || null),
      }));

      res.json({ page, limit, total, items: mapped });
    } catch (err) {
      console.error("GET /scores/mine error:", err);
      res.status(500).json({
        message: "Erreur serveur lors du chargement de vos évaluations.",
      });
    }
  }
);

/* =========================================================================
 *  LECTURE : ÉVALUATIONS DU PILOTE CONNECTÉ (PILOTE)
 * ========================================================================= */

/**
 * GET /scores/me
 * Vue utilisée par le PilotDashboard :
 * - rôle : pilote
 * - renvoie { average, count, scores[] }
 *   où "total" = somme des items (/45 si 9 items)
 */
router.get(
  "/me",
  permit("pilote"),
  async (req, res) => {
    try {
      const pilotId = req.user.id || req.user._id;

      const scores = await Score.find({ pilot: pilotId })
        .populate("evaluator")
        .sort({ createdAt: -1 })
        .lean();

      // Load grids for compliance calculation
      const gridIds = [...new Set(scores.map((s) => s.gridId).filter(Boolean).map(String))];
      const grids = gridIds.length ? await Grid.find({ _id: { $in: gridIds } }).lean() : [];
      const gridById = new Map(grids.map((g) => [String(g._id), g]));

      const mapped = scores.map((s) => {
        const grid = s.gridId ? gridById.get(String(s.gridId)) : null;
        const total = computeCompliancePercent(s, grid);

        let cq = null;
        if (s.evaluator && s.evaluator.role === "cq") {
          cq = {
            name: s.evaluator.name || "",
            email: s.evaluator.email || "",
          };
        }

        return {
          _id: s._id,
          items: s.items || [],
          total,
          eps: s.eps || "",
          callDuration: s.callDuration || "",
          comment: s.comment || "",
          contested: !!s.contested,
          contestComment: s.contestComment || "",
          cq,

          // Informations génériques de l'évaluateur :
          // fonctionne pour CQ et Management.
          evaluator: s.evaluator
            ? {
                _id: s.evaluator._id || null,
                name: s.evaluator.name || "",
                email: s.evaluator.email || "",
                role: s.evaluator.role || "",
              }
            : null,

          evaluatorName: s.evaluator?.name || "",
          evaluatorRole: s.evaluator?.role || "",

          // Picking prime visible dans la vision pilote.
          pickingPrime: !!s.pickingPrime,

          listeningDate: s.listeningDate || null,
          interactionDate: s.interactionDate || null,
          callDate: s.callDate || null,
          createdAt: s.createdAt || null,
        };
      });

      const count = mapped.length;
      const average =
        count > 0
          ? mapped.reduce(
              (acc, s) => acc + Number(s.total || 0),
              0
            ) / count
          : 0;

      res.json({
        average,
        count,
        scores: mapped,
      });
    } catch (err) {
      console.error("GET /scores/me error:", err);
      res.status(500).json({
        message: "Erreur serveur lors du chargement de vos évaluations.",
      });
    }
  }
);

/* =========================================================================
 *  LECTURE : TOUTES LES ÉVALUATIONS (ADMIN / MANAGEMENT)
 * ========================================================================= */

/**
 * GET /scores
 * Vue globale (ADMIN + MANAGEMENT) si tu veux un écran "toutes les écoutes".
 */
router.get(
  "/",
  permit("admin", "management", "formateur"),
  async (req, res) => {
    try {
      const page = Math.max(1, parseInt(req.query.page || "1", 10));
      const limit = Math.min(500, Math.max(10, parseInt(req.query.limit || "50", 10)));
      const skip = (page - 1) * limit;

      const q = applyScorePilotScope({}, req);

      // multi filters
      const pilotIds = parseCsvList(req.query.pilotId);
      const validPilotIds = pilotIds.filter((x) => mongoose.Types.ObjectId.isValid(x));
      if (validPilotIds.length) q.pilot = { $in: validPilotIds };

      const evaluatorIds = parseCsvList(req.query.evaluatorId);
      const validEvaluatorIds = evaluatorIds.filter((x) => mongoose.Types.ObjectId.isValid(x));
      if (validEvaluatorIds.length) q.evaluator = { $in: validEvaluatorIds };

      const years = parseCsvList(req.query.year);
      const months = parseCsvList(req.query.month);
      if (years.length && months.length) {
        const ranges = [];
        for (const y of years) {
          for (const m of months) {
            const s = startOfMonth(y, m);
            const e = endOfMonth(y, m);
            if (s && e) ranges.push({ createdAt: { $gte: s, $lte: e } });
          }
        }
        if (ranges.length) q.$or = ranges;
      }

      if (req.query.startDate || req.query.endDate) {
        const sd = req.query.startDate ? new Date(String(req.query.startDate)) : null;
        const ed = req.query.endDate ? new Date(String(req.query.endDate)) : null;
        if (sd && !isNaN(sd.getTime())) q.createdAt = { ...(q.createdAt || {}), $gte: sd };
        if (ed && !isNaN(ed.getTime())) q.createdAt = { ...(q.createdAt || {}), $lte: ed };
      }

      const eps = (req.query.eps || "").toString().trim();
      if (eps) q.eps = eps;

      if (req.query.pickingPrime !== undefined) {
        const v = String(req.query.pickingPrime).toLowerCase();
        if (["true", "1", "yes", "on"].includes(v)) q.pickingPrime = true;
        if (["false", "0", "no", "off"].includes(v)) q.pickingPrime = false;
      }

      // Contested filter
      if (req.query.contested !== undefined) {
        const v = String(req.query.contested).toLowerCase();
        if (["true", "1", "yes", "on"].includes(v)) q.contested = true;
        if (["false", "0", "no", "off"].includes(v)) q.contested = false;
      }

      const [scores, total] = await Promise.all([
        Score.find(q)
          .populate("pilot")
          .populate("evaluator")
          .sort({ createdAt: -1 })
          .skip(skip)
          .limit(limit)
          .lean(),
        Score.countDocuments(q),
      ]);


      // Load grids once to apply phase rules (hardFail/malus) correctly
      const gridIds = [
        ...new Set(
          (scores || [])
            .map((s) => (s && s.gridId ? String(s.gridId) : ""))
            .filter(Boolean)
        ),
      ];
      const grids = gridIds.length
        ? await Grid.find({ _id: { $in: gridIds } }).lean()
        : [];
      const gridsById = new Map((grids || []).map((g) => [String(g._id), g]));


      const mapped = scores.map((s) => ({
        ...s,
        pilotName: s.pilot?.name || "",
        pilotEmail: s.pilot?.email || "",
        pilotCell: s.pilot?.cell || "",
        evaluatorName: s.evaluator?.name || "",
        evaluatorRole: s.evaluator?.role || "",
        avgScore: computeAverageScore(s),
        compliancePercent: computeCompliancePercent(s, gridsById.get(String(s.gridId)) || null),
      }));

      res.json({ page, limit, total, items: mapped });
    } catch (err) {
      console.error("GET /scores error:", err);
      res.status(500).json({
        message: "Erreur serveur lors du chargement des évaluations.",
      });
    }
  }
);

/* =========================================================================
 *  LECTURE : DÉTAIL D’UNE ÉVALUATION
 * ========================================================================= */

/**
 * GET /scores/:id
 */
/* =========================================================================
 *  LISTE DES ÉVALUATIONS CONTESTÉES EN ATTENTE DE RÉÉVALUATION (CQ)
 * ========================================================================= */

/**
 * GET /scores/contested
 * Retourne les évaluations avec contested=true.
 * Visible uniquement par le rôle CQ (et admin).
 */
router.get(
  "/contested",
  permit("cq", "admin", "formateur"),
  async (req, res) => {
    try {
      const page = Math.max(1, parseInt(req.query.page || "1", 10));
      const limit = Math.min(500, Math.max(10, parseInt(req.query.limit || "50", 10)));
      const skip = (page - 1) * limit;

      const q = applyScorePilotScope({ contested: true }, req);
      const pilotIds = parseCsvList(req.query.pilotId);
      const validPilotIds = pilotIds.filter((x) => mongoose.Types.ObjectId.isValid(x));
      if (validPilotIds.length) q.pilot = { $in: validPilotIds };

      const [scores, total] = await Promise.all([
        Score.find(q)
          .populate("pilot")
          .populate("evaluator")
          .sort({ createdAt: -1 })
          .skip(skip)
          .limit(limit)
          .lean(),
        Score.countDocuments(q),
      ]);

      // Load grids once to apply phase rules (hardFail/malus) correctly
      const gridIds = [
        ...new Set(
          (scores || [])
            .map((s) => (s && s.gridId ? String(s.gridId) : ""))
            .filter(Boolean)
        ),
      ];
      const grids = gridIds.length
        ? await Grid.find({ _id: { $in: gridIds } }).lean()
        : [];
      const gridsById = new Map((grids || []).map((g) => [String(g._id), g]));

      const mapped = scores.map((s) => ({
        ...s,
        pilotName: s.pilot?.name || "",
        pilotEmail: s.pilot?.email || "",
        pilotCell: s.pilot?.cell || "",
        evaluatorName: s.evaluator?.name || "",
        evaluatorRole: s.evaluator?.role || "",
        avgScore: computeAverageScore(s),
        compliancePercent: computeCompliancePercent(
          s,
          gridsById.get(String(s.gridId)) || null
        ),
      }));

      return res.json({ page, limit, total, items: mapped });
    } catch (err) {
      console.error(err);
      return res.status(500).json({
        message:
          "Erreur serveur lors du chargement des évaluations contestées.",
      });
    }
  }
);

/* =========================================================================
 *  PICKING CALLS: liste des appels à évaluer
 *
 *  2 modes (voir backend/config/recordings.js) :
 *   - HTTP  : interroge la nouvelle app d'enregistrement (RECORDINGS_BASE_URL,
 *             par défaut http://enreg.kyntus.fr:8085) — mode par défaut.
 *   - LOCAL : lit /app/audio_mails (montage NAS historique) — fallback.
 *
 *  Endpoints HTTP supposés sur la nouvelle app (à ajuster selon son API réelle) :
 *    GET {BASE}/api/picking-calls?year=YYYY&month=MM&limit=N
 *      -> { items: [{ eps, phone, audioDay, audioFile, callDate, ... }, ...] }
 *    GET {BASE}/api/picking-audio/{day}/{file}  -> stream audio
 *
 *  Si l'application expose d'autres chemins (ex. /list.php?day=...), modifier
 *  les fonctions httpPickingCalls / httpPickingAudio ci-dessous.
 * ========================================================================= */
function parseAudioFilename(filename) {
  const ext = path.extname(filename).toLowerCase();
  const allowed = [".mp3", ".wav", ".m4a", ".ogg", ".flac"];
  if (!allowed.includes(ext)) return null;

  const base = path.basename(filename, ext);
  const parts = base.split("_");

  if (parts.length < 3) return null;

  const eps = parts[0] || "";
  const phone = parts[1] || "";
  const rawDate = parts[2] || ""; // ex: 2026-03-11-11-06-50

  let isoCallDate = null;
  const m = rawDate.match(/^(\d{4}-\d{2}-\d{2})-(\d{2})-(\d{2})-(\d{2})$/);
  if (m) {
    isoCallDate = `${m[1]}T${m[2]}:${m[3]}:${m[4]}`;
  }

  return {
    eps,
    cell: eps,
    phone,
    callDate: isoCallDate,
  };
}

// ---- LOCAL mode helpers ----
function listAudioCallsLocal(rootDir, limit = 5000, year = null, month = null) {
  if (!rootDir || !fs.existsSync(rootDir)) return [];

  const allDateDirs = fs
    .readdirSync(rootDir, { withFileTypes: true })
    .filter((d) => d.isDirectory() && /^\d{4}-\d{2}-\d{2}$/.test(d.name))
    .map((d) => d.name)
    .sort((a, b) => b.localeCompare(a));

  let dateDirs = allDateDirs;

  if (year && month) {
    const ym = `${String(year)}-${String(month).padStart(2, "0")}`;
    dateDirs = allDateDirs.filter((d) => d.startsWith(ym + "-"));
  }

  const out = [];
  const audioExts = [".mp3", ".wav", ".m4a", ".ogg", ".flac"];

  for (const day of dateDirs) {
    const dayPath = path.join(rootDir, day);
    const files = fs
      .readdirSync(dayPath, { withFileTypes: true })
      .filter((f) => f.isFile() && audioExts.includes(path.extname(f.name).toLowerCase()))
      .map((f) => f.name)
      .sort((a, b) => b.localeCompare(a));

    for (const file of files) {
      const parsed = parseAudioFilename(file);
      if (!parsed) continue;

      out.push({
        _id: `${day}/${file}`,
        eps: parsed.eps,
        pilotId: "",
        pilotName: "",
        callDate: parsed.callDate || `${day}T00:00:00`,
        callDuration: "",
        audioFile: file,
        audioDay: day,
        cell: parsed.cell,
        phone: parsed.phone,
        comment: "",
      });

      if (out.length >= limit) return out;
    }
  }

  return out;
}


// ---- MYSQL mode helper ----
async function mysqlPickingCalls({ year, month, limit }) {
  const safeLimit = Math.min(
    20000,
    Math.max(100, Number.parseInt(limit, 10) || 5000)
  );

  const nextYear = month === 12 ? year + 1 : year;
  const nextMonth = month === 12 ? 1 : month + 1;

  const startDate =
    `${String(year).padStart(4, "0")}-` +
    `${String(month).padStart(2, "0")}-01 00:00:00`;

  const endDate =
    `${String(nextYear).padStart(4, "0")}-` +
    `${String(nextMonth).padStart(2, "0")}-01 00:00:00`;

  const sql = `
    SELECT
      tag,
      numero2,
      direction,
      call_dt,
      filename,
      filepath,
      filesize
    FROM audio_index
    WHERE call_dt >= ?
      AND call_dt < ?
    ORDER BY call_dt DESC
    LIMIT ${safeLimit}
  `;

  const [rows] = await getMysqlPool().execute(
    sql,
    [startDate, endDate]
  );

  return rows
    .map((row) => {
      const normalizedPath = String(
        row.filepath || ""
      ).replace(/\\/g, "/");

      const pathParts = normalizedPath
        .split("/")
        .filter(Boolean);

      const rawCallDate = row.call_dt
        ? String(row.call_dt)
        : "";

      const callDate = rawCallDate
        ? rawCallDate.replace(" ", "T")
        : "";

      const dateFromPath =
        pathParts.find((part) =>
          /^\d{4}-\d{2}-\d{2}$/.test(part)
        ) || "";

      const audioDay =
        dateFromPath ||
        (callDate ? callDate.slice(0, 10) : "");

      const audioFile = path.basename(
        String(
          row.filename ||
          pathParts[pathParts.length - 1] ||
          ""
        )
      );

      const directionRaw = String(
        row.direction || ""
      ).toLowerCase();

      const filesize = Number(row.filesize || 0);

      let direction = "Inconnu";
      let directionClass = "unknown";

      // Convention de la base :
      // in  = appel affiché comme Sortant
      // out = appel affiché comme Entrant
      if (directionRaw === "in") {
        direction = "Sortant";
        directionClass = "outbound";
      } else if (directionRaw === "out") {
        direction = "Entrant";
        directionClass = "inbound";

        if (filesize > 0 && filesize < 15360) {
          direction = "Manqué";
          directionClass = "missed";
        }
      }

      const tag = String(row.tag || "");
      const phone = String(row.numero2 || "");

      return {
        _id:
          normalizedPath ||
          `${audioDay}/${audioFile}`,

        eps: tag,
        cell: tag,
        phone,

        pilotId: "",
        pilotName: "",

        callDate,
        callDuration: "",

        audioFile,
        audioDay,

        direction,
        directionClass,
        directionRaw,

        filesize,
        filepath: normalizedPath,

        comment: "",
        source: "mysql",
      };
    })
    .filter((call) => call.audioDay && call.audioFile);
}

// ---- HTTP mode helpers ----
async function httpPickingCalls({ year, month, limit }) {
  const url = `${RECORDINGS_BASE_URL}/api/picking-calls`;
  const r = await axios.get(url, {
    params: { year, month, limit },
    auth: httpAuth(),
    timeout: 30000,
  });
  // accepte { items: [...] } OU directement [...]
  const items = Array.isArray(r.data) ? r.data : Array.isArray(r.data?.items) ? r.data.items : [];
  return items.map((it) => {
    // garantit la présence des champs essentiels
    const audioFile = it.audioFile || it.file || it.filename || "";
    const audioDay = it.audioDay || it.day || it.date || "";
    const parsed = parseAudioFilename(audioFile) || {};
    return {
      _id: it._id || `${audioDay}/${audioFile}`,
      eps: it.eps ?? parsed.eps ?? "",
      pilotId: it.pilotId || "",
      pilotName: it.pilotName || "",
      callDate: it.callDate || parsed.callDate || (audioDay ? `${audioDay}T00:00:00` : ""),
      callDuration: it.callDuration || "",
      audioFile,
      audioDay,
      cell: it.cell ?? parsed.cell ?? "",
      phone: it.phone ?? parsed.phone ?? "",
      comment: it.comment || "",
    };
  });
}

async function httpPickingAudio(day, file, req, res) {
  const url = `${RECORDINGS_BASE_URL}/api/picking-audio/${encodeURIComponent(day)}/${encodeURIComponent(file)}`;
  const upstream = await axios.get(url, {
    auth: httpAuth(),
    responseType: "stream",
    timeout: 0,
    headers: req.headers.range ? { Range: req.headers.range } : {},
    validateStatus: () => true,
  });
  res.status(upstream.status);
  const pass = ["content-type", "content-length", "accept-ranges", "content-range"];
  for (const h of pass) {
    if (upstream.headers[h]) res.setHeader(h, upstream.headers[h]);
  }
  upstream.data.pipe(res);
}

router.get(
  "/picking-calls",
  permit("cq", "management", "formateur"),
  async (req, res) => {
    try {
      const now = new Date();
      const year = parseInt(req.query.year || now.getFullYear(), 10);
      const month = parseInt(req.query.month || (now.getMonth() + 1), 10);
      const limit = Math.min(20000, Math.max(100, parseInt(req.query.limit || "5000", 10)));

      if (!year || !month || month < 1 || month > 12) {
        return res.status(400).json({ message: "Paramètres year/month invalides." });
      }

      let source = resolvePickingSource();
      let baseCalls = [];

      if (source === "mysql") {
        try {
          baseCalls = await mysqlPickingCalls({ year, month, limit });
        } catch (mysqlErr) {
          console.warn(
            "Picking MySQL indisponible, bascule locale:",
            mysqlErr?.message || mysqlErr
          );
          source = "local";
          baseCalls = listAudioCallsLocal(AUDIO_BASE_PATH, limit, year, month);
        }
      } else if (source === "http") {
        baseCalls = await httpPickingCalls({ year, month, limit });
      } else {
        baseCalls = listAudioCallsLocal(AUDIO_BASE_PATH, limit, year, month);
      }

      const calls = baseCalls.map((call) => {
        const audioUrl =
          `/api/qualite/scores/picking-audio/` +
          `${encodeURIComponent(call.audioDay)}/${encodeURIComponent(call.audioFile)}`;
        return { ...call, audioUrl };
      });

      return res.json({
        year,
        month,
        count: calls.length,
        items: calls,
        source,
      });
    } catch (err) {
      console.error("GET /scores/picking-calls error:", err?.message || err);
      const source = resolvePickingSource();
      return res.status(500).json({
        message: "Erreur serveur lors du chargement des appels.",
        hint:
          source === "mysql"
            ? "Vérifie KYNTUS_KCQ_DB_HOST et la table audio_index."
            : source === "http"
            ? `Vérifie ${RECORDINGS_BASE_URL}/api/picking-calls (et son schéma de réponse).`
            : `Vérifie le montage ${AUDIO_BASE_PATH} (dossiers YYYY-MM-DD).`,
      });
    }
  }
);

router.get(
  "/picking-audio/:day/:file",
  permit("cq", "management", "formateur"),
  async (req, res) => {
    try {
      const day = path.basename(req.params.day || "");
      const file = path.basename(req.params.file || "");

      if (!/^\d{4}-\d{2}-\d{2}$/.test(day)) {
        return res.status(400).json({ message: "Date invalide." });
      }

      if (isHttpMode()) {
        return await httpPickingAudio(day, file, req, res);
      }

      const fullPath = path.join(AUDIO_BASE_PATH, day, file);
      if (!fs.existsSync(fullPath)) {
        return res.status(404).json({ message: "Fichier audio introuvable." });
      }
      return res.sendFile(fullPath);
    } catch (err) {
      console.error("GET /scores/picking-audio error:", err?.message || err);
      if (!res.headersSent) {
        return res.status(500).json({ message: "Erreur serveur." });
      }
    }
  }
);

router.get(
  "/:id",
  permit("admin", "management", "cq", "pilote", "formateur"),
  async (req, res) => {
    try {
      const { id } = req.params;
      if (!mongoose.Types.ObjectId.isValid(id)) {
        return res.status(400).json({ message: "ID invalide." });
      }

      const score = await Score.findById(id)
        .populate("pilot")
        .populate("evaluator")
        .populate("gridId")
        .lean();

      if (!score) {
        return res.status(404).json({ message: "Évaluation introuvable." });
      }

      res.json(score);
    } catch (err) {
      console.error("GET /scores/:id error:", err);
      res.status(500).json({
        message: "Erreur serveur lors du chargement de l'évaluation.",
      });
    }
  }
);

/* =========================================================================
 *  MISE À JOUR D'UNE ÉVALUATION (CQ / MANAGEMENT / ADMIN)
 * ========================================================================= */

/**
 * PATCH /scores/:id
 * - CQ / Management peuvent modifier leurs propres évaluations
 * - Admin peut modifier n'importe quelle évaluation
 */
router.patch(
  "/:id",
  permit("cq", "management", "admin", "formateur"),
  async (req, res) => {
    try {
      const { id } = req.params;

      if (!mongoose.Types.ObjectId.isValid(id)) {
        return res.status(400).json({ message: "ID invalide." });
      }

      const score = await Score.findById(id);
      if (!score) {
        return res
          .status(404)
          .json({ message: "Évaluation introuvable." });
      }

      const isAdmin = req.user.role === "admin";
      const isCq = req.user.role === "cq";
      const evaluatorId = String(score.evaluator);
      const currentId = String(req.user.id || req.user._id);

      // CQ peut réévaluer une écoute contestée même si ce n'est pas lui l'évaluateur
      const allowCqReeval = isCq && score.contested === true;

      if (!isAdmin && !allowCqReeval && evaluatorId !== currentId) {
        return res.status(403).json({
          message: "Vous ne pouvez modifier que vos propres évaluations.",
        });
      }

      // Restrict edits to current cycle (15 → 14 du mois suivant) based on evaluation creation date
      if (!isAdmin && !allowCqReeval) {
        const baseDate = score.createdAt || new Date();
        const d = new Date(baseDate);
        const now = new Date();

        // Le cycle courant démarre le 15 du mois courant si on est >= 15,
        // sinon le 15 du mois précédent.
        const cycleStart = (ref) => {
          const x = new Date(ref);
          if (x.getDate() >= 15) {
            return new Date(x.getFullYear(), x.getMonth(), 15, 0, 0, 0, 0);
          }
          return new Date(x.getFullYear(), x.getMonth() - 1, 15, 0, 0, 0, 0);
        };

        const startNow = cycleStart(now);
        const endNow = new Date(startNow.getFullYear(), startNow.getMonth() + 1, 15, 0, 0, 0, 0); // 15 du mois suivant, exclusif

        if (d < startNow || d >= endNow) {
          return res.status(403).json({
            message: "Modification autorisée uniquement sur le cycle en cours (du 15 au 14 du mois suivant).",
            code: "EDIT_CYCLE_LOCK",
          });
        }
      }

      const {
        pilot,
        pilotId,
        listeningDate,
        callDate,
        interactionDate,
        eps,
        pickingPrime,
        callDuration,
        comment,
        items,
        contested,
        contestComment,
      } = req.body;

      const finalPilot = pilotId || pilot;
      if (finalPilot && mongoose.Types.ObjectId.isValid(finalPilot)) {
        score.pilot = finalPilot;
      }

      if (listeningDate !== undefined) {
        score.listeningDate = listeningDate
          ? new Date(listeningDate)
          : null;
      }
      if (callDate !== undefined) {
        score.callDate = callDate ? new Date(callDate) : null;
      }
      if (interactionDate !== undefined) {
        score.interactionDate = interactionDate
          ? new Date(interactionDate)
          : null;
      }


if (eps !== undefined) {
  const epsClean = (eps || "").toString().trim();
  if (epsClean) {
    const existing = await Score.findOne({ eps: epsClean, _id: { $ne: id } }).lean();
    if (existing) {
      return res.status(409).json({
        message: "EPS doublon : une évaluation existe déjà avec cet EPS.",
        code: "EPS_DUPLICATE",
        duplicate: true,
        existingId: existing._id,
      });
    }
  }
  score.eps = epsClean;
}

      if (callDuration !== undefined) score.callDuration = callDuration;
      if (comment !== undefined) score.comment = comment;

      if (pickingPrime !== undefined) {
        score.pickingPrime = isTrue(pickingPrime);
      }


if (Array.isArray(items)) {
  score.items = items.map((it) => ({
    label: (it?.label || "").toString(),
    status: normalizeStatus(it?.status),
    value:
      typeof it.value === "number"
        ? it.value
        : Number(it.value) || 0,
  }));
}

      if (contested !== undefined) {
        score.contested = !!contested;
      }
      if (contestComment !== undefined) {
        score.contestComment = contestComment || "";
      }

      
// Si un CQ réévalue une écoute contestée, on "résout" la contestation.
if (req.user.role === "cq" && score.contested === true) {
  // CQ réévalue une écoute contestée => on clôture la contestation
  score.contested = false;
  score.reevaluatedAt = new Date();
  const _rePilot = await User.findById(score.pilot).select("name").lean(); await logAudit(req, { action: "REEVALUATE", targetType: "score", targetId: score._id, metadata: { eps: score.eps || "", pilotName: _rePilot?.name || "", previousEvaluatorId: String(score.evaluator) } });
  // on conserve le commentaire de contestation pour l’historique
  score.evaluator = currentId;
}

await score.save();

if (req.user.role === "cq") {
  { const _aP = await User.findById(score.pilot).select("name").lean(); await logAudit(req, { action: "UPDATE_EVALUATION", targetType: "score", targetId: score._id, metadata: { by: "cq", eps: score.eps || "", pilotName: _aP?.name || "" } }); }
} else if (req.user.role === "management") {
  { const _aP2 = await User.findById(score.pilot).select("name").lean(); await logAudit(req, { action: "UPDATE_EVALUATION", targetType: "score", targetId: score._id, metadata: { by: "management", eps: score.eps || "", pilotName: _aP2?.name || "" } }); }
}


      const populated = await Score.findById(score._id)
        .populate("pilot")
        .populate("evaluator")
        .lean();

      // 🔔 Realtime push
      try {
        const io = req.app.get("io");
        if (io) {
          io.emit("scores:changed", { action: "updated", id: String(score._id) });
          const cell = populated?.pilot?.cell ? String(populated.pilot.cell).trim() : "";
          if (cell) io.to(`cell:${cell}`).emit("scores:changed", { action: "updated", id: String(score._id) });
          const evId = populated?.evaluator?._id ? String(populated.evaluator._id) : null;
          if (evId) io.to(`user:${evId}`).emit("scores:changed", { action: "updated", id: String(score._id) });
        }
      } catch (_) {}

      res.json(populated);
    } catch (err) {
      console.error("PATCH /scores/:id error:", err);
      res.status(500).json({
        message: "Erreur serveur lors de la mise à jour de l'évaluation.",
      });
    }
  }
);

/* =========================================================================
 *  CONTESTATION D'UNE ÉVALUATION PAR LE PILOTE
 * ========================================================================= */

/**
 * POST /scores/:id/contest
 * Body : { contestComment }
 */
router.post(
  "/:id/contest",
  permit("pilote", "management", "formateur"),
  async (req, res) => {
    try {
      const { id } = req.params;
      const { contestComment } = req.body;

      if (!mongoose.Types.ObjectId.isValid(id)) {
        return res.status(400).json({ message: "ID invalide." });
      }

      const score = await Score.findById(id);
      if (!score) {
        return res
          .status(404)
          .json({ message: "Évaluation introuvable." });
      }

      // Règles :
      // - Pilote : peut contester uniquement ses propres évaluations
      // - Management : peut contester les évaluations des pilotes de sa cellule (si sa cellule est renseignée)
      const role = String(req.user.role || "");
      const currentUserId = String(req.user.id || req.user._id);

      if (role === "pilote") {
        const pilotId = String(score.pilot);
        if (pilotId !== currentUserId) {
          return res.status(403).json({
            message: "Vous ne pouvez contester que vos propres évaluations.",
          });
        }
      }

      if (role === "management") {
        const manager = await User.findById(currentUserId).select("cell").lean();
        const managerCell = (manager?.cell || "").toString().trim();
        if (managerCell) {
          const pilot = await User.findById(score.pilot).select("cell").lean();
          const pilotCell = (pilot?.cell || "").toString().trim();
          if (pilotCell !== managerCell) {
            return res.status(403).json({
              message: "Vous ne pouvez contester que les évaluations de votre cellule.",
            });
          }
        }
      }

      // Contestation uniquement des évaluations faites par un CQ (champ evaluator)
      const evaluatorId = score.evaluator ? String(score.evaluator) : "";
      if (!evaluatorId || !mongoose.Types.ObjectId.isValid(evaluatorId)) {
        return res.status(400).json({
          message: "Cette évaluation ne possède pas d'évaluateur valide (CQ).",
        });
      }
      const evaluatorUser = await User.findById(evaluatorId)
        .select("role name email")
        .lean();
      if (!evaluatorUser || String(evaluatorUser.role || "") !== "cq") {
        return res.status(400).json({
          message: "Seules les évaluations réalisées par un CQ peuvent être contestées.",
        });
      }

      score.contested = true;
      score.contestComment = contestComment || "";

      await score.save();

      const populated = await Score.findById(score._id)
        .populate("pilot")
        .populate("evaluator")
        .lean();

            // 🔔 Notifier instantanément le CQ évaluateur
      try {
        const notif = await Notification.create({
          type: "alerte",
          title: "Évaluation contestée",
          message: `Une évaluation a été contestée (${score.eps || "EPS"}). Merci de la réévaluer.`,
          targetAll: false,
          targetCells: [],
          targetUsers: [new mongoose.Types.ObjectId(String(evaluatorId))],
          createdBy: req.user.id || req.user._id,
          meta: { action: "reevaluate", scoreId: String(score._id) },
        });

        const io = req.app.get("io");
        if (io) {
          const payload = await Notification.findById(notif._id)
            .populate("createdBy", "name email role")
            .lean();
          io.to(`user:${evaluatorId}`).emit("notification:new", payload);
        }
      } catch (e) {
        // ne bloque pas la contestation si la notif échoue
      }

res.json(populated);
    } catch (err) {
      console.error("POST /scores/:id/contest error:", err);
      res.status(500).json({
        message: "Erreur serveur lors de la contestation de l'évaluation.",
      });
    }
  }
);

/* =========================================================================
 *  SUPPRESSION D'UNE ÉVALUATION (CQ / MANAGEMENT / ADMIN)
 * ========================================================================= */

/**
 * DELETE /scores/:id
 * - Admin seulement
 */
router.delete(
  "/:id",
  permit("admin"),
  async (req, res) => {
    try {
      const { id } = req.params;

      if (!mongoose.Types.ObjectId.isValid(id)) {
        return res.status(400).json({ message: "ID invalide." });
      }

      const score = await Score.findById(id);
      if (!score) {
        return res
          .status(404)
          .json({ message: "Évaluation introuvable." });
      }

      const isAdmin = req.user.role === "admin";
      const evaluatorId = String(score.evaluator);
      const currentId = String(req.user.id || req.user._id);

      if (!isAdmin && evaluatorId !== currentId) {
        return res.status(403).json({
          message:
            "Vous ne pouvez supprimer que vos propres évaluations.",
        });
      }

      await score.deleteOne();

      res.json({ message: "Évaluation supprimée." });
    } catch (err) {
      console.error("DELETE /scores/:id error:", err);
      res.status(500).json({
        message: "Erreur serveur lors de la suppression de l'évaluation.",
      });
    }
  }
);


module.exports = router;

