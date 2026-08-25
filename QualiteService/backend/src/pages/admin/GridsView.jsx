import React, { useEffect, useMemo, useState } from "react";
import axios from "../../api";
import Card from "./components/Card.jsx";

const uid = () => Math.random().toString(36).slice(2) + Date.now().toString(36);

// Helpers: flat items with group markers
const ensureIds = (items) =>
  (Array.isArray(items) ? items : []).map((it, idx) => ({
    ...it,
    _localId: it._localId || it._id || `${idx}-${uid()}`,
  }));

const isGroup = (it) => it?.type === "group";
const isItem = (it) => it?.type === "item";

export default function GridsView() {
  const [grids, setGrids] = useState([]);
  const [selectedId, setSelectedId] = useState(null);

  const [editorGrid, setEditorGrid] = useState(null); // { _id, name, description, active, rolesAllowed, items: [] }
  const [collapsed, setCollapsed] = useState({}); // groupLocalId -> bool

  const load = async () => {
    const r = await axios.get("/admin/grids");
    const list = Array.isArray(r.data) ? r.data : [];
    setGrids(list);
    // keep selection if exists
    if (!selectedId && list[0]?._id) setSelectedId(list[0]._id);
  };

  useEffect(() => { load(); }, []);

  useEffect(() => {
    const g = grids.find((x) => x._id === selectedId);
    if (!g) return;
    setEditorGrid({
      ...g,
      gridType: (g.gridType || "classic"),
      rolesAllowed: Array.isArray(g.rolesAllowed) ? g.rolesAllowed : (g.role ? [g.role] : ["cq", "management"]),
      items: ensureIds(g.items || []),
    });
  }, [selectedId, grids]);

  const selectedGrid = useMemo(() => grids.find((g) => g._id === selectedId), [grids, selectedId]);

  const createGrid = async () => {
    const payload = {
      name: "Nouvelle grille",
      gridType: "classic",
      description: "",
      active: true,
      rolesAllowed: ["cq", "management"],
      items: [
        { type: "group", title: "Phase 1", hardFail: false, malusPercent: 0 },
        { type: "item", label: "Critère 1", pointsConforme: 1, pointsNonConforme: 0, malusPercent: 0 },
      ],
    };
    const r = await axios.post("/admin/grids", payload);
    await load();
    setSelectedId(r.data?._id || null);
  };

  const deleteGrid = async (id) => {
    await axios.delete(`/admin/grids/${id}`);
    await load();
    if (id === selectedId) setSelectedId(null);
  };

  const saveGridInfo = async () => {
    if (!editorGrid?._id) return;
    await axios.patch(`/admin/grids/${editorGrid._id}`, {
      name: editorGrid.name,
      description: editorGrid.description,
      active: editorGrid.active,
      rolesAllowed: editorGrid.rolesAllowed,
      gridType: editorGrid.gridType,
    });
    await load();
  };

  const saveGridItems = async () => {
    if (!editorGrid?._id) return;
    const itemsToSave = (editorGrid.items || []).map(({ _localId, ...rest }, idx) => ({ ...rest, order: idx }));
    await axios.patch(`/admin/grids/${editorGrid._id}/items`, { items: itemsToSave });
    await load();
  };

  // ---------- Editor operations ----------
  const items = editorGrid?.items || [];

  const addPhase = () => {
    const next = [
      ...items,
      { type: "group", title: `Phase ${countGroups(items) + 1}`, hardFail: false, malusPercent: 0, _localId: uid() },
      { type: "item", label: "Critère", pointsConforme: 1, pointsNonConforme: 0, malusPercent: 0, _localId: uid() },
    ];
    setEditorGrid({ ...editorGrid, items: next });
  };

  const addItemAfterGroup = (groupLocalId) => {
    const idx = items.findIndex((x) => x._localId === groupLocalId);
    if (idx < 0) return;
    // insert after group's last item (until next group)
    let insertAt = idx + 1;
    while (insertAt < items.length && !isGroup(items[insertAt])) insertAt++;
    const next = [...items.slice(0, insertAt), { type: "item", label: "Critère", pointsConforme: 1, pointsNonConforme: 0, malusPercent: 0, _localId: uid() }, ...items.slice(insertAt)];
    setEditorGrid({ ...editorGrid, items: next });
  };

  const updateItem = (localId, patch) => {
    const next = items.map((x) => (x._localId === localId ? { ...x, ...patch } : x));
    setEditorGrid({ ...editorGrid, items: next });
  };

  const removeById = (localId) => {
    const next = items.filter((x) => x._localId !== localId);
    setEditorGrid({ ...editorGrid, items: next });
  };

  const duplicatePhase = (groupLocalId) => {
    const idx = items.findIndex((x) => x._localId === groupLocalId);
    if (idx < 0) return;
    const group = items[idx];
    let j = idx + 1;
    while (j < items.length && !isGroup(items[j])) j++;
    const phaseItems = items.slice(idx, j);
    const copied = phaseItems.map((it) => ({
      ...it,
      _localId: uid(),
      ...(isGroup(it) ? { title: `${it.title} (copie)` } : {}),
    }));
    const next = [...items.slice(0, j), ...copied, ...items.slice(j)];
    setEditorGrid({ ...editorGrid, items: next });
  };

  const duplicateItem = (localId) => {
    const idx = items.findIndex((x) => x._localId === localId);
    if (idx < 0) return;
    const it = items[idx];
    const copied = { ...it, _localId: uid(), label: `${it.label} (copie)` };
    const next = [...items.slice(0, idx + 1), copied, ...items.slice(idx + 1)];
    setEditorGrid({ ...editorGrid, items: next });
  };

  // ---------- Drag & Drop (HTML5) ----------
  const [drag, setDrag] = useState(null); // { type: 'group'|'item', id: localId }

  const onDragStart = (it) => (e) => {
    setDrag({ type: it.type, id: it._localId });
    e.dataTransfer.effectAllowed = "move";
    e.dataTransfer.setData("text/plain", it._localId);
  };

  const onDropOn = (targetIt) => (e) => {
    e.preventDefault();
    if (!drag) return;

    const srcId = drag.id;
    const dstId = targetIt._localId;
    if (srcId === dstId) return;

    const next = reorder(items, drag.type, srcId, dstId, targetIt.type);
    setEditorGrid({ ...editorGrid, items: next });
    setDrag(null);
  };

  const onDragOver = (e) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = "move";
  };

  // Editor layout: list (top) + editor full width bottom
  return (
    <>
      <Card title="Gestion grilles" right={<button className="btn-outline" onClick={createGrid}>+ Créer une grille</button>}>
        <div style={{ display: "grid", gridTemplateColumns: "1fr", gap: "0.75rem" }}>
          {grids.map((g) => (
            <div
              key={g._id}
              style={{
                border: "1px solid rgba(209,213,219,0.9)",
                borderRadius: "1rem",
                padding: "0.85rem",
                background: g._id === selectedId ? "rgba(16,185,129,0.10)" : "transparent",
                cursor: "pointer",
              }}
              onClick={() => setSelectedId(g._id)}
            >
              <div style={{ display: "flex", justifyContent: "space-between", gap: "0.75rem", alignItems: "center" }}>
                <div>
                  <div style={{ fontWeight: 900 }}>{g.name}</div>
                  <div style={{ opacity: 0.7, fontSize: "0.92rem" }}>{g.description || "—"}</div>
                </div>
                <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
                  <button className="btn-outline" onClick={(e) => { e.stopPropagation(); deleteGrid(g._id); }}>
                    Supprimer
                  </button>
                </div>
              </div>
            </div>
          ))}
        </div>
      </Card>

      {/* Full width editor at bottom */}
      {editorGrid && (
        <Card
          title="Éditeur grille"
          right={
            <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
              <button className="btn-outline" onClick={saveGridInfo}>Sauver infos</button>
              <button className="btn-outline" onClick={saveGridItems}>Sauver items</button>
            </div>
          }
          style={{ width: "100%" }}
        >
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
            <input
              value={editorGrid.name || ""}
              onChange={(e) => setEditorGrid({ ...editorGrid, name: e.target.value })}
              placeholder="Titre de la grille"
              style={inputStyle()}
            />
            <input
              value={editorGrid.description || ""}
              onChange={(e) => setEditorGrid({ ...editorGrid, description: e.target.value })}
              placeholder="Description"
              style={inputStyle()}
            />
            <label style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
              <input
                type="checkbox"
                checked={editorGrid.active !== false}
                onChange={(e) => setEditorGrid({ ...editorGrid, active: e.target.checked })}
              />
              Grille active
            </label>


<label style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
  <span style={{ opacity: 0.85, fontWeight: 800 }}>Type de grille:</span>
  <select
    value={editorGrid.gridType || "classic"}
    onChange={(e) => setEditorGrid({ ...editorGrid, gridType: e.target.value })}
    style={{ ...inputStyle(), maxWidth: 220 }}
  >
    <option value="classic">Grille 1 (classique)</option>
    <option value="presence">Grille 2 (présence)</option>
  </select>
</label>

            <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap", alignItems: "center" }}>
              <span style={{ opacity: 0.85, fontWeight: 800 }}>Rôles:</span>
              {["cq", "management", "admin", "pilote"].map((r) => (
                <label key={r} style={{ display: "flex", gap: "0.35rem", alignItems: "center" }}>
                  <input
                    type="checkbox"
                    checked={(editorGrid.rolesAllowed || []).includes(r)}
                    onChange={(e) => {
                      const set = new Set(editorGrid.rolesAllowed || []);
                      if (e.target.checked) set.add(r);
                      else set.delete(r);
                      setEditorGrid({ ...editorGrid, rolesAllowed: Array.from(set) });
                    }}
                  />
                  {r}
                </label>
              ))}
            </div>
          </div>

          <div
            style={{
              marginTop: "1rem",
              height: "calc(100vh - 360px)",
              minHeight: 420,
              overflowY: "auto",
              paddingRight: "0.25rem",
            }}
          >
            <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap", marginBottom: "0.75rem" }}>
              <button className="btn-outline" onClick={addPhase}>+ Ajouter phase</button>
            </div>

            {renderItems({
              items,
              collapsed,
              setCollapsed,
              addItemAfterGroup,
              updateItem,
              removeById,
              duplicatePhase,
              duplicateItem,
              onDragStart,
              onDragOver,
              onDropOn,
            })}
          </div>

          <div style={{ marginTop: "0.75rem", opacity: 0.8 }}>
            <b>Règles:</b> si <b>Hard fail</b> activé sur une phase et qu’un item est NC → score final = <b>0%</b>.
            Sinon, si <b>malus (%)</b> défini sur l’item → -x% par item NC (sinon fallback sur malus de phase).
          </div>
        </Card>
      )}
    </>
  );
}

function inputStyle() {
  return {
    padding: "0.55rem 0.7rem",
    borderRadius: "0.75rem",
    border: "1px solid rgba(209,213,219,0.9)",
    background: "#ffffff",
    color: "#111827",
  };
}

function countGroups(items) {
  return (items || []).filter((x) => x.type === "group").length;
}

function renderItems(props) {
  const {
    items,
    collapsed,
    setCollapsed,
    addItemAfterGroup,
    updateItem,
    removeById,
    duplicatePhase,
    duplicateItem,
    onDragStart,
    onDragOver,
    onDropOn,
  } = props;

  let currentGroupId = null;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
      {items.map((it) => {
        if (it.type === "group") currentGroupId = it._localId;
        const isHidden = it.type === "item" && currentGroupId && collapsed[currentGroupId];

        if (isHidden) return null;

        if (it.type === "group") {
          const isCol = !!collapsed[it._localId];
          return (
            <div
              key={it._localId}
              draggable
              onDragStart={onDragStart(it)}
              onDragOver={onDragOver}
              onDrop={onDropOn(it)}
              style={{
                border: "1px solid rgba(209,213,219,0.9)",
                borderRadius: "1rem",
                padding: "0.85rem",
                background: "#f9fafb",
              }}
            >
              <div style={{ display: "flex", justifyContent: "space-between", gap: "0.75rem", alignItems: "center", flexWrap: "wrap" }}>
                <div style={{ display: "flex", gap: "0.5rem", alignItems: "center", flexWrap: "wrap" }}>
                  <span style={{ fontWeight: 900 }}>Phase</span>
                  <input value={it.title || ""} onChange={(e) => updateItem(it._localId, { title: e.target.value })} style={{ ...inputStyle(), minWidth: 260 }} />
                  <button className="btn-outline" onClick={() => setCollapsed((p) => ({ ...p, [it._localId]: !p[it._localId] }))}>
                    {isCol ? "Afficher" : "Masquer"}
                  </button>
                </div>

                <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
                  <button className="btn-outline" onClick={() => addItemAfterGroup(it._localId)}>+ Item</button>
                  <button className="btn-outline" onClick={() => duplicatePhase(it._localId)}>Dupliquer</button>
                  <button className="btn-outline" onClick={() => removeById(it._localId)}>Supprimer</button>
                </div>
              </div>

              <div style={{ marginTop: "0.65rem", display: "flex", gap: "0.75rem", flexWrap: "wrap", alignItems: "center" }}>
                <label style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
                  <input type="checkbox" checked={!!it.hardFail} onChange={(e) => updateItem(it._localId, { hardFail: e.target.checked })} />
                  Hard fail (NC ⇒ 0%)
                </label>

                <label style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
                  Malus (%)
                  <input
                    type="number"
                    value={Number.isFinite(it.malusPercent) ? it.malusPercent : 0}
                    onChange={(e) => updateItem(it._localId, { malusPercent: Number(e.target.value) || 0 })}
                    style={{ ...inputStyle(), width: 110 }}
                  />
                </label>
              </div>
            </div>
          );
        }

        // item
        return (
          <div
            key={it._localId}
            draggable
            onDragStart={onDragStart(it)}
            onDragOver={onDragOver}
            onDrop={onDropOn(it)}
            style={{
              border: "1px solid rgba(209,213,219,0.9)",
              borderRadius: "1rem",
              padding: "0.85rem",
              marginLeft: "0.75rem",
              background: "#ffffff",
            }}
          >
            <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr 1fr 1fr auto", gap: "0.5rem", alignItems: "start" }}>
              <textarea
                value={it.label || ""}
                onChange={(e) => updateItem(it._localId, { label: e.target.value })}
                placeholder="Libellé item (vous pouvez mettre des retours à la ligne)"
                rows={2}
                style={{ ...inputStyle(), resize: "vertical", whiteSpace: "pre-wrap" }}
              />
              <input type="number" value={Number.isFinite(it.pointsConforme) ? it.pointsConforme : 1} onChange={(e) => updateItem(it._localId, { pointsConforme: Number(e.target.value) || 0 })} style={inputStyle()} />
              <input type="number" value={Number.isFinite(it.pointsNonConforme) ? it.pointsNonConforme : 0} onChange={(e) => updateItem(it._localId, { pointsNonConforme: Number(e.target.value) || 0 })} style={inputStyle()} />
              <input type="number" value={Number.isFinite(it.malusPercent) ? it.malusPercent : 0} onChange={(e) => updateItem(it._localId, { malusPercent: Number(e.target.value) || 0 })} style={inputStyle()} />
              <div style={{ display: "flex", gap: "0.5rem" }}>
                <button className="btn-outline" onClick={() => duplicateItem(it._localId)}>Dupliquer</button>
                <button className="btn-outline" onClick={() => removeById(it._localId)}>Supprimer</button>
              </div>
            </div>
            <div style={{ marginTop: "0.35rem", opacity: 0.75, fontSize: "0.9rem" }}>
              Notes: conforme / non conforme
            </div>
          </div>
        );
      })}
    </div>
  );
}

function reorder(items, dragType, srcId, dstId, dstType) {
  // If dragging a group: move the group with its subsequent items until next group
  const arr = [...items];
  const srcIdx = arr.findIndex((x) => x._localId === srcId);
  const dstIdx = arr.findIndex((x) => x._localId === dstId);
  if (srcIdx < 0 || dstIdx < 0) return arr;

  if (dragType === "group") {
    const [block, rest] = extractPhaseBlock(arr, srcIdx);
    const dstIdxNew = rest.findIndex((x) => x._localId === dstId);
    if (dstIdxNew < 0) return arr;

    // insert block before destination group
    const insertAt = dstIdxNew;
    const next = [...rest.slice(0, insertAt), ...block, ...rest.slice(insertAt)];
    return next;
  }

  // dragging item: simple move within flat array (allow crossing phases)
  const item = arr[srcIdx];
  const without = arr.filter((x) => x._localId !== srcId);
  const dstIdx2 = without.findIndex((x) => x._localId === dstId);
  const insertAt = dstIdx2 + (dstType === "item" ? 0 : 1); // drop on group -> insert after group
  const next = [...without.slice(0, insertAt), item, ...without.slice(insertAt)];
  return next;
}

function extractPhaseBlock(arr, groupIndex) {
  const block = [];
  block.push(arr[groupIndex]);
  let i = groupIndex + 1;
  while (i < arr.length && arr[i].type !== "group") {
    block.push(arr[i]);
    i++;
  }
  const rest = [...arr.slice(0, groupIndex), ...arr.slice(i)];
  return [block, rest];
}