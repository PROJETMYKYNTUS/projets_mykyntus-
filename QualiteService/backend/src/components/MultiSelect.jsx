import React from "react";
import Select from "react-select";

/**
 * MultiSelect
 * - single-line multi selection (chips are clipped instead of wrapping)
 * - values are primitive strings
 */
export default function MultiSelect({
  value,
  onChange,
  options,
  placeholder = "Sélectionner...",
  isMulti = true,
  isClearable = true,
  isDisabled = false,
  width = "100%",
}) {
  const safeOptions = Array.isArray(options) ? options : [];
  const safeValue = Array.isArray(value) ? value : value ? [value] : [];

  const selected = safeOptions.filter((o) => safeValue.includes(String(o.value)));

  return (
    <div style={{ width }}>
      <Select
        isMulti={isMulti}
        isClearable={isClearable}
        isDisabled={isDisabled}
        options={safeOptions}
        value={selected}
        placeholder={placeholder}
        closeMenuOnSelect={!isMulti}
        onChange={(vals) => {
          if (!vals || (Array.isArray(vals) && vals.length === 0)) {
            onChange([]);
            return;
          }
          const arr = Array.isArray(vals) ? vals : [vals];
          onChange(arr.map((v) => String(v.value)));
        }}
        styles={{
          control: (base) => ({
            ...base,
            minHeight: 40,
            borderRadius: 12,
            borderColor: "#d1d5db",
            boxShadow: "none",
            overflow: "hidden",
          }),
          valueContainer: (base) => ({
            ...base,
            flexWrap: "nowrap",
            overflow: "hidden",
          }),
          multiValue: (base) => ({
            ...base,
            maxWidth: 140,
          }),
          multiValueLabel: (base) => ({
            ...base,
            whiteSpace: "nowrap",
            overflow: "hidden",
            textOverflow: "ellipsis",
            maxWidth: 110,
          }),
          indicatorsContainer: (base) => ({
            ...base,
            flexShrink: 0,
          }),
          menu: (base) => ({
            ...base,
            zIndex: 50,
          }),
        }}
      />
    </div>
  );
}
