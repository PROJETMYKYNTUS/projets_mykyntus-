const NORM = (s) => String(s || "").trim();

function isReferentTechnique(role) {
  const r = NORM(role);
  return r === "Référent technique" || r === "Referent technique" || r === "Coach";
}

function isChefDeProjet(role) {
  const r = NORM(role);
  return r === "Chef de projet" || r === "RP";
}

function isPilote(role) {
  const r = NORM(role);
  return r === "Pilote" || r === "Employee";
}

function isAdminRh(role) {
  const r = NORM(role);
  return r === "Admin" || r === "RH";
}

/**
 * Mappe un rôle JWT MyKyntus vers les capacités KCQ historiques
 * (admin | cq | management | pilote). Formateur n'a pas d'accès CQ.
 */
function mapMyKyntusRole(myRole) {
  const r = NORM(myRole);
  if (isAdminRh(r)) {
    return { kcqRole: "admin", kcqRoles: ["admin", "management", "cq"], scopeMode: "all" };
  }
  if (r === "Qualiticien") {
    return { kcqRole: "cq", kcqRoles: ["cq"], scopeMode: "all" };
  }
  if (r === "Superviseur") {
    return { kcqRole: "management", kcqRoles: ["management"], scopeMode: "superviseur" };
  }
  if (isReferentTechnique(r)) {
    return { kcqRole: "management", kcqRoles: ["management"], scopeMode: "coach" };
  }
  if (r === "Manager") {
    return { kcqRole: "management", kcqRoles: ["management"], scopeMode: "pole" };
  }
  if (isChefDeProjet(r)) {
    return { kcqRole: "management", kcqRoles: ["management"], scopeMode: "department" };
  }
  if (isPilote(r)) {
    return { kcqRole: "pilote", kcqRoles: ["pilote"], scopeMode: "self" };
  }
  return { kcqRole: "pilote", kcqRoles: [], scopeMode: "none" };
}

function hasKcqRole(user, ...allowed) {
  if (!user) return false;
  const roles = Array.isArray(user.kcqRoles) && user.kcqRoles.length
    ? user.kcqRoles
    : [user.role].filter(Boolean);
  return allowed.some((a) => roles.includes(a));
}

module.exports = {
  mapMyKyntusRole,
  hasKcqRole,
  isReferentTechnique,
  isChefDeProjet,
  isPilote,
  isAdminRh,
};
