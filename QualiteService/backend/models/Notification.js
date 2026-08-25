const mongoose = require("mongoose");

const NotificationSchema = new mongoose.Schema(
  {
    type: { type: String, enum: ["information", "notification", "alerte"], required: true },
    title: { type: String, default: "" },
    message: { type: String, required: true },

    // targeting
    targetAll: { type: Boolean, default: false },
    targetCells: { type: [String], default: [] },
    targetUsers: [{ type: mongoose.Schema.Types.ObjectId, ref: "User" }],

    // Lecture par utilisateur (ajout non cassant pour la DB existante)
    readBy: [{ type: mongoose.Schema.Types.ObjectId, ref: "User", default: [] }],

    createdBy: { type: mongoose.Schema.Types.ObjectId, ref: "User", required: true },

    // Données optionnelles pour permettre une action (ex: ouvrir une évaluation)
    meta: { type: mongoose.Schema.Types.Mixed, default: {} },
  },
  { timestamps: true }
);

NotificationSchema.index({ targetAll: 1, createdAt: -1 });
NotificationSchema.index({ targetUsers: 1, createdAt: -1 });

module.exports = mongoose.model("Notification", NotificationSchema);
