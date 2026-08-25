import React from "react";

const cardStyle = {
  border: "1px solid #e5e7eb",
  borderRadius: 16,
  padding: "0.9rem",
  background: "#fff",
};

const labelStyle = { fontSize: "0.8rem", opacity: 0.75, marginBottom: 6 };
const valueStyle = { fontSize: "1.25rem", fontWeight: 900 };

export default function StatsCards({ cqRows, mgRows }) {
  const avg = (rows) => {
    if (!rows.length) return 0;
    const s = rows.reduce((acc, r) => acc + (Number(r.score) || 0), 0);
    return Math.round((s / rows.length) * 10) / 10;
  };

  return (
    <div style={{ display: "grid", gridTemplateColumns: "repeat(4, minmax(0, 1fr))", gap: "0.8rem" }}>
      <div style={cardStyle}>
        <div style={labelStyle}>Évaluations CQ</div>
        <div style={valueStyle}>{cqRows.length}</div>
      </div>
      <div style={cardStyle}>
        <div style={labelStyle}>Score moyen CQ</div>
        <div style={valueStyle}>{avg(cqRows)}%</div>
      </div>
      <div style={cardStyle}>
        <div style={labelStyle}>Évaluations Management</div>
        <div style={valueStyle}>{mgRows.length}</div>
      </div>
      <div style={cardStyle}>
        <div style={labelStyle}>Score moyen Management</div>
        <div style={valueStyle}>{avg(mgRows)}%</div>
      </div>
    </div>
  );
}
