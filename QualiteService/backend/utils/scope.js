const mongoose = require("mongoose");

function objectIds(ids) {
  return (ids || [])
    .map((id) => String(id))
    .filter((id) => mongoose.Types.ObjectId.isValid(id))
    .map((id) => new mongoose.Types.ObjectId(id));
}

function applyPilotScopeToUserQuery(where, req) {
  const ids = req.allowedPilotIds;
  if (ids == null) return where;
  const oid = objectIds(ids);
  where._id = { $in: oid };
  return where;
}

function applyScorePilotScope(match, req) {
  const ids = req.allowedPilotIds;
  if (ids == null) return match;
  const oid = objectIds(ids);
  if (match.pilot && match.pilot.$in) {
    const allowed = new Set(oid.map(String));
    match.pilot.$in = match.pilot.$in.filter((x) => allowed.has(String(x)));
    return match;
  }
  if (match.pilot) {
    const ok = oid.some((x) => String(x) === String(match.pilot));
    match.pilot = ok ? match.pilot : { $in: [] };
    return match;
  }
  match.pilot = { $in: oid };
  return match;
}

async function assertPilotInScope(req, pilotId) {
  if (req.allowedPilotIds == null) return true;
  return req.allowedPilotIds.map(String).includes(String(pilotId));
}

module.exports = {
  applyPilotScopeToUserQuery,
  applyScorePilotScope,
  assertPilotInScope,
};
