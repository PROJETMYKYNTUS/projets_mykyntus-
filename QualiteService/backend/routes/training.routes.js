const express = require("express");
const path = require("path");
const multer = require("multer");
const auth = require("../middleware/auth");
const permit = require("../middleware/roles");
const Training = require("../models/Training");
const Notification = require("../models/Notification");
const router = express.Router();
router.use(auth);

// === Multer config ===
const storage = multer.diskStorage({
  destination: (req, file, cb) => {
    const fs = require("fs");
    const dir = path.join(__dirname, "..", "uploads", "training");
    if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
    cb(null, dir);
  },
  filename: (req, file, cb) => {
    const ext = path.extname(file.originalname) || "";
    cb(null, `${Date.now()}-${Math.random().toString(36).slice(2)}${ext}`);
  },
});
const upload = multer({
  storage,
  limits: { fileSize: 500 * 1024 * 1024 },
  fileFilter: (req, file, cb) => {
    if (/\.(mp4|webm|mov|avi|mkv|pdf)$/i.test(path.extname(file.originalname))) cb(null, true);
    else cb(new Error("Format non supporté."));
  },
});

// ======================================================================
//  STATIC ROUTES (must be declared BEFORE /:id to avoid param collision)
// ======================================================================

/** POST /training/upload — Upload video/PDF file */
router.post("/upload", permit("admin", "formateur"), upload.single("file"), (req, res) => {
  try {
    if (!req.file) return res.status(400).json({ message: "Aucun fichier." });
    res.json({ url: `/uploads/training/${req.file.filename}`, filename: req.file.originalname, size: req.file.size });
  } catch (err) {
    res.status(500).json({ message: err.message || "Erreur upload." });
  }
});

/** GET /training/admin — Admin/Formateur: list all trainings with stats */
router.get("/admin", permit("admin", "formateur"), async (req, res) => {
  try {
    const items = await Training.find({ active: { $ne: false } })
      .select("-pdfData -questions.imageData")
      .sort({ createdAt: -1 })
      .lean();

    const result = items.map((t) => {
      const attempts = t.attempts || [];
      const attemptCount = attempts.length;
      let sumPct = 0;
      for (const a of attempts) sumPct += a.total > 0 ? (a.score / a.total) * 100 : 0;
      return { ...t, attempts: undefined, attemptCount, avgScore: attemptCount ? Math.round(sumPct / attemptCount) : 0 };
    });
    res.json(result);
  } catch (err) {
    console.error("GET /training/admin error:", err);
    res.status(500).json({ message: "Erreur." });
  }
});

/** GET /training/history/me — User quiz history */
router.get("/history/me", async (req, res) => {
  try {
    const userId = req.user.id || req.user._id;
    const trainings = await Training.find({ "attempts.user": userId })
      .select("title category attempts")
      .lean();

    const history = [];
    for (const t of trainings) {
      for (const a of (t.attempts || [])) {
        if (String(a.user) === String(userId)) {
          history.push({
            trainingId: t._id, trainingTitle: t.title, category: t.category || "",
            score: a.score, total: a.total,
            percent: a.total > 0 ? Math.round((a.score / a.total) * 100) : 0,
            completedAt: a.completedAt,
          });
        }
      }
    }
    history.sort((a, b) => new Date(b.completedAt) - new Date(a.completedAt));
    res.json(history);
  } catch (err) {
    console.error("GET /training/history/me error:", err);
    res.status(500).json({ message: "Erreur." });
  }
});

/** GET /training/stats/global — Admin: training platform KPIs */
router.get("/stats/global", permit("admin", "formateur"), async (req, res) => {
  try {
    const trainings = await Training.find({ active: { $ne: false } }).select("title questions attempts category").lean();

    let totalTrainings = trainings.length, totalQuestions = 0, totalAttempts = 0, totalScore = 0;
    const byTraining = [];

    for (const t of trainings) {
      const qCount = (t.questions || []).length;
      totalQuestions += qCount;
      const attempts = t.attempts || [];
      totalAttempts += attempts.length;
      let sum = 0;
      for (const a of attempts) { const pct = a.total > 0 ? (a.score / a.total) * 100 : 0; sum += pct; totalScore += pct; }
      byTraining.push({ id: t._id, title: t.title, category: t.category || "", questions: qCount, attempts: attempts.length, avgScore: attempts.length ? Math.round(sum / attempts.length) : 0 });
    }
    res.json({ totalTrainings, totalQuestions, totalAttempts, avgScore: totalAttempts > 0 ? Math.round(totalScore / totalAttempts) : 0, byTraining });
  } catch (err) {
    console.error("GET /training/stats/global error:", err);
    res.status(500).json({ message: "Erreur." });
  }
});

/** GET /training — User: list visible trainings */
router.get("/", async (req, res) => {
  try {
    const userId = req.user.id || req.user._id;
    const role = req.user.role;
    const User = require("../models/User");
    const me = await User.findById(userId).select("cell").lean();
    const myCell = me?.cell || "";

    const q = {
      active: { $ne: false },
      $or: [
        { $and: [
          { $or: [{ roles: { $size: 0 } }, { roles: { $exists: false } }] },
          { $or: [{ targetCells: { $size: 0 } }, { targetCells: { $exists: false } }] },
          { $or: [{ targetUsers: { $size: 0 } }, { targetUsers: { $exists: false } }] },
        ]},
        { roles: role },
        ...(myCell ? [{ targetCells: myCell }] : []),
        { targetUsers: userId },
      ],
    };
    const items = await Training.find(q).select("-pdfData -attempts -questions.imageData").sort({ createdAt: -1 }).lean();
    res.json(items);
  } catch (err) {
    console.error("GET /training error:", err);
    res.status(500).json({ message: "Erreur." });
  }
});

/** POST /training — Create training */
router.post("/", permit("admin", "formateur"), async (req, res) => {
  try {
    const { title, description, pdfUrl, pdfData, videoUrl, category, roles, targetCells, targetUsers, questions, allowMultipleAttempts, passThreshold } = req.body;
    if (!title || !title.trim()) return res.status(400).json({ message: "Titre obligatoire." });

    const t = await Training.create({
      title: title.trim(), description: description || "", pdfUrl: pdfUrl || "", pdfData: pdfData || "",
      videoUrl: videoUrl || "", category: category || "",
      roles: Array.isArray(roles) ? roles : [], targetCells: Array.isArray(targetCells) ? targetCells : [],
      targetUsers: Array.isArray(targetUsers) ? targetUsers : [],
      allowMultipleAttempts: allowMultipleAttempts !== false,
      passThreshold: Number.isFinite(Number(passThreshold)) ? Number(passThreshold) : 80,
      questions: Array.isArray(questions) ? questions : [],
      createdBy: req.user.id || req.user._id,
    });

    // Push notification
    try {
      const notif = await Notification.create({
        type: "information", title: "Nouvelle formation disponible",
        message: `📚 "${t.title}" est disponible. ${(t.questions || []).length > 0 ? `Quiz de ${t.questions.length} questions inclus.` : ""}`,
        targetAll: true, createdBy: req.user.id || req.user._id, meta: { trainingId: String(t._id) },
      });
      const io = req.app.get("io");
      if (io) io.emit("notification:new", notif);
    } catch (_) {}

    res.status(201).json(t);
  } catch (err) {
    console.error("POST /training error:", err);
    res.status(500).json({ message: "Erreur création." });
  }
});

// ======================================================================
//  PARAMETERIZED ROUTES (/:id) — must come AFTER all static routes
// ======================================================================

/** GET /training/:id — Get full training detail */
router.get("/:id", async (req, res) => {
  try {
    const t = await Training.findById(req.params.id).select("-attempts").lean();
    if (!t) return res.status(404).json({ message: "Formation introuvable." });
    res.json(t);
  } catch (err) {
    res.status(500).json({ message: "Erreur." });
  }
});

/** PATCH /training/:id — Update training */
router.patch("/:id", permit("admin", "formateur"), async (req, res) => {
  try {
    const update = {};
    const { title, description, pdfUrl, pdfData, videoUrl, category, roles, targetCells, targetUsers, questions, active, allowMultipleAttempts, passThreshold } = req.body;
    if (title !== undefined) update.title = String(title).trim();
    if (description !== undefined) update.description = String(description);
    if (pdfUrl !== undefined) update.pdfUrl = String(pdfUrl);
    if (pdfData !== undefined) update.pdfData = String(pdfData);
    if (videoUrl !== undefined) update.videoUrl = String(videoUrl);
    if (category !== undefined) update.category = String(category);
    if (roles !== undefined) update.roles = Array.isArray(roles) ? roles : [];
    if (targetCells !== undefined) update.targetCells = Array.isArray(targetCells) ? targetCells : [];
    if (targetUsers !== undefined) update.targetUsers = Array.isArray(targetUsers) ? targetUsers : [];
    if (questions !== undefined) update.questions = Array.isArray(questions) ? questions : [];
    if (active !== undefined) update.active = !!active;
    if (allowMultipleAttempts !== undefined) update.allowMultipleAttempts = !!allowMultipleAttempts;
    if (passThreshold !== undefined) update.passThreshold = Number.isFinite(Number(passThreshold)) ? Number(passThreshold) : 80;

    const t = await Training.findByIdAndUpdate(req.params.id, update, { new: true }).select("-pdfData -attempts");
    if (!t) return res.status(404).json({ message: "Formation introuvable." });
    res.json(t);
  } catch (err) {
    res.status(500).json({ message: "Erreur mise à jour." });
  }
});

/** DELETE /training/:id — Admin only: permanent delete */
router.delete("/:id", permit("admin"), async (req, res) => {
  try {
    const t = await Training.findById(req.params.id);
    if (!t) return res.status(404).json({ message: "Formation introuvable." });

    // Cleanup uploaded video
    if (t.videoUrl && t.videoUrl.startsWith("/uploads/")) {
      const fs = require("fs");
      const filePath = path.join(__dirname, "..", t.videoUrl);
      if (fs.existsSync(filePath)) try { fs.unlinkSync(filePath); } catch (_) {}
    }

    await Training.deleteOne({ _id: req.params.id });
    res.json({ ok: true, message: "Formation supprimée." });
  } catch (err) {
    console.error("DELETE /training/:id error:", err);
    res.status(500).json({ message: "Erreur suppression." });
  }
});

/** POST /training/:id/attempt — Submit quiz answers */
router.post("/:id/attempt", async (req, res) => {
  try {
    const t = await Training.findById(req.params.id);
    if (!t) return res.status(404).json({ message: "Formation introuvable." });

    const userId = String(req.user.id || req.user._id);

    if (t.allowMultipleAttempts === false) {
      if ((t.attempts || []).some((a) => String(a.user) === userId)) {
        return res.status(403).json({ message: "Une seule tentative autorisée pour ce quiz. Vous l'avez déjà passé." });
      }
    }

    const { answers } = req.body;
    if (!Array.isArray(answers)) return res.status(400).json({ message: "Réponses requises." });

    let score = 0;
    const total = (t.questions || []).length;
    (t.questions || []).forEach((q, idx) => { if (answers[idx] === q.correctIndex) score++; });

    const percent = total > 0 ? Math.round((score / total) * 100) : 0;
    const threshold = Number.isFinite(t.passThreshold) ? t.passThreshold : 80;

    t.attempts.push({ user: req.user.id || req.user._id, answers, score, total, completedAt: new Date() });
    await t.save();

    res.json({ score, total, percent, passed: percent >= threshold, threshold });
  } catch (err) {
    console.error("POST /training/:id/attempt error:", err);
    res.status(500).json({ message: "Erreur." });
  }
});

/** GET /training/:id/results — Admin: quiz results for a training */
router.get("/:id/results", permit("admin", "formateur"), async (req, res) => {
  try {
    const t = await Training.findById(req.params.id)
      .select("title attempts questions")
      .populate("attempts.user", "name email role cell")
      .lean();
    if (!t) return res.status(404).json({ message: "Formation introuvable." });

    res.json({
      title: t.title,
      questionCount: (t.questions || []).length,
      attempts: (t.attempts || []).map((a) => ({
        user: a.user, score: a.score, total: a.total,
        percent: a.total > 0 ? Math.round((a.score / a.total) * 100) : 0,
        completedAt: a.completedAt,
      })),
    });
  } catch (err) {
    res.status(500).json({ message: "Erreur." });
  }
});

module.exports = router;
