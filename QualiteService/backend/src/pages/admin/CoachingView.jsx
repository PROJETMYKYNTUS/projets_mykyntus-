import React, { useEffect, useMemo, useState } from "react";
import api from "../../api";
import MultiSelect from "../../components/MultiSelect.jsx";

const S = {
  card: {
    borderRadius: "1rem",
    padding: "1rem",
    border: "1px solid #e5e7eb",
    background: "#ffffff",
    boxShadow: "0 10px 25px rgba(0,0,0,0.06)",
  },
  th: {
    padding: "0.6rem",
    textAlign: "left",
    background: "#f9fafb",
    borderBottom: "1px solid rgba(0,0,0,0.2)",
    fontWeight: 800,
    fontSize: "0.85rem",
  },
  td: {
    padding: "0.55rem 0.6rem",
    borderBottom: "1px solid rgba(0,0,0,0.12)",
    fontSize: "0.85rem",
  },
  btn: {
    padding: "0.45rem 0.75rem",
    borderRadius: "999px",
    border: "1px solid #d1d5db",
    background: "#fff",
    cursor: "pointer",
    fontWeight: 700,
  },
  btnDanger: {
    padding: "0.45rem 0.75rem",
    borderRadius: "999px",
    border: "1px solid #ef4444",
    background: "#fff",
    color: "#b91c1c",
    cursor: "pointer",
    fontWeight: 800,
  },
  select: {
    padding: "0.45rem 0.7rem",
    borderRadius: "999px",
    border: "1px solid #d1d5db",
    background: "#fff",
  },
};

function fmtDate(d) {
  if (!d) return "";
  const dt = new Date(d);
  if (Number.isNaN(dt.getTime())) return String(d);
  return dt.toLocaleString("fr-FR");
}

function statusLabel(s) {
  if (s === "open") return "Ouvert";
  if (s === "in_progress") return "En cours";
  if (s === "done") return "Clôturé";
  return s || "";
}

export default function CoachingView() {
  const [items, setItems] = useState([]);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [status, setStatus] = useState("");
  const [filterStatus, setFilterStatus] = useState("");
  const [filterPilotIds, setFilterPilotIds] = useState([]);
  const [filterEvaluatorIds, setFilterEvaluatorIds] = useState([]);
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [pilotOptions, setPilotOptions] = useState([]);
  const [evaluatorOptions, setEvaluatorOptions] = useState([]);

  const pageSize = 50;
  const maxPage = useMemo(() => Math.max(1, Math.ceil(total / pageSize)), [total]);

  
  async function loadOptions() {
    try {
      const [pilotsRes, evalRes] = await Promise.all([
        api.get("/admin/pilots"),
        api.get("/scores/evaluators"),
      ]);
      const pilots = Array.isArray(pilotsRes.data) ? pilotsRes.data : [];
      setPilotOptions(pilots.map((p) => ({ value: String(p._id), label: p.name || p.email || "Agent" })));

      const evs = Array.isArray(evalRes.data) ? evalRes.data : [];
      setEvaluatorOptions(evs.map((u) => ({ value: String(u.id), label: u.name || u.email || "Évaluateur" })));
    } catch (e) {
      console.error(e);
      setPilotOptions([]);
      setEvaluatorOptions([]);
    }
  }
async function fetchAll(p = 1) {
    try {
      setStatus("");
      const res = await api.get("/coaching", {
        params: {
          page: p,
          limit: pageSize,
          status: filterStatus || undefined,
          pilotId: (filterPilotIds||[]).join(",") || undefined,
          evaluatorId: (filterEvaluatorIds||[]).join(",") || undefined,
          dateFrom: dateFrom || undefined,
          dateTo: dateTo || undefined,
        },
      });
      const payload = res?.data || {};
      setItems(Array.isArray(payload.items) ? payload.items : []);
      setTotal(Number(payload.total || 0));
      setPage(Number(payload.page || p));
    } catch (e) {
      console.error(e);
      setItems([]);
      setTotal(0);
      setStatus("Erreur lors du chargement des coachings.");
    }
  }

  useEffect(() => {
    loadOptions();
    fetchAll(1);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filterStatus, filterPilotIds, filterEvaluatorIds, dateFrom, dateTo]);

  async function handleDelete(id) {
    if (!id) return;
    if (!window.confirm("Supprimer ce coaching ?")) return;
    try {
      await api.delete(`/coaching/${id}`);
      setStatus("✅ Coaching supprimé.");
      fetchAll(page);
    } catch (e) {
      console.error(e);
      setStatus("❌ Suppression impossible.");
    }
  }

  return (
    <div style={S.card}>
      <div style={{ display: "flex", gap: "0.6rem", flexWrap: "wrap", alignItems: "center" }}>
        <h2 style={{ margin: 0, fontSize: "1.1rem" }}>Coaching — Administration</h2>
        <div style={{ marginLeft: "auto", display: "flex", gap: "0.6rem", alignItems: "center" }}>
          <select
            style={S.select}
            value={filterStatus}
            onChange={(e) => setFilterStatus(e.target.value)}
          >
            <option value="">Tous les statuts</option>
            <option value="open">Ouvert</option>
            <option value="in_progress">En cours</option>
            <option value="done">Clôturé</option>
          </select>
          <button
            style={S.btn}
            type="button"
            onClick={() => fetchAll(page)}
          >
            Rafraîchir
          </button>
        </div>
      </div>

      {status && (
        <div style={{ marginTop: "0.75rem", fontWeight: 700 }}>{status}</div>
      )}

      <div style={{ marginTop: "0.9rem", overflowX: "auto" }}>
        <table style={{ width: "100%", borderCollapse: "collapse" }}>
          <thead>
            <tr>
              <th style={S.th}>Créé le</th>
              <th style={S.th}>Coach</th>
              <th style={S.th}>Pilote</th>
              <th style={S.th}>EPS</th>
              <th style={S.th}>Statut</th>
              <th style={S.th}>Relance</th>
              <th style={{ ...S.th, textAlign: "right" }}>Actions</th>
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr>
                <td colSpan={7} style={{ ...S.td, textAlign: "center" }}>
                  Aucun coaching.
                </td>
              </tr>
            ) : (
              items.map((c) => (
                <tr key={c._id}>
                  <td style={S.td}>{fmtDate(c.createdAt)}</td>
                  <td style={S.td}>{c.coach?.name || "—"}</td>
                  <td style={S.td}>{c.pilot?.name || c.score?.pilot?.name || "—"}</td>
                  <td style={S.td}>{c.score?.eps || "—"}</td>
                  <td style={S.td}>{statusLabel(c.status)}</td>
                  <td style={S.td}>{fmtDate(c.followUpDate)}</td>
                  <td style={{ ...S.td, textAlign: "right" }}>
                    <button style={S.btnDanger} type="button" onClick={() => handleDelete(c._id)}>
                      Supprimer
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <div style={{ marginTop: "0.9rem", display: "flex", gap: "0.5rem", alignItems: "center" }}>
        <button style={S.btn} type="button" onClick={() => fetchAll(1)} disabled={page <= 1}>
          « Début
        </button>
        <button style={S.btn} type="button" onClick={() => fetchAll(page - 1)} disabled={page <= 1}>
          ‹ Préc.
        </button>
        <div style={{ fontWeight: 800 }}>
          Page {page} / {maxPage} — {total} coachings
        </div>
        <button style={S.btn} type="button" onClick={() => fetchAll(page + 1)} disabled={page >= maxPage}>
          Suiv. ›
        </button>
        <button style={S.btn} type="button" onClick={() => fetchAll(maxPage)} disabled={page >= maxPage}>
          Fin »
        </button>
      </div>
    </div>
  );
}
