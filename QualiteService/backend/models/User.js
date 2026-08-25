const mongoose = require("mongoose");

const userSchema = new mongoose.Schema(
  {
    name: {
      type: String,
      required: true,
      trim: true,
    },

    email: {
      type: String,
      required: true,
      unique: true,
      lowercase: true,
      trim: true,
    },

    passwordHash: {
      type: String,
      default: "",
    },

    role: {
      type: String,
      enum: ["admin", "cq", "pilote", "management", "formateur"],
      default: "pilote",
    },

    myKyntusRole: {
      type: String,
      default: "",
    },

    subjectId: {
      type: String,
      default: "",
      index: true,
    },

    employeeId: {
      type: String,
      default: "",
      index: true,
    },

    active: {
      type: Boolean,
      default: true,
    },

    cell: {
      type: String,
      default: "",
    },

    celluleId: { type: String, default: "" },
    serviceId: { type: String, default: "" },
    poleId: { type: String, default: "" },
    businessDepartmentId: { type: String, default: "" },

    assignedGrids: [
      {
        type: mongoose.Schema.Types.ObjectId,
        ref: "Grid",
      },
    ],

    mergedIntoId: {
      type: mongoose.Schema.Types.ObjectId,
      ref: "User",
      default: null,
    },
  },
  { timestamps: true }
);

userSchema.index({ role: 1, active: 1 });
userSchema.index({ cell: 1 });
userSchema.index({ celluleId: 1 });

module.exports = mongoose.model("User", userSchema);
