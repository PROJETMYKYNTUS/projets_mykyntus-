import React from "react";
import Select from "react-select";

export default function MultiSelect({
  value,
  onChange,
  options,
  placeholder = "Sélectionner...",
  isMulti = true,
  isClearable = true,
  isDisabled = false,
}) {
  const safeOptions = Array.isArray(options) ? options : [];
  const safeValue = Array.isArray(value) ? value : value ? [value] : [];
  const selected = safeOptions.filter((o) => safeValue.includes(String(o.value)));

  return (
    <Select
      isMulti={isMulti}
      isClearable={isClearable}
      isDisabled={isDisabled}
      options={safeOptions}
      value={selected}
      placeholder={placeholder}
      closeMenuOnSelect={!isMulti}
      onChange={(vals) => {
        if (!vals || (Array.isArray(vals) && vals.length === 0)) { onChange([]); return; }
        const arr = Array.isArray(vals) ? vals : [vals];
        onChange(arr.map((v) => String(v.value)));
      }}
      styles={{
        control: (base, state) => ({
          ...base,
          minHeight: 38,
          borderRadius: 10,
          borderColor: state.isFocused ? "#4f46e5" : "rgba(15,23,42,0.14)",
          boxShadow: state.isFocused ? "0 0 0 3px rgba(79,70,229,0.08)" : "none",
          background: "var(--panel)",
          fontSize: "0.875rem",
          "&:hover": { borderColor: "rgba(79,70,229,0.3)" },
        }),
        valueContainer: (base) => ({
          ...base,
          flexWrap: "nowrap",
          overflow: "hidden",
          padding: "2px 8px",
        }),
        multiValue: (base) => ({
          ...base,
          maxWidth: 140,
          borderRadius: 6,
          background: "rgba(79,70,229,0.08)",
        }),
        multiValueLabel: (base) => ({
          ...base,
          whiteSpace: "nowrap",
          overflow: "hidden",
          textOverflow: "ellipsis",
          maxWidth: 110,
          fontSize: "0.8rem",
          fontWeight: 600,
          color: "#4f46e5",
        }),
        multiValueRemove: (base) => ({
          ...base,
          color: "#4f46e5",
          "&:hover": { background: "rgba(79,70,229,0.15)", color: "#4f46e5" },
        }),
        placeholder: (base) => ({
          ...base,
          color: "#94a3b8",
          fontSize: "0.875rem",
        }),
        input: (base) => ({
          ...base,
          color: "var(--text)",
          fontSize: "0.875rem",
        }),
        singleValue: (base) => ({
          ...base,
          color: "var(--text)",
        }),
        indicatorsContainer: (base) => ({
          ...base,
          flexShrink: 0,
        }),
        indicatorSeparator: () => ({ display: "none" }),
        dropdownIndicator: (base) => ({
          ...base,
          padding: "0 6px",
          color: "#94a3b8",
        }),
        menu: (base) => ({
          ...base,
          zIndex: 50,
          borderRadius: 10,
          overflow: "hidden",
          border: "1px solid rgba(15,23,42,0.08)",
          boxShadow: "0 4px 20px rgba(0,0,0,0.1)",
        }),
        menuList: (base) => ({
          ...base,
          padding: 4,
        }),
        option: (base, state) => ({
          ...base,
          fontSize: "0.875rem",
          fontWeight: state.isSelected ? 700 : 500,
          borderRadius: 6,
          padding: "8px 10px",
          background: state.isSelected ? "rgba(79,70,229,0.1)" : state.isFocused ? "rgba(15,23,42,0.04)" : "transparent",
          color: state.isSelected ? "#4f46e5" : "var(--text)",
          cursor: "pointer",
          "&:active": { background: "rgba(79,70,229,0.08)" },
        }),
      }}
    />
  );
}
