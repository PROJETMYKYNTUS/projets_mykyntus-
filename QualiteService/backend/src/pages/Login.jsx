// src/pages/Login.jsx
import React, { useEffect, useMemo, useState } from "react";
import api from "../api.js";

// LOGO ICI
// 1) fichier du logo : src/assets/logo.png
// 2) décommentez la ligne ci-dessous et ajustez le chemin
// import logo from "../assets/logo.png";

function Login({ onLoginSuccess }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");

  const [status, setStatus] = useState("");
  const [loading, setLoading] = useState(false);
  const [shake, setShake] = useState(false);

  const [mounted, setMounted] = useState(false);
  useEffect(() => {
    const t = setTimeout(() => setMounted(true), 10);
    return () => clearTimeout(t);
  }, []);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setStatus("");
    setLoading(true);

    try {
      const res = await api.post("/auth/login", { email, password });
      const { token, user } = res.data;
      onLoginSuccess(user, token);
    } catch (err) {
      setShake(true);
      setTimeout(() => setShake(false), 450);

      if (err.response?.data?.message) {
        setStatus("❌ " + err.response.data.message);
      } else {
        setStatus("❌ Email ou mot de passe incorrect ou serveur indisponible.");
      }
    } finally {
      setLoading(false);
    }
  };

  const styles = useMemo(
    () => ({
      page: {
        minHeight: "100vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: "24px",
        background:"transparent",
      },
      card: {
        width: "min(460px, 92vw)",
        background: "#ffffff",
        borderRadius: 18,
        padding: "34px 32px",
        boxShadow: "0 25px 60px rgba(0,0,0,0.12)",
        border: "1px solid #e5e7eb",
        transform: mounted
          ? "translateY(0px) scale(1)"
          : "translateY(10px) scale(0.99)",
        opacity: mounted ? 1 : 0,
        transition: "transform 420ms ease, opacity 420ms ease",
      },
      logoWrap: {
        display: "flex",
        justifyContent: "center",
        marginBottom: 12,
      },
      logoPlaceholder: {
        width: 64,
        height: 64,
        borderRadius: 16,
        background: "linear-gradient(135deg, #2563eb, #10b981)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        color: "#ffffff",
        fontWeight: 800,
        fontSize: 20,
        letterSpacing: 0.5,
        boxShadow: "0 12px 30px rgba(37,99,235,0.35)",
      },
      title: {
        margin: "8px 0 4px 0",
        fontSize: 22,
        color: "#111827",
        textAlign: "center",
      },
      subtitle: {
        margin: "0 0 22px 0",
        fontSize: 13,
        color: "#6b7280",
        textAlign: "center",
      },
      field: { marginBottom: 14, textAlign: "left" },
      label: { fontSize: 13, color: "#374151", marginBottom: 6 },
      input: {
        width: "100%",
        padding: "12px 14px",
        borderRadius: 10,
        border: "1px solid #d1d5db",
        background: "#f9fafb",
      },
      btn: {
        width: "100%",
        marginTop: 6,
        padding: "12px",
        borderRadius: 999,
        border: "none",
        background: loading
          ? "#93c5fd"
          : "linear-gradient(135deg, #2563eb, #1d4ed8)",
        color: "#ffffff",
        fontWeight: 700,
        cursor: loading ? "default" : "pointer",
      },
      alert: {
        marginTop: 14,
        padding: "12px",
        borderRadius: 12,
        background: "#fee2e2",
        color: "#991b1b",
        fontSize: 13,
      },
      footer: {
        marginTop: 18,
        fontSize: 12,
        color: "#9ca3af",
        textAlign: "center",
      },
    }),
    [mounted, loading]
  );

  return (
    <div style={styles.page}>
      <div style={{ ...styles.card, ...(shake ? { animation: "login-shake 420ms" } : {}) }}>
        {/* LOGO */}
        <div style={styles.logoWrap}>
          {/*
            👉 OPTION 1 : Logo image
            <img src={logo} alt="KyntusCQ" style={{ height: 64 }} />
          */}

          {/* 👉 OPTION 2 : Placeholder temporaire */}
          <div style={styles.logoPlaceholder}>KCQ</div>
        </div>

        <h2 style={styles.title}>Espace qualité</h2>
        <p style={styles.subtitle}>Merci de saisir vos identifiants.</p>

        <form onSubmit={handleSubmit}>
          <div style={styles.field}>
            <label style={styles.label}>Email</label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              style={styles.input}
            />
          </div>

          <div style={styles.field}>
            <label style={styles.label}>Mot de passe</label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              style={styles.input}
            />
          </div>

          <button type="submit" style={styles.btn} disabled={loading}>
            {loading ? "Connexion..." : "Se connecter"}
          </button>

          {status && <div style={styles.alert}>{status}</div>}
        </form>

        <div style={styles.footer}>
          <div>© LK - Kyntus Morocco</div>
          <div>V 2.2.1</div>
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

export default Login;
