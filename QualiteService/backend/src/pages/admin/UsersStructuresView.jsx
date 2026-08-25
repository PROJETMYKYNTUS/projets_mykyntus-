import React, { useEffect, useMemo, useRef, useState } from "react";
import axios from "../../api";
import Card from "./components/Card.jsx";
import Pager from "./components/Pager.jsx";
import { exportToXlsx } from "./components/exportXlsx.js";

/**
 * Collaborateurs & Structures (Admin)
 * - Liste utilisateurs paginée + filtres
 * - Actions: modifier / supprimer / mot de passe
 * - Ajout unitaire + ajout en masse (textarea + upload Excel)
 * - Gestion cellules (structures): CRUD
 *
 * IMPORTANT: ne touche pas au dashboard/grilles; ce composant ne fait que Users & Structures.
 */

const PAGE_SIZE = 25;
const LISTEN_PAGE_SIZE = 25;

const safeText = (v) => {
  if (v === null || v === undefined) return "";
  if (typeof v === "string" || typeof v === "number" || typeof v === "boolean") return String(v);
  if (typeof v === "object") return String(v.name || v.label || v.title || v.email || v._id || v.id || "");
  return String(v);
};
const get = (obj, keys, fallback = "") => {
  for (const k of keys) {
    const v = obj?.[k];
    if (v !== undefined && v !== null && String(v).length > 0) return v;
  }
  return fallback;
};


const safeStr = (v) => {
  if (v === null || v === undefined) return "";
  if (typeof v === "string" || typeof v === "number" || typeof v === "boolean") return String(v);
  if (typeof v === "object") return String(v.name || v.label || v.title || v.email || v._id || v.id || "");
  return String(v);
};

const normalizeRole = (r) => (r || "").toString().trim().toLowerCase();

export default function UsersStructuresView() {
  // data
  const [users, setUsers] = useState([]);
  const [cells, setCells] = useState([]);

  // --- Écoutes (CQ + Management) pour export/visibilité (date = createdAt validation) ---
  const [cqEvals, setCqEvals] = useState([]);
  const [mgEvals, setMgEvals] = useState([]);
  const [pageCq, setPageCq] = useState(1);
  const [pageMg, setPageMg] = useState(1);


  // filters + pagination
  const [q, setQ] = useState("");
  const [role, setRole] = useState("all");
  const [active, setActive] = useState("all");
  const [page, setPage] = useState(1);

  // add user
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPasswordValue] = useState("");
  const [newRole, setNewRole] = useState("cq");
  const [cellSelection, setCellSelection] = useState("");

  // edit user / password
  const [editing, setEditing] = useState(null); // full user object
  const [pwd, setPwd] = useState({ id: null, value: "" });

  // bulk import
  const [bulkText, setBulkText] = useState("");
  const [isBulkUploading, setIsBulkUploading] = useState(false);
  const bulkFileRef = useRef(null);

  // cells CRUD
  const [cellName, setCellName] = useState("");
  const [cellDescription, setCellDescription] = useState("");
  const [editCell, setEditCell] = useState(null);

  const load = async () => {
    const [u, c] = await Promise.all([axios.get("/admin/users"), axios.get("/admin/cells")]);
    setUsers(Array.isArray(u.data) ? u.data : []);
    setCells(Array.isArray(c.data) ? c.data : []);
  };

  useEffect(() => {
    load().catch(() => {
      setUsers([]);
      setCells([]);
    });
  

    // Écoutes (pour consultation rapide + exports depuis cette vue)
    axios.get("/admin/evaluations/cq")
      .then((r) => setCqEvals(Array.isArray(r.data) ? r.data : []))
      .catch(() => setCqEvals([]));

    axios.get("/admin/evaluations/management")
      .then((r) => setMgEvals(Array.isArray(r.data) ? r.data : []))
      .catch(() => setMgEvals([]));
}, []);

  // ---------- Filters ----------
  const filtered = useMemo(() => {
    const qq = q.trim().toLowerCase();
    return users.filter((u) => {
      const uRole = normalizeRole(u.role);
      if (role !== "all" && uRole !== role) return false;

      if (active !== "all") {
        const isActive = u.active !== false;
        if (active === "active" && !isActive) return false;
        if (active === "inactive" && isActive) return false;
      }

      if (!qq) return true;
      const t = `${safeStr(u.name)} ${safeStr(u.email)} ${safeStr(u.role)} ${safeStr(u.cell)}`.toLowerCase();
      return t.includes(qq);
    });
  }, [users, q, role, active]);

  useEffect(() => {
    setPage(1);
  }, [q, role, active]);

  const pageRows = useMemo(() => {
    const start = (page - 1) * PAGE_SIZE;
    return filtered.slice(start, start + PAGE_SIZE);
  }, [filtered, page]);

  // ---------- Users actions ----------
  const createUser = async (e) => {
    e?.preventDefault?.();
    if (!name.trim() || !email.trim() || !password.trim()) return;

    await axios.post("/admin/users", {
      name: name.trim(),
      email: email.trim(),
      password: password.trim(),
      role: newRole,
      cell: cellSelection || "",
    });

    setName("");
    setEmail("");
    setPasswordValue("");
    setNewRole("cq");
    setCellSelection("");
    await load();
  };

  const upsertUser = async () => {
    if (!editing?._id) return;

    await axios.patch(`/admin/users/${editing._id}`, {
      name: editing.name,
      email: editing.email,
      role: editing.role,
      cell: editing.cell,
      active: editing.active,
    });

    setEditing(null);
    await load();
  };

  const deleteUser = async (id) => {
    if (!id) return;
    await axios.delete(`/admin/users/${id}`);
    await load();
  };

  const setPassword = async () => {
    if (!pwd.id || !pwd.value) return;
    await axios.patch(`/admin/users/${pwd.id}`, { password: pwd.value });
    setPwd({ id: null, value: "" });
    await load();
  };

  // ---------- Bulk import (textarea) ----------
  const parseBulkText = () => {
    const lines = bulkText
      .split("\n")
      .map((l) => l.trim())
      .filter(Boolean);

    const out = [];
    for (const line of lines) {
      const sep = line.includes(";") ? ";" : ",";
      const parts = line.split(sep).map((x) => x.trim());
      const [n, em, pw, rl, cl] = parts;
      if (!n || !em || !pw || !rl) continue;
      out.push({ name: n, email: em, password: pw, role: rl, cell: cl || "" });
    }
    return out;
  };

  const bulkCreateFromList = async (list) => {
    if (!Array.isArray(list) || list.length === 0) return;
    await axios.post("/admin/users/bulk", { users: list });
    await load();
  };

  const bulkCreate = async () => {
    const list = parseBulkText();
    if (!list.length) return;
    await bulkCreateFromList(list);
    setBulkText("");
  };

  // ---------- Bulk import (Excel upload) ----------
  const handleBulkFileChange = async (e) => {
    const file = e?.target?.files?.[0];
    if (!file) return;

    setIsBulkUploading(true);
    try {
      // Lazy-load to avoid forcing xlsx into initial bundle
      const XLSX = (await import("xlsx")).default || (await import("xlsx"));
      const data = await file.arrayBuffer();
      const wb = XLSX.read(data, { type: "array" });

      // read first sheet by default
      const sheetName = wb.SheetNames?.[0];
      const ws = wb.Sheets?.[sheetName];
      if (!ws) throw new Error("Aucune feuille dans le fichier.");

      const jsonRows = XLSX.utils.sheet_to_json(ws, { defval: "" });

      // Expected columns: name | email | password | role | cell (case-insensitive)
      const list = jsonRows
        .map((r) => {
          const obj = {};
          for (const k of Object.keys(r || {})) obj[k.toString().trim().toLowerCase()] = r[k];
          const n = safeStr(obj.name || obj.nom || obj.username || obj.user || "");
          const em = safeStr(obj.email || obj.mail || "");
          const pw = safeStr(obj.password || obj.motdepasse || obj.mdp || "");
          const rl = safeStr(obj.role || obj.rôle || "");
          const cl = safeStr(obj.cell || obj.cellule || "");
          if (!n || !em || !pw || !rl) return null;
          return { name: n, email: em, password: pw, role: rl, cell: cl || "" };
        })
        .filter(Boolean);

      await bulkCreateFromList(list);

      // reset input
      if (bulkFileRef.current) bulkFileRef.current.value = "";
    } catch (err) {
      // eslint-disable-next-line no-console
      console.error(err);
      alert("Import Excel impossible. Vérifie le format (colonnes: name, email, password, role, cell).");
    } finally {
      setIsBulkUploading(false);
    }
  };

  // ---------- Cells actions ----------
  const createCell = async () => {
    if (!cellName.trim()) return;
    await axios.post("/admin/cells", { name: cellName.trim(), description: cellDescription || "" });
    setCellName("");
    setCellDescription("");
    await load();
  };

  const updateCell = async () => {
    if (!editCell?._id) return;
    await axios.patch(`/admin/cells/${editCell._id}`, {
      name: editCell.name,
      description: editCell.description,
      active: editCell.active,
    });
    setEditCell(null);
    await load();
  };

  const deleteCell = async (id) => {
    if (!id) return;
    await axios.delete(`/admin/cells/${id}`);
    await load();
  };

  const closeAll = () => {
    setEditing(null);
    setPwd({ id: null, value: "" });
    setEditCell(null);
  };

  // ---------- UI helpers ----------
  const th = {
    textAlign: "left",
    padding: "0.6rem",
    fontSize: "0.82rem",
    opacity: 0.85,
    whiteSpace: "nowrap",
  };
  const td = {
    padding: "0.55rem",
    borderTop: "1px solid #e5e7eb",
    verticalAlign: "top",
  };

  // ---------- Écoutes (tables + export) ----------
  const listensCq = useMemo(() => {
    const rows = Array.isArray(cqEvals) ? cqEvals : [];
    return rows.map((r) => ({
      date: safeText(get(r, ["createdAt", "validatedAt", "updatedAt"], "")), // date validation
      pilot: safeText(get(r, ["pilotName", "pilot", "pilotUserName", "pilotUser", "agent"], "")),
      cell: safeText(get(r, ["cell", "cellName", "pilotCell"], "")),
      evaluator: safeText(get(r, ["cqName", "evaluatorName", "evaluator", "userName"], "")),
      score: Number(get(r, ["finalScore", "score", "percentage", "percent", "compliancePercent"], 0)) || 0,
      status: safeText(get(r, ["status", "result", "state"], "")),
      comment: safeText(get(r, ["comment", "commentaire", "notes"], "")),
      __raw: r,
    }));
  }, [cqEvals]);

  const listensMg = useMemo(() => {
    const rows = Array.isArray(mgEvals) ? mgEvals : [];
    return rows.map((r) => ({
      date: safeText(get(r, ["createdAt", "validatedAt", "updatedAt"], "")),
      pilot: safeText(get(r, ["pilotName", "pilot", "pilotUserName", "pilotUser", "agent"], "")),
      cell: safeText(get(r, ["cell", "cellName", "pilotCell"], "")),
      evaluator: safeText(get(r, ["managerName", "evaluatorName", "evaluator", "userName"], "")),
      score: Number(get(r, ["finalScore", "score", "percentage", "percent", "compliancePercent"], 0)) || 0,
      status: safeText(get(r, ["status", "result", "state"], "")),
      comment: safeText(get(r, ["comment", "commentaire", "notes"], "")),
      __raw: r,
    }));
  }, [mgEvals]);

  const toExportRow = (r) => ({
    Date: r.date,
    Pilote: r.pilot,
    Cellule: r.cell,
    "CQ/manager évaluateur": r.evaluator,
    Score: r.score,
    Statut: r.status,
    Commentaire: r.comment,
  });

  const exportListens = () => {
    exportToXlsx("Ecoutes_export_multi_feuilles.xlsx", {
      "Écoutes CQ": listensCq.map(toExportRow),
      "Écoutes Management": listensMg.map(toExportRow),
    });
  };

  const slice = (rows, page) => rows.slice((page - 1) * LISTEN_PAGE_SIZE, page * LISTEN_PAGE_SIZE);
  const listensCqPage = useMemo(() => slice(listensCq, pageCq), [listensCq, pageCq]);
  const listensMgPage = useMemo(() => slice(listensMg, pageMg), [listensMg, pageMg]);

  return (
    <>
      <Card
        title="Collaborateurs"
        right={
          <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap", alignItems: "center" }}>
            <input
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder="Recherche…"
              style={{
                padding: "0.45rem 0.7rem",
                borderRadius: "999px",
                border: "1px solid #d1d5db",
                background: "#ffffff",
                color: "#111827",
                minWidth: 220,
              }}
            />

            <select
              value={role}
              onChange={(e) => setRole(e.target.value)}
              style={{
                padding: "0.45rem 0.7rem",
                borderRadius: "999px",
                border: "1px solid #d1d5db",
                background: "#ffffff",
                color: "#111827",
              }}
            >
              <option value="all">Tous rôles</option>
              <option value="admin">Admin</option>
              <option value="cq">CQ</option>
              <option value="management">Management</option>
              <option value="pilote">Pilote</option>
            </select>

            <select
              value={active}
              onChange={(e) => setActive(e.target.value)}
              style={{
                padding: "0.45rem 0.7rem",
                borderRadius: "999px",
                border: "1px solid #d1d5db",
                background: "#ffffff",
                color: "#111827",
              }}
            >
              <option value="all">Actifs + inactifs</option>
              <option value="active">Actifs</option>
              <option value="inactive">Inactifs</option>
            </select>
          </div>
        }
      >
        <div style={{ overflowX: "auto" }}>
          <table style={{ width: "100%", borderCollapse: "collapse" }}>
            <thead>
              <tr>
                <th style={th}>Nom</th>
                <th style={th}>Email</th>
                <th style={th}>Rôle</th>
                <th style={th}>Cellule</th>
                <th style={th}>Actif</th>
                <th style={th}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {pageRows.map((u) => (
                <tr key={u._id}>
                  <td style={td}>{safeStr(u.name)}</td>
                  <td style={td}>{safeStr(u.email)}</td>
                  <td style={td}>{safeStr(u.role)}</td>
                  <td style={td}>{safeStr(u.cell) || "-"}</td>
                  <td style={td}>{u.active === false ? "Non" : "Oui"}</td>
                  <td style={{ ...td, display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
                    <button className="btn-outline" onClick={() => setEditing({ ...u })}>
                      Modifier
                    </button>
                    <button className="btn-outline" onClick={() => setPwd({ id: u._id, value: "" })}>
                      Mot de passe
                    </button>
                    <button className="btn-outline" onClick={() => deleteUser(u._id)}>
                      Supprimer
                    </button>
                  </td>
                </tr>
              ))}
              {filtered.length === 0 && (
                <tr>
                  <td style={td} colSpan={6}>
                    <span style={{ opacity: 0.8 }}>Aucun utilisateur</span>
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        <Pager
          page={page}
          pageSize={PAGE_SIZE}
          total={filtered.length}
          onPrev={() => setPage((p) => Math.max(1, p - 1))}
          onNext={() => setPage((p) => p + 1)}
        />
      </Card>

      <div style={{ display: "grid", gridTemplateColumns: "1.1fr 0.9fr", gap: "1rem", alignItems: "start" }}>
        <Card title="Ajouter un utilisateur">
          <form onSubmit={createUser} style={{ display: "grid", gap: "0.6rem" }}>
            <div style={{ display: "grid", gridTemplateColumns: "repeat(2, minmax(0, 1fr))", gap: "0.6rem" }}>
              <input className="input" placeholder="Nom" value={name} onChange={(e) => setName(e.target.value)} required />
              <input className="input" placeholder="Email" value={email} onChange={(e) => setEmail(e.target.value)} required />
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "repeat(3, minmax(0, 1fr))", gap: "0.6rem" }}>
              <input className="input" placeholder="Mot de passe" value={password} onChange={(e) => setPasswordValue(e.target.value)} required />
              <select className="input" value={newRole} onChange={(e) => setNewRole(e.target.value)} required>
                <option value="admin">Admin</option>
                <option value="cq">CQ</option>
                <option value="management">Management</option>
                <option value="pilote">Pilote</option>
              </select>
              <select className="input" value={cellSelection} onChange={(e) => setCellSelection(e.target.value)}>
                <option value="">Aucune cellule</option>
                {cells.map((c) => (
                  <option key={c._id} value={c.name}>
                    {c.name} {c.active === false ? "(inactive)" : ""}
                  </option>
                ))}
              </select>
            </div>

            <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
              <button type="submit" className="btn-primary">
                Ajouter
              </button>
            </div>
          </form>
        </Card>

        <Card
          title="Ajout en masse (CSV + Excel)"
          right={
            <button className="btn-outline" onClick={bulkCreate} disabled={!bulkText.trim()}>
              Importer texte
            </button>
          }
        >
          <div style={{ opacity: 0.8, marginBottom: "0.5rem" }}>
            <b>Texte</b> (1 ligne = 1 utilisateur) : <code>name,email,password,role,cell</code> (virgule ou point-virgule)
          </div>
          <textarea
            value={bulkText}
            onChange={(e) => setBulkText(e.target.value)}
            placeholder="John Doe,john@mail.com,Pass123,cq,Cell A"
            style={{
              width: "100%",
              minHeight: 140,
              padding: "0.75rem",
              borderRadius: "0.75rem",
              border: "1px solid #d1d5db",
              background: "#ffffff",
              color: "#111827",
            }}
          />

          <div style={{ marginTop: "0.75rem", display: "flex", gap: "0.6rem", alignItems: "center", flexWrap: "wrap" }}>
            <div style={{ fontSize: "0.85rem", opacity: 0.85 }}>
              <b>Excel</b> : colonnes <code>name</code>, <code>email</code>, <code>password</code>, <code>role</code>, <code>cell</code>
            </div>
            <input
              ref={bulkFileRef}
              type="file"
              accept=".xlsx,.xls,.csv"
              onChange={handleBulkFileChange}
              style={{ fontSize: "0.85rem" }}
            />
            {isBulkUploading && <span style={{ opacity: 0.85 }}>Import en cours…</span>}
          </div>
        </Card>
      </div>

      <Card title="Structures / Cellules" right={<button className="btn-outline" onClick={createCell}>Ajouter cellule</button>}>
        <div style={{ display: "grid", gridTemplateColumns: "minmax(0,1.2fr) minmax(0,2fr) auto", gap: "0.5rem", alignItems: "center" }}>
          <input className="input" placeholder="Nom de cellule" value={cellName} onChange={(e) => setCellName(e.target.value)} />
          <input className="input" placeholder="Description (optionnelle)" value={cellDescription} onChange={(e) => setCellDescription(e.target.value)} />
          <div />
        </div>

        <div style={{ marginTop: "0.8rem", overflowX: "auto" }}>
          <table style={{ width: "100%", borderCollapse: "collapse" }}>
            <thead>
              <tr>
                <th style={th}>Nom</th>
                <th style={th}>Description</th>
                <th style={th}>Actif</th>
                <th style={th}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {cells.map((c) => (
                <tr key={c._id}>
                  <td style={td}>{safeStr(c.name)}</td>
                  <td style={td}>{safeStr(c.description) || "—"}</td>
                  <td style={td}>{c.active === false ? "Non" : "Oui"}</td>
                  <td style={{ ...td, display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
                    <button className="btn-outline" onClick={() => setEditCell({ ...c })}>
                      Modifier
                    </button>
                    <button className="btn-outline" onClick={() => deleteCell(c._id)}>
                      Supprimer
                    </button>
                  </td>
                </tr>
              ))}
              {cells.length === 0 && (
                <tr>
                  <td style={td} colSpan={4}>
                    <span style={{ opacity: 0.8 }}>Aucune cellule définie</span>
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </Card>

      {/* ---------- Modals ---------- */}
      {(editing || pwd.id || editCell) && (
        <div
          onMouseDown={closeAll}
          style={{
            position: "fixed",
            inset: 0,
            background: "rgba(0,0,0,0.55)",
            display: "grid",
            placeItems: "center",
            zIndex: 50,
            padding: "1rem",
          }}
        >
          <div
            onMouseDown={(e) => e.stopPropagation()}
            style={{
              width: "min(860px, 96vw)",
              background: "#ffffff",
              border: "1px solid #d1d5db",
              borderRadius: "1rem",
              padding: "1rem",
            }}
          >
            {editing && (
              <>
                <div style={{ fontWeight: 900, fontSize: "1.05rem", marginBottom: "0.75rem" }}>Modifier utilisateur</div>
                <div style={{ display: "grid", gridTemplateColumns: "repeat(2, minmax(0,1fr))", gap: "0.6rem" }}>
                  <input className="input" value={safeStr(editing.name)} onChange={(e) => setEditing({ ...editing, name: e.target.value })} />
                  <input className="input" value={safeStr(editing.email)} onChange={(e) => setEditing({ ...editing, email: e.target.value })} />
                  <select className="input" value={safeStr(editing.role)} onChange={(e) => setEditing({ ...editing, role: e.target.value })}>
                    <option value="admin">Admin</option>
                    <option value="cq">CQ</option>
                    <option value="management">Management</option>
                    <option value="pilote">Pilote</option>
                  </select>
                  <select className="input" value={safeStr(editing.cell || "")} onChange={(e) => setEditing({ ...editing, cell: e.target.value })}>
                    <option value="">Aucune cellule</option>
                    {cells.map((c) => (
                      <option key={c._id} value={c.name}>
                        {c.name}
                      </option>
                    ))}
                  </select>

                  <label style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
                    <input
                      type="checkbox"
                      checked={editing.active !== false}
                      onChange={(e) => setEditing({ ...editing, active: e.target.checked })}
                    />
                    Actif
                  </label>
                </div>

                <div style={{ marginTop: "0.9rem", display: "flex", gap: "0.5rem", justifyContent: "flex-end", flexWrap: "wrap" }}>
                  <button className="btn-outline" onClick={closeAll}>Annuler</button>
                  <button className="btn-primary" onClick={upsertUser}>Enregistrer</button>
                </div>
              </>
            )}

            {pwd.id && (
              <>
                <div style={{ fontWeight: 900, fontSize: "1.05rem", marginBottom: "0.75rem" }}>Modifier mot de passe</div>
                <input className="input" placeholder="Nouveau mot de passe" value={pwd.value} onChange={(e) => setPwd({ ...pwd, value: e.target.value })} />
                <div style={{ marginTop: "0.9rem", display: "flex", gap: "0.5rem", justifyContent: "flex-end", flexWrap: "wrap" }}>
                  <button className="btn-outline" onClick={closeAll}>Annuler</button>
                  <button className="btn-primary" onClick={setPassword}>Enregistrer</button>
                </div>
              </>
            )}

            {editCell && (
              <>
                <div style={{ fontWeight: 900, fontSize: "1.05rem", marginBottom: "0.75rem" }}>Modifier cellule</div>
                <div style={{ display: "grid", gap: "0.6rem" }}>
                  <input className="input" value={safeStr(editCell.name)} onChange={(e) => setEditCell({ ...editCell, name: e.target.value })} />
                  <input className="input" value={safeStr(editCell.description)} onChange={(e) => setEditCell({ ...editCell, description: e.target.value })} />
                  <label style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
                    <input
                      type="checkbox"
                      checked={editCell.active !== false}
                      onChange={(e) => setEditCell({ ...editCell, active: e.target.checked })}
                    />
                    Actif
                  </label>
                </div>

                <div style={{ marginTop: "0.9rem", display: "flex", gap: "0.5rem", justifyContent: "flex-end", flexWrap: "wrap" }}>
                  <button className="btn-outline" onClick={closeAll}>Annuler</button>
                  <button className="btn-primary" onClick={updateCell}>Enregistrer</button>
                </div>
              </>
            )}
          </div>
        </div>
      )}
    </>
  );
}
