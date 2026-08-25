const mongoose = require("mongoose");

const QuestionSchema = new mongoose.Schema({
  question: { type: String, required: true },
  imageData: { type: String, default: "" },
  options: [{ type: String }],
  correctIndex: { type: Number, default: 0 },
}, { _id: true });

const QuizAttemptSchema = new mongoose.Schema({
  user: { type: mongoose.Schema.Types.ObjectId, ref: "User", required: true },
  answers: [{ type: Number }],
  score: { type: Number, default: 0 },
  total: { type: Number, default: 0 },
  completedAt: { type: Date, default: Date.now },
}, { _id: true });

const TrainingSchema = new mongoose.Schema({
  title: { type: String, required: true },
  description: { type: String, default: "" },
  pdfUrl: { type: String, default: "" },
  pdfData: { type: String, default: "" },
  videoUrl: { type: String, default: "" },
  category: { type: String, default: "" },
  // Target: roles, cells, individual users (all empty = visible to everyone)
  roles: [{ type: String }],
  targetCells: [{ type: String }],
  targetUsers: [{ type: mongoose.Schema.Types.ObjectId, ref: "User" }],
  active: { type: Boolean, default: true },
  // Quiz settings
  allowMultipleAttempts: { type: Boolean, default: true },
  passThreshold: { type: Number, default: 80 },
  questions: [QuestionSchema],
  attempts: [QuizAttemptSchema],
  createdBy: { type: mongoose.Schema.Types.ObjectId, ref: "User" },
}, { timestamps: true });

TrainingSchema.index({ active: 1, createdAt: -1 });

module.exports = mongoose.model("Training", TrainingSchema);
