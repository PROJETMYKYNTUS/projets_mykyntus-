const { hasKcqRole } = require("./kcqRoles");

module.exports = function permit(...allowedRoles) {
  return (req, res, next) => {
    if (!req.user) return res.status(401).json({ message: "Non authentifié" });
    if (!hasKcqRole(req.user, ...allowedRoles)) {
      return res.status(403).json({ message: "Accès interdit" });
    }
    next();
  };
};
