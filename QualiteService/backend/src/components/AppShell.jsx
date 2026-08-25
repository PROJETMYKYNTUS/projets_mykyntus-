import React, { useMemo, useState } from "react";
import { FiMenu, FiSearch, FiLogOut } from "react-icons/fi";
import ThemeToggle from "./ThemeToggle.jsx";
import NotificationsBell from "./NotificationsBell.jsx";

/**
 * AppShell
 * - Sidebar à gauche
 * - Topbar (search + actions)
 * - Main content
 *
 * Props:
 * - user: { name, email, role }
 * - onLogout: fn
 * - navSections: [{ title, items: [{ key, label, icon, onClick, active }] }]
 * - headerRight: ReactNode
 */
export default function AppShell({ user, onLogout, navSections, children }) {
  const [collapsed, setCollapsed] = useState(false);

  const initials = useMemo(() => {
    const n = (user?.name || "").trim();
    if (!n) return "U";
    const parts = n.split(/\s+/).filter(Boolean);
    return (parts[0]?.[0] || "U").toUpperCase() + (parts[1]?.[0] || "").toUpperCase();
  }, [user?.name]);

  return (
    <div className={collapsed ? "shell shell--collapsed" : "shell"}>
      <aside className="sidebar">
        <div className="sidebar__top">
          <div className="brand">
            <div className="brand__mark" aria-hidden="true">
              <img src="/kyntus.svg" alt="" style={{ width: 28, height: 28, display: "block" }} />
            </div>
            <div className="brand__name">Kyntus</div>
          </div>
        </div>

        <nav className="nav">
          {(navSections || []).map((sec) => (
            <div key={sec.title} className="nav__section">
              <div className="nav__title">{sec.title}</div>
              <div className="nav__items">
                {sec.items.map((it) => (
                  <button
                    key={it.key}
                    type="button"
                    className={it.active ? "nav__item nav__item--active" : "nav__item"}
                    onClick={it.onClick}
                    title={it.label}
                  >
                    <span className="nav__icon">{it.icon}</span>
                    <span className="nav__label">{it.label}</span>
                  </button>
                ))}
              </div>
            </div>
          ))}
        </nav>

        <div className="sidebar__bottom">
          <div className="usercard">
            <div className="usercard__avatar">{initials}</div>
            <div className="usercard__meta">
              <div className="usercard__name">{user?.name || "Utilisateur"}</div>
              <div className="usercard__role">{(user?.role || "").toUpperCase()}</div>
            </div>
            <button className="iconbtn" onClick={onLogout} title="Déconnexion">
              <FiLogOut />
            </button>
          </div>
        </div>
      </aside>

      <div className="main">
        <header className="topbar">
          <button className="iconbtn" onClick={() => setCollapsed((v) => !v)} title="Menu">
            <FiMenu />
          </button>

          <div className="topbar__center">
          <div className="search">
            <FiSearch className="search__icon" />
            <input className="search__input" placeholder="Questionnez l'IA" />
</div>
          </div>

          <div className="topbar__actions">
            <ThemeToggle />
            <NotificationsBell />
</div>
        </header>

        <main className="content">{children}</main>
      </div>
    </div>
  );
}
