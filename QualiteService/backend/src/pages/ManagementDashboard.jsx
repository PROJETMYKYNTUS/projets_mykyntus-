import React from "react";
import MgmtHome from "./management/MgmtHome.jsx";
import MgmtEvaluations from "./management/MgmtEvaluations.jsx";
import MgmtCoaching from "./management/MgmtCoaching.jsx";
import NewEvaluationPage from "./shared/NewEvaluationPage.jsx";

/**
 * ManagementDashboard (refactor)
 * - Plus de navigation interne.
 * - Sidebar contrôle forcedView.
 */
export default function ManagementDashboard({ forcedView, editScoreId }) {
  const view = forcedView || "overview";
  if (view === "new") return <NewEvaluationPage title="Nouvelle évaluation" editScoreId={editScoreId} />;
  if (view === "form") return <MgmtEvaluations />;
  if (view === "coaching") return <MgmtCoaching />;
  return <MgmtHome />;
}
