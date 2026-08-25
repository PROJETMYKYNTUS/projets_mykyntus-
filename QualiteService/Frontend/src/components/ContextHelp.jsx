import React, { useMemo } from "react";
import "./contextHelp.css";

const HELP = {
  new_evaluation: {
    title: "Créer / modifier une évaluation",
    bullets: [
      "Renseigne les informations, puis complète les critères.",
      "La conformité est calculée en temps réel.",
    ],
    shortcuts: [
      { k: "Ctrl + Entrée", v: "Créer / enregistrer" },
      { k: "Échap", v: "Fermer un modal" },
    ],
  },
  cq_evaluations: {
    title: "Mes évaluations (CQ)",
    bullets: [
      "Utilise les filtres pour cibler la période et l’agent.",
      "Traite les contestations via le filtre “Contestées”.",
    ],
    shortcuts: [
      { k: "Ctrl + E", v: "Exporter Excel" },
    ],
  },
  mgmt_evaluations: {
    title: "Évaluations (Management)",
    bullets: [
      "Conteste une évaluation pour demander une réévaluation au CQ.",
    ],
    shortcuts: [
      { k: "Ctrl + E", v: "Exporter Excel" },
    ],
  },
  admin_audit: {
    title: "Audit log (Admin)",
    bullets: [
      "Traçabilité des actions sensibles (contestation, réévaluation, suppression).",
    ],
    shortcuts: [
      { k: "Ctrl + E", v: "Exporter" },
    ],
  },
  admin_health: {
    title: "Santé (Admin)",
    bullets: [
      "État API, MongoDB et Socket en temps réel.",
    ],
    shortcuts: [],
  },
};

export default function ContextHelp({ pageKey }) {
  const cfg = useMemo(() => HELP[pageKey] || null, [pageKey]);
  if (!cfg) return null;

  return (
    <div className="kcq-help">
      <div className="kcq-help__title">{cfg.title}</div>
      <ul className="kcq-help__list">
        {cfg.bullets.map((b) => (<li key={b}>{b}</li>))}
      </ul>
      {cfg.shortcuts?.length ? (
        <div className="kcq-help__shortcuts" aria-label="Raccourcis clavier">
          {cfg.shortcuts.map((s) => (
            <div className="kcq-help__shortcut" key={s.k}>
              <kbd>{s.k}</kbd>
              <span>{s.v}</span>
            </div>
          ))}
        </div>
      ) : null}
    </div>
  );
}
