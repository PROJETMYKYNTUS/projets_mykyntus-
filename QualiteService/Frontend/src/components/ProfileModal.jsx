import React, { useEffect, useState } from "react";
import api from "../api";
import { FiX, FiUser, FiLock, FiCheck } from "react-icons/fi";

/**
 * ProfileModal — Self-service profile editor.
 * - Change display name
 * - Change password (current + new)
 * Triggered from AppShell usercard gear icon.
 */
export default function ProfileModal({ open, onClose }) {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [err, setErr] = useState("");
  const [ok, setOk] = useState("");

  const [profile, setProfile] = useState(null);
  const [name, setName] = useState("");
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  useEffect(() => {
    if (!open) return;
    let m = true;
    setLoading(true); setErr(""); setOk("");
    (async () => {
      try {
        const res = await api.get("/auth/me");
        if (!m) return;
        setProfile(res.data);
        setName(res.data?.name || "");
      } catch (e) {
        if (m) setErr("Impossible de charger le profil.");
      } finally { if (m) setLoading(false); }
    })();
    return () => { m = false; };
  }, [open]);

  async function handleSave() {
    setErr(""); setOk("");
    if (newPassword && newPassword !== confirmPassword) return setErr("Les mots de passe ne correspondent pas.");
    if (newPassword && newPassword.length < 6) return setErr("Le nouveau mot de passe doit contenir au moins 6 caractères.");
    if (newPassword && !currentPassword) return setErr("Veuillez saisir votre mot de passe actuel.");

    setSaving(true);
    try {
      const payload = {};
      if (newPassword) {
        payload.currentPassword = currentPassword;
        payload.newPassword = newPassword;
      }
      const res = await api.patch("/auth/profile", payload);
      setOk("Profil mis à jour avec succès.");
      setCurrentPassword(""); setNewPassword(""); setConfirmPassword("");
      // Update localStorage
      try {
        const u = JSON.parse(localStorage.getItem("user") || "{}");
        u.name = res.data?.name || name.trim();
        localStorage.setItem("user", JSON.stringify(u));
      } catch {}
    } catch (e) {
      setErr(e?.response?.data?.message || "Erreur lors de la mise à jour.");
    } finally { setSaving(false); }
  }

  if (!open) return null;

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()} style={{ maxWidth: 480 }}>
        {/* Header */}
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 20 }}>
          <div>
            <div style={{ fontWeight: 800, fontSize: "1.05rem" }}>Mon profil</div>
            <div style={{ color: "var(--muted)", fontSize: "0.8rem", marginTop: 2 }}>
              {profile?.email || ""} • {(profile?.role || "").toUpperCase()}
            </div>
          </div>
          <button className="iconbtn" onClick={onClose} title="Fermer"><FiX /></button>
        </div>

        {loading ? (
          <div style={{ padding: 30, textAlign: "center", color: "var(--muted)" }}>Chargement…</div>
        ) : (
          <>
            {/* Name section */}
            <div style={{ marginBottom: 20 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 10 }}>
                <FiUser style={{ color: "var(--primary)" }} />
                <span style={{ fontWeight: 700, fontSize: "0.9rem" }}>Informations</span>
              </div>
              <div className="label" style={{ marginBottom: 4 }}>Nom complet</div>
              <input className="input" value={name} readOnly style={{ background: "var(--chip)", cursor: "default" }} />
              <div style={{ marginTop: 6, fontSize: "0.78rem", color: "var(--muted)" }}>
                Cellule : <b>{profile?.cell || "—"}</b>
              </div>
            </div>

            {/* Divider */}
            <div style={{ height: 1, background: "var(--border)", margin: "16px 0" }} />

            {/* Password section */}
            <div style={{ marginBottom: 16 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 10 }}>
                <FiLock style={{ color: "var(--warning)" }} />
                <span style={{ fontWeight: 700, fontSize: "0.9rem" }}>Changer le mot de passe</span>
              </div>
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                <div>
                  <div className="label" style={{ marginBottom: 4 }}>Mot de passe actuel</div>
                  <input className="input" type="password" value={currentPassword} onChange={(e) => setCurrentPassword(e.target.value)} placeholder="••••••••" />
                </div>
                <div>
                  <div className="label" style={{ marginBottom: 4 }}>Nouveau mot de passe</div>
                  <input className="input" type="password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} placeholder="Min. 6 caractères" />
                </div>
                <div>
                  <div className="label" style={{ marginBottom: 4 }}>Confirmer le nouveau mot de passe</div>
                  <input className="input" type="password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} placeholder="Répétez le mot de passe" />
                </div>
              </div>
            </div>

            {/* Messages */}
            {err && <div style={{ padding: "10px 12px", borderRadius: 10, background: "var(--danger-bg)", color: "var(--danger)", fontWeight: 600, fontSize: "0.85rem", marginBottom: 12, border: "1px solid rgba(220,38,38,0.2)" }}>{err}</div>}
            {ok && <div style={{ padding: "10px 12px", borderRadius: 10, background: "var(--success-bg)", color: "var(--success)", fontWeight: 600, fontSize: "0.85rem", marginBottom: 12, border: "1px solid rgba(5,150,105,0.2)", display: "flex", alignItems: "center", gap: 6 }}><FiCheck /> {ok}</div>}

            {/* Actions */}
            <div style={{ display: "flex", justifyContent: "flex-end", gap: 8 }}>
              <button className="btn btn--ghost" type="button" onClick={onClose}>Annuler</button>
              <button className="btn" type="button" onClick={handleSave} disabled={saving}>
                {saving ? "Enregistrement…" : "Enregistrer"}
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
