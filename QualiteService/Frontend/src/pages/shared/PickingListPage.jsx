import React, { useEffect, useState, useMemo } from "react";
import api from "../../api";
import PageHeader from "../../components/PageHeader.jsx";

function fmtDate(d) {
  if (!d) return "—";
  const dt = new Date(d);
  return Number.isNaN(dt.getTime())
    ? String(d)
    : dt.toLocaleDateString("fr-FR", {
        day: "2-digit",
        month: "short",
        year: "numeric",
      });
}

function fmtDateInput(d) {
  if (!d) return "";
  const dt = new Date(d);
  if (Number.isNaN(dt.getTime())) return "";
  const y = dt.getFullYear();
  const m = String(dt.getMonth() + 1).padStart(2, "0");
  const day = String(dt.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

function fmtTime(d) {
  if (!d) return "—";
  const dt = new Date(d);
  if (Number.isNaN(dt.getTime())) return String(d);
  return dt.toLocaleTimeString("fr-FR", {
    hour: "2-digit",
    minute: "2-digit",
  });
}

function normalizePickingCall(call) {
  return {
    ...call,
    audioUrl: call.audioUrl || call.recordingUrl || call.audio || "",
    eps: call.eps || "",
    cell: call.cell || "",
    phone: call.phone || "",
    callDate: call.callDate || "",
    callDuration: call.callDuration || "",
    pilotId: call.pilotId || call.pilot?._id || "",
  };
}

function getTodayValue() {
  const now = new Date();
  const y = now.getFullYear();
  const m = String(now.getMonth() + 1).padStart(2, "0");
  const d = String(now.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

export default function PickingListPage() {
  const [calls, setCalls] = useState([]);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState("");
  const [search, setSearch] = useState("");
  const [selectedDate, setSelectedDate] = useState(getTodayValue());

  useEffect(() => {
    let mounted = true;

    async function loadCalls() {
      try {
        setLoading(true);
        setErr("");

        const [year, month] = selectedDate.split("-");

        const res = await api.get("/scores/picking-calls", {
          params: {
            year,
            month,
            limit: 5000,
          },
        });

        if (!mounted) return;

        const data = Array.isArray(res.data)
          ? res.data
          : Array.isArray(res.data?.items)
            ? res.data.items
            : [];

        setCalls(data.map(normalizePickingCall));
      } catch (e) {
        if (!mounted) return;
        setErr(e?.response?.data?.message || "Impossible de charger les appels.");
      } finally {
        if (mounted) setLoading(false);
      }
    }

    loadCalls();

    return () => {
      mounted = false;
    };
  }, [selectedDate]);

  const filtered = useMemo(() => {
    let out = calls;

    if (selectedDate) {
      out = out.filter((c) => fmtDateInput(c.callDate) === selectedDate);
    }

    if (search.trim()) {
      const q = search.trim().toLowerCase();
      out = out.filter((c) =>
        `${c.eps || ""} ${c.cell || ""} ${c.phone || ""} ${c.callDuration || ""}`
          .toLowerCase()
          .includes(q)
      );
    }

    out = [...out].sort((a, b) => {
      const da = new Date(a.callDate).getTime() || 0;
      const db = new Date(b.callDate).getTime() || 0;
      return db - da;
    });

    return out;
  }, [calls, search, selectedDate]);

  function handleEvaluate(call) {
    sessionStorage.setItem("pending_call_evaluation", JSON.stringify(call));

    const user = JSON.parse(localStorage.getItem("user") || "null");
    const role = user?.role || "cq";

    window.dispatchEvent(
      new CustomEvent("kcq:navigate", {
        detail: { role, view: "new" },
      })
    );
  }

  return (
    <div className="page">
      <PageHeader
        title="Appels à évaluer"
        subtitle="Sélectionnez une date précise puis cliquez sur Évaluer pour ouvrir la fiche."
      />

      <div className="card" style={{ padding: "12px 14px", marginBottom: 12 }}>
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "minmax(280px, 420px) minmax(180px, 220px)",
            gap: 10,
            alignItems: "end",
          }}
        >
          <input
            className="input"
            placeholder="Rechercher par TAG, N° cellule, N° client…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />

          <input
            className="input"
            type="date"
            value={selectedDate}
            onChange={(e) => setSelectedDate(e.target.value)}
          />
        </div>
      </div>

      {err && (
        <div
          className="card"
          style={{
            borderColor: "rgba(220,38,38,0.25)",
            padding: 14,
            color: "var(--danger)",
            marginBottom: 12,
            background: "var(--danger-bg)",
          }}
        >
          {err}
        </div>
      )}

      {loading ? (
        <div className="card" style={{ padding: 40, textAlign: "center", color: "var(--muted)" }}>
          <div style={{ fontSize: "1.5rem", marginBottom: 8 }}>⏳</div>
          <div style={{ fontWeight: 600 }}>Chargement des appels…</div>
        </div>
      ) : filtered.length === 0 ? (
        <div className="card" style={{ padding: 40, textAlign: "center", color: "var(--muted)" }}>
          <div style={{ fontSize: "2.5rem", marginBottom: 10 }}>📞</div>
          <div style={{ fontWeight: 700, fontSize: "1rem", marginBottom: 6 }}>
            {calls.length === 0 ? "Aucun appel disponible" : "Aucun appel pour cette date"}
          </div>
          <div style={{ fontSize: "0.85rem", maxWidth: 420, margin: "0 auto", lineHeight: 1.5 }}>
            Vérifiez la date choisie ou modifiez votre recherche.
          </div>
        </div>
      ) : (
        <div className="card" style={{ padding: 0, overflow: "hidden" }}>
          <div
            style={{
              padding: "12px 16px",
              borderBottom: "1px solid var(--border)",
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
            }}
          >
            <div style={{ fontWeight: 700, fontSize: "0.9rem" }}>
              Liste des appels du {selectedDate}
            </div>
            <div style={{ color: "var(--muted)", fontSize: "0.8rem", fontWeight: 600 }}>
              {filtered.length} appel{filtered.length > 1 ? "s" : ""}
            </div>
          </div>

          <div style={{ overflowX: "auto" }}>
            <table className="data-table">
              <thead>
                <tr>
                  <th>TAG</th>
                  <th>N° Cellule</th>
                  <th>N° Client</th>
                  <th>Date</th>
                  <th>Heure</th>
                  <th>Durée</th>
                  <th>Audio</th>
                  <th style={{ textAlign: "right" }}>Action</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((call, idx) => {
                  const id = call._id || call.id || idx;
                  const hasAudio = !!call.audioUrl;

                  return (
                    <tr key={id}>
                      <td style={{ fontFamily: "monospace", fontWeight: 700, fontSize: "0.85rem" }}>
                        {call.eps || "—"}
                      </td>
                      <td>{call.cell ? <span className="badge badge--muted">{call.cell}</span> : "—"}</td>
                      <td style={{ fontFamily: "monospace", fontSize: "0.85rem" }}>
                        {call.phone || "—"}
                      </td>
                      <td>{fmtDate(call.callDate)}</td>
                      <td style={{ color: "var(--text-secondary)", fontSize: "0.85rem" }}>
                        {fmtTime(call.callDate)}
                      </td>
                      <td>
                        {call.callDuration ? <span className="badge badge--primary">{call.callDuration}</span> : "—"}
                      </td>
                      <td>{hasAudio ? <span className="badge badge--success">🎧 Dispo</span> : "—"}</td>
                      <td style={{ textAlign: "right" }}>
                        <button className="btn btn--sm" type="button" onClick={() => handleEvaluate(call)}>
                          Évaluer
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  );
}
