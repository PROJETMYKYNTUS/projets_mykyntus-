import React, { useEffect, useMemo, useState } from "react";
import axios from "../../api";
import Card from "./components/Card.jsx";

const inputStyle = () => ({
  width: "100%",
  padding: "0.55rem 0.75rem",
  borderRadius: "0.85rem",
  border: "1px solid rgba(209,213,219,0.9)",
  outline: "none",
  background: "transparent",
});

export default function NotificationsView() {
  const [list, setList] = useState([]);
  const [cells, setCells] = useState([]);
  const [pilots, setPilots] = useState([]);

  const [type, setType] = useState("information");
  const [title, setTitle] = useState("");
  const [message, setMessage] = useState("");

  const [targetMode, setTargetMode] = useState("all"); // all | cell | users
  const [targetCell, setTargetCell] = useState("");
  const [targetUsers, setTargetUsers] = useState([]);

  const [status, setStatus] = useState("");

  const load = async () => {
    setStatus("");
    try {
      const [nRes, cellsRes, pilotsRes] = await Promise.all([
        axios.get("/admin/notifications"),
        axios.get("/admin/cells"),
        axios.get("/admin/pilots"),
      ]);
      setList(Array.isArray(nRes.data) ? nRes.data : []);
      setCells(Array.isArray(cellsRes.data) ? cellsRes.data : []);
      setPilots(Array.isArray(pilotsRes.data) ? pilotsRes.data : []);
    } catch (e) {
      console.error(e);
      setStatus("Erreur lors du chargement des notifications.");
    }
  };

  useEffect(() => { load(); }, []);

  const canCreate = useMemo(() => (message || "").trim().length > 0, [message]);

  const resetForm = () => {
    setType("information");
    setTitle("");
    setMessage("");
    setTargetMode("all");
    setTargetCell("");
    setTargetUsers([]);
  };

  const create = async () => {
    if (!canCreate) return;
    setStatus("");
    try {
      const payload = {
        type,
        title,
        message,
        targetAll: targetMode === "all",
        targetCells: targetMode === "cell" && targetCell ? [targetCell] : [],
        targetUsers: targetMode === "users" ? targetUsers : [],
      };
      await axios.post("/admin/notifications", payload);
      setStatus("✅ Notification envoyée.");
      resetForm();
      await load();
    } catch (e) {
      console.error(e);
      setStatus(e?.response?.data?.message || "Erreur lors de l'envoi.");
    }
  };

  const remove = async (id) => {
    if (!id) return;
    setStatus("");
    try {
      await axios.delete(`/admin/notifications/${id}`);
      await load();
    } catch (e) {
      console.error(e);
      setStatus("Erreur lors de la suppression.");
    }
  };

  return (
    <>
      <Card title="Notifications système">
        {status && (
          <div
            style={{
              padding: "0.6rem 0.75rem",
              borderRadius: "0.9rem",
              border: "1px solid rgba(148,163,184,0.35)",
              marginBottom: "0.75rem",
              fontWeight: 800,
              opacity: 0.95,
            }}
          >
            {status}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <label style={{ display: "grid", gap: 6 }}>
            <div style={{ fontWeight: 900, opacity: 0.85 }}>Type</div>
            <select value={type} onChange={(e) => setType(e.target.value)} style={inputStyle()}>
              <option value="information">Information</option>
              <option value="notification">Notification</option>
              <option value="alerte">Alerte</option>
            </select>
          </label>

          <label style={{ display: "grid", gap: 6 }}>
            <div style={{ fontWeight: 900, opacity: 0.85 }}>Ciblage</div>
            <select value={targetMode} onChange={(e) => setTargetMode(e.target.value)} style={inputStyle()}>
              <option value="all">Tous les utilisateurs</option>
              <option value="cell">Une cellule</option>
              <option value="users">Utilisateurs spécifiques</option>
            </select>
          </label>

          <label style={{ display: "grid", gap: 6, gridColumn: "1 / -1" }}>
            <div style={{ fontWeight: 900, opacity: 0.85 }}>Titre (optionnel)</div>
            <input value={title} onChange={(e) => setTitle(e.target.value)} style={inputStyle()} placeholder="Ex : Mise à jour planning" />
          </label>

          <label style={{ display: "grid", gap: 6, gridColumn: "1 / -1" }}>
            <div style={{ fontWeight: 900, opacity: 0.85 }}>Message</div>
            <textarea value={message} onChange={(e) => setMessage(e.target.value)} style={inputStyle()} rows={4} placeholder="Votre message..." />
          </label>

          {targetMode === "cell" && (
            <label style={{ display: "grid", gap: 6, gridColumn: "1 / -1" }}>
              <div style={{ fontWeight: 900, opacity: 0.85 }}>Cellule à notifier</div>
              <select value={targetCell} onChange={(e) => setTargetCell(e.target.value)} style={inputStyle()}>
                <option value="">Choisir…</option>
                {cells.map((c) => (
                  <option key={c._id || c.name} value={c.name}>{c.name}</option>
                ))}
              </select>
            </label>
          )}

          {targetMode === "users" && (
            <label style={{ display: "grid", gap: 6, gridColumn: "1 / -1" }}>
              <div style={{ fontWeight: 900, opacity: 0.85 }}>Utilisateurs à notifier</div>
              <select
                multiple
                value={targetUsers}
                onChange={(e) => {
                  const opts = Array.from(e.target.options).filter((o) => o.selected).map((o) => o.value);
                  setTargetUsers(opts);
                }}
                style={{ ...inputStyle(), minHeight: 120 }}
              >
                {pilots.map((p) => (
                  <option key={p._id} value={p._id}>{p.name} ({p.email})</option>
                ))}
              </select>
              <div style={{ opacity: 0.7, fontSize: 12 }}>Astuce: Ctrl/Cmd + clic pour sélectionner plusieurs.</div>
            </label>
          )}

          <div style={{ gridColumn: "1 / -1", display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
            <button className="btn-outline" onClick={resetForm}>Réinitialiser</button>
            <button className="btn-outline" disabled={!canCreate} onClick={create}>
              Envoyer
            </button>
          </div>
        </div>
      </Card>

      <Card title="Historique des notifications" style={{ marginTop: "1rem" }}>
        <div style={{ display: "grid", gap: "0.6rem" }}>
          {list.length === 0 ? (
            <div style={{ opacity: 0.7 }}>Aucune notification.</div>
          ) : (
            list.map((n) => (
              <div
                key={n._id}
                style={{
                  border: "1px solid rgba(209,213,219,0.9)",
                  borderRadius: "1rem",
                  padding: "0.75rem 0.85rem",
                  display: "grid",
                  gap: 6,
                }}
              >
                <div style={{ display: "flex", justifyContent: "space-between", gap: "0.75rem", alignItems: "center" }}>
                  <div style={{ fontWeight: 900 }}>
                    {(n.title || "").trim() ? n.title : "—"}
                    <span style={{ marginLeft: 10, opacity: 0.7, fontWeight: 800, fontSize: 12 }}>
                      [{n.type}]
                    </span>
                  </div>
                  <button className="btn-outline" onClick={() => remove(n._id)}>Supprimer</button>
                </div>
                <div style={{ opacity: 0.85, whiteSpace: "pre-wrap" }}>{n.message}</div>
                <div style={{ opacity: 0.65, fontSize: 12 }}>
                  {n.targetAll
                    ? "Cible: Tous"
                    : (Array.isArray(n.targetCells) && n.targetCells.length > 0)
                      ? `Cible: Cellule (${n.targetCells.join(", ")})`
                      : (Array.isArray(n.targetUsers) && n.targetUsers.length > 0)
                        ? `Cible: ${n.targetUsers.length} utilisateur(s)`
                        : "Cible: —"}
                  {" · "}
                  {n.createdAt ? new Date(n.createdAt).toLocaleString() : ""}
                </div>
              </div>
            ))
          )}
        </div>
      </Card>
    </>
  );
}
