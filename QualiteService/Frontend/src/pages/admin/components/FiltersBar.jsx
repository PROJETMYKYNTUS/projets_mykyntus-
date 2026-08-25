import React from "react";
import MultiSelect from "../../../components/MultiSelect.jsx";

export default function FiltersBar({
  q,
  setQ,
  year,
  setYear,
  month,
  setMonth,
  cell,
  setCell,
  grid,
  setGrid,
  pickingPrime,
  setPickingPrime,
  allYears,
  allMonths,
  allCells,
  allGrids,
  onExportAll,
  isTruncated,
}) {
  return (
    <div style={{ display: "flex", gap: "0.6rem", flexWrap: "wrap", alignItems: "center" }}>
      <input
        value={q}
        onChange={(e) => setQ(e.target.value)}
        placeholder="Recherche…"
        style={{
          padding: "0.5rem 0.75rem",
          borderRadius: "999px",
          border: "1px solid #d1d5db",
          background: "#ffffff",
          color: "#111827",
          minWidth: 220,
        }}
      />

      <div style={{ minWidth: 180 }}>
        <MultiSelect
          value={year}
          onChange={setYear}
          options={allYears.map((y) => ({ value: y, label: y }))}
          placeholder="Année"
        />
      </div>

      <div style={{ minWidth: 180 }}>
        <MultiSelect
          value={month}
          onChange={setMonth}
          options={allMonths.map((m) => ({ value: m, label: m }))}
          placeholder="Mois"
        />
      </div>

      <div style={{ minWidth: 220 }}>
        <MultiSelect
          value={cell}
          onChange={setCell}
          options={allCells.map((c) => ({ value: c, label: c }))}
          placeholder="Cellule"
        />
      </div>

      <div style={{ minWidth: 260 }}>
        <MultiSelect
          value={grid}
          onChange={setGrid}
          options={allGrids.map((g) => ({ value: g.id, label: g.name }))}
          placeholder="Grille"
        />
      </div>

      <div style={{ minWidth: 220 }}>
        <MultiSelect
          value={pickingPrime}
          onChange={setPickingPrime}
          isMulti={false}
          options={[
            { value: "true", label: "Picking prime : Vrai" },
            { value: "false", label: "Picking prime : Faux" },
          ]}
          placeholder="Picking prime"
        />
      </div>

      <button
        type="button"
        onClick={onExportAll}
        style={{
          padding: "0.5rem 0.9rem",
          borderRadius: "999px",
          border: "1px solid #d1d5db",
          background: "#ffffff",
          color: "#111827",
          cursor: "pointer",
          fontWeight: 700,
        }}
      >
        Export
      </button>

      {isTruncated ? (
        <span style={{ color: "#b45309", fontWeight: 700 }}>
          Volume important : affichage limité à 2000 lignes (affinez les filtres)
        </span>
      ) : null}
    </div>
  );
}
