import React from "react";
import { isCqEmbed } from "../embed.js";

export default function PageHeader({ title, subtitle, right }) {
  if (isCqEmbed()) {
    return right ? <div style={{ display: "flex", justifyContent: "flex-end", marginBottom: 12 }}>{right}</div> : null;
  }
  return (
    <div style={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: 16, marginBottom: 16 }}>
      <div>
        <div style={{ fontSize: "1.25rem", fontWeight: 800, letterSpacing: "-0.02em", color: "var(--text)" }}>{title}</div>
        {subtitle ? <div style={{ marginTop: 4, color: "var(--muted)", fontSize: "0.875rem" }}>{subtitle}</div> : null}
      </div>
      {right ? <div>{right}</div> : null}
    </div>
  );
}
