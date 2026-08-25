// backend/routes/grid.routes.js
const express = require("express");
const auth = require("../middleware/auth");
const permit = require("../middleware/roles");
const User = require("../models/User");
const Grid = require("../models/Grid");

const router = express.Router();

/* ---- Helper ---- */
const normalizeGridItems = (items) => {
  const arr = Array.isArray(items) ? items : [];
  return arr.map((it, idx) => {
    const order = typeof it?.order === "number" ? it.order : idx;
    const isGrp = it?.type === "group" || (typeof it?.title === "string" && it.title.trim().length > 0);
    if (isGrp) {
      const title = (it?.title || it?.label || "").toString().replace(/^#+/, "").trim();
      return title ? { type: "group", title, hardFail: !!it?.hardFail, malusPercent: Number(it?.malusPercent) || 0, order } : null;
    }
    const label = (it?.label || "").toString().trim();
    if (!label) return null;
    return {
      type: "item", label, order,
      pointsConforme: Number.isFinite(Number(it?.pointsConforme)) ? Number(it.pointsConforme) : 1,
      pointsNonConforme: Number.isFinite(Number(it?.pointsNonConforme)) ? Number(it.pointsNonConforme) : 0,
      malusPercent: Number.isFinite(Number(it?.malusPercent)) ? Number(it.malusPercent) : 0,
    };
  }).filter(Boolean);
};

/* ===========================================================
 *  STATIC ROUTES (must be declared BEFORE /:id)
 * =========================================================== */

/**
 * GET /grids/my
 * Grilles visibles pour l'utilisateur connecté.
 * Only returns active grids (approved).
 */
router.get("/my", auth, async (req, res) => {
  try {
    const user = await User.findById(req.user.id).populate("assignedGrids");
    if (!user) return res.status(404).json({ message: "Utilisateur introuvable." });

    if (user.assignedGrids && user.assignedGrids.length > 0) {
      const activeGrids = user.assignedGrids.filter((g) => g && g.active !== false && g.isDeleted !== true);
      return res.json(activeGrids);
    }

    const grids = await Grid.find({ active: true, isDeleted: { $ne: true } }).sort({ name: 1 }).lean();
    return res.json(grids);
  } catch (err) {
    console.error(err);
    return res.status(500).json({ message: "Erreur lors du chargement des grilles." });
  }
});

/**
 * GET /grids/pending
 * Grilles en attente de validation (créées par CQ).
 * Accessible par management et admin.
 */
router.get("/pending", auth, permit("admin"), async (req, res) => {
  try {
    const grids = await Grid.find({
      roles: "pending_approval",
      isDeleted: { $ne: true },
    }).sort({ createdAt: -1 }).lean();
    res.json(grids);
  } catch (err) {
    console.error("GET /grids/pending error:", err);
    res.status(500).json({ message: "Erreur serveur." });
  }
});

/**
 * GET /grids/my-proposals
 * Returns all pending grids (visible to CQ to track their proposals).
 */
router.get("/my-proposals", auth, permit("cq", "formateur"), async (req, res) => {
  try {
    const grids = await Grid.find({
      roles: "pending_approval",
      isDeleted: { $ne: true },
    }).sort({ createdAt: -1 }).lean();
    res.json(grids);
  } catch (err) {
    console.error("GET /grids/my-proposals error:", err);
    res.status(500).json({ message: "Erreur serveur." });
  }
});

/**
 * POST /grids/propose
 * CQ proposes a new grid (created as inactive, pending management approval).
 */
router.post("/propose", auth, permit("cq", "formateur"), async (req, res) => {
  try {
    const { name, description, items, gridType } = req.body;

    if (!name || !name.trim()) return res.status(400).json({ message: "Le nom de la grille est obligatoire." });
    if (!Array.isArray(items) || items.length === 0) return res.status(400).json({ message: "La grille doit contenir au moins un item." });

    const normalized = normalizeGridItems(items);
    if (!normalized.some((x) => x.type === "item")) return res.status(400).json({ message: "La grille doit contenir au moins un critère." });

    const grid = await Grid.create({
      name: name.trim(),
      description: (description || "") + `\n[Proposée par: ${req.user.name || req.user.id}]`,
      gridType: (gridType || "classic") === "presence" ? "presence" : "classic",
      items: normalized,
      roles: ["pending_approval"],
      active: false,
    });

    res.status(201).json(grid);
  } catch (err) {
    console.error("POST /grids/propose error:", err);
    res.status(500).json({ message: "Erreur serveur lors de la proposition de grille." });
  }
});


/* ===========================================================
 *  PARAM ROUTES (/:id)
 * =========================================================== */

/**
 * POST /grids/:id/approve
 * Management or Admin approves a pending grid.
 */
router.post("/:id/approve", auth, permit("admin"), async (req, res) => {
  try {
    const grid = await Grid.findById(req.params.id);
    if (!grid) return res.status(404).json({ message: "Grille introuvable." });

    grid.active = true;
    grid.roles = (grid.roles || []).filter((r) => r !== "pending_approval");
    if (!grid.roles.length) grid.roles = ["cq", "management"];
    await grid.save();

    res.json(grid);
  } catch (err) {
    console.error("POST /grids/:id/approve error:", err);
    res.status(500).json({ message: "Erreur serveur." });
  }
});

/**
 * POST /grids/:id/reject
 * Management or Admin rejects a pending grid (soft-deletes it).
 */
router.post("/:id/reject", auth, permit("admin"), async (req, res) => {
  try {
    const grid = await Grid.findById(req.params.id);
    if (!grid) return res.status(404).json({ message: "Grille introuvable." });

    grid.isDeleted = true;
    grid.deletedAt = new Date();
    grid.active = false;
    await grid.save();

    res.json({ message: "Grille rejetée." });
  } catch (err) {
    console.error("POST /grids/:id/reject error:", err);
    res.status(500).json({ message: "Erreur serveur." });
  }
});

/**
 * GET /grids/:id
 */
router.get("/:id", auth, async (req, res) => {
  try {
    const { id } = req.params;
    const includeDeleted = String(req.query.includeDeleted || "") === "1";
    const q = { _id: id };
    if (!includeDeleted) q.isDeleted = { $ne: true };
    const grid = await Grid.findOne(q).lean();
    if (!grid) return res.status(404).json({ message: "Grille introuvable." });
    return res.json(grid);
  } catch (err) {
    console.error(err);
    return res.status(500).json({ message: "Erreur lors du chargement de la grille." });
  }
});

module.exports = router;
