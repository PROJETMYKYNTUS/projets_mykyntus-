// src/pages/PilotDashboard.jsx
import React, { useEffect, useState, useMemo } from "react";
import api from "../api.js";
import {
  ResponsiveContainer,
  LineChart,
  Line,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
} from "recharts";
import { FiTrendingUp, FiStar, FiAlertTriangle } from "react-icons/fi";

// Helper : choisir une date de référence pour une évaluation
function getRefDate(score) {
  const d =
    score.listeningDate ||
    score.interactionDate ||
    score.callDate ||
    score.createdAt;
  return d ? new Date(d) : null;
}

const MONTHS_FR = [
  "Janvier",
  "Février",
  "Mars",
  "Avril",
  "Mai",
  "Juin",
  "Juillet",
  "Août",
  "Septembre",
  "Octobre",
  "Novembre",
  "Décembre",
];

function PilotDashboard() {
  const [loading, setLoading] = useState(true);
  const [status, setStatus] = useState("");
  const [scores, setScores] = useState([]);
  const [globalAverage, setGlobalAverage] = useState(0); // taux de conformité moyen (%)
  const [totalCount, setTotalCount] = useState(0);

  // Filtres tableau
  const [filterYear, setFilterYear] = useState("all");
  const [filterMonth, setFilterMonth] = useState("all");
  const [filterCq, setFilterCq] = useState("all");
  const [filterEps, setFilterEps] = useState("all");

  // Filtres KPI
  const [kpiYear, setKpiYear] = useState("all");
  const [kpiMonth, setKpiMonth] = useState("all");

  // Modal détail
  const [selectedEval, setSelectedEval] = useState(null);

  // Infos du pilote
  const user = useMemo(() => {
    try {
      const raw = localStorage.getItem("user");
      return raw ? JSON.parse(raw) : null;
    } catch {
      return null;
    }
  }, []);

  useEffect(() => {
    const fetchScores = async () => {
      try {
        const res = await api.get("/scores/me");
        const data = res.data || {};
        setGlobalAverage(data.average || 0); // taux de conformité moyen (%)
        setTotalCount(data.count || 0);
        setScores(data.scores || []);
      } catch (err) {
        console.error(err);
        setStatus("Erreur lors du chargement de vos évaluations.");
      } finally {
        setLoading(false);
      }
    };
    fetchScores();
  }, []);

  // Liste des années dispo
  const years = useMemo(() => {
    const set = new Set();
    scores.forEach((s) => {
      const d = getRefDate(s);
      if (d) set.add(d.getFullYear());
    });
    return Array.from(set).sort((a, b) => b - a);
  }, [scores]);

  // CQ distincts
  const cqOptions = useMemo(() => {
    const map = new Map();
    scores.forEach((s) => {
      if (s.cq && s.cq.email) {
        const key = s.cq.email;
        const label = `${s.cq.name || "CQ"} (${s.cq.email})`;
        map.set(key, label);
      }
    });
    return Array.from(map.entries()).map(([value, label]) => ({
      value,
      label,
    }));
  }, [scores]);

  // EPS distinctes
  const epsOptions = useMemo(() => {
    const set = new Set();
    scores.forEach((s) => {
      if (s.eps) set.add(s.eps);
    });
    return Array.from(set);
  }, [scores]);

  // Évals filtrées tableau
  const filteredScores = useMemo(() => {
    return scores.filter((s) => {
      const d = getRefDate(s);
      if (!d) return false;

      const y = d.getFullYear();
      const m = d.getMonth() + 1;

      if (filterYear !== "all" && String(y) !== String(filterYear)) return false;
      if (filterMonth !== "all" && String(m) !== String(filterMonth)) return false;

      if (filterCq !== "all") {
        if (!s.cq || s.cq.email !== filterCq) return false;
      }

      if (filterEps !== "all") {
        if ((s.eps || "").toLowerCase() !== filterEps.toLowerCase()) return false;
      }

      return true;
    });
  }, [scores, filterYear, filterMonth, filterCq, filterEps]);

  // Nb évaluations du mois en cours
  const currentMonthCount = useMemo(() => {
    const now = new Date();
    const y = now.getFullYear();
    const m = now.getMonth();
    return scores.filter((s) => {
      const d = getRefDate(s);
      if (!d) return false;
      return d.getFullYear() === y && d.getMonth() === m;
    }).length;
  }, [scores]);

  // Stat par item (taux de conformité % par critère)
  // Règle: C=100, NC=0, NA exclu du calcul.
  const itemsStats = useMemo(() => {
    const map = new Map();

    scores.forEach((s) => {
      (s.items || []).forEach((it) => {
        const key = it.label || "Item";

        // On ignore les lignes "group" si jamais elles arrivent par erreur côté scores
        if (it.type === "group") return;

        const status = (it.status || "").toUpperCase();
        if (!map.has(key)) {
          map.set(key, { label: key, compliant: 0, applicable: 0 });
        }
        const obj = map.get(key);

        if (status === "NA") return; // non applicable : exclu
        if (status === "C") {
          obj.compliant += 1;
          obj.applicable += 1;
          return;
        }
        if (status === "NC") {
          obj.applicable += 1;
          return;
        }

        // Compat legacy (value 1..5) si status absent
        const value = Number(it.value || 0);
        if (value <= 0) return;
        // Mapping simple : 4-5 => Conforme, 1-3 => Non conforme
        if (value >= 4) obj.compliant += 1;
        obj.applicable += 1;
      });
    });

    const arr = Array.from(map.values()).map((o) => ({
      label: o.label,
      avg: o.applicable > 0 ? (o.compliant / o.applicable) * 100 : 0,
    }));

    return arr.sort((a, b) => a.label.localeCompare(b.label));
  }, [scores]);

  const bestItem = useMemo(() => {
    if (!itemsStats.length) return null;
    return itemsStats.reduce((best, it) =>
      it.avg > (best?.avg ?? -1) ? it : best
    );
  }, [itemsStats]);

  const worstItem = useMemo(() => {
    if (!itemsStats.length) return null;
    return itemsStats.reduce((worst, it) =>
      it.avg < (worst?.avg ?? 999) ? it : worst
    );
  }, [itemsStats]);

  const improvementAreas = useMemo(() => {
    const list = itemsStats.filter((it) => it.avg < 70);
    return list.sort((a, b) => a.avg - b.avg);
  }, [itemsStats]);

  // Données graphe évolution (%)
  const trendData = useMemo(() => {
    const arr = [...scores]
      .map((s) => {
        const d = getRefDate(s);
        if (!d) return null;
        return {
          date: d,
          label: d.toLocaleDateString("fr-FR", {
            day: "2-digit",
            month: "2-digit",
          }),
          total: Number(s.total || 0),
        };
      })
      .filter(Boolean)
      .sort((a, b) => a.date - b.date);
    return arr;
  }, [scores]);

  // Scores utilisés pour la KPI (année/mois)
  const kpiScores = useMemo(
    () =>
      scores.filter((s) => {
        const d = getRefDate(s);
        if (!d) return false;
        const y = d.getFullYear();
        const m = d.getMonth() + 1;

        if (kpiYear !== "all" && String(y) !== String(kpiYear)) return false;
        if (kpiMonth !== "all" && String(m) !== String(kpiMonth)) return false;

        return true;
      }),
    [scores, kpiYear, kpiMonth]
  );

  const kpiAverage = useMemo(() => {
    if (!kpiScores.length) return 0;
    const sum = kpiScores.reduce((acc, s) => acc + Number(s.total || 0), 0);
    return sum / kpiScores.length;
  }, [kpiScores]);

  const kpiCount = kpiScores.length;

  const getScoreBadgeClass = (value) => {
    const v = Number(value || 0);
    if (v >= 85) return "badge badge-green";
    if (v >= 70) return "badge badge-yellow";
    return "badge badge-red";
  };

  const getGaugeClass = (value) => {
    if (value >= 85) return "gauge-green";
    if (value >= 70) return "gauge-yellow";
    return "gauge-red";
  };

  // ---------- KPIs ----------
  const renderKpis = () => (
    <div className="kpi-grid">
      {/* KPI 1 : Score moyen filtré + jauge + filtres */}
      <div className="kpi-card">
        <div className="kpi-icon kpi-icon-blue">
          <FiTrendingUp />
        </div>

        <div className="kpi-content kpi-content-full">
          {/* Titre + filtres sur une ligne */}
          <div className="kpi-header-row">
            <div className="kpi-label">Score moyen (période filtrée)</div>
            <div className="kpi-filters">
              <select
                className="input"
                value={kpiYear}
                onChange={(e) => setKpiYear(e.target.value)}
              >
                <option value="all">Toutes années</option>
                {years.map((y) => (
                  <option key={y} value={y}>
                    {y}
                  </option>
                ))}
              </select>

              <select
                className="input"
                value={kpiMonth}
                onChange={(e) => setKpiMonth(e.target.value)}
              >
                <option value="all">Tous mois</option>
                {MONTHS_FR.map((m, idx) => (
                  <option key={idx + 1} value={idx + 1}>
                    {m}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="kpi-value" style={{ marginTop: "0.4rem" }}>
            {kpiAverage.toFixed(2)} <span className="kpi-unit">%</span>
          </div>
          <div className="kpi-sub">Taux de conformité moyen sur la période sélectionnée.</div>
          <div className="kpi-sub">
            {kpiCount > 0
              ? `Basé sur ${kpiCount} évaluation${kpiCount > 1 ? "s" : ""}.`
              : "Aucune évaluation sur cette période."}
          </div>

          {/* Jauge */}
          <div className="gauge-container">
            <div className="gauge-track">
              <div
                className={`gauge-fill ${getGaugeClass(kpiAverage)}`}
                style={{
                  width: `${Math.min(100, Number(kpiAverage || 0))}%`,
                }}
              />
            </div>
            <div className="gauge-legend">
              <span className="gauge-dot gauge-dot-red" /> Zone rouge
              <span className="gauge-dot gauge-dot-yellow" /> Zone orange
              <span className="gauge-dot gauge-dot-green" /> Zone verte
            </div>
          </div>
        </div>
      </div>

      {/* KPI 2 : évaluations du mois */}
      <div className="kpi-card">
        <div className="kpi-icon kpi-icon-green">
          <FiStar />
        </div>
        <div className="kpi-content">
          <div className="kpi-label">Évaluations ce mois</div>
          <div className="kpi-value">{currentMonthCount}</div>
          <div className="kpi-sub">
            {currentMonthCount === 0
              ? "Aucune écoute enregistrée ce mois-ci."
              : "Continuons sur cette dynamique."}
          </div>
        </div>
      </div>

      {/* KPI 3 : point fort */}
      <div className="kpi-card">
        <div className="kpi-icon kpi-icon-green">
          <FiStar />
        </div>
        <div className="kpi-content">
          <div className="kpi-label">Point fort</div>
          <div className="kpi-value">
            {bestItem ? bestItem.label : "-"}
          </div>
          <div className="kpi-sub">
            {bestItem
              ? `Taux : ${bestItem.avg.toFixed(1)}%`
              : "En attente de premières évaluations."}
          </div>
        </div>
      </div>

      {/* KPI 4 : axe prioritaire */}
      <div className="kpi-card">
        <div className="kpi-icon kpi-icon-orange">
          <FiAlertTriangle />
        </div>
        <div className="kpi-content">
          <div className="kpi-label">Axe prioritaire</div>
          <div className="kpi-value">
            {worstItem ? worstItem.label : "-"}
          </div>
          <div className="kpi-sub">
            {worstItem
              ? `Taux : ${worstItem.avg.toFixed(1)}%`
              : "Pas assez de données pour identifier un axe."}
          </div>
        </div>
      </div>
    </div>
  );

  // ---------- Graphique évolution ----------
  const renderTrendChart = () => (
    <div className="card-section">
      <div className="card-section-header">
        <h3>Évolution de vos scores</h3>
        <span className="card-section-sub">
          Progression de vos évaluations dans le temps (taux de conformité %).
        </span>
      </div>
      {trendData.length === 0 ? (
        <p style={{ fontSize: "0.85rem" }}>Aucune évaluation pour le moment.</p>
      ) : (
        <div style={{ width: "100%", height: 240 }}>
          <ResponsiveContainer>
            <LineChart data={trendData}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="label" tick={{ fontSize: 10 }} height={40} />
              <YAxis domain={[0, 100]} tick={{ fontSize: 10 }} />
              <Tooltip />
              <Line type="monotone" dataKey="total" strokeWidth={2} dot={{ r: 3 }} />
            </LineChart>
          </ResponsiveContainer>
        </div>
      )}
    </div>
  );

  // ---------- Graphique par item ----------
  const renderItemsChart = () => (
    <div className="card-section">
      <div className="card-section-header">
        <h3>Profil par item</h3>
        <span className="card-section-sub">
          Taux de conformité moyen par critère (en %).
        </span>
      </div>
      {itemsStats.length === 0 ? (
        <p style={{ fontSize: "0.85rem" }}>Aucune donnée par item pour le moment.</p>
      ) : (
        <div style={{ width: "100%", height: 260 }}>
          <ResponsiveContainer>
            <BarChart data={itemsStats}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis
                dataKey="label"
                angle={-20}
                textAnchor="end"
                interval={0}
                height={70}
                tick={{ fontSize: 10 }}
              />
              <YAxis domain={[0, 100]} tick={{ fontSize: 10 }} />
              <Tooltip />
              <Bar dataKey="avg" />
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}
    </div>
  );

  // ---------- Axes de progrès ----------
  const renderImprovementBlock = () => (
    <div className="card-section">
      <div className="card-section-header">
        <h3>Mes axes de progrès</h3>
        <span className="card-section-sub">
          Points à travailler en priorité (taux &lt; 70%).
        </span>
      </div>
      {improvementAreas.length === 0 ? (
        <p style={{ fontSize: "0.85rem" }}>
          Aucun axe prioritaire identifié pour le moment. Continuez comme ça !
        </p>
      ) : (
        <ul className="improvement-list">
          {improvementAreas.map((it) => (
            <li key={it.label} className="improvement-item">
              <div className="improvement-title">{it.label}</div>
              <div className="improvement-score">
                <span className={getScoreBadgeClass(it.avg)}>
                  {it.avg.toFixed(1)} %
                </span>
              </div>
              <div className="improvement-hint">
                Pensez à : adapter votre discours, vérifier la compréhension du
                client, reformuler les informations clés, et rester dans une
                posture d’écoute active.
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );

  // ---------- Tableau des évaluations ----------
  const renderTable = () => (
    <div className="card-section">
      <div className="card-section-header">
        <div>
          <h3>Mes évaluations</h3>
          <span className="card-section-sub">
            Filtrez vos écoutes par période, CQ ou EPS, et consultez le détail.
            Les évaluations contestées par le management sont barrées et signalées.
          </span>
        </div>
      </div>

      {/* Filtres tableau */}
      <div
        style={{
          display: "flex",
          flexWrap: "wrap",
          gap: "0.5rem",
          marginBottom: "0.75rem",
        }}
      >
        <select
          className="input"
          style={{ maxWidth: "120px" }}
          value={filterYear}
          onChange={(e) => setFilterYear(e.target.value)}
        >
          <option value="all">Toutes années</option>
          {years.map((y) => (
            <option key={y} value={y}>
              {y}
            </option>
          ))}
        </select>

        <select
          className="input"
          style={{ maxWidth: "140px" }}
          value={filterMonth}
          onChange={(e) => setFilterMonth(e.target.value)}
        >
          <option value="all">Tous mois</option>
          {MONTHS_FR.map((m, idx) => (
            <option key={idx + 1} value={idx + 1}>
              {m}
            </option>
          ))}
        </select>

        <select
          className="input"
          style={{ maxWidth: "200px" }}
          value={filterCq}
          onChange={(e) => setFilterCq(e.target.value)}
        >
          <option value="all">Tous CQ</option>
          {cqOptions.map((cq) => (
            <option key={cq.value} value={cq.value}>
              {cq.label}
            </option>
          ))}
        </select>

        <select
          className="input"
          style={{ maxWidth: "200px" }}
          value={filterEps}
          onChange={(e) => setFilterEps(e.target.value)}
        >
          <option value="all">Toutes EPS</option>
          {epsOptions.map((eps) => (
            <option key={eps} value={eps}>
              {eps}
            </option>
          ))}
        </select>
      </div>

      {filteredScores.length === 0 ? (
        <p style={{ fontSize: "0.85rem" }}>
          Aucune évaluation ne correspond à ces filtres.
        </p>
      ) : (
        <div
          style={{
            overflowX: "auto",
          }}
        >
          <table
            style={{
              width: "100%",
              fontSize: "0.85rem",
              borderCollapse: "collapse",
            }}
          >
            <thead>
              <tr>
                <th
                  style={{
                    textAlign: "left",
                    borderBottom: "1px solid #1f2937",
                    padding: "0.35rem",
                  }}
                >
                  Date
                </th>
                <th
                  style={{
                    textAlign: "left",
                    borderBottom: "1px solid #1f2937",
                    padding: "0.35rem",
                  }}
                >
                  CQ
                </th>
                <th
                  style={{
                    textAlign: "left",
                    borderBottom: "1px solid #1f2937",
                    padding: "0.35rem",
                  }}
                >
                  EPS
                </th>
                <th
                  style={{
                    textAlign: "left",
                    borderBottom: "1px solid #1f2937",
                    padding: "0.35rem",
                  }}
                >
                  Durée
                </th>
                <th
                  style={{
                    textAlign: "right",
                    borderBottom: "1px solid #1f2937",
                    padding: "0.35rem",
                  }}
                >
                  Score total
                </th>
                <th
                  style={{
                    textAlign: "right",
                    borderBottom: "1px solid #1f2937",
                    padding: "0.35rem",
                  }}
                >
                  Détail
                </th>
              </tr>
            </thead>
            <tbody>
              {filteredScores.map((s) => {
                const d = getRefDate(s);
                const dateLabel = d ? d.toLocaleDateString("fr-FR") : "-";
                const cqLabel = s.cq
                  ? `${s.cq.name || "CQ"} (${s.cq.email || ""})`
                  : "-";
                const isContested = !!s.contested;

                return (
                  <tr
                    key={s._id}
                    style={{
                      textDecoration: isContested ? "line-through" : "none",
                      opacity: isContested ? 0.6 : 1,
                      borderBottom: "1px solid #111827",
                    }}
                  >
                    <td style={{ padding: "0.35rem" }}>{dateLabel}</td>
                    <td style={{ padding: "0.35rem" }}>{cqLabel}</td>
                    <td style={{ padding: "0.35rem" }}>{s.eps || "—"}</td>
                    <td style={{ padding: "0.35rem" }}>
                      {s.callDuration || "—"}
                    </td>
                    <td
                      style={{
                        padding: "0.35rem",
                        textAlign: "right",
                      }}
                    >
                      <div
                        style={{
                          display: "flex",
                          justifyContent: "flex-end",
                          alignItems: "center",
                          gap: "0.4rem",
                        }}
                      >
                        <span className={getScoreBadgeClass(s.total || 0)}>
                          {(s.total || 0).toFixed(1)}
                        </span>
                        {isContested && (
                          <span
                            style={{
                              fontSize: "0.7rem",
                              padding: "0.1rem 0.35rem",
                              borderRadius: "999px",
                              border: "1px solid #f97316",
                              color: "#f97316",
                              whiteSpace: "nowrap",
                            }}
                          >
                            Contestée
                          </span>
                        )}
                      </div>
                    </td>
                    <td
                      style={{
                        padding: "0.35rem",
                        textAlign: "right",
                      }}
                    >
                      <button
                        className="btn-outline"
                        style={{ fontSize: "0.75rem" }}
                        onClick={() => setSelectedEval(s)}
                      >
                        Voir
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );

  // ---------- Modal détail (scrollable & centrée) ----------
  const renderDetailModal = () => {
    if (!selectedEval) return null;

    const s = selectedEval;
    const d = getRefDate(s);
    const dateLabel = d
      ? d.toLocaleDateString("fr-FR", {
          day: "2-digit",
          month: "2-digit",
          year: "numeric",
        })
      : "-";

    const isContested = !!s.contested;

    const scoreBadgeClass = (it) => {
      const status = (it && it.status) || "";
      if (status === "C") return "badge badge-green";
      if (status === "NC") return "badge badge-red";
      if (status === "NA") return "badge badge-gray";

      // compat legacy : value 1..5
      const v =
        typeof it?.value === "number" ? it.value : Number(it?.value) || 0;
      if (v >= 4) return "badge badge-green";
      if (v >= 3) return "badge badge-yellow";
      return "badge badge-red";
    };

    return (
      <div
        className="modal-overlay"
        onClick={() => setSelectedEval(null)}
        style={{
          position: "fixed",
          inset: 0,
          backgroundColor: "rgba(0,0,0,0.6)",
          display: "flex",
          justifyContent: "center",
          alignItems: "center",
          padding: "1rem",
          zIndex: 50,
        }}
      >
        <div
          className="modal"
          onClick={(e) => e.stopPropagation()}
          style={{
            maxWidth: "720px",
            width: "100%",
            maxHeight: "90vh",
            overflowY: "auto",
            backgroundColor: "#020617",
            borderRadius: "0.75rem",
            padding: "1rem 1.25rem 1.2rem",
            boxShadow: "0 20px 45px rgba(0,0,0,0.7)",
            border: "1px solid #1f2937",
          }}
        >
          <div className="modal-header" style={{ marginBottom: "0.75rem" }}>
            <h3>Détail de l’évaluation</h3>
            <button
              className="btn-outline"
              style={{ padding: "0.1rem 0.6rem", fontSize: "0.8rem" }}
              onClick={() => setSelectedEval(null)}
            >
              Fermer
            </button>
          </div>
          <div className="modal-body">
            <div
              className="modal-info-grid"
              style={{
                display: "grid",
                gridTemplateColumns: "repeat(auto-fit,minmax(150px,1fr))",
                gap: "0.75rem",
                marginBottom: "0.9rem",
              }}
            >
              <div>
                <div className="modal-label">Date d’écoute</div>
                <div className="modal-value">{dateLabel}</div>
              </div>
              <div>
                <div className="modal-label">CQ</div>
                <div className="modal-value">
                  {s.cq
                    ? `${s.cq.name || "CQ"} (${s.cq.email || ""})`
                    : "—"}
                </div>
              </div>
              <div>
                <div className="modal-label">EPS</div>
                <div className="modal-value">{s.eps || "—"}</div>
              </div>
              <div>
                <div className="modal-label">Durée</div>
                <div className="modal-value">
                  {s.callDuration || "—"}
                </div>
              </div>
              <div>
                <div className="modal-label">Score total</div>
                <div className="modal-value">
                  <span className={getScoreBadgeClass(s.total || 0)}>
                    {(s.total || 0).toFixed(1)} %
                  </span>
                </div>
              </div>
            </div>

            {/* Bloc contestation */}
            {isContested && (
              <div
                style={{
                  marginBottom: "0.9rem",
                  padding: "0.6rem 0.8rem",
                  borderRadius: "0.5rem",
                  border: "1px solid #f97316",
                  background: "#451a03",
                  fontSize: "0.8rem",
                }}
              >
                <strong>
                  ⚠ Cette évaluation a été contestée par le management.
                </strong>
                {s.contestComment && (
                  <div style={{ marginTop: "0.25rem" }}>
                    Motif : {s.contestComment}
                  </div>
                )}
              </div>
            )}

            <h4 style={{ marginBottom: "0.5rem" }}>Détail par item</h4>
            <div className="items-grid" style={{ marginBottom: "0.9rem" }}>
              {(s.items || []).map((it, idx) => (
                <div key={idx} className="item-row">
                  <div className="item-label">
                    {idx + 1}. {it.label}
                  </div>
                  <span className={scoreBadgeClass(it)}>
                    {it.status === 'NA' ? 'Non applicable' : it.status === 'NC' ? 'Non conforme' : it.status === 'C' ? 'Conforme' : `${((it.value||0)).toFixed(1)} / 5`}`
                  </span>
                </div>
              ))}
            </div>

            <h4 style={{ marginBottom: "0.35rem" }}>Commentaire du CQ</h4>
            <div
              className="modal-comment"
              style={{
                fontSize: "0.85rem",
                backgroundColor: "#020617",
                border: "1px solid #111827",
                borderRadius: "0.5rem",
                padding: "0.6rem 0.75rem",
                whiteSpace: "pre-wrap",
              }}
            >
              {s.comment && s.comment.trim().length > 0
                ? s.comment
                : "Aucun commentaire renseigné."}
            </div>
          </div>
        </div>
      </div>
    );
  };

  if (loading) {
    return (
      <div className="page">
        <div className="card">
          <p>Chargement de vos données…</p>
        </div>
      </div>
    );
  }

  return (
    <div className="page">
      <div className="card">
        {/* HEADER PILOTE */}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "flex-start",
            gap: "1rem",
          }}
        >
          <div>
            <h2>Bonjour {user?.name || "Pilote"} 👋</h2>
            <p className="subtitle">
              Voici une vue d’ensemble de vos évaluations et de vos axes de
              progrès.
            </p>
          </div>

          <div
            style={{
              display: "flex",
              flexDirection: "column",
              alignItems: "flex-end",
              gap: "0.4rem",
            }}
          >
            <div
              style={{
                textAlign: "right",
                fontSize: "0.85rem",
                color: "#9ca3af",
              }}
            >
              <div>
                Total d’évaluations : <strong>{totalCount}</strong>
              </div>
              <div>
                Taux de conformité moyen :{" "}
                <strong>{globalAverage.toFixed(1)}%</strong>{" "}
                <span style={{ fontSize: "0.8rem" }}>
                  
                </span>
              </div>
            </div>
          </div>
        </div>

        {status && (
          <div className="alert" style={{ marginTop: "0.75rem" }}>
            {status}
          </div>
        )}

        {/* KPIs */}
        <div style={{ marginTop: "0.75rem" }}>{renderKpis()}</div>

        {/* Graphiques */}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "minmax(0, 1.2fr) minmax(0, 1fr)",
            gap: "1rem",
            marginTop: "1.25rem",
          }}
        >
          {renderTrendChart()}
          {renderItemsChart()}
        </div>

        {/* Axes de progrès + tableau */}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "minmax(0, 1fr) minmax(0, 1.4fr)",
            gap: "1rem",
            marginTop: "1.25rem",
          }}
        >
          {renderImprovementBlock()}
          {renderTable()}
        </div>
      </div>

      {renderDetailModal()}
    </div>
  );
}

export default PilotDashboard;
