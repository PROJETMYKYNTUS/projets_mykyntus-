const express = require("express");
const mongoose = require("mongoose");
const auth = require("../middleware/auth");
const Notification = require("../models/Notification");
const User = require("../models/User");

const router = express.Router();
router.use(auth);

/**
 * GET /api/notifications/mine
 * Retourne les notifications ciblées pour l'utilisateur connecté:
 * - targetAll=true
 * - OR cell in targetCells
 * - OR userId in targetUsers
 */
router.get("/mine", async (req, res) => {
  try {
    const userId = req.user.id || req.user._id;
    const user = await User.findById(userId).lean();
    const cell = (user?.cell || "").toString();

    const or = [{ targetAll: true }, { targetUsers: userId }];
    if (cell) or.push({ targetCells: cell });

    const list = await Notification.find({ $or: or })
      .sort({ createdAt: -1 })
      .lean();

    const uid = userId.toString();
    const withRead = (Array.isArray(list) ? list : []).map((n) => {
      const rb = Array.isArray(n.readBy) ? n.readBy.map(String) : [];
      return { ...n, isRead: rb.includes(uid) };
    });

    res.json(withRead);
  } catch (err) {
    console.error("GET /notifications/mine error:", err);
    res.status(500).json({ message: "Erreur serveur lors du chargement des notifications." });
  }
});

/**
 * POST /api/notifications/:id/read
 * Marque une notification comme lue pour l'utilisateur connecté.
 */
router.post("/:id/read", async (req, res) => {
  try {
    const userId = req.user.id || req.user._id;
    const { id } = req.params;
    if (!mongoose.Types.ObjectId.isValid(id)) {
      return res.status(400).json({ message: "ID notification invalide." });
    }
    await Notification.updateOne({ _id: id }, { $addToSet: { readBy: userId } });
    res.json({ ok: true });
  } catch (err) {
    console.error("POST /notifications/:id/read error:", err);
    res.status(500).json({ message: "Erreur serveur lors du marquage lu." });
  }
});

/**
 * POST /api/notifications/read-all
 * Marque toutes les notifications ciblées pour l'utilisateur comme lues.
 */
router.post("/read-all", async (req, res) => {
  try {
    const userId = req.user.id || req.user._id;
    const user = await User.findById(userId).lean();
    const cell = (user?.cell || "").toString();

    const or = [{ targetAll: true }, { targetUsers: userId }];
    if (cell) or.push({ targetCells: cell });

    await Notification.updateMany({ $or: or }, { $addToSet: { readBy: userId } });
    res.json({ ok: true });
  } catch (err) {
    console.error("POST /notifications/read-all error:", err);
    res.status(500).json({ message: "Erreur serveur lors du marquage lu." });
  }
});

module.exports = router;
