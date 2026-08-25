import React from "react";
import CQEvaluations from "./cq/CQEvaluations.jsx";
import CQCoaching from "./cq/CQCoaching.jsx";
import CQSettings from "./cq/CQSettings.jsx";
import CQGrids from "./cq/CQGrids.jsx";
import CQHome from "./cq/CQHome.jsx";
import NewEvaluationPage from "./shared/NewEvaluationPage.jsx";
import PickingListPage from "./shared/PickingListPage.jsx";

export default function CQDashboard({ forcedView, onViewChange, editScoreId }) {
  const view = forcedView || "list";

  if (view === "picking") {
    return <PickingListPage />;
  }

  if (view === "new") {
    return <NewEvaluationPage title="Nouvelle évaluation" editScoreId={editScoreId} />;
  }

  if (view === "coaching") return <CQCoaching />;
  if (view === "grids") return <CQGrids />;
  if (view === "settings") return <CQSettings />;
  if (view === "stats" || view === "dashboard" || view === "overview") return <CQHome />;

  return <CQEvaluations />;
}

