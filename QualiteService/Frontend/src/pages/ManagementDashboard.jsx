import React from "react";
import MgmtHome from "./management/MgmtHome.jsx";
import MgmtEvaluations from "./management/MgmtEvaluations.jsx";
import MgmtCoaching from "./management/MgmtCoaching.jsx";
import NewEvaluationPage from "./shared/NewEvaluationPage.jsx";
import PickingListPage from "./shared/PickingListPage.jsx";

export default function ManagementDashboard({ forcedView, onViewChange, editScoreId }) {
  const view = forcedView || "overview";

  if (view === "picking") {
    return <PickingListPage />;
  }

  if (view === "new") {
    return <NewEvaluationPage title="Nouvelle évaluation" editScoreId={editScoreId} />;
  }

  if (view === "form") return <MgmtEvaluations />;
  if (view === "coaching") return <MgmtCoaching />;

  return <MgmtHome />;
}
