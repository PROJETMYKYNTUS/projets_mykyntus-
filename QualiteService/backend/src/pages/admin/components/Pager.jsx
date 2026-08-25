import React from "react";

export default function Pager({ page, pageSize, total, onPrev, onNext }) {
  const maxPage = Math.max(1, Math.ceil((total || 0) / pageSize));
  const start = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const end = Math.min(total || 0, page * pageSize);

  return (
    <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", flexWrap: "wrap", marginTop: "0.75rem" }}>
      <button
        onClick={onPrev}
        disabled={page <= 1}
        className="btn-outline"
        style={{ opacity: page <= 1 ? 0.5 : 1 }}
      >
        Précédent
      </button>
      <button
        onClick={onNext}
        disabled={page >= maxPage}
        className="btn-outline"
        style={{ opacity: page >= maxPage ? 0.5 : 1 }}
      >
        Suivant
      </button>
      <span style={{ opacity: 0.85 }}>
        {start}–{end} / {total} • Page {page} / {maxPage}
      </span>
    </div>
  );
}