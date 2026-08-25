/**
 * Smoke mapping rôles MyKyntus → capacités KCQ + JWT (sans Mongo).
 * node scripts/role-map-smoke.js
 */
process.env.JWT_SECRET = process.env.JWT_SECRET || "smoke-secret";
process.env.JWT_ISSUER = process.env.JWT_ISSUER || "AuthService";
process.env.JWT_AUDIENCE = process.env.JWT_AUDIENCE || "AuthServiceClient";

const jwt = require("jsonwebtoken");
const { mapMyKyntusRole, hasKcqRole } = require("../middleware/kcqRoles");
const { verifyKyntusToken, extractKyntusClaims } = require("../middleware/kyntusJwt");

const cases = [
  ["Qualiticien", "cq", "all"],
  ["Admin", "admin", "all"],
  ["RH", "admin", "all"],
  ["Superviseur", "management", "superviseur"],
  ["Coach", "management", "coach"],
  ["Référent technique", "management", "coach"],
  ["Manager", "management", "pole"],
  ["RP", "management", "department"],
  ["Chef de projet", "management", "department"],
  ["Pilote", "pilote", "self"],
  ["Employee", "pilote", "self"],
  ["Formateur", "pilote", "none"],
];

let failed = 0;
for (const [role, kcq, mode] of cases) {
  const m = mapMyKyntusRole(role);
  const okRole = m.kcqRole === kcq;
  const okMode = m.scopeMode === mode;
  const coachActsAsSuperviseur =
    role === "Coach" || role === "Référent technique"
      ? hasKcqRole({ kcqRoles: m.kcqRoles }, "management")
      : true;
  if (!okRole || !okMode || !coachActsAsSuperviseur) {
    failed += 1;
    console.error("FAIL", role, m);
  } else {
    console.log("OK  ", role, "->", m.kcqRole, m.scopeMode, m.kcqRoles.join("+"));
  }
}

function expectThrow(label, fn, code) {
  try {
    fn();
    failed += 1;
    console.error("FAIL", label, "(aurait dû lever)");
  } catch (e) {
    if (code && e.code !== code) {
      failed += 1;
      console.error("FAIL", label, e.code, e.message);
    } else {
      console.log("OK  ", label);
    }
  }
}

expectThrow("JWT sans token → 401", () => verifyKyntusToken(""), "NO_TOKEN");
expectThrow("JWT invalide", () => verifyKyntusToken("not-a-jwt"));

const shortClaims = jwt.sign(
  { sub: "11111111-1111-1111-1111-111111111111", email: "qualiticien@kyntus.ma", role: "Qualiticien", unique_name: "Qualiticien Demo" },
  process.env.JWT_SECRET,
  { algorithm: "HS256", issuer: "AuthService", audience: "AuthServiceClient", expiresIn: "1h" }
);
const shortDecoded = verifyKyntusToken(shortClaims);
const extractedShort = extractKyntusClaims(shortDecoded);
if (extractedShort.role !== "Qualiticien" || extractedShort.email !== "qualiticien@kyntus.ma") {
  failed += 1;
  console.error("FAIL claims courts", extractedShort);
} else {
  console.log("OK   JWT claims courts (role/email)");
}

const uriClaims = jwt.sign(
  {
    sub: "22222222-2222-2222-2222-222222222222",
    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress": "superviseur@kyntus.ma",
    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name": "Superviseur Demo",
    "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "Superviseur",
  },
  process.env.JWT_SECRET,
  { algorithm: "HS256", issuer: "AuthService", audience: "AuthServiceClient", expiresIn: "1h" }
);
const extractedUri = extractKyntusClaims(verifyKyntusToken(uriClaims));
if (extractedUri.role !== "Superviseur" || extractedUri.email !== "superviseur@kyntus.ma") {
  failed += 1;
  console.error("FAIL claims URI", extractedUri);
} else {
  console.log("OK   JWT claims URI (ClaimTypes.Role/Email)");
}

const badIss = jwt.sign({ sub: "x", role: "Admin" }, process.env.JWT_SECRET, {
  algorithm: "HS256",
  issuer: "Other",
  audience: "AuthServiceClient",
  expiresIn: "1h",
});
expectThrow("JWT issuer refusé", () => verifyKyntusToken(badIss));

if (failed) {
  console.error(`${failed} cas en échec`);
  process.exit(1);
}
console.log("Smoke rôles + JWT Qualité: OK");
