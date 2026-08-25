import React, { useMemo, useState } from "react";

const backdrop = {
  position: "fixed",
  inset: 0,
  background: "rgba(0,0,0,0.35)",
  display: "flex",
  alignItems: "center",
  justifyContent: "center",
  zIndex: 1000,
  padding: "1rem",
};

const panel = {
  width: "min(920px, 96vw)",
  maxHeight: "85vh",
  overflow: "auto",
  background: "white",
  borderRadius: 18,
  border: "1px solid #e5e7eb",
  padding: "1rem",
};

function notationOptionsFor(items) {
  const hasPresence = (items || []).some((it) => ["PC", "PNC", "NP"].includes(String(it?.status || "").toUpperCase()));
  return hasPresence
    ? [
        { value: "PC", label: "Présent Conforme (PC)" },
        { value: "PNC", label: "Présent Non Conforme (PNC)" },
        { value: "NP", label: "Non Présent (NP)" },
        { value: "NA", label: "Non Applicable (NA)" },
      ]
    : [
        { value: "C", label: "Conforme (C)" },
        { value: "NC", label: "Non Conforme (NC)" },
        { value: "NA", label: "Non Applicable (NA)" },
      ];
}

export default function EditScoreModal({ score, onClose, onSave, isSaving }) {
  const initialItems = useMemo(() => {
    const baseItems = Array.isArray(score?.items) ? score.items : [];
    return baseItems
      .filter((it) => (it?.label || "").toString().trim().length > 0)
      .map((it) => ({
        label: (it?.label || "").toString(),
        status: (it?.status || "").toString(),
        value: typeof it?.value === "number" ? it.value : 0,
      }));
  }, [score]);

  const [items, setItems] = useState(initialItems);

  const options = useMemo(() => notationOptionsFor(items), [items]);

  const updateStatus = (idx, status) => {
    setItems((prev) => prev.map((it, i) => (i === idx ? { ...it, status } : it)));
  };

  const handleSave = () => {
    onSave?.(items);
  };

  if (!score) return null;

  return (
    <div style={backdrop} onClick={onClose}>
      <div style={panel} onClick={(e) => e.stopPropagation()}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: "1rem" }}>
          <div>
            <div style={{ fontWeight: 900, fontSize: "1.05rem" }}>Éditer la notation</div>
            <div style={{ opacity: 0.75, marginTop: 2, fontSize: "0.9rem" }}>
              {score?.pilotName || score?.pilot || ""} — {score?.eps || "Sans EPS"}
            </div>
          </div>
          <button className="btn-outline" onClick={onClose}>
            Fermer
          </button>
        </div>

        <div style={{ marginTop: "0.9rem", borderTop: "1px solid #e5e7eb" }} />

        <div style={{ display: "grid", gridTemplateColumns: "1fr", gap: "0.6rem", marginTop: "0.9rem" }}>
          {items.map((it, idx) => (
            <div
              key={`${it.label}-${idx}`}
              style={{
                display: "grid",
                gridTemplateColumns: "1fr 260px",
                gap: "0.8rem",
                alignItems: "center",
                padding: "0.65rem",
                border: "1px solid #e5e7eb",
                borderRadius: 14,
              }}
            >
              <div style={{ fontWeight: 700, whiteSpace: "pre-wrap" }}>{it.label}</div>
              <select
                value={it.status}
                onChange={(e) => updateStatus(idx, e.target.value)}
                style={{
                  width: "100%",
                  padding: "0.55rem 0.7rem",
                  borderRadius: 12,
                  border: "1px solid #d1d5db",
                  background: "#fff",
                }}
              >
                <option value="">—</option>
                {options.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </select>
            </div>
          ))}
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.6rem", marginTop: "1rem" }}>
          <button className="btn-outline" onClick={onClose} disabled={isSaving}>
            Annuler
          </button>
          <button className="btn" onClick={handleSave} disabled={isSaving}>
            {isSaving ? "Enregistrement…" : "Enregistrer"}
          </button>
        </div>
      </div>
    </div>
  );
}
