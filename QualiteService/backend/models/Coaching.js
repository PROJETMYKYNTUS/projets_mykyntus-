const mongoose = require("mongoose");

/**
 * Coaching
 * - Links to a Score (evaluation)
 * - Backward compatible: pilot is duplicated for easier search even if score is later removed
 */
const CoachingSchema = new mongoose.Schema(
  {
    score: { type: mongoose.Schema.Types.ObjectId, ref: "Score", required: true },
    pilot: { type: mongoose.Schema.Types.ObjectId, ref: "User", required: true },
    evaluator: { type: mongoose.Schema.Types.ObjectId, ref: "User", required: false },
    coach: { type: mongoose.Schema.Types.ObjectId, ref: "User", required: true },

    notes: { type: String, default: "" },
    actionPlan: { type: String, default: "" },
    status: {
      type: String,
      enum: ["open", "in_progress", "done"],
      default: "open",
    },
    followUpDate: { type: Date, default: null },

    // Pilot acknowledgment
    pilotAcknowledged: { type: Boolean, default: false },
    pilotComment: { type: String, default: "" },
    pilotAcknowledgedAt: { type: Date, default: null },
  },
  { timestamps: true }
);

CoachingSchema.index({ pilot: 1, createdAt: -1 });
CoachingSchema.index({ coach: 1, createdAt: -1 });
CoachingSchema.index({ status: 1 });

module.exports = mongoose.model("Coaching", CoachingSchema);
