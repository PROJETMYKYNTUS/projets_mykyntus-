const express = require("express");
const fs = require("fs");
const path = require("path");
const axios = require("axios");

const {
  RECORDINGS_BASE_URL,
  AUDIO_BASE_PATH,
  isHttpMode,
  httpAuth,
} = require("../config/recordings");

const auth = require("../middleware/auth");
const permit = require("../middleware/roles");

const router = express.Router();
router.use(auth);
router.use(permit("cq", "management", "admin"));

const ALLOWED_EXTENSIONS = new Set([".mp3", ".wav", ".m4a", ".ogg", ".flac"]);

function isValidDateFolder(value) {
  return /^\d{4}-\d{2}-\d{2}$/.test(String(value));
}

function parseAudioFilename(filename) {
  const ext = path.extname(filename);
  const base = path.basename(filename, ext);
  const parts = base.split("_");

  return {
    filename,
    extension: ext,
    caller: parts[0] || "",
    callee: parts[1] || "",
    datetimeRaw: parts[2] || "",
    source: parts.slice(3).join("_") || "",
  };
}

/* =========================================================================
 *  HTTP MODE — appelle la nouvelle application d'enregistrement
 *  http://enreg.kyntus.fr:8085 (configurable via RECORDINGS_BASE_URL)
 *
 *  NOTE : les routes ci-dessous supposent la convention :
 *    GET /api/dates                          -> ["2026-03-12", ...]
 *    GET /api/calls?date=YYYY-MM-DD&search=  -> [{ filename, caller, callee, ... }, ...]
 *    GET /api/stream/:date/:filename         -> stream du fichier audio
 *
 *  Adaptez ici si la nouvelle app expose des chemins différents.
 * ========================================================================= */

async function httpGetDates() {
  const url = `${RECORDINGS_BASE_URL}/api/dates`;
  const r = await axios.get(url, { auth: httpAuth(), timeout: 15000 });
  return Array.isArray(r.data) ? r.data : [];
}

async function httpGetCalls(date, search) {
  const url = `${RECORDINGS_BASE_URL}/api/calls`;
  const r = await axios.get(url, {
    params: { date, search: search || "" },
    auth: httpAuth(),
    timeout: 20000,
  });
  return Array.isArray(r.data) ? r.data : [];
}

async function httpStreamCall(date, filename, req, res) {
  const url = `${RECORDINGS_BASE_URL}/api/stream/${encodeURIComponent(date)}/${encodeURIComponent(filename)}`;
  const upstream = await axios.get(url, {
    auth: httpAuth(),
    responseType: "stream",
    timeout: 0,
    headers: req.headers.range ? { Range: req.headers.range } : {},
    validateStatus: () => true,
  });

  res.status(upstream.status);
  // forward useful headers
  const passHeaders = ["content-type", "content-length", "accept-ranges", "content-range"];
  for (const h of passHeaders) {
    if (upstream.headers[h]) res.setHeader(h, upstream.headers[h]);
  }
  upstream.data.pipe(res);
}

/* =========================================================================
 *  LOCAL MODE — lit les fichiers sur le filesystem (NAS monté)
 *  Comportement historique : conservé en fallback / pour rétro-compatibilité.
 * ========================================================================= */

function localGetDates() {
  if (!fs.existsSync(AUDIO_BASE_PATH)) return [];
  return fs
    .readdirSync(AUDIO_BASE_PATH, { withFileTypes: true })
    .filter((entry) => entry.isDirectory() && isValidDateFolder(entry.name))
    .map((entry) => entry.name)
    .sort((a, b) => b.localeCompare(a));
}

function localGetCalls(date, search) {
  const targetDir = path.join(AUDIO_BASE_PATH, String(date));
  if (!fs.existsSync(targetDir)) return [];

  return fs
    .readdirSync(targetDir, { withFileTypes: true })
    .filter((entry) => entry.isFile())
    .map((entry) => entry.name)
    .filter((name) => ALLOWED_EXTENSIONS.has(path.extname(name).toLowerCase()))
    .filter((name) => name.toLowerCase().includes(String(search || "").toLowerCase()))
    .sort((a, b) => a.localeCompare(b))
    .map((filename) => {
      const parsed = parseAudioFilename(filename);
      return {
        ...parsed,
        date: String(date),
        streamUrl: `/calls/stream/${encodeURIComponent(String(date))}/${encodeURIComponent(filename)}`,
      };
    });
}

function localStreamCall(date, filename, req, res) {
  const safeFilename = path.basename(filename);
  const filePath = path.join(AUDIO_BASE_PATH, date, safeFilename);

  const resolvedBase = path.resolve(AUDIO_BASE_PATH);
  const resolvedFile = path.resolve(filePath);

  if (!resolvedFile.startsWith(resolvedBase)) {
    return res.status(403).json({ message: "Chemin interdit." });
  }
  if (!fs.existsSync(resolvedFile)) {
    return res.status(404).json({ message: "Fichier introuvable." });
  }

  const ext = path.extname(resolvedFile).toLowerCase();
  const contentTypes = {
    ".mp3": "audio/mpeg",
    ".wav": "audio/wav",
    ".m4a": "audio/mp4",
    ".ogg": "audio/ogg",
    ".flac": "audio/flac",
  };

  const stat = fs.statSync(resolvedFile);
  const range = req.headers.range;

  res.setHeader("Content-Type", contentTypes[ext] || "application/octet-stream");
  res.setHeader("Accept-Ranges", "bytes");

  if (!range) {
    res.setHeader("Content-Length", stat.size);
    return fs.createReadStream(resolvedFile).pipe(res);
  }

  const parts = range.replace(/bytes=/, "").split("-");
  const start = parseInt(parts[0], 10);
  const end = parts[1] ? parseInt(parts[1], 10) : stat.size - 1;

  if (Number.isNaN(start) || Number.isNaN(end) || start > end || end >= stat.size) {
    return res.status(416).end();
  }

  res.status(206);
  res.setHeader("Content-Range", `bytes ${start}-${end}/${stat.size}`);
  res.setHeader("Content-Length", end - start + 1);

  return fs.createReadStream(resolvedFile, { start, end }).pipe(res);
}

/* =========================================================================
 *  ROUTES — dispatchent vers HTTP ou LOCAL selon RECORDINGS_MODE
 * ========================================================================= */

// GET /api/calls/dates
router.get("/dates", async (req, res) => {
  try {
    if (isHttpMode()) {
      const dates = await httpGetDates();
      return res.json(dates);
    }
    return res.json(localGetDates());
  } catch (error) {
    console.error("GET /api/calls/dates error:", error?.message || error);
    return res.status(500).json({
      message: "Erreur lors de la lecture des dates d'appels.",
      error: error.message,
      hint: isHttpMode()
        ? `Vérifie l'endpoint ${RECORDINGS_BASE_URL}/api/dates et son format de réponse.`
        : undefined,
    });
  }
});

// GET /api/calls?date=2026-03-12&search=0180432030
router.get("/", async (req, res) => {
  try {
    const { date, search = "" } = req.query;

    if (!date || !isValidDateFolder(date)) {
      return res.status(400).json({
        message: "Paramètre date invalide. Format attendu : YYYY-MM-DD",
      });
    }

    if (isHttpMode()) {
      const items = await httpGetCalls(date, search);
      // normalise vers le format attendu par le front
      const normalized = items.map((c) => {
        const filename = c.filename || c.file || "";
        return {
          ...parseAudioFilename(filename),
          ...c,
          date: String(date),
          streamUrl:
            c.streamUrl ||
            `/calls/stream/${encodeURIComponent(String(date))}/${encodeURIComponent(filename)}`,
        };
      });
      return res.json(normalized);
    }

    return res.json(localGetCalls(date, search));
  } catch (error) {
    console.error("GET /api/calls error:", error?.message || error);
    return res.status(500).json({
      message: "Erreur lors de la lecture des audios.",
      error: error.message,
      hint: isHttpMode()
        ? `Vérifie l'endpoint ${RECORDINGS_BASE_URL}/api/calls?date=...`
        : undefined,
    });
  }
});

// GET /api/calls/stream/:date/:filename
router.get("/stream/:date/:filename", async (req, res) => {
  try {
    const { date, filename } = req.params;
    if (!isValidDateFolder(date)) {
      return res.status(400).json({ message: "Date invalide." });
    }

    if (isHttpMode()) {
      return await httpStreamCall(date, filename, req, res);
    }
    return localStreamCall(date, filename, req, res);
  } catch (error) {
    console.error("GET /api/calls/stream error:", error?.message || error);
    if (!res.headersSent) {
      return res.status(500).json({
        message: "Erreur lors du streaming audio.",
        error: error.message,
      });
    }
  }
});

module.exports = router;

