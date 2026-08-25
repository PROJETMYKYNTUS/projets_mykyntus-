import React, { useEffect, useState } from "react";
import api from "../../api";
import PageHeader from "../../components/PageHeader.jsx";
import { FiUser, FiGrid, FiInfo } from "react-icons/fi";

export default function CQSettings() {
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState("");
  const [profile, setProfile] = useState(null);
  const [grids, setGrids] = useState([]);

  useEffect(() => {
    let m = true;
    (async () => {
      try {
        const [profileR, gridsR] = await Promise.all([
          api.get("/auth/me"),
          api.get("/grids/my").catch(() => ({ data: [] })),
        ]);
        if (!m) return;
        setProfile(profileR.data);
        setGrids(Array.isArray(gridsR.data) ? gridsR.data : []);
      } catch (e) {
        if (m) setErr("Impossible de charger le profil.");
      } finally { if (m) setLoading(false); }
    })();
    return () => { m = false; };
  }, []);

  return (
    <div className="page">
      <PageHeader title="Paramètres CQ" subtitle="Profil et grilles assignées. Le mot de passe se gère dans MyKyntus." />

      {loading ? (
        <div className="card" style={{ padding: 40, textAlign: "center", color: "var(--muted)" }}>Chargement…</div>
      ) : (
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
          <div className="card" style={{ padding: 20 }}>
            <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 16 }}>
              <FiUser style={{ color: "var(--primary)", fontSize: 18 }} />
              <span style={{ fontWeight: 800, fontSize: "1rem" }}>Mon profil</span>
            </div>

            <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
              <div>
                <div className="label" style={{ marginBottom: 4 }}>Nom complet</div>
                <input className="input" value={profile?.name || ""} readOnly style={{ background: "var(--chip)", cursor: "default" }} />
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 }}>
                <div>
                  <div className="label" style={{ marginBottom: 4 }}>Email</div>
                  <input className="input" value={profile?.email || ""} readOnly style={{ background: "var(--chip)" }} />
                </div>
                <div>
                  <div className="label" style={{ marginBottom: 4 }}>Cellule</div>
                  <input className="input" value={profile?.cell || "—"} readOnly style={{ background: "var(--chip)" }} />
                </div>
              </div>
              <div>
                <div className="label" style={{ marginBottom: 4 }}>Rôle</div>
                <div style={{ padding: "8px 12px", borderRadius: "var(--radius)", background: "var(--primary-bg)", color: "var(--primary)", fontWeight: 700, fontSize: "0.875rem", display: "inline-block" }}>
                  {(profile?.role || "").toUpperCase()}
                </div>
              </div>
            </div>
          </div>

          <div className="card" style={{ padding: 20 }}>
            <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 16 }}>
              <FiGrid style={{ color: "var(--success)", fontSize: 18 }} />
              <span style={{ fontWeight: 800, fontSize: "1rem" }}>Mes grilles d'évaluation</span>
            </div>
            {grids.length === 0 ? (
              <div style={{ color: "var(--muted)", fontSize: "0.875rem" }}>Toutes les grilles actives vous sont accessibles.</div>
            ) : (
              <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                {grids.map((g) => (
                  <div key={String(g._id)} style={{ padding: "8px 12px", borderRadius: 8, border: "1px solid var(--border)", background: "var(--panel-2)", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                    <span style={{ fontWeight: 600, fontSize: "0.875rem" }}>{g.name || g.title || "Grille"}</span>
                    <span className="badge badge--muted">{g.gridType === "presence" ? "Présence" : "Classique"}</span>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="card" style={{ padding: 20 }}>
            <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 16 }}>
              <FiInfo style={{ color: "var(--primary)", fontSize: 18 }} />
              <span style={{ fontWeight: 800, fontSize: "1rem" }}>À propos</span>
            </div>
            <div style={{ color: "var(--text-secondary)", fontSize: "0.85rem", lineHeight: 1.7 }}>
              <p style={{ margin: "0 0 8px" }}>
                Espace de contrôle qualité pour le suivi des interactions. Le compte et le mot de passe se gèrent dans MyKyntus.
              </p>
              <p style={{ margin: 0 }}>
                Workflow : évaluez un appel, l’agent voit son score, une contestation peut être réévaluée.
              </p>
            </div>
          </div>
        </div>
      )}

      {err && <div style={{ marginTop: 12, padding: "10px 14px", borderRadius: "var(--radius)", background: "var(--danger-bg)", color: "var(--danger)", fontWeight: 600, fontSize: "0.875rem", border: "1px solid rgba(220,38,38,0.2)" }}>{err}</div>}
    </div>
  );
}
