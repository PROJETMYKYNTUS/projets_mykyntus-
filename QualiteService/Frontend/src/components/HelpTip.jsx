// src/components/HelpTip.jsx
import React from "react";
import { FiInfo } from "react-icons/fi";
import "./helpTip.css";

/**
 * Minimal tooltip without external deps.
 * Usage: <HelpTip text="..." />
 */
export default function HelpTip({ text, side = "top" }) {
  if (!text) return null;
  return (
    <span className={`kcq-helptip kcq-helptip--${side}`} tabIndex={0} aria-label={text}>
      <FiInfo className="kcq-helptip__icon" aria-hidden="true" />
      <span className="kcq-helptip__bubble" role="tooltip">
        {text}
      </span>
    </span>
  );
}
