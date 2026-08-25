const express = require("express");
const User = require("../models/User");
const auth = require("../middleware/auth");

const router = express.Router();

router.post("/login", (_req, res) => {
  res.status(410).json({
    message: "Authentifiez-vous via MyKyntus. Le login local Qualité est désactivé.",
  });
});

router.get("/ping", auth, (req, res) => {
  res.json({ ok: true, user: req.user || null });
});

router.get("/me", auth, async (req, res) => {
  try {
    const userId = req.user.id || req.user._id;
    const user = await User.findById(userId).select("-passwordHash").lean();
    if (!user) return res.status(404).json({ message: "Utilisateur introuvable." });
    res.json({
      ...user,
      id: user._id,
      role: req.user.role,
      kcqRoles: req.user.kcqRoles,
      myKyntusRole: req.user.myKyntusRole,
    });
  } catch (err) {
    console.error("GET /auth/me error:", err);
    res.status(500).json({ message: "Erreur serveur." });
  }
});

router.patch("/profile", auth, async (req, res) => {
  try {
    const userId = req.user.id || req.user._id;
    const { name } = req.body || {};
    if (req.body?.newPassword || req.body?.currentPassword) {
      return res.status(410).json({
        message: "Le mot de passe se gère dans MyKyntus (Auth).",
      });
    }
    const user = await User.findById(userId);
    if (!user) return res.status(404).json({ message: "Utilisateur introuvable." });
    if (name && typeof name === "string" && name.trim()) {
      user.name = name.trim();
      await user.save();
    }
    const plain = user.toObject();
    delete plain.passwordHash;
    res.json(plain);
  } catch (err) {
    console.error("PATCH /auth/profile error:", err);
    res.status(500).json({ message: "Erreur serveur." });
  }
});

module.exports = router;
