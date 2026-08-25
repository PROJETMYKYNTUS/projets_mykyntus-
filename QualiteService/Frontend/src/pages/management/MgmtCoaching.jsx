import React from "react";
import CQCoaching from "../cq/CQCoaching.jsx";

/**
 * Management coaching = mêmes features que CQ (create + filters agent/date/evaluateur)
 * On réutilise le composant CQCoaching pour éviter duplication.
 */
export default function MgmtCoaching() {
  return <CQCoaching />;
}
