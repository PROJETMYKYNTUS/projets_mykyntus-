import React, { useEffect, useState } from "react";
import api from "../../api";
import PageHeader from "../../components/PageHeader";
import { useToast } from "../../toast/ToastProvider.jsx";

export default function AdminEvaluations() {
  const toast = useToast();

  const [evaluations, setEvaluations] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  const loadEvaluations = async () => {
    try {
      setLoading(true);
      const res = await api.get("/scores?page=1&limit=500");
      setEvaluations(res.data.items || []);
    } catch (err) {
      setError("Erreur chargement évaluations");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadEvaluations();
  }, []);

  const handleDelete = async (id) => {
    const confirmDelete = window.confirm("Supprimer cette évaluation ?");
    if (!confirmDelete) return;

    try {
      await api.delete(`/scores/${id}`);
      setEvaluations((prev) => prev.filter((e) => e._id !== id));
    } catch (err) {
      toast.error("Erreur suppression");
    }
  };

  return (
    <div className="page">
      <PageHeader title="Gestion des évaluations" />

      {loading && <div>Chargement...</div>}
      {error && <div style={{color:"red"}}>{error}</div>}

      {!loading && (
        <div className="card">
          <table style={{ width: "100%" }}>
            <thead>
              <tr>
                <th>Agent</th>
                <th>EPS</th>
                <th>Date</th>
                <th>Score</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {evaluations.map((e) => (
                <tr key={e._id}>
                  <td>{e.pilotName || e.pilot?.name}</td>
                  <td>{e.eps}</td>
                  <td>
                    {new Date(
                      e.interactionDate || e.callDate || e.createdAt
                    ).toLocaleDateString()}
                  </td>
                  <td>
                    {Number(e.compliancePercent || 0).toFixed(1)}%
                  </td>
                  <td>
                    <button
                      style={{
                        background: "#dc2626",
                        color: "white",
                        border: "none",
                        padding: "6px 12px",
                        borderRadius: 6,
                        cursor: "pointer"
                      }}
                      onClick={() => handleDelete(e._id)}
                    >
                      Supprimer
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          {evaluations.length === 0 && (
            <div style={{ padding: 20 }}>Aucune évaluation</div>
          )}
        </div>
      )}
    </div>
  );
}