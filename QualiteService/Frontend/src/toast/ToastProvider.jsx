// src/toast/ToastProvider.jsx
import React, { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import "./toast.css";
import { setToastApi } from "./toastBus";

const ToastContext = createContext(null);

let idSeq = 1;

export function ToastProvider({ children }) {
  const [toasts, setToasts] = useState([]);
  const timersRef = useRef(new Map());

  const remove = useCallback((id) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
    const t = timersRef.current.get(id);
    if (t) clearTimeout(t);
    timersRef.current.delete(id);
  }, []);

  const push = useCallback((toast) => {
    const id = idSeq++;
    const t = {
      id,
      type: toast.type || "info",
      title: toast.title || "",
      message: toast.message || "",
      durationMs: Number.isFinite(toast.durationMs) ? toast.durationMs : 3200,
    };
    setToasts((prev) => [t, ...prev].slice(0, 5));

    if (t.durationMs > 0) {
      const timer = setTimeout(() => remove(id), t.durationMs);
      timersRef.current.set(id, timer);
    }
    return id;
  }, [remove]);

  const api = useMemo(() => ({
    push,
    remove,
    success: (message, opts = {}) => push({ ...opts, type: "success", message }),
    error: (message, opts = {}) => push({ ...opts, type: "error", message }),
    info: (message, opts = {}) => push({ ...opts, type: "info", message }),
    warning: (message, opts = {}) => push({ ...opts, type: "warning", message }),
  }), [push, remove]);

  // Expose a global bridge for non-React callers (axios interceptors, utils).
  useEffect(() => {
    setToastApi(api);
    return () => setToastApi(null);
  }, [api]);

  return (
    <ToastContext.Provider value={api}>
      {children}
      <div className="kcq-toast-host" role="region" aria-label="Notifications">
        {toasts.map((t) => (
          <div
            key={t.id}
            className={`kcq-toast kcq-toast--${t.type}`}
            role={t.type === "error" ? "alert" : "status"}
            aria-live={t.type === "error" ? "assertive" : "polite"}
          >
            <div className="kcq-toast__body">
              {t.title ? <div className="kcq-toast__title">{t.title}</div> : null}
              <div className="kcq-toast__msg">{t.message}</div>
            </div>
            <button className="kcq-toast__close" onClick={() => remove(t.id)} aria-label="Fermer">
              ×
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast() {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error("useToast must be used within ToastProvider");
  return ctx;
}
