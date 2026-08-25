// backend/routes/cq.routes.js
const express = require("express");
const mongoose = require("mongoose");

const Score = require("../models/Score");
const User = require("../models/User");

const auth = require("../middleware/auth");
const permit = require("../middleware/roles");
const { applyPilotScopeToUserQuery } = require("../utils/scope");

const router = express.Router();

// Toutes les routes /cq/* nécessitent auth + rôle CQ ou Management
router.use(auth);
router.use(permit("cq", "management", "formateur"));

function computeAverageScore(scoreDoc) {
  if (!scoreDoc.items || !Array.isArray(scoreDoc.items) || scoreDoc.items.length === 0) {
    return 0;
  }
  const sum = scoreDoc.items.reduce((acc, it) => {
    const v = typeof it.value === "number" ? it.value : Number(it.value) || 0;
    return acc + v;
  }, 0);
  return sum / scoreDoc.items.length;
}

/**
 * GET /cq/pilots
 * Liste des pilotes (utile si tu veux séparer l'API CQ et ADMIN)
 */
router.get("/pilots", async (req, res) => {
  try {
    const pilots = await User.find(
      applyPilotScopeToUserQuery({ role: "pilote", active: { $ne: false } }, req)
    )
      .select("-passwordHash")
      .lean();
    res.json(pilots);
  } catch (err) {
    console.error("GET /cq/pilots error:", err);
    res.status(500).json({ message: "Erreur serveur lors du chargement des pilotes." });
  }
});

/**
 * GET /cq/pilots/search
 * Recherche légère et paginée (prévu pour 200+ agents).
 * Query:
 *  - q: string (name/email/cell)
 *  - limit: number (default 50, max 200)
 */
router.get("/pilots/search", async (req, res) => {
  try {
    const q = String(req.query.q || "").trim();
    const limit = Math.min(200, Math.max(10, parseInt(req.query.limit || "50", 10)));

    const where = applyPilotScopeToUserQuery({ role: "pilote", active: { $ne: false } }, req);
    if (q) {
      // Basic regex search (fast enough for a few hundred agents)
      const rx = new RegExp(q.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"), "i");
      where.$or = [{ name: rx }, { email: rx }, { cell: rx }];
    }

    const pilots = await User.find(where)
      .select("name email cell role active")
      .sort({ name: 1 })
      .limit(limit)
      .lean();

    res.json(pilots);
  } catch (err) {
    console.error("GET /cq/pilots/search error:", err);
    res.status(500).json({ message: "Erreur serveur lors de la recherche des pilotes." });
  }
});

/**
 * GET /cq/stats
 * Stats personnelles du CQ connecté (pour un éventuel dashboard dédié)
 * Retour :
 * {
 *   global: { count, avgTotal, contested },
 *   monthly: [{ month, count, avgTotal }]
 * }
 */
router.get("/stats", async (req, res) => {
  try {
    const cqId = req.user.id || req.user._id;

    const scores = await Score.find({ evaluator: cqId }).lean();

    let total = 0;
    let sum = 0;
    let contested = 0;

    const monthlyMap = new Map();

    for (const s of scores) {
      const avg = computeAverageScore(s);
      total += 1;
      sum += avg;
      if (s.contested) contested += 1;

      const ref =
        s.listeningDate ||
        s.interactionDate ||
        s.callDate ||
        s.createdAt;

      if (ref) {
        const d = new Date(ref);
        const key = `${d.getFullYear()}-${d.getMonth() + 1}`;
        if (!monthlyMap.has(key)) {
          monthlyMap.set(key, {
            year: d.getFullYear(),
            month: d.getMonth() + 1,
            count: 0,
            sum: 0,
          });
        }
        const entry = monthlyMap.get(key);
        entry.count += 1;
        entry.sum += avg;
      }
    }

    const globalAvg = total ? sum / total : 0;
    const monthly = Array.from(monthlyMap.values())
      .sort((a, b) =>
        a.year === b.year ? a.month - b.month : a.year - b.year
      )
      .map((m) => ({
        year: m.year,
        month: m.month,
        count: m.count,
        avgTotal: m.count ? m.sum / m.count : 0,
      }));

    res.json({
      global: {
        count: total,
        avgTotal: globalAvg,
        contested,
      },
      monthly,
    });
  } catch (err) {
    console.error("GET /cq/stats error:", err);
    res.status(500).json({
      message: "Erreur serveur lors du chargement des statistiques CQ.",
    });
  }
});

module.exports = router;
