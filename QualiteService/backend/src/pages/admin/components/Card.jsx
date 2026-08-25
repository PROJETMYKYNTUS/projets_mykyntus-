import React from "react";

export default function Card({ title, right, children, style }) {
  return (
    <div
      style={{
        background:
          "#ffffff",
        border: "1px solid #e5e7eb",
        borderRadius: "1rem",
        padding: "1rem",
        marginBottom: "1rem",
        color: "#111827",
        ...style,
      }}
    >
      {(title || right) && (
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: "1rem" }}>
          <div style={{ fontWeight: 900, fontSize: "1.05rem" }}>{title}</div>
          <div>{right}</div>
        </div>
      )}
      <div style={{ marginTop: title || right ? "0.75rem" : 0 }}>{children}</div>
    </div>
  );
}