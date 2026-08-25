// backend/models/AuditLog.js
const mongoose = require("mongoose");

const AuditLogSchema = new mongoose.Schema(
  {
    action: { type: String, required: true, index: true }, // e.g. CONTEST, REEVALUATE, DELETE_EVALUATION
    actor: { type: mongoose.Schema.Types.ObjectId, ref: "User", required: true, index: true },
    actorRole: { type: String, default: "" },
    targetType: { type: String, required: true, index: true }, // "score", "grid", ...
    targetId: { type: mongoose.Schema.Types.ObjectId, required: true, index: true },
    metadata: { type: Object, default: {} },
    ip: { type: String, default: "" },
    userAgent: { type: String, default: "" },
  },
  { timestamps: true }
);

module.exports = mongoose.model("AuditLog", AuditLogSchema);
