const axios = require("axios");
const User = require("../models/User");
const Cell = require("../models/Cell");
const { mapMyKyntusRole } = require("../middleware/kcqRoles");

const DIRECTORY_BASE_URL = (
  process.env.DIRECTORY_BASE_URL || "http://employee-directory-backend:8080"
).replace(/\/+$/, "");

const CACHE_MS = Number(process.env.DIRECTORY_SYNC_TTL_MS || 60_000);

let lastFullSyncAt = 0;
let fullSyncInFlight = null;
let lastOverview = { data: null, at: 0 };
let syncStatus = {
  lastSyncAt: null,
  lastSyncOk: null,
  lastError: "",
  employees: 0,
  pilotes: 0,
};

function displayName(emp) {
  const n = `${emp?.firstName || emp?.FirstName || ""} ${emp?.lastName || emp?.LastName || ""}`.trim();
  return n || emp?.name || emp?.email || emp?.Email || "Utilisateur";
}

function pick(emp, ...keys) {
  for (const k of keys) {
    if (emp?.[k] != null && String(emp[k]).trim()) return String(emp[k]).trim();
  }
  return "";
}

function asArray(v) {
  return Array.isArray(v) ? v : [];
}

async function directoryGet(path, bearer, params) {
  const res = await axios.get(`${DIRECTORY_BASE_URL}${path}`, {
    headers: bearer ? { Authorization: bearer } : {},
    params,
    timeout: 12_000,
    validateStatus: (s) => s >= 200 && s < 500,
  });
  if (res.status >= 400) return null;
  return res.data;
}

function buildOrgMaps(overview) {
  const cellule = new Map();
  const service = new Map();
  const pole = new Map();
  for (const s of asArray(overview?.services)) {
    const id = pick(s, "id", "Id");
    const name = pick(s, "name", "Name");
    if (id && name) cellule.set(id, name);
  }
  for (const s of asArray(overview?.sousServices)) {
    const id = pick(s, "id", "Id");
    const name = pick(s, "name", "Name");
    if (id && name) service.set(id, name);
  }
  for (const s of asArray(overview?.etages)) {
    const id = pick(s, "id", "Id");
    const name = pick(s, "name", "Name");
    if (id && name) pole.set(id, name);
  }
  return { cellule, service, pole };
}

function enrichEmployee(emp, maps) {
  const celluleId = pick(emp, "celluleId", "CelluleId");
  const fromDto = pick(emp, "celluleName", "CelluleName");
  const celluleName = fromDto || (celluleId && maps?.cellule?.get(celluleId)) || "";
  return { ...emp, celluleName: celluleName || celluleId };
}

async function fetchOverview(bearer) {
  const data = await directoryGet("/api/directory/org/overview", bearer);
  if (data && typeof data === "object") {
    lastOverview = { data, at: Date.now() };
    return data;
  }
  if (lastOverview.data) return lastOverview.data;
  return null;
}

async function getOverviewCached(bearer) {
  if (lastOverview.data && Date.now() - lastOverview.at < CACHE_MS) {
    return lastOverview.data;
  }
  return fetchOverview(bearer);
}

function managedIdsFromOverview(overview, employeeId, kind) {
  if (!employeeId || !overview) return [];
  const id = String(employeeId);
  if (kind === "Superviseur") {
    return [
      ...new Set(
        asArray(overview.supervisorService)
          .filter((x) => String(x.userId || x.employeeId || "") === id)
          .map((x) => String(x.celluleId || x.serviceId || ""))
          .filter(Boolean)
      ),
    ];
  }
  if (kind === "ReferentTechnique") {
    const fromSs = asArray(overview.coachSousService)
      .filter((x) => String(x.userId || x.employeeId || "") === id)
      .map((x) => String(x.serviceId || x.sousServiceId || ""))
      .filter(Boolean);
    const fromPilot = asArray(overview.coachPilot)
      .filter((x) => String(x.userId || x.employeeId || "") === id)
      .map((x) => String(x.serviceId || x.celluleId || ""))
      .filter(Boolean);
    return [...new Set([...fromSs, ...fromPilot])];
  }
  return [];
}

async function loadManagedNodeIds(employeeId, kind, bearer) {
  if (!employeeId) return [];
  const overview = lastOverview.data || (await getOverviewCached(bearer).catch(() => null));
  const fromOverview = managedIdsFromOverview(overview, employeeId, kind);
  if (fromOverview.length) return fromOverview;

  const data = await directoryGet("/api/directory/rebac/managed-nodes", bearer, {
    employeeId,
    kind,
  });
  const ids = data?.nodeIds || data?.NodeIds || [];
  return Array.isArray(ids) ? ids.map(String) : [];
}

async function upsertEmployeeSnapshot(emp, fallbackRole) {
  const email = pick(emp, "email", "Email").toLowerCase();
  if (!email || !email.includes("@")) return null;

  const myRole = pick(emp, "role", "Role") || fallbackRole || "Pilote";
  const mapped = mapMyKyntusRole(myRole);
  const kcqRole = mapped.kcqRoles.length ? mapped.kcqRole : "pilote";

  const celluleId = pick(emp, "celluleId", "CelluleId");
  const maps = lastOverview.data ? buildOrgMaps(lastOverview.data) : null;
  const celluleName =
    pick(emp, "celluleName", "CelluleName") ||
    (celluleId && maps?.cellule?.get(celluleId)) ||
    "";
  const cellLabel = celluleName || celluleId;
  const serviceId = pick(emp, "serviceId", "ServiceId");
  const poleId = pick(emp, "poleId", "PoleId");
  const businessDepartmentId = pick(emp, "businessDepartmentId", "BusinessDepartmentId");
  const employeeId = pick(emp, "id", "Id");
  const name = displayName(emp);

  if (celluleId && cellLabel) {
    await Cell.findOneAndUpdate(
      { name: cellLabel },
      { name: cellLabel, description: celluleId, active: true },
      { upsert: true, new: true }
    ).catch(() => null);
  }

  const doc = await User.findOneAndUpdate(
    { email },
    {
      $set: {
        name,
        email,
        role: kcqRole,
        myKyntusRole: myRole,
        active: true,
        cell: cellLabel,
        celluleId,
        serviceId,
        poleId,
        businessDepartmentId,
        employeeId,
      },
      $setOnInsert: {
        passwordHash: "",
      },
    },
    { upsert: true, new: true }
  );
  return doc;
}

async function renameIdNamedCells(maps) {
  if (!maps?.cellule) return { renamed: 0, deactivated: 0 };
  const idCells = await Cell.find({ name: /^cell-/ });
  let renamed = 0;
  let deactivated = 0;
  for (const c of idCells) {
    const pretty = maps.cellule.get(c.name) || maps.cellule.get(c.description) || "";
    if (!pretty || pretty === c.name) continue;
    const existing = await Cell.findOne({ name: pretty, _id: { $ne: c._id } });
    if (existing) {
      if (!existing.description) existing.description = c.name;
      existing.active = true;
      await existing.save().catch(() => null);
      c.active = false;
      if (!c.description) c.description = c.name;
      await c.save().catch(() => null);
      deactivated += 1;
    } else {
      try {
        c.description = c.description || c.name;
        c.name = pretty;
        c.active = true;
        await c.save();
        renamed += 1;
      } catch (e) {
        console.warn("Cell rename skipped:", c.name, "->", pretty, e?.message || e);
      }
    }
  }
  return { renamed, deactivated };
}

async function syncAllEmployees(bearer) {
  const overview = await fetchOverview(bearer);
  if (!overview) {
    throw new Error("Annuaire indisponible (org/overview).");
  }
  const maps = buildOrgMaps(overview);
  await renameIdNamedCells(maps);
  const list = asArray(overview.employees);
  const docs = [];
  for (const emp of list) {
    const d = await upsertEmployeeSnapshot(enrichEmployee(emp, maps));
    if (d) docs.push(d);
  }
  return { docs, maps, employees: list.length };
}

function getSyncStatus() {
  return { ...syncStatus, running: Boolean(fullSyncInFlight) };
}

async function runScheduledSync(bearer) {
  if (fullSyncInFlight) {
    await fullSyncInFlight.catch(() => null);
    return getSyncStatus();
  }
  fullSyncInFlight = (async () => {
    const { docs, employees } = await syncAllEmployees(bearer);
    const pilotes = docs.filter((d) => d.role === "pilote" && d.active !== false).length;
    lastFullSyncAt = Date.now();
    syncStatus = {
      lastSyncAt: new Date().toISOString(),
      lastSyncOk: true,
      lastError: "",
      employees,
      pilotes,
    };
    console.log(`Directory sync OK: ${employees} employés, ${pilotes} pilotes`);
  })();
  try {
    await fullSyncInFlight;
  } catch (e) {
    syncStatus = {
      ...syncStatus,
      lastSyncAt: new Date().toISOString(),
      lastSyncOk: false,
      lastError: e?.message || String(e),
    };
    console.error("Directory sync failed:", e?.message || e);
  } finally {
    fullSyncInFlight = null;
  }
  return getSyncStatus();
}

function startDirectorySyncScheduler() {
  const interval = Number(process.env.DIRECTORY_SYNC_INTERVAL_MS || 10 * 60 * 1000);
  runScheduledSync().catch((e) => {
    console.error("Directory sync initiale échouée:", e?.message || e);
  });
  if (interval > 0) {
    setInterval(() => {
      runScheduledSync().catch((e) => {
        console.error("Directory sync planifiée échouée:", e?.message || e);
      });
    }, interval);
    console.log(`Directory sync planifiée toutes les ${Math.round(interval / 1000)}s`);
  }
}

function matchesOrg(user, scope) {
  if (!scope || scope.mode === "all") return true;
  if (scope.mode === "none") return false;
  if (scope.mode === "self") {
    return String(user._id) === String(scope.selfId);
  }
  if (scope.mode === "superviseur") {
    return scope.celluleIds.includes(String(user.celluleId || ""));
  }
  if (scope.mode === "coach") {
    if (scope.serviceIds?.length && scope.serviceIds.includes(String(user.serviceId || ""))) return true;
    if (scope.serviceId && String(user.serviceId || "") === scope.serviceId) return true;
    if (scope.celluleId && String(user.celluleId || "") === scope.celluleId) return true;
    return false;
  }
  if (scope.mode === "pole") {
    return String(user.poleId || "") === scope.poleId;
  }
  if (scope.mode === "department") {
    return String(user.businessDepartmentId || "") === scope.departmentId;
  }
  return false;
}

async function resolveCurrentUser(claims, bearer) {
  const mapped = mapMyKyntusRole(claims.role);
  if (!mapped.kcqRoles.length) {
    const err = new Error("Rôle non autorisé sur le module Qualité.");
    err.code = "FORBIDDEN";
    throw err;
  }

  let emp = null;
  let doc = claims.email
    ? await User.findOne({ email: claims.email.toLowerCase() })
    : null;
  const stale = !doc || Date.now() - new Date(doc.updatedAt).getTime() > CACHE_MS;

  if (stale && claims.email) {
    const overview = await getOverviewCached(bearer).catch((e) => {
      console.warn("Directory overview (profil) échouée:", e?.message || e);
      return lastOverview.data;
    });
    const list = asArray(overview?.employees);
    const maps = overview ? buildOrgMaps(overview) : null;
    const raw = list.find((e) => String(e.email || e.Email || "").toLowerCase() === claims.email) || null;
    emp = raw ? enrichEmployee(raw, maps) : null;
  }

  const snapshotSource = emp || {
    email: claims.email,
    firstName: claims.name || doc?.name || claims.email || "Utilisateur",
    lastName: "",
    role: claims.role,
    celluleId: doc?.celluleId,
    celluleName: doc?.cell,
    serviceId: doc?.serviceId,
    poleId: doc?.poleId,
    businessDepartmentId: doc?.businessDepartmentId,
    id: doc?.employeeId,
  };

  doc = await upsertEmployeeSnapshot(snapshotSource, claims.role);
  if (!doc) {
    const err = new Error("Impossible de résoudre le profil Qualité (email JWT manquant).");
    err.code = "NO_PROFILE";
    throw err;
  }

  if (claims.subjectId) {
    doc.subjectId = claims.subjectId;
    await doc.save().catch(() => null);
  }

  const employeeId = doc.employeeId || "";
  let managedCelluleIds = [];
  let managedServiceIds = [];
  if (mapped.scopeMode === "superviseur") {
    if (employeeId) {
      managedCelluleIds = await loadManagedNodeIds(employeeId, "Superviseur", bearer);
    }
    if (!managedCelluleIds.length && doc.celluleId) managedCelluleIds = [String(doc.celluleId)];
  }
  if (mapped.scopeMode === "coach") {
    if (employeeId) {
      managedServiceIds = await loadManagedNodeIds(employeeId, "ReferentTechnique", bearer);
    }
  }

  const scope = {
    mode: mapped.scopeMode,
    selfId: String(doc._id),
    celluleId: doc.celluleId || "",
    serviceId: doc.serviceId || "",
    poleId: doc.poleId || "",
    departmentId: doc.businessDepartmentId || "",
    celluleIds: managedCelluleIds.map(String),
    serviceIds: managedServiceIds.map(String),
  };

  return { doc, mapped, scope };
}

async function listAllowedPilotIds(scope) {
  if (!scope || scope.mode === "all") return null;
  if (scope.mode === "none") return [];
  if (scope.mode === "self") return [scope.selfId];

  const pilots = await User.find({ role: "pilote", active: { $ne: false } })
    .select("_id celluleId serviceId poleId businessDepartmentId")
    .lean();
  return pilots.filter((p) => matchesOrg(p, scope)).map((p) => String(p._id));
}

module.exports = {
  resolveCurrentUser,
  listAllowedPilotIds,
  matchesOrg,
  syncAllEmployees,
  runScheduledSync,
  startDirectorySyncScheduler,
  getSyncStatus,
  renameIdNamedCells,
  buildOrgMaps,
};
