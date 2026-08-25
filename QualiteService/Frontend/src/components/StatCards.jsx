import React from "react";

export function StatCards({ stats }) {
  return (
    <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: 10, marginBottom: 16 }}>
      {stats.map((s) => (
        <div key={s.label} className="card" style={{ padding: "14px 16px" }}>
          <div style={{ color: "var(--muted)", fontSize: "0.75rem", fontWeight: 700, textTransform: "uppercase", letterSpacing: "0.04em" }}>{s.label}</div>
          <div style={{ fontSize: "1.5rem", fontWeight: 800, marginTop: 4, letterSpacing: "-0.02em" }}>{s.value}</div>
          {s.help ? <div style={{ marginTop: 4, color: "var(--muted)", fontSize: "0.78rem" }}>{s.help}</div> : null}
        </div>
      ))}
    </div>
  );
}
