// src/App.jsx
import React, { useEffect, useState } from "react";
import Login from "./pages/Login.jsx";
import CQDashboard from "./pages/CQDashboard.jsx";
import AdminDashboard from "./pages/AdminDashboard.jsx";
import PilotDashboard from "./pages/PilotDashboard.jsx";
import ManagementDashboard from "./pages/ManagementDashboard.jsx";

import { ThemeProvider } from "./theme/theme.js";
import AppShell from "./components/AppShell.jsx";
import { resetSocketAuth } from "./socket.js";
import {
  FiBarChart2,
  FiList,
  FiHeadphones,
  FiSettings,
  FiGrid,
  FiUsers,
  FiPlus,
} from "react-icons/fi";

function App() {
  const [user, setUser] = useState(null);
  const [adminTab, setAdminTab] = useState("dashboard");
  const [cqView, setCqView] = useState("stats");
  const [mgmtView, setMgmtView] = useState("overview");
  const [editScoreId, setEditScoreId] = useState("");

  // Relire user depuis localStorage au chargement
  useEffect(() => {
    const savedUser = localStorage.getItem("user");
    if (savedUser) setUser(JSON.parse(savedUser));
  }, []);
  // Navigation interne sans router (édition évaluation via event global)
  useEffect(() => {
    const handler = (ev) => {
      try {
        const d = ev?.detail || {};
        const role = d.role;
        const view = d.view;
        const id = d.editScoreId || "";
        if (typeof id === "string") setEditScoreId(id);
        if (view) {
          if (role === "cq") setCqView(view);
          if (role === "management") setMgmtView(view);
        }
      } catch {}
    };
    window.addEventListener("kcq:navigate", handler);
    return () => window.removeEventListener("kcq:navigate", handler);
  }, []);


  const handleLoginSuccess = (userData, token) => {
    localStorage.setItem("token", token);
    localStorage.setItem("user", JSON.stringify(userData));
    resetSocketAuth();
    setUser(userData);
  };

  const handleLogout = () => {
    localStorage.removeItem("token");
    localStorage.removeItem("user");
    setUser(null);
  };

  const content = (() => {
    if (!user) {
      return <Login onLoginSuccess={handleLoginSuccess} />;
    }

    // ADMIN
    if (user.role === "admin") {
      const navSections = [
        {
          title: "WORKSPACE",
          items: [
            {
              key: "dashboard",
              label: "Tableau de bord",
              icon: <FiBarChart2 />,
              active: adminTab === "dashboard",
              onClick: () => setAdminTab("dashboard"),
            },
            {
              key: "evaluations",
              label: "Évaluations",
              icon: <FiList />,
              active: adminTab === "evaluations",
              onClick: () => setAdminTab("evaluations"),
            },
            {
              key: "coaching",
              label: "Coaching",
              icon: <FiHeadphones />,
              active: adminTab === "coaching",
              onClick: () => setAdminTab("coaching"),
            },
          ],
        },
        {
          title: "MANAGEMENT",
          items: [
            {
              key: "grids",
              label: "Grilles",
              icon: <FiGrid />,
              active: adminTab === "grids",
              onClick: () => setAdminTab("grids"),
            },
            {
              key: "notifications",
              label: "Notifications",
              icon: <FiUsers />,
              active: adminTab === "notifications",
              onClick: () => setAdminTab("notifications"),
            },
            {
              key: "settings",
              label: "Panneau Admin",
              icon: <FiSettings />,
              active: adminTab === "settings",
              onClick: () => setAdminTab("settings"),
            },
          ],
        },
      ];

      return (
        <AppShell user={user} onLogout={handleLogout} navSections={navSections}>
          <AdminDashboard controlledTab={adminTab} />
        </AppShell>
      );
    }

    // CQ
    if (user.role === "cq") {
      const navSections = [
        {
          title: "WORKSPACE",
          items: [
            {
              key: "stats",
              label: "Tableau de bord",
              icon: <FiBarChart2 />,
              active: cqView === "stats",
              onClick: () => setCqView("stats"),
            },
            {
              key: "list",
              label: "Évaluations",
              icon: <FiList />,
              active: cqView === "list",
              onClick: () => setCqView("list"),
            },
            {
              key: "new",
              label: "Nouvelle évaluation",
              icon: <FiPlus />,
              active: cqView === "new",
              onClick: () => { setEditScoreId(""); setCqView("new"); },
            },
            {
              key: "coaching",
              label: "Coaching",
              icon: <FiHeadphones />,
              active: cqView === "coaching",
              onClick: () => setCqView("coaching"),
            },
          ],
        },
        {
          title: "PREFERENCES",
          items: [
            {
              key: "settings",
              label: "Paramètres",
              icon: <FiSettings />,
              active: false,
              onClick: () => {},
            },
          ],
        },
      ];

      return (
        <AppShell user={user} onLogout={handleLogout} navSections={navSections}>
          <CQDashboard forcedView={cqView} onViewChange={setCqView} />
        </AppShell>
      );
    }

    // PILOTE
    if (user.role === "pilote") {
      return (
        <AppShell
          user={user}
          onLogout={handleLogout}
          navSections={[
            {
              title: "WORKSPACE",
              items: [
                {
                  key: "dashboard",
                  label: "Tableau de bord",
                  icon: <FiBarChart2 />,
                  active: true,
                  onClick: () => {},
                },
              ],
            },
          ]} >
          <PilotDashboard />
        </AppShell>
      );
    }

    // MANAGEMENT
    if (user.role === "management") {
      const navSections = [
        {
          title: "WORKSPACE",
          items: [
            {
              key: "overview",
              label: "Tableau de bord",
              icon: <FiBarChart2 />,
              active: mgmtView === "overview",
              onClick: () => setMgmtView("overview"),
            },
            {
              key: "form",
              label: "Évaluations",
              icon: <FiList />,
              active: mgmtView === "form",
              onClick: () => setMgmtView("form"),
            },
            {
              key: "new",
              label: "Nouvelle évaluation",
              icon: <FiPlus />,
              active: mgmtView === "new",
              onClick: () => { setEditScoreId(""); setMgmtView("new"); },
            },
            {
              key: "coaching",
              label: "Coaching",
              icon: <FiHeadphones />,
              active: mgmtView === "coaching",
              onClick: () => setMgmtView("coaching"),
            },
          ],
        },
      ];

      return (
        <AppShell user={user} onLogout={handleLogout} navSections={navSections}>
          <ManagementDashboard forcedView={mgmtView} onViewChange={setMgmtView} />
        </AppShell>
      );
    }

    // fallback
    return (
      <AppShell user={user} onLogout={handleLogout} navSections={[]}>
        <div className="page">
          <div className="card">
            <p>Rôle non reconnu : {user.role}</p>
          </div>
        </div>
      </AppShell>
    );
  })();

  return <ThemeProvider defaultTheme="light">{content}</ThemeProvider>;
}

export default App;