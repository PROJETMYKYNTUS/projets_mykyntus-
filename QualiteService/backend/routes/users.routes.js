// backend/routes/users.routes.js
const express = require("express");

const User = require("../models/User");
const auth = require("../middleware/auth");
const permit = require("../middleware/roles");

const router = express.Router();

router.use(auth);

/**
 * GET /users/evaluators
 * Retourne la liste des évaluateurs (CQ + Management) pour les filtres.
 */
router.get(
  "/evaluators",
  permit("management", "admin", "cq", "formateur"),
  async (req, res) => {
    try {
      const users = await User.find({ role: { $in: ["cq", "management", "formateur"] }, active: { $ne: false } })
        .select("name email cell role")
        .sort({ name: 1 })
        .lean();

      res.json(
        (users || []).map((u) => ({
          id: u._id,
          _id: u._id,
          name: u.name,
          email: u.email,
          cell: u.cell || "",
          role: u.role,
        }))
      );
    } catch (err) {
      console.error("GET /users/evaluators error:", err);
      res.status(500).json({ message: "Erreur lors du chargement des évaluateurs." });
    }
  }
);

module.exports = router;
