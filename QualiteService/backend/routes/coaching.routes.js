// backend/routes/coaching.routes.js
const express = require("express");
const mongoose = require("mongoose");

const auth = require("../middleware/auth");
const permit = require("../middleware/roles");
const { applyScorePilotScope } = require("../utils/scope");

const Coaching = require("../models/Coaching");
const Score = require("../models/Score");

const router = express.Router();
router.use(auth);

function isValidObjectId(id) {
  return Boolean(id) && mongoose.Types.ObjectId.isValid(String(id));
}

function parseCsvList(v) {
  if (!v) return [];
  if (Array.isArray(v)) return v.flatMap((x) => String(x).split(",")).map((s) => s.trim()).filter(Boolean);
  return String(v).split(",").map((s) => s.trim()).filter(Boolean);
}

function parseDateRange(from, to) {
  const out = {};
  const f = from ? new Date(String(from)) : null;
  const t = to ? new Date(String(to)) : null;
  if (f && !Number.isNaN(f.getTime())) out.$gte = f;
  if (t && !Number.isNaN(t.getTime())) {
    // include full day
    t.setHours(23, 59, 59, 999);
    out.$lte = t;
  }
  return Object.keys(out).length ? out : null;
}

async function resolveScoreIdsFromFilters(query, req) {
  const pilotIds = parseCsvList(query.pilotId).filter((x) => mongoose.Types.ObjectId.isValid(x));
  const evaluatorIds = parseCsvList(query.evaluatorId).filter((x) => mongoose.Types.ObjectId.isValid(x));
  const dateRange = parseDateRange(query.dateFrom, query.dateTo);

  const scoreQ = {};
  if (pilotIds.length) scoreQ.pilot = { $in: pilotIds };
  if (evaluatorIds.length) scoreQ.evaluator = { $in: evaluatorIds };
  if (dateRange) scoreQ.createdAt = dateRange;
  applyScorePilotScope(scoreQ, req);

  if (!Object.keys(scoreQ).length) return null;
  const ids = await Score.find(scoreQ).select("_id").lean();
  return ids.map((d) => d._id);
}

/**
 * POST /api/coaching
 * Create a coaching linked to an evaluation.
 * Roles: cq, management, admin
 */
router.post("/", permit("cq", "management", "admin", "formateur"), async (req, res) => {
  try {
    const { scoreId, notes, actionPlan, status, followUpDate } = req.body || {};
    if (!isValidObjectId(scoreId)) {
      return res.status(400).json({ message: "scoreId invalide" });
    }

    const score = await Score.findById(scoreId).lean();
    if (!score) return res.status(404).json({ message: "Évaluation introuvable" });

    const doc = await Coaching.create({
      score: score._id,
      pilot: score.pilot,
      evaluator: score.evaluator,
      // auth middleware sets req.user = { id, role, name, ... }
      // Some tokens may include _id, so we support both.
      coach: req.user.id || req.user._id,
      notes: typeof notes === "string" ? notes : "",
      actionPlan: typeof actionPlan === "string" ? actionPlan : "",
      status: ["open", "in_progress", "done"].includes(String(status))
        ? String(status)
        : "open",
      followUpDate: followUpDate ? new Date(String(followUpDate)) : null,
    });

    const populated = await Coaching.findById(doc._id)
      .populate({ path: "score", populate: [{ path: "pilot" }, { path: "evaluator" }] })
      .populate("pilot")
      .populate("coach")
      .lean();

    // Notify pilot of new coaching
    try {
      const Notification = require("../models/Notification");
      const coachName = req.user.name || "Coach";
      const pilotUserId = score.pilot;
      const notif = await Notification.create({
        type: "notification",
        title: "Nouveau coaching",
        message: `🎯 ${coachName} vous a créé un coaching suite à l'évaluation ${score.eps || ""}. Consultez-le et validez-le.`,
        targetUsers: [pilotUserId],
        createdBy: req.user.id || req.user._id,
        meta: { coachingId: String(doc._id) },
      });
      const io = req.app.get("io");
      if (io) io.emit("notification:new", notif);
    } catch (_) {}

    res.status(201).json(populated);
  } catch (err) {
    console.error("POST /api/coaching error:", err);
    res.status(500).json({ message: "Erreur serveur lors de la création du coaching" });
  }
});

/**
 * GET /api/coaching/mine
 * Roles: cq, management, admin
 */
router.get("/mine", permit("cq", "management", "admin", "formateur"), async (req, res) => {
  try {
    const page = Math.max(1, parseInt(req.query.page || "1", 10));
    const limit = Math.min(200, Math.max(10, parseInt(req.query.limit || "50", 10)));
    const skip = (page - 1) * limit;

    const q = { coach: req.user.id || req.user._id };

    // Optional filters (agent / evaluator / date) are applied on the linked evaluation (Score)
    const scoreIds = await resolveScoreIdsFromFilters(req.query, req);
    if (Array.isArray(scoreIds)) {
      // If filters yield no score, return empty quickly
      if (scoreIds.length === 0) return res.json({ page, limit, total: 0, items: [] });
      q.score = { $in: scoreIds };
    }

    const [items, total] = await Promise.all([
      Coaching.find(q)
        .populate({ path: "score", populate: [{ path: "pilot" }, { path: "evaluator" }] })
        .populate("pilot")
        .populate("coach")
        .sort({ createdAt: -1 })
        .skip(skip)
        .limit(limit)
        .lean(),
      Coaching.countDocuments(q),
    ]);

    res.json({ page, limit, total, items });
  } catch (err) {
    console.error("GET /api/coaching/mine error:", err);
    res.status(500).json({ message: "Erreur serveur lors du chargement des coachings" });
  }
});

/**
 * GET /api/coaching
 * Admin only: list all coachings
 */
router.get("/", permit("admin"), async (req, res) => {
  try {
    const page = Math.max(1, parseInt(req.query.page || "1", 10));
    const limit = Math.min(200, Math.max(10, parseInt(req.query.limit || "50", 10)));
    const skip = (page - 1) * limit;

    const q = {};
    if (req.query.status) q.status = String(req.query.status);

    const scoreIds = await resolveScoreIdsFromFilters(req.query, req);
    if (Array.isArray(scoreIds)) {
      if (scoreIds.length === 0) return res.json({ page, limit, total: 0, items: [] });
      q.score = { $in: scoreIds };
    }

    const [items, total] = await Promise.all([
      Coaching.find(q)
        .populate({ path: "score", populate: [{ path: "pilot" }, { path: "evaluator" }] })
        .populate("pilot")
        .populate("coach")
        .sort({ createdAt: -1 })
        .skip(skip)
        .limit(limit)
        .lean(),
      Coaching.countDocuments(q),
    ]);

    res.json({ page, limit, total, items });
  } catch (err) {
    console.error("GET /api/coaching error:", err);
    res.status(500).json({ message: "Erreur serveur lors du chargement des coachings" });
  }
});

/**
 * PATCH /api/coaching/:id
 * Roles: cq, management, admin
 */
router.patch("/:id", permit("cq", "management", "admin", "formateur"), async (req, res) => {
  try {
    const id = req.params.id;
    if (!isValidObjectId(id)) return res.status(400).json({ message: "id invalide" });

    const existing = await Coaching.findById(id).lean();
    if (!existing) return res.status(404).json({ message: "Coaching introuvable" });

    const me = req.user.id || req.user._id;
    if (req.user.role !== "admin" && String(existing.coach) !== String(me)) {
      return res.status(403).json({ message: "Accès interdit" });
    }

    const patch = {};
    if (req.body.notes !== undefined) patch.notes = String(req.body.notes || "");
    if (req.body.actionPlan !== undefined) patch.actionPlan = String(req.body.actionPlan || "");
    if (req.body.status !== undefined) {
      const s = String(req.body.status);
      if (["open", "in_progress", "done"].includes(s)) patch.status = s;
    }
    if (req.body.followUpDate !== undefined) {
      patch.followUpDate = req.body.followUpDate ? new Date(String(req.body.followUpDate)) : null;
    }

    await Coaching.updateOne({ _id: id }, { $set: patch });

    const populated = await Coaching.findById(id)
      .populate({ path: "score", populate: [{ path: "pilot" }, { path: "evaluator" }] })
      .populate("pilot")
      .populate("coach")
      .lean();

    res.json(populated);
  } catch (err) {
    console.error("PATCH /api/coaching/:id error:", err);
    res.status(500).json({ message: "Erreur serveur lors de la mise à jour du coaching" });
  }
});

/**
 * DELETE /api/coaching/:id
 * Admin only
 */
router.delete("/:id", permit("admin"), async (req, res) => {
  try {
    const id = req.params.id;
    if (!isValidObjectId(id)) return res.status(400).json({ message: "id invalide" });

    await Coaching.deleteOne({ _id: id });
    res.json({ ok: true });
  } catch (err) {
    console.error("DELETE /api/coaching/:id error:", err);
    res.status(500).json({ message: "Erreur serveur lors de la suppression du coaching" });
  }
});

/**
 * GET /api/coaching/my-coachings
 * Pilot: list coachings assigned to me
 */
router.get("/my-coachings", permit("pilote"), async (req, res) => {
  try {
    const userId = req.user.id || req.user._id;
    const items = await Coaching.find({ pilot: userId })
      .populate({ path: "score", populate: [{ path: "pilot" }, { path: "evaluator" }] })
      .populate("coach", "name email role")
      .sort({ createdAt: -1 })
      .lean();
    res.json(items);
  } catch (err) {
    console.error("GET /api/coaching/my-coachings error:", err);
    res.status(500).json({ message: "Erreur chargement coachings." });
  }
});

/**
 * POST /api/coaching/:id/acknowledge
 * Pilot acknowledges a coaching and adds a comment
 */
router.post("/:id/acknowledge", permit("pilote"), async (req, res) => {
  try {
    const id = req.params.id;
    if (!isValidObjectId(id)) return res.status(400).json({ message: "id invalide" });

    const coaching = await Coaching.findById(id);
    if (!coaching) return res.status(404).json({ message: "Coaching introuvable" });

    const userId = String(req.user.id || req.user._id);
    if (String(coaching.pilot) !== userId) {
      return res.status(403).json({ message: "Ce coaching ne vous est pas assigné." });
    }

    if (coaching.pilotAcknowledged) {
      return res.status(400).json({ message: "Coaching déjà validé." });
    }

    coaching.pilotAcknowledged = true;
    coaching.pilotComment = String(req.body.comment || "").trim();
    coaching.pilotAcknowledgedAt = new Date();
    await coaching.save();

    const populated = await Coaching.findById(id)
      .populate({ path: "score", populate: [{ path: "pilot" }, { path: "evaluator" }] })
      .populate("coach", "name email role")
      .lean();

    // Notify the coach
    try {
      const Notification = require("../models/Notification");
      const pilotName = req.user.name || "Agent";
      const notif = await Notification.create({
        type: "notification",
        title: "Coaching validé",
        message: `${pilotName} a validé le coaching et ajouté un commentaire.`,
        targetUsers: [coaching.coach],
        createdBy: req.user.id || req.user._id,
        meta: { coachingId: String(id) },
      });
      const io = req.app.get("io");
      if (io) io.emit("notification:new", notif);
    } catch (e) { /* don't block */ }

    res.json(populated);
  } catch (err) {
    console.error("POST /api/coaching/:id/acknowledge error:", err);
    res.status(500).json({ message: "Erreur validation coaching." });
  }
});

module.exports = router;