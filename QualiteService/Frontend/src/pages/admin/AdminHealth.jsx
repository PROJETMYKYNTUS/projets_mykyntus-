import React, { useEffect, useState, useRef } from "react";
import api from "../../api";

function fmtTime(d) { if (!d) return "—"; const dt = new Date(d); return isNaN(dt.getTime()) ? "—" : dt.toLocaleTimeString("fr-FR", { hour: "2-digit", minute: "2-digit" }); }

export default function AdminHealth() {
  const [health, setHealth] = useState(null);
  const [sup, setSup] = useState(null);
  const [loading, setLoading] = useState(true);
  const [autoRefresh, setAutoRefresh] = useState(true);
  const intervalRef = useRef(null);

  // Config
  const [config, setConfig] = useState(null);
  const [aiKey, setAiKey] = useState("");
  const [pickingUrl, setPickingUrl] = useState("");
  const [pickingKey, setPickingKey] = useState("");
  const [configMsg, setConfigMsg] = useState("");
  const [configErr, setConfigErr] = useState("");
  const [saving, setSaving] = useState(false);
  const [resyncing, setResyncing] = useState(false);

  async function loadAll() {
    setLoading(true);
    try {
      const [h, s, c] = await Promise.all([
        api.get("/admin/health").catch(() => ({ data: null })),
        api.get("/admin/supervision").catch(() => ({ data: null })),
        api.get("/admin/config").catch(() => ({ data: null })),
      ]);
      setHealth(h.data); setSup(s.data); setConfig(c.data);
    } catch {} finally { setLoading(false); }
  }

  useEffect(() => { loadAll(); }, []);

  // Auto-refresh every 15s
  useEffect(() => {
    if (autoRefresh) {
      intervalRef.current = setInterval(loadAll, 15000);
    } else {
      clearInterval(intervalRef.current);
    }
    return () => clearInterval(intervalRef.current);
  }, [autoRefresh]);

  async function saveConfig() {
    setConfigMsg(""); setConfigErr(""); setSaving(true);
    try {
      const payload = {};
      if (aiKey) payload.aiKey = aiKey;
      if (pickingUrl) payload.pickingApiUrl = pickingUrl;
      if (pickingKey) payload.pickingApiKey = pickingKey;
      await api.patch("/admin/config", payload, { toast: false });
      setConfigMsg("Configuration sauvegardée. Ajoutez aussi les clés dans .env pour persister après redémarrage.");
      setAiKey(""); setPickingKey("");
      const c = await api.get("/admin/config").catch(() => ({ data: null }));
      setConfig(c.data);
    } catch (e) { setConfigErr(e?.response?.data?.message || "Erreur."); }
    finally { setSaving(false); }
  }

  const Dot = ({ ok }) => <div style={{ width: 10, height: 10, borderRadius: "50%", background: ok ? "var(--success)" : "var(--danger)", flexShrink: 0 }} />;

  return (
    <div className="page">
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: 16 }}>
        <div>
          <div className="cq-dup-title">
          <div style={{ fontSize: "1.25rem", fontWeight: 800 }}>Santé & Supervision</div>
          <div style={{ color: "var(--muted)", fontSize: "0.875rem", marginTop: 2 }}>Monitoring temps réel • Configuration API</div>
          </div>
        </div>
        <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
          <label style={{ display: "flex", alignItems: "center", gap: 6, fontSize: "0.8rem", fontWeight: 600, color: "var(--muted)", cursor: "pointer" }}>
            <input type="checkbox" checked={autoRefresh} onChange={(e) => setAutoRefresh(e.target.checked)} style={{ accentColor: "var(--primary)" }} />
            Auto (15s)
          </label>
          <button className="btn btn--ghost btn--sm" onClick={loadAll} disabled={loading}>↻ Rafraîchir</button>
        </div>
      </div>

      {/* ===== Live KPIs ===== */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(150px, 1fr))", gap: 10, marginBottom: 16 }}>
        {[
          { label: "Évals. aujourd'hui", value: sup?.evaluations?.today ?? "—", icon: "📝", color: "var(--primary)" },
          { label: "Évals. ce mois", value: sup?.evaluations?.month ?? "—", icon: "📊", color: "var(--primary)" },
          { label: "Contestations ouvertes", value: sup?.contested ?? "—", icon: "⚠", color: (sup?.contested || 0) > 0 ? "var(--warning)" : "var(--success)" },
          { label: "Coachings actifs", value: sup?.coachingActive ?? "—", icon: "🎯", color: "var(--primary)" },
          { label: "Utilisateurs en ligne", value: health?.socket?.connectedClients ?? "—", icon: "🟢", color: "var(--success)" },
          { label: "Agents actifs", value: sup?.users?.pilots ?? "—", icon: "👤", color: "var(--text-secondary)" },
          { label: "CQ actifs", value: sup?.users?.cq ?? "—", icon: "🎧", color: "var(--primary)" },
          { label: "Management", value: sup?.users?.management ?? "—", icon: "📋", color: "var(--warning)" },
        ].map((k) => (
          <div key={k.label} className="card" style={{ padding: "12px 14px" }}>
            <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
              <span style={{ fontSize: "0.9rem" }}>{k.icon}</span>
              <span style={{ fontSize: "0.68rem", fontWeight: 700, color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.04em" }}>{k.label}</span>
            </div>
            <div style={{ fontSize: "1.4rem", fontWeight: 800, marginTop: 4, color: k.color }}>{k.value}</div>
          </div>
        ))}
      </div>

      {/* ===== System Health + Recent Activity ===== */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1.5fr", gap: 12, marginBottom: 16 }}>
        {/* System cards */}
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <div className="card" style={{ padding: 16 }}>
            <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 10, display: "flex", alignItems: "center", gap: 8 }}>🌐 API Backend</div>
            <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}><Dot ok={!!health?.ok} /><span style={{ fontWeight: 600, fontSize: "0.85rem" }}>{health?.ok ? "Opérationnelle" : "Erreur"}</span></div>
              <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.83rem" }}><span style={{ color: "var(--muted)" }}>Uptime</span><span style={{ fontWeight: 700 }}>{health?.uptimeSec ? `${Math.floor(health.uptimeSec / 3600)}h ${Math.floor((health.uptimeSec % 3600) / 60)}m` : "—"}</span></div>
              <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.83rem" }}><span style={{ color: "var(--muted)" }}>Version</span><span style={{ fontWeight: 700 }}>{health?.version || "3.1.15"}</span></div>
              <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.83rem" }}><span style={{ color: "var(--muted)" }}>Démarré</span><span style={{ fontWeight: 700 }}>{health?.startedAt ? new Date(health.startedAt).toLocaleString("fr-FR") : "—"}</span></div>
            </div>
          </div>
          <div className="card" style={{ padding: 16 }}>
            <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 10, display: "flex", alignItems: "center", gap: 8 }}>🗄 MongoDB</div>
            <div style={{ display: "flex", alignItems: "center", gap: 8 }}><Dot ok={health?.mongo?.connected} /><span style={{ fontWeight: 600, fontSize: "0.85rem" }}>{health?.mongo?.connected ? "Connectée" : "Déconnectée"}</span></div>
            <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.83rem", marginTop: 6 }}><span style={{ color: "var(--muted)" }}>Ping</span><span style={{ fontWeight: 700 }}>{health?.mongo?.pingMs != null ? `${health.mongo.pingMs} ms` : "—"}</span></div>
          </div>
          <div className="card" style={{ padding: 16 }}>
            <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 10, display: "flex", alignItems: "center", gap: 8 }}>📡 Socket.IO</div>
            <div style={{ display: "flex", alignItems: "center", gap: 8 }}><Dot ok={(health?.socket?.connectedClients || 0) > 0} /><span style={{ fontWeight: 600, fontSize: "0.85rem" }}>{health?.socket?.connectedClients || 0} client(s) connecté(s)</span></div>
          </div>
          <div className="card" style={{ padding: 16 }}>
            <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 10, display: "flex", justifyContent: "space-between", alignItems: "center", gap: 8 }}>
              <span>👥 Annuaire MyKyntus</span>
              <button
                className="btn btn--ghost btn--sm"
                disabled={resyncing || !!sup?.directory?.running}
                onClick={async () => {
                  setResyncing(true);
                  try {
                    await api.post("/admin/directory/resync", {}, { toast: false });
                    await loadAll();
                  } catch (e) {
                    setConfigErr(e?.response?.data?.message || "Synchro annuaire échouée.");
                  } finally {
                    setResyncing(false);
                  }
                }}
              >
                {resyncing || sup?.directory?.running ? "Synchro…" : "Resynchroniser"}
              </button>
            </div>
            <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
              <Dot ok={sup?.directory?.lastSyncOk !== false && !!sup?.directory?.lastSyncAt} />
              <span style={{ fontWeight: 600, fontSize: "0.85rem" }}>
                {sup?.directory?.lastSyncOk === false
                  ? "Échec"
                  : sup?.directory?.lastSyncAt
                    ? "À jour"
                    : "Jamais synchronisé"}
              </span>
            </div>
            <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.83rem", marginTop: 6 }}>
              <span style={{ color: "var(--muted)" }}>Dernière synchro</span>
              <span style={{ fontWeight: 700 }}>{sup?.directory?.lastSyncAt ? new Date(sup.directory.lastSyncAt).toLocaleString("fr-FR") : "—"}</span>
            </div>
            <div style={{ display: "flex", justifyContent: "space-between", fontSize: "0.83rem", marginTop: 4 }}>
              <span style={{ color: "var(--muted)" }}>Employés / agents</span>
              <span style={{ fontWeight: 700 }}>{sup?.directory?.employees ?? "—"} / {sup?.directory?.pilotes ?? "—"}</span>
            </div>
            {sup?.directory?.lastError ? (
              <div style={{ marginTop: 8, fontSize: "0.78rem", color: "var(--danger)", fontWeight: 600 }}>{sup.directory.lastError}</div>
            ) : null}
          </div>
        </div>

        {/* Recent activity feed */}
        <div className="card" style={{ padding: 0, overflow: "hidden" }}>
          <div style={{ padding: "12px 16px", borderBottom: "1px solid var(--border)", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <span style={{ fontWeight: 700, fontSize: "0.9rem" }}>📍 Activité en direct (aujourd'hui)</span>
            <span style={{ fontSize: "0.78rem", fontWeight: 600, color: "var(--muted)" }}>{sup?.recentEvals?.length || 0} éval.</span>
          </div>
          <div style={{ maxHeight: 340, overflow: "auto" }}>
            {(sup?.recentEvals || []).length === 0 ? (
              <div style={{ padding: 30, textAlign: "center", color: "var(--muted)", fontSize: "0.85rem" }}>Aucune évaluation aujourd'hui.</div>
            ) : (sup?.recentEvals || []).map((s, i) => (
              <div key={s._id || i} style={{ padding: "10px 16px", borderBottom: "1px solid var(--border)", display: "flex", alignItems: "center", gap: 10 }}>
                <div style={{ width: 8, height: 8, borderRadius: "50%", background: s.evaluatorRole === "cq" ? "var(--primary)" : "var(--warning)", flexShrink: 0 }} />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div style={{ fontSize: "0.85rem", fontWeight: 600, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                    <span style={{ fontWeight: 700 }}>{s.evaluatorName}</span>
                    <span style={{ color: "var(--muted)", fontWeight: 400 }}> a évalué </span>
                    <span style={{ fontWeight: 700 }}>{s.pilotName}</span>
                  </div>
                  <div style={{ fontSize: "0.72rem", color: "var(--muted)" }}>
                    {s.pilotCell} • EPS: {s.eps || "—"} • {fmtTime(s.createdAt)}
                  </div>
                </div>
                <span className={`badge ${s.evaluatorRole === "cq" ? "badge--primary" : "badge--warning"}`} style={{ fontSize: "0.68rem" }}>
                  {s.evaluatorRole === "cq" ? "CQ" : "MGMT"}
                </span>
              </div>
            ))}
          </div>
        </div>
      </div>

      {/* ===== API Configuration ===== */}
      <div className="card" style={{ padding: 20, marginBottom: 16 }}>
        <div style={{ fontWeight: 800, fontSize: "1rem", marginBottom: 4 }}>🔑 Configuration des API</div>
        <div style={{ color: "var(--muted)", fontSize: "0.8rem", marginBottom: 16 }}>
          Les clés sont appliquées au runtime. Ajoutez-les dans le fichier <code>.env</code> pour persister après redémarrage.
        </div>

        {/* Status indicators */}
        <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 10, marginBottom: 16 }}>
          <div style={{ padding: "10px 12px", borderRadius: 10, border: "1px solid var(--border)", background: "var(--panel-2)" }}>
            <div style={{ fontSize: "0.72rem", fontWeight: 700, color: "var(--muted)", textTransform: "uppercase" }}>Clé IA</div>
            <div style={{ display: "flex", alignItems: "center", gap: 6, marginTop: 4 }}>
              <Dot ok={!!config?.aiKey} />
              <span style={{ fontSize: "0.85rem", fontWeight: 700 }}>{config?.aiKey || "Non configurée"}</span>
            </div>
          </div>
          <div style={{ padding: "10px 12px", borderRadius: 10, border: "1px solid var(--border)", background: "var(--panel-2)" }}>
            <div style={{ fontSize: "0.72rem", fontWeight: 700, color: "var(--muted)", textTransform: "uppercase" }}>Picking API</div>
            <div style={{ fontSize: "0.85rem", fontWeight: 700, marginTop: 4, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{config?.pickingApiUrl || "Non configurée"}</div>
          </div>
          <div style={{ padding: "10px 12px", borderRadius: 10, border: "1px solid var(--border)", background: "var(--panel-2)" }}>
            <div style={{ fontSize: "0.72rem", fontWeight: 700, color: "var(--muted)", textTransform: "uppercase" }}>Picking Key</div>
            <div style={{ display: "flex", alignItems: "center", gap: 6, marginTop: 4 }}>
              <Dot ok={!!config?.pickingApiKey} />
              <span style={{ fontSize: "0.85rem", fontWeight: 700 }}>{config?.pickingApiKey || "Non configurée"}</span>
            </div>
          </div>
        </div>

        {/* Input fields */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 10, marginBottom: 12 }}>
          <div>
            <div className="label" style={{ marginBottom: 4 }}>Clé API IA</div>
            <input className="input" type="password" value={aiKey} onChange={(e) => setAiKey(e.target.value)} placeholder="sk-or-…" />
          </div>
          <div>
            <div className="label" style={{ marginBottom: 4 }}>Picking API URL</div>
            <input className="input" value={pickingUrl} onChange={(e) => setPickingUrl(e.target.value)} placeholder="https://…" />
          </div>
          <div>
            <div className="label" style={{ marginBottom: 4 }}>Picking API Key</div>
            <input className="input" type="password" value={pickingKey} onChange={(e) => setPickingKey(e.target.value)} placeholder="Clé…" />
          </div>
        </div>

        {configMsg && <div style={{ padding: "8px 12px", borderRadius: 8, background: "var(--success-bg)", color: "var(--success)", fontSize: "0.82rem", fontWeight: 600, marginBottom: 10 }}>{configMsg}</div>}
        {configErr && <div style={{ padding: "8px 12px", borderRadius: 8, background: "var(--danger-bg)", color: "var(--danger)", fontSize: "0.82rem", fontWeight: 600, marginBottom: 10 }}>{configErr}</div>}

        <div style={{ display: "flex", justifyContent: "flex-end" }}>
          <button className="btn" onClick={saveConfig} disabled={saving}>{saving ? "Sauvegarde…" : "Sauvegarder"}</button>
        </div>
      </div>

      {/* Users breakdown */}
      <div className="card" style={{ padding: 16 }}>
        <div style={{ fontWeight: 700, fontSize: "0.9rem", marginBottom: 12 }}>👥 Effectifs</div>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(130px, 1fr))", gap: 10 }}>
          {[
            { label: "Total", v: sup?.users?.total ?? "—", color: "var(--text)" },
            { label: "Actifs", v: sup?.users?.active ?? "—", color: "var(--success)" },
            { label: "Pilotes", v: sup?.users?.pilots ?? "—", color: "var(--text-secondary)" },
            { label: "CQ", v: sup?.users?.cq ?? "—", color: "var(--primary)" },
            { label: "Management", v: sup?.users?.management ?? "—", color: "var(--warning)" },
            { label: "Inactifs", v: ((sup?.users?.total || 0) - (sup?.users?.active || 0)) || "—", color: "var(--danger)" },
          ].map((u) => (
            <div key={u.label} style={{ padding: "10px 12px", borderRadius: 10, border: "1px solid var(--border)", background: "var(--panel-2)" }}>
              <div style={{ fontSize: "0.72rem", fontWeight: 700, color: "var(--muted)", textTransform: "uppercase" }}>{u.label}</div>
              <div style={{ fontSize: "1.2rem", fontWeight: 800, marginTop: 3, color: u.color }}>{u.v}</div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
