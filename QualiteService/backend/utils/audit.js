// backend/utils/audit.js
const AuditLog = require("../models/AuditLog");

async function logAudit(req, { action, targetType, targetId, metadata = {} }) {
  try {
    const actor = req.user?.id || req.user?._id;
    if (!actor) return;

    await AuditLog.create({
      action,
      actor,
      actorRole: req.user?.role || "",
      targetType,
      targetId,
      metadata,
      ip: (req.headers["x-forwarded-for"] || req.socket?.remoteAddress || "").toString().slice(0, 120),
      userAgent: (req.headers["user-agent"] || "").toString().slice(0, 240),
    });
  } catch (e) {
    // do not block business operation
  }
}

module.exports = { logAudit };
