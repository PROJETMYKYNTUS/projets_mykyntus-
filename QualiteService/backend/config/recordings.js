/**
 * Configuration centralisée des enregistrements audio.
 *
 * Modes disponibles :
 *  - mysql : lit les métadonnées dans MySQL et les fichiers sur le NAS
 *  - local : analyse directement les fichiers du NAS
 *  - http  : interroge une application distante
 */

const mysql = require("mysql2/promise");

const RECORDINGS_MODE = (
  process.env.RECORDINGS_MODE || "mysql"
).toLowerCase();

const RECORDINGS_BASE_URL = (
  process.env.RECORDINGS_BASE_URL ||
  "http://enreg.kyntus.fr:8085"
).replace(/\/+$/, "");

const RECORDINGS_HTTP_USER =
  process.env.RECORDINGS_HTTP_USER || "";

const RECORDINGS_HTTP_PASS =
  process.env.RECORDINGS_HTTP_PASS || "";

const AUDIO_BASE_PATH =
  process.env.AUDIO_BASE_PATH ||
  process.env.PICKING_AUDIO_DIR ||
  "/app/audio_mails";

const DB_HOST = String(process.env.DB_HOST || "").trim();
const DB_PORT = Number(process.env.DB_PORT || 3306);
const DB_NAME =
  process.env.DB_NAME || "enregistrement_audio";
const DB_USER = process.env.DB_USER || "kpi";
const DB_PASSWORD = process.env.DB_PASSWORD || "";

let mysqlPool = null;

function isHttpMode() {
  return RECORDINGS_MODE === "http";
}

function isLocalMode() {
  return RECORDINGS_MODE === "local";
}

function isMysqlMode() {
  return RECORDINGS_MODE === "mysql";
}

function isMysqlConfigured() {
  return DB_HOST.length > 0;
}

/** mysql seulement si un hôte est défini ; sinon lecture locale du volume NAS / audio_mails. */
function resolvePickingSource() {
  if (isHttpMode()) return "http";
  if (isMysqlMode() && isMysqlConfigured()) return "mysql";
  return "local";
}

function httpAuth() {
  if (!RECORDINGS_HTTP_USER) return undefined;

  return {
    username: RECORDINGS_HTTP_USER,
    password: RECORDINGS_HTTP_PASS,
  };
}

function getMysqlPool() {
  if (!isMysqlConfigured()) {
    throw new Error("MySQL picking non configuré (DB_HOST / KYNTUS_KCQ_DB_HOST vide).");
  }
  if (!mysqlPool) {
    mysqlPool = mysql.createPool({
      host: DB_HOST,
      port: DB_PORT,
      user: DB_USER,
      password: DB_PASSWORD,
      database: DB_NAME,

      waitForConnections: true,
      connectionLimit: 10,
      queueLimit: 0,

      charset: "utf8mb4",
      dateStrings: true,
      enableKeepAlive: true,
      keepAliveInitialDelay: 0,
    });
  }

  return mysqlPool;
}

module.exports = {
  RECORDINGS_MODE,
  RECORDINGS_BASE_URL,
  RECORDINGS_HTTP_USER,
  RECORDINGS_HTTP_PASS,
  AUDIO_BASE_PATH,

  DB_HOST,
  DB_PORT,
  DB_NAME,
  DB_USER,

  isHttpMode,
  isLocalMode,
  isMysqlMode,
  isMysqlConfigured,
  resolvePickingSource,

  httpAuth,
  getMysqlPool,
};
