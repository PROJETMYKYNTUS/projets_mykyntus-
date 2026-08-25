import React from "react";
import DashboardView from "./admin/DashboardView.jsx";
import CoachingView from "./admin/CoachingView.jsx";
import UsersStructuresView from "./admin/UsersStructuresView.jsx";
import GridsView from "./admin/GridsView.jsx";
import NotificationsView from "./admin/NotificationsView.jsx";

/**
 * AdminDashboard (refactor)
 * - Fichier volontairement court (évite erreurs JSX).
 * - Navigation gérée UNIQUEMENT via sidebar (controlledTab depuis App.jsx).
 */
export default function AdminDashboard({ controlledTab }) {
  const tab = controlledTab || "dashboard";
  if (tab === "coaching") return <CoachingView />;
  if (tab === "grids") return <GridsView />;
  if (tab === "notifications") return <NotificationsView />;
  if (tab === "settings") return <UsersStructuresView />;
  // "dashboard" et "evaluations" : DashboardView (contient stats + table + export)
  return <DashboardView />;
}
