import React from "react";
import CQHome from "./cq/CQHome.jsx";
import CQEvaluations from "./cq/CQEvaluations.jsx";
import CQCoaching from "./cq/CQCoaching.jsx";
import NewEvaluationPage from "./shared/NewEvaluationPage.jsx";

/**
 * CQDashboard (refactor)
 * - Plus de navigation interne.
 * - La sidebar (AppShell) contrôle forcedView.
 */
export default function CQDashboard({ forcedView, editScoreId }) {
  const view = forcedView || "stats";
  if (view === "new") return <NewEvaluationPage title="Nouvelle évaluation" editScoreId={editScoreId} />;
  if (view === "list") return <CQEvaluations />;
  if (view === "coaching") return <CQCoaching />;
  return <CQHome />;
}
