import React, { useEffect, useMemo, useRef, useState } from "react";
import api from "../api.js";
import { getSocket } from "../socket.js";
import { FiBell, FiTrash2, FiX } from "react-icons/fi";

function normalizeLevel(level) {
  const s = (level || "").toString().trim().toLowerCase();
  if (["critique", "critical", "crit"].includes(s)) return "critique";
  if (["information", "info", "informative"].includes(s)) return "information";
  if (["avertissement", "warning", "warn", "alerte"].includes(s)) return "avertissement";
  return "information";
}

function levelUi(level) {
  const l = normalizeLevel(level);
  if (l === "critique")
    return {
      label: "Critique",
      box: {
        background: "rgba(239,68,68,0.10)",
        border: "1px solid rgba(239,68,68,0.35)",
        color: "var(--text)",
      },
      dot: "#ef4444",
    };
  if (l === "avertissement")
    return {
      label: "Avertissement",
      box: {
        background: "rgba(249,115,22,0.10)",
        border: "1px solid rgba(249,115,22,0.35)",
        color: "var(--text)",
      },
      dot: "#f97316",
    };
  return {
    label: "Information",
    box: {
      background: "rgba(37,99,235,0.10)",
      border: "1px solid rgba(37,99,235,0.35)",
      color: "var(--text)",
    },
    dot: "#2563eb",
  };
}

function fmtDateTime(v) {
  if (!v) return "—";
  const d = new Date(v);
  if (isNaN(d.getTime())) return "—";
  return d.toLocaleString("fr-FR");
}

export default function NotificationsBell() {
  const [open, setOpen] = useState(false);
  const [rows, setRows] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const boxRef = useRef(null);

  const user = useMemo(() => {
    try {
      return JSON.parse(localStorage.getItem("user") || "null");
    } catch {
      return null;
    }
  }, []);

  const userId = user?._id || user?.id || "";
  const role = (user?.role || "").toString();

  const readKey = userId ? `notifReadAt_${userId}` : "notifReadAt";
  const dismissedKey = userId ? `notifDismissed_${userId}` : "notifDismissed";

  const [readAt, setReadAt] = useState(() => {
    const raw = localStorage.getItem(readKey);
    const n = raw ? Number(raw) : 0;
    return Number.isFinite(n) ? n : 0;
  });

  const [dismissed, setDismissed] = useState(() => {
    try {
      const raw = localStorage.getItem(dismissedKey);
      const arr = raw ? JSON.parse(raw) : [];
      return Array.isArray(arr) ? arr : [];
    } catch {
      return [];
    }
  });

  const saveDismissed = (arr) => {
    setDismissed(arr);
    try {
      localStorage.setItem(dismissedKey, JSON.stringify(arr));
    } catch {
      // ignore
    }
  };

  const load = async () => {
    setLoading(true);
    setError("");
    try {
      // Admin: notifications globales via /admin/notifications
      // Autres rôles: notifications ciblées via /notifications/mine
      const endpoint = role === "admin" ? "/admin/notifications" : "/notifications/mine";
      const res = await api.get(endpoint);
      const data = res?.data;
      const list = Array.isArray(data) ? data : data?.notifications || [];
      setRows(list);
    } catch (e) {
      setError(e?.response?.data?.message || "Impossible de charger les notifications.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
    const id = window.setInterval(load, 60_000);
    return () => window.clearInterval(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // 🔔 Real-time notifications (socket.io)
  useEffect(() => {
    const s = getSocket();
    const onNew = (notif) => {
      if (!notif || !notif._id) return;
      // keep state name consistent (rows)
      setRows((prev) => {
        const arr = Array.isArray(prev) ? prev : [];
        if (arr.some((n) => String(n._id) === String(notif._id))) return arr;
        // push newest on top, mark unread by default
        return [{ ...notif, isRead: false }, ...arr];
      });
    };

    s.on("notification:new", onNew);
    return () => {
      s.off("notification:new", onNew);
    };
  }, []);


  // click outside -> close
  useEffect(() => {
    const onDown = (e) => {
      if (!open) return;
      if (!boxRef.current) return;
      if (!boxRef.current.contains(e.target)) setOpen(false);
    };
    document.addEventListener("mousedown", onDown);
    return () => document.removeEventListener("mousedown", onDown);
  }, [open]);

  const visibleRows = useMemo(() => {
    const hidden = new Set(dismissed);
    return (rows || []).filter((n) => !hidden.has(n?._id || n?.id));
  }, [rows, dismissed]);

  const unreadCount = useMemo(() => {
    return visibleRows.filter((n) => {
      const t = new Date(n?.createdAt || 0).getTime();
      return t > (readAt || 0);
    }).length;
  }, [visibleRows, readAt]);

  // mark as read on open
  useEffect(() => {
    if (!open) return;
    const now = Date.now();
    setReadAt(now);
    try {
      localStorage.setItem(readKey, String(now));
    } catch {
      // ignore
    }
    // refresh on open
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const dismissOne = (id) => {
    if (!id) return;
    saveDismissed([...(dismissed || []), id]);
  };

  const resetHidden = () => {
    saveDismissed([]);
  };

  const deleteOne = async (id) => {
    if (!id) return;
    if (role !== "admin") return;

    const ok = window.confirm("Supprimer cette notification ? Elle disparaîtra pour tout le monde.");
    if (!ok) return;

    setError("");
    try {
      await api.delete(`/admin/notifications/${id}`);
      setRows((prev) => (prev || []).filter((x) => (x?._id || x?.id) !== id));
    } catch (e) {
      setError(e?.response?.data?.message || "Suppression impossible.");
    }
  };

  return (
    <div ref={boxRef} style={{ position: "relative", display: "inline-block" }}>
      <button
        type="button"
        className="iconbtn"
        onClick={() => setOpen((v) => !v)}
        style={{ position: "relative" }}
        title="Notifications" >
        <FiBell />
        {unreadCount > 0 && (
          <span
            style={{
              position: "absolute",
              top: -6,
              right: -6,
              minWidth: 18,
              height: 18,
              padding: "0 5px",
              borderRadius: 999,
              background: "#ef4444",
              color: "var(--text)",
              display: "inline-flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: 11,
              fontWeight: 900,
              lineHeight: 1,
              border: "2px solid var(--panel)",
            }} >
            {unreadCount > 99 ? "99+" : unreadCount}
          </span>
        )}
      </button>

      {open && (
        <div
          style={{
            position: "absolute",
            right: 0,
            marginTop: 10,
            width: "min(460px, 92vw)",
            maxHeight: "70vh",
            overflowY: "auto",
            zIndex: 999,
            borderRadius: "0.9rem",
            padding: "0.7rem",
            background: "var(--panel)",
            border: "1px solid var(--border)",
            boxShadow: "var(--shadow)",
            color: "var(--text)",
          }} >
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              gap: "0.75rem",
              marginBottom: "0.55rem",
            }} >
            <div style={{ fontWeight: 900 }}>Notifications</div>
            <div style={{ display: "inline-flex", alignItems: "center", gap: "0.4rem" }}>
              <button
                type="button"
                className="btn-outline"
                onClick={resetHidden}
                style={{ padding: "0.25rem 0.55rem", fontSize: "0.8rem" }}
                title="Ré-afficher les notifications masquées" >
                Réinitialiser
              </button>
              <button
                type="button"
                className="btn-outline"
                onClick={() => setOpen(false)}
                style={{ padding: "0.25rem 0.55rem", fontSize: "0.85rem" }}
                title="Fermer" >
                <FiX />
              </button>
            </div>
          </div>

          {loading ? (
            <div style={{ color: "var(--muted)", fontSize: "0.85rem" }}>Chargement…</div>
          ) : visibleRows.length === 0 ? (
            <div style={{ color: "var(--muted)", fontSize: "0.85rem" }}>
              Aucune notification.
            </div>
          ) : (
            <div style={{ display: "grid", gap: "0.5rem" }}>
              {visibleRows
                .slice()
                .sort(
                  (a, b) =>
                    new Date(b?.createdAt || 0).getTime() -
                    new Date(a?.createdAt || 0).getTime()
                )
                .map((n) => {
                  const id = n?._id || n?.id;
                  const ui = levelUi(n?.level || n?.severity || n?.type);
                  const scoreId = n?.meta?.scoreId || n?.scoreId || null;

                  return (
                    <div
                      key={id}
                      onClick={() => {
                        if (!scoreId) return;
                        try {
                          const u = JSON.parse(localStorage.getItem('user') || '{}');
                          const role = u?.role || 'cq';
                          window.dispatchEvent(new CustomEvent('kcq:navigate', { detail: { role, view: 'new', editScoreId: String(scoreId) } }));
                          setOpen(false);
                        } catch {}
                      }}
                      style={{ cursor: scoreId ? 'pointer' : 'default',
                        borderRadius: "0.85rem",
                        padding: "0.65rem 0.75rem",
                        border: "1px solid var(--border)",
                        background: "var(--panel-2)",
                        display: "grid",
                        gap: "0.25rem",
                      }} >
                      <div style={{ display: "flex", justifyContent: "space-between", gap: "0.75rem" }}>
                        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", minWidth: 0 }}>
                          <span
                            style={{
                              width: 10,
                              height: 10,
                              borderRadius: 999,
                              background: ui.dot,
                              flex: "0 0 auto",
                            }}
                          />
                          <span
                            style={{
                              ...ui.box,
                              padding: "0.12rem 0.5rem",
                              borderRadius: 999,
                              fontSize: "0.72rem",
                              fontWeight: 900,
                              flex: "0 0 auto",
                            }} >
                            {ui.label}
                          </span>
                          <span
                            style={{
                              fontWeight: 900,
                              overflow: "hidden",
                              textOverflow: "ellipsis",
                              whiteSpace: "nowrap",
                            }}
                            title={n?.title || ""} >
                            {n?.title || "Sans titre"}
                          </span>
                        </div>

                        <div style={{ display: "inline-flex", gap: "0.35rem", flex: "0 0 auto" }}>
                          {role === "admin" && (
                            <button
                              type="button"
                              className="btn-outline"
                              onClick={(e) => { e.stopPropagation(); deleteOne(id); }}
                              style={{
                                padding: "0.2rem 0.55rem",
                                fontSize: "0.8rem",
                                display: "inline-flex",
                                alignItems: "center",
                                gap: "0.25rem",
                              }}
                              title="Supprimer" >
                              <FiTrash2 />
                            </button>
                          )}
                          <button
                            type="button"
                            className="btn-outline"
                            onClick={(e) => { e.stopPropagation(); dismissOne(id); }}
                            style={{ padding: "0.2rem 0.55rem", fontSize: "0.8rem" }}
                            title="Masquer" >
                            <FiX />
                          </button>
                        </div>
                      </div>

                      <div style={{ color: "var(--text)", whiteSpace: "pre-wrap", fontSize: "0.85rem" }}>
                        {n?.message || n?.body || ""}
                      </div>

                      <div style={{ color: "var(--muted)", fontSize: "0.75rem" }}>
                        {fmtDateTime(n?.createdAt)}
                      </div>
                    </div>
                  );
                })}
            </div>
          )}

          {error && (
            <div
              style={{
                marginTop: "0.55rem",
                padding: "0.55rem 0.7rem",
                borderRadius: "0.75rem",
                border: "1px solid rgba(253,186,116,0.55)",
                background: "rgba(124,45,18,0.25)",
                color: "var(--text)",
                fontSize: "0.85rem",
              }} >
              {error}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
