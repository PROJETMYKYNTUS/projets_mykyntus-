// backend/controllers/gridController.js
const Grid = require("../models/gridModel");

// ✅ Créer une grille
const createGrid = async (req, res) => {
  try {
    const { name, description, service, version, criteria } = req.body;

    if (!name) {
      return res.status(400).json({ message: "Le nom de la grille est requis." });
    }

    const grid = await Grid.create({
      name,
      description,
      service,
      version,
      criteria: criteria || [],
    });

    return res.status(201).json(grid);
  } catch (error) {
    console.error("Erreur createGrid:", error);
    return res
      .status(500)
      .json({ message: "Erreur serveur lors de la création de la grille." });
  }
};

// ✅ Récupérer toutes les grilles
const getGrids = async (req, res) => {
  try {
    const grids = await Grid.find().sort({ createdAt: -1 });
    return res.json(grids);
  } catch (error) {
    console.error("Erreur getGrids:", error);
    return res
      .status(500)
      .json({ message: "Erreur serveur lors de la récupération des grilles." });
  }
};

// ✅ Récupérer une grille par ID
const getGridById = async (req, res) => {
  try {
    const grid = await Grid.findById(req.params.id);
    if (!grid) {
      return res.status(404).json({ message: "Grille non trouvée." });
    }
    return res.json(grid);
  } catch (error) {
    console.error("Erreur getGridById:", error);
    return res
      .status(500)
      .json({ message: "Erreur serveur lors de la récupération de la grille." });
  }
};

// ✅ Mettre à jour une grille
const updateGrid = async (req, res) => {
  try {
    const { name, description, service, version, criteria, isActive } = req.body;

    const grid = await Grid.findById(req.params.id);
    if (!grid) {
      return res.status(404).json({ message: "Grille non trouvée." });
    }

    grid.name = name ?? grid.name;
    grid.description = description ?? grid.description;
    grid.service = service ?? grid.service;
    grid.version = version ?? grid.version;
    grid.isActive = typeof isActive === "boolean" ? isActive : grid.isActive;

    if (Array.isArray(criteria)) {
      grid.criteria = criteria;
    }

    const updated = await grid.save();
    return res.json(updated);
  } catch (error) {
    console.error("Erreur updateGrid:", error);
    return res
      .status(500)
      .json({ message: "Erreur serveur lors de la mise à jour de la grille." });
  }
};

// ✅ Supprimer une grille
const deleteGrid = async (req, res) => {
  try {
    const grid = await Grid.findById(req.params.id);
    if (!grid) {
      return res.status(404).json({ message: "Grille non trouvée." });
    }

    await grid.deleteOne();
    return res.json({ message: "Grille supprimée avec succès." });
  } catch (error) {
    console.error("Erreur deleteGrid:", error);
    return res
      .status(500)
      .json({ message: "Erreur serveur lors de la suppression de la grille." });
  }
};

// ✅ Affecter une grille à des utilisateurs / rôles
const assignGrid = async (req, res) => {
  try {
    const { userIds, roles } = req.body; // userIds: [ObjectId], roles: ["CQ", "MANAGEMENT"]

    const grid = await Grid.findById(req.params.id);
    if (!grid) {
      return res.status(404).json({ message: "Grille non trouvée." });
    }

    if (Array.isArray(userIds)) {
      grid.assignedUsers = userIds;
    }

    if (Array.isArray(roles)) {
      grid.assignedRoles = roles;
    }

    const updated = await grid.save();
    return res.json(updated);
  } catch (error) {
    console.error("Erreur assignGrid:", error);
    return res
      .status(500)
      .json({ message: "Erreur serveur lors de l'affectation de la grille." });
  }
};

// ✅ Récupérer les grilles affectées à l'utilisateur connecté
const getAssignedGrids = async (req, res) => {
  try {
    const userId = req.user && req.user._id;
    const role = req.user && req.user.role;

    if (!userId) {
      return res.status(401).json({ message: "Utilisateur non authentifié." });
    }

    const grids = await Grid.find({
      isActive: true,
      $or: [
        { assignedUsers: userId },
        { assignedRoles: role },
      ],
    }).sort({ createdAt: -1 });

    return res.json({ grids });
  } catch (error) {
    console.error("Erreur getAssignedGrids:", error);
    return res
      .status(500)
      .json({ message: "Erreur serveur lors de la récupération des grilles." });
  }
};

module.exports = {
  createGrid,
  getGrids,
  getGridById,
  updateGrid,
  deleteGrid,
  assignGrid,
  getAssignedGrids,
};
