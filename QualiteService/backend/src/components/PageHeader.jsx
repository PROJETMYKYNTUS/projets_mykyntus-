import React from "react";

export default function PageHeader({ title, subtitle, right }) {
  return (
    <div style={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: 16, marginBottom: 16 }}>
      <div>
        <div style={{ fontSize: 20, fontWeight: 700, letterSpacing: "-0.02em" }}>{title}</div>
        {subtitle ? <div style={{ marginTop: 4, color: "rgba(30,41,59,0.8)" }}>{subtitle}</div> : null}
      </div>
      {right ? <div>{right}</div> : null}
    </div>
  );
}
