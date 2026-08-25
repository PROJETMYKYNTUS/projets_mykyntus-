import React from "react";

export default function StatsCards(props) {
  const { stats, cqRows = [], mgRows = [] } = props;

  if (Array.isArray(stats)) {
    return (
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: 10 }}>
        {stats.slice(0, 6).map((c, i) => (
          <div key={i} className="card" style={{ padding: "14px 16px" }}>
            <div style={{ fontSize: "0.72rem", fontWeight: 700, color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>{c.label}</div>
            <div style={{ fontSize: "1.4rem", fontWeight: 800, marginTop: 4, letterSpacing: "-0.02em" }}>{c.value}</div>
          </div>
        ))}
      </div>
    );
  }

  const avg = (rows) => {
    const safe = Array.isArray(rows) ? rows : [];
    if (!safe.length) return 0;
    return Math.round((safe.reduce((acc, r) => acc + (Number(r.score) || 0), 0) / safe.length) * 10) / 10;
  };

  const items = [
    { label: "Évaluations CQ", value: cqRows.length },
    { label: "Score moyen CQ", value: `${avg(cqRows)}%` },
    { label: "Évaluations Management", value: mgRows.length },
    { label: "Score moyen Management", value: `${avg(mgRows)}%` },
  ];

  return (
    <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))", gap: 10 }}>
      {items.map((c, i) => (
        <div key={i} className="card" style={{ padding: "14px 16px" }}>
          <div style={{ fontSize: "0.72rem", fontWeight: 700, color: "var(--muted)", textTransform: "uppercase", letterSpacing: "0.05em" }}>{c.label}</div>
          <div style={{ fontSize: "1.4rem", fontWeight: 800, marginTop: 4, letterSpacing: "-0.02em" }}>{c.value}</div>
        </div>
      ))}
    </div>
  );
}
