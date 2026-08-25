import React, { useEffect, useState } from "react";
import api from "../api.js";

function Login({ onLoginSuccess }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [status, setStatus] = useState("");
  const [loading, setLoading] = useState(false);
  const [shake, setShake] = useState(false);
  const [mounted, setMounted] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  useEffect(() => {
    const t = setTimeout(() => setMounted(true), 10);
    return () => clearTimeout(t);
  }, []);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setStatus(""); setLoading(true);
    try {
      const res = await api.post("/auth/login", { email, password });
      const { token, user } = res.data;
      onLoginSuccess(user, token);
    } catch (err) {
      setShake(true);
      setTimeout(() => setShake(false), 450);
      setStatus(err.response?.data?.message || "Email ou mot de passe incorrect.");
    } finally { setLoading(false); }
  };

  return (
    <div style={styles.page}>
      <div style={styles.bgDecor} />
      <div style={{ ...styles.card, opacity: mounted ? 1 : 0, transform: mounted ? "translateY(0) scale(1)" : "translateY(8px) scale(0.98)", ...(shake ? { animation: "login-shake 420ms" } : {}) }}>
        <div style={styles.logoWrap}>
          <div style={styles.logo}>
            <img src="/kyntus.svg" alt="" style={{ width: 28, height: 28 }} />
          </div>
        </div>

        <h2 style={styles.title}>Espace Qualité</h2>
        <p style={styles.subtitle}>Connectez-vous pour accéder à la plateforme</p>

        <form onSubmit={handleSubmit} style={{ marginTop: 24 }}>
          <div style={styles.field}>
            <label style={styles.label}>Adresse email</label>
            <input
              type="email" value={email} onChange={(e) => setEmail(e.target.value)}
              required style={styles.input} placeholder="nom@entreprise.com"
            />
          </div>

          <div style={styles.field}>
            <label style={styles.label}>Mot de passe</label>
            <div style={{ position: "relative" }}>
              <input
                type={showPassword ? "text" : "password"} value={password}
                onChange={(e) => setPassword(e.target.value)}
                required style={styles.input} placeholder="••••••••"
              />
              <button
                type="button" onClick={() => setShowPassword(!showPassword)}
                style={styles.eyeBtn} tabIndex={-1}
              >
                {showPassword ? "🙈" : "👁"}
              </button>
            </div>
          </div>

          <button type="submit" style={{ ...styles.btn, background: loading ? "#94a3b8" : "var(--primary)" }} disabled={loading}>
            {loading ? "Connexion en cours…" : "Se connecter"}
          </button>

          {status && (
            <div style={styles.alert}>
              <span style={{ marginRight: 6 }}>⚠️</span>{status}
            </div>
          )}
        </form>

        <div style={styles.footer}>
          <div>© LK - Kyntus Morocco</div>
          <div style={{ fontSize: 11, color: "#94a3b8" }}>V 3.1.15</div>
        </div>
      </div>

      <style>{`
        @keyframes login-shake {
          0% { transform: translateX(0); }
          25% { transform: translateX(-6px); }
          50% { transform: translateX(6px); }
          75% { transform: translateX(-4px); }
          100% { transform: translateX(0); }
        }
      `}</style>
    </div>
  );
}

const styles = {
  page: {
    minHeight: "100vh",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    padding: 24,
    background: "#f1f4f9",
    position: "relative",
    overflow: "hidden",
  },
  bgDecor: {
    position: "absolute",
    top: -200,
    right: -200,
    width: 600,
    height: 600,
    borderRadius: "50%",
    background: "radial-gradient(circle, rgba(79,70,229,0.08) 0%, transparent 70%)",
    pointerEvents: "none",
  },
  card: {
    width: "min(420px, 92vw)",
    background: "#ffffff",
    borderRadius: 20,
    padding: "36px 32px",
    boxShadow: "0 1px 3px rgba(0,0,0,0.04), 0 20px 50px rgba(0,0,0,0.08)",
    border: "1px solid rgba(15,23,42,0.08)",
    transition: "transform 420ms ease, opacity 420ms ease",
    position: "relative",
    zIndex: 1,
  },
  logoWrap: {
    display: "flex",
    justifyContent: "center",
    marginBottom: 16,
  },
  logo: {
    width: 52,
    height: 52,
    borderRadius: 14,
    background: "#4f46e5",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    boxShadow: "0 8px 24px rgba(79,70,229,0.3)",
  },
  title: {
    margin: "8px 0 4px",
    fontSize: 22,
    fontWeight: 800,
    color: "#0f172a",
    textAlign: "center",
    letterSpacing: "-0.02em",
  },
  subtitle: {
    margin: 0,
    fontSize: 14,
    color: "#64748b",
    textAlign: "center",
  },
  field: { marginBottom: 16, textAlign: "left" },
  label: {
    display: "block",
    fontSize: 13,
    fontWeight: 600,
    color: "#475569",
    marginBottom: 6,
  },
  input: {
    width: "100%",
    padding: "11px 14px",
    borderRadius: 12,
    border: "1px solid rgba(15,23,42,0.14)",
    background: "#f8fafc",
    fontSize: 14,
    color: "#0f172a",
    outline: "none",
    transition: "border-color 200ms, box-shadow 200ms",
  },
  eyeBtn: {
    position: "absolute",
    right: 12,
    top: "50%",
    transform: "translateY(-50%)",
    background: "none",
    border: "none",
    cursor: "pointer",
    fontSize: 16,
    padding: 0,
  },
  btn: {
    width: "100%",
    marginTop: 8,
    padding: 12,
    borderRadius: 12,
    border: "none",
    color: "#ffffff",
    fontWeight: 700,
    fontSize: 15,
    cursor: "pointer",
    transition: "all 200ms",
  },
  alert: {
    marginTop: 14,
    padding: 12,
    borderRadius: 12,
    background: "rgba(220,38,38,0.06)",
    border: "1px solid rgba(220,38,38,0.15)",
    color: "#dc2626",
    fontSize: 13,
    fontWeight: 600,
  },
  footer: {
    marginTop: 24,
    fontSize: 12,
    color: "#64748b",
    textAlign: "center",
    lineHeight: 1.6,
  },
};

export default Login;
