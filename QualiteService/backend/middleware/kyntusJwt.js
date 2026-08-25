const jwt = require("jsonwebtoken");

const ROLE_URI = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
const EMAIL_URI = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress";
const NAME_URI = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";
const NAMEID_URI = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";

function firstClaim(decoded, ...keys) {
  for (const key of keys) {
    const v = decoded?.[key];
    if (typeof v === "string" && v.trim()) return v.trim();
    if (Array.isArray(v) && v.length) {
      const s = String(v[0] || "").trim();
      if (s) return s;
    }
  }
  return "";
}

function getJwtSecret() {
  return (
    process.env.JWT_SECRET ||
    process.env.JwtSettings__Secret ||
    process.env.SECRET ||
    ""
  );
}

function getJwtIssuer() {
  return process.env.JWT_ISSUER || process.env.JwtSettings__Issuer || "AuthService";
}

function getJwtAudience() {
  return process.env.JWT_AUDIENCE || process.env.JwtSettings__Audience || "AuthServiceClient";
}

function verifyKyntusToken(rawToken) {
  const secret = getJwtSecret();
  if (!secret) {
    const err = new Error("JWT_SECRET manquant");
    err.code = "CONFIG";
    throw err;
  }
  const token = String(rawToken || "").replace(/^Bearer\s+/i, "").trim();
  if (!token) {
    const err = new Error("Token manquant");
    err.code = "NO_TOKEN";
    throw err;
  }
  return jwt.verify(token, secret, {
    algorithms: ["HS256"],
    issuer: getJwtIssuer(),
    audience: getJwtAudience(),
    clockTolerance: 0,
  });
}

function extractKyntusClaims(decoded) {
  const subjectId = firstClaim(decoded, "sub");
  const email = firstClaim(decoded, EMAIL_URI, "email", "Email").toLowerCase();
  const name = firstClaim(decoded, NAME_URI, "unique_name", "name", "given_name");
  const role = firstClaim(decoded, ROLE_URI, "role", "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
  const nameId = firstClaim(decoded, NAMEID_URI, "nameid");
  return { subjectId, email, name, role, nameId, raw: decoded };
}

module.exports = {
  getJwtSecret,
  getJwtIssuer,
  getJwtAudience,
  verifyKyntusToken,
  extractKyntusClaims,
};
