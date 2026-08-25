// src/theme/theme.js
import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";

/**
 * Supported theme values:
 * - "light"
 * - "dark"
 * - "system" (follows OS preference)
 */
const THEME_STORAGE_KEY = "theme";
const ThemeContext = createContext(null);

function getSystemTheme() {
  if (typeof window === "undefined" || !window.matchMedia) return "light";
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

function normalizeTheme(value) {
  return value === "light" || value === "dark" || value === "system" ? value : null;
}

function readStoredTheme() {
  try {
    return normalizeTheme(localStorage.getItem(THEME_STORAGE_KEY));
  } catch {
    return null;
  }
}

function writeStoredTheme(theme) {
  try {
    localStorage.setItem(THEME_STORAGE_KEY, theme);
  } catch {
    // ignore
  }
}

function applyThemeClass(theme) {
  if (typeof document === "undefined") return;

  const html = document.documentElement;
  const resolved = theme === "system" ? getSystemTheme() : theme;

  html.classList.remove("light", "dark");
  html.classList.add(resolved);
  html.dataset.theme = resolved;
}

export function ThemeProvider({ children, defaultTheme = "light" }) {
  const [theme, setThemeState] = useState(() => readStoredTheme() ?? defaultTheme);

  const overrideStackRef = useRef([]);
  const [overrideTheme, setOverrideTheme] = useState(null);

  const effectiveTheme = overrideTheme ?? theme;

  useEffect(() => {
    applyThemeClass(effectiveTheme);
  }, [effectiveTheme]);

  useEffect(() => {
    if (theme !== "system") return undefined;
    if (typeof window === "undefined" || !window.matchMedia) return undefined;

    const mq = window.matchMedia("(prefers-color-scheme: dark)");
    const handler = () => applyThemeClass("system");

    if (mq.addEventListener) mq.addEventListener("change", handler);
    else mq.addListener(handler);

    return () => {
      if (mq.removeEventListener) mq.removeEventListener("change", handler);
      else mq.removeListener(handler);
    };
  }, [theme]);

  const setTheme = useCallback((nextTheme, { persist = true } = {}) => {
    const normalized = normalizeTheme(nextTheme);
    if (!normalized) return;

    setThemeState(normalized);
    if (persist) writeStoredTheme(normalized);
  }, []);

  const pushTheme = useCallback((nextTheme) => {
    if (nextTheme !== "light" && nextTheme !== "dark") return null;

    const id =
      globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`;

    overrideStackRef.current.push({ id, theme: nextTheme });
    setOverrideTheme(nextTheme);
    return id;
  }, []);

  const popTheme = useCallback((id) => {
    if (!id) return;

    overrideStackRef.current = overrideStackRef.current.filter((x) => x.id !== id);
    const last = overrideStackRef.current[overrideStackRef.current.length - 1];
    setOverrideTheme(last?.theme ?? null);
  }, []);

  const value = useMemo(
    () => ({ theme, effectiveTheme, setTheme, pushTheme, popTheme }),
    [theme, effectiveTheme, setTheme, pushTheme, popTheme]
  );

  // IMPORTANT: no JSX here => avoids Vite/OXC "Unexpected token" on .js files
  return React.createElement(ThemeContext.Provider, { value }, children);
}

export function useTheme() {
  const ctx = useContext(ThemeContext);

  const fallback = useMemo(() => {
    const stored = readStoredTheme() ?? "light";
    const effective = stored === "system" ? getSystemTheme() : stored;

    return {
      theme: stored,
      effectiveTheme: effective,
      setTheme: (nextTheme, { persist = true } = {}) => {
        const normalized = normalizeTheme(nextTheme);
        if (!normalized) return;
        applyThemeClass(normalized);
        if (persist) writeStoredTheme(normalized);
      },
      pushTheme: (nextTheme) => {
        if (nextTheme !== "light" && nextTheme !== "dark") return null;
        const prev = document?.documentElement?.classList?.contains("dark") ? "dark" : "light";
        applyThemeClass(nextTheme);
        return prev;
      },
      popTheme: (token) => {
        if (token !== "light" && token !== "dark") return;
        applyThemeClass(token);
      },
    };
  }, []);

  return ctx ?? fallback;
}

/**
 * Page-level theme override with auto-restore on unmount.
 * Example: usePageTheme("light") inside a dashboard.
 */
export function usePageTheme(pageTheme) {
  const { pushTheme, popTheme } = useTheme();

  useEffect(() => {
    if (pageTheme !== "light" && pageTheme !== "dark") return undefined;
    const token = pushTheme(pageTheme);
    return () => popTheme(token);
  }, [pageTheme, pushTheme, popTheme]);
}
