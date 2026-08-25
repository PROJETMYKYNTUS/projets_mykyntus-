import React from "react";

export function StatCards({ stats }) {
  return (
    <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: 12, marginBottom: 16 }}>
      {stats.map((s) => (
        <div key={s.label} className="card" style={{ padding: 14 }}>
          <div style={{ color: "rgba(30,41,59,0.75)", fontSize: 12 }}>{s.label}</div>
          <div style={{ fontSize: 26, fontWeight: 800, marginTop: 6 }}>{s.value}</div>
          {s.help ? <div style={{ marginTop: 6, color: "rgba(30,41,59,0.65)", fontSize: 12 }}>{s.help}</div> : null}
        </div>
      ))}
    </div>
  );
}
