// backend/routes/management.routes.js
const express = require("express");
const mongoose = require("mongoose");
const Score = require("../models/Score");
const User = require("../models/User");
const Notification = require("../models/Notification");
const { logAudit } = require("../utils/audit");
const auth = require("../middleware/auth");
const permit = require("../middleware/roles");
const { applyScorePilotScope } = require("../utils/scope");

const router = express.Router();

/**
 * 🔹 GET /management/evaluations
 * Liste des évaluations pour la supervision Management
 * Query params optionnels :
 *   - pilotId : filtrer par pilote
 *   - cqId    : filtrer par CQ évaluateur (en réalité champ evaluator)
 */
router.get(
  "/evaluations",
  auth,
  permit("management"),
  async (req, res) => {
    try {
      const { pilotId, cqId } = req.query;

      const filter = applyScorePilotScope({}, req);
      if (pilotId && mongoose.Types.ObjectId.isValid(pilotId)) {
        filter.pilot = pilotId;
        applyScorePilotScope(filter, req);
      }
      if (cqId && mongoose.Types.ObjectId.isValid(cqId)) {
        // anciennement filter.cq ; le champ dans Score est "evaluator"
        filter.evaluator = cqId;
      }

      const data = await Score.find(filter)
        .populate("pilot", "name email")
        .populate("evaluator", "name email role")
        .sort({ listeningDate: -1, createdAt: -1 });

      const mapped = data.map((e) => ({
        id: e._id,
        date: e.listeningDate || e.interactionDate || e.callDate || e.createdAt,
        cqName: e.evaluator?.name || "—",
        cqEmail: e.evaluator?.email || "—",
        pilotName: e.pilot?.name || "—",
        pilotEmail: e.pilot?.email || "—",
        score: e.total,
        eps: e.eps || "",
        callDuration: e.callDuration || "",
        comment: e.comment || "",
        contested: !!e.contested,
        contestComment: e.contestComment || "",
      }));

      res.json(mapped);
    } catch (err) {
      console.error("GET /management/evaluations error:", err);
      res
        .status(500)
        .json({ message: "Erreur lors du chargement des évaluations." });
    }
  }
);

/**
 * 🔹 POST /management/scores/:id/contest
 * Contester une évaluation.
 * Body : { comment: "raison de la contestation" }
 */
router.post(
  "/scores/:id/contest",
  auth,
  permit("management"),
  async (req, res) => {
    try {
      const { id } = req.params;
      const { comment } = req.body;

      if (!mongoose.Types.ObjectId.isValid(id)) {
        return res.status(400).json({ message: "ID invalide." });
      }

      const score = await Score.findById(id);
      if (!score) {
        return res.status(404).json({ message: "Évaluation introuvable." });
      }

      // Contestation uniquement des évaluations faites par un CQ
      const evaluatorId = score.evaluator;
      if (!evaluatorId) {
        return res.status(400).json({ message: "Évaluateur manquant sur l’évaluation." });
      }
      const evaluator = await User.findById(evaluatorId).select("role name email").lean();
      if (!evaluator || evaluator.role !== "cq") {
        return res.status(400).json({ message: "Contestation autorisée uniquement sur les évaluations réalisées par un CQ." });
      }

      score.contested = true;
      // Le commentaire est désormais optionnel
      score.contestComment = (comment && String(comment).trim()) || "";
      score.contestedAt = new Date();

      await score.save();

      // Notify evaluator (CQ) instantly
      const notif = await Notification.create({
        title: "Évaluation contestée",
        message: `Une évaluation a été contestée. EPS: ${score.eps || "-"}.`,
        type: "alerte",
        targetUsers: [evaluatorId],
        targetAll: false,
        createdBy: req.user.id || req.user._id,
        meta: { scoreId: String(score._id), kind: "contest" },
      });

      const io = req.app.get("io");
      if (io) {
        io.to(`user:${String(evaluatorId)}`).emit("notification:new", {
          _id: notif._id,
          title: notif.title,
          message: notif.message,
          createdAt: notif.createdAt,
          meta: notif.meta,
        });
      }

      await logAudit(req, { action: "CONTEST", targetType: "score", targetId: score._id, metadata: { contestComment: score.contestComment, evaluatorId: String(evaluatorId) } });

      res.json({ message: "Évaluation contestée avec succès." });
    } catch (err) {
      console.error("POST /management/scores/:id/contest error:", err);
      res.status(500).json({
        message: "Erreur lors de la contestation de l’évaluation.",
      });
    }
  }
);

module.exports = router;

