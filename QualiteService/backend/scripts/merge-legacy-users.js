/**
 * Fusionne les agents historiques du dump KCQ sur les employés MyKyntus.
 *
 * Dry-run (défaut) :
 *   node scripts/merge-legacy-users.js
 * Application :
 *   node scripts/merge-legacy-users.js --apply
 */
require("dotenv").config();
const mongoose = require("mongoose");

const User = require("../models/User");
const Score = require("../models/Score");
const Coaching = require("../models/Coaching");
const Notification = require("../models/Notification");
const AuditLog = require("../models/AuditLog");

const APPLY = process.argv.includes("--apply");

function normalizeName(s) {
  return String(s || "")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, " ")
    .trim()
    .split(/\s+/)
    .filter(Boolean)
    .sort()
    .join(" ");
}

function localPart(email) {
  return normalizeName(String(email || "").split("@")[0]);
}

function isLegacy(u) {
  return !String(u.myKyntusRole || "").trim();
}

async function countRefs(id) {
  const oid = new mongoose.Types.ObjectId(String(id));
  const [scoresPilot, scoresEval, coachPilot, coachCoach, notif, audit] = await Promise.all([
    Score.countDocuments({ pilot: oid }),
    Score.countDocuments({ evaluator: oid }),
    Coaching.countDocuments({ pilot: oid }),
    Coaching.countDocuments({ coach: oid }),
    Notification.countDocuments({ $or: [{ targetUsers: oid }, { createdBy: oid }] }),
    AuditLog.countDocuments({ actor: oid }),
  ]);
  return {
    scoresPilot,
    scoresEval,
    coachPilot,
    coachCoach,
    notif,
    audit,
    total: scoresPilot + scoresEval + coachPilot + coachCoach + notif + audit,
  };
}

async function reassign(fromId, toId) {
  const from = new mongoose.Types.ObjectId(String(fromId));
  const to = new mongoose.Types.ObjectId(String(toId));
  const [sp, se, cp, cc, nt, cb, na] = await Promise.all([
    Score.updateMany({ pilot: from }, { $set: { pilot: to } }),
    Score.updateMany({ evaluator: from }, { $set: { evaluator: to } }),
    Coaching.updateMany({ pilot: from }, { $set: { pilot: to } }),
    Coaching.updateMany({ coach: from }, { $set: { coach: to } }),
    Notification.updateMany({ targetUsers: from }, { $addToSet: { targetUsers: to } }),
    Notification.updateMany({ createdBy: from }, { $set: { createdBy: to } }),
    AuditLog.updateMany({ actor: from }, { $set: { actor: to } }),
  ]);
  await Coaching.updateMany({ evaluator: from }, { $set: { evaluator: to } });
  await Notification.updateMany({ targetUsers: from }, { $pull: { targetUsers: from } });
  return {
    scoresPilot: sp.modifiedCount,
    scoresEval: se.modifiedCount,
    coachPilot: cp.modifiedCount,
    coachCoach: cc.modifiedCount,
    notifTargets: nt.modifiedCount,
    notifCreated: cb.modifiedCount,
    audit: na.modifiedCount,
  };
}

async function main() {
  const uri = process.env.MONGO_URI || "mongodb://127.0.0.1:27017/kcq";
  await mongoose.connect(uri);

  const users = await User.find({}).lean();
  const directory = users.filter((u) => !isLegacy(u));
  const legacy = users.filter((u) => isLegacy(u));

  const byName = new Map();
  const byLocal = new Map();
  for (const d of directory) {
    const n = normalizeName(d.name);
    if (n && !byName.has(n)) byName.set(n, d);
    const loc = localPart(d.email);
    if (loc && !byLocal.has(loc)) byLocal.set(loc, d);
  }

  const matched = [];
  const unmatched = [];
  const usedKeepers = new Set();

  for (const l of legacy) {
    const n = normalizeName(l.name);
    const loc = localPart(l.email);
    let keep = (n && byName.get(n)) || (loc && byLocal.get(loc)) || null;
    if (keep && usedKeepers.has(String(keep._id))) {
      keep = null;
    }
    if (!keep) {
      unmatched.push(l);
      continue;
    }
    usedKeepers.add(String(keep._id));
    const refs = await countRefs(l._id);
    matched.push({
      from: { id: String(l._id), name: l.name, email: l.email, cell: l.cell },
      to: { id: String(keep._id), name: keep.name, email: keep.email, cell: keep.cell },
      via: n && byName.get(n) && String(byName.get(n)._id) === String(keep._id) ? "name" : "emailLocal",
      refs,
    });
  }

  const scoresToMove = matched.reduce((s, m) => s + m.refs.scoresPilot + m.refs.scoresEval, 0);

  console.log(JSON.stringify({
    mode: APPLY ? "apply" : "dry-run",
    directory: directory.length,
    legacy: legacy.length,
    matched: matched.length,
    unmatched: unmatched.length,
    scoresToMove,
    pairs: matched.map((m) => ({
      via: m.via,
      from: `${m.from.name} <${m.from.email}>`,
      to: `${m.to.name} <${m.to.email}>`,
      refs: m.refs,
    })),
    unmatchedSample: unmatched.slice(0, 40).map((u) => `${u.name} | ${u.email} | ${u.cell || ""}`),
  }, null, 2));

  if (!APPLY) {
    console.log("Dry-run. Relancer avec --apply pour écrire.");
    await mongoose.disconnect();
    return;
  }

  let applied = 0;
  for (const m of matched) {
    await reassign(m.from.id, m.to.id);
    await User.updateOne(
      { _id: m.from.id },
      { $set: { active: false, mergedIntoId: m.to.id } }
    );
    applied += 1;
  }

  const unmatchedIds = unmatched.map((u) => u._id);
  if (unmatchedIds.length) {
    await User.updateMany(
      { _id: { $in: unmatchedIds } },
      { $set: { active: false } }
    );
  }

  console.log(JSON.stringify({ applied, unmatchedDeactivated: unmatchedIds.length }, null, 2));
  await mongoose.disconnect();
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
