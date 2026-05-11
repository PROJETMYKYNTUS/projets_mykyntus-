# Spec officielle — Fiche PRIME template Excel (v1)

`templateFormatVersion`: **1**

Ce document est le contrat normatif entre un classeur `.xlsx` et l’application Prime (import → schéma JSON → saisie dynamique). Tout écart non prévu implique une **nouvelle version** du format (v2, v3…).

> **Note** : un second layout **v2** (exemplaire métier, colonnes décalées, sans `ID_UNIQUE` en E) est décrit dans [prime-fiche-template-v2.md](prime-fiche-template-v2.md). L’import tente **d’abord la v1**, puis la **v2** si les en-têtes correspondent.

## Feuille et plage

- **Feuille utilisée** : la **première** feuille du classeur (`SheetNames[0]`).
- **Pas de ligne vide** entre deux lignes de données utiles : une ligne vide en colonne **ID_UNIQUE** (E) marque la **fin du tableau**.

## Lignes d’en-tête (obligatoires)

| Ligne Excel | Rôle |
|-------------|------|
| 1 | Titres de secteurs (facultatif pour le parseur ; libellés humains au-dessus des blocs Prime / Challenge). |
| 2 | **Sous-en-têtes obligatoires** pour chaque bloc secteur : voir section « Blocs secteur ». |

Les données métier commencent à la **ligne 3** Excel (indice interne `2`).

## Colonnes fixes A–F (indices 0–5)

| Col Excel | Indice | Contenu | Règle |
|-----------|--------|---------|--------|
| A | 0 | **Contrat** | Texte (ex. `RACC`, `SAV`, autre). **Fusions verticales autorisées et recommandées** : toutes les lignes d’un même contrat partagent la fusion ; le parseur résout via `!merges` et propage la valeur sur chaque ligne. Si une cellule A est vide après résolution de fusions, la valeur du **contrat courant** est héritée de la ligne précédente. |
| B | 1 | **Indicateur** | Texte libre (non vide pour une ligne valide). |
| C | 2 | **Barème** | Texte ; peut être vide si non applicable. |
| D | 3 | **Groupe** | Texte ; peut être vide. |
| E | 4 | **ID_UNIQUE** | **Obligatoire** sur chaque ligne de données. Stable, unique dans tout le classeur. Ne doit pas être modifié une fois des données de saisie rattachées en production. |
| F | 5 | **Répartition des RDV** | Valeur par défaut (nombre ou texte selon l’UI) pour le champ `repartitionRdv`. |

## Blocs secteur (colonnes dynamiques à partir de G)

À partir de la colonne **G** (indice **6**), les secteurs se répètent horizontalement par **tranches de 11 colonnes** :

- **6 colonnes Prime** (dans l’ordre) : alignées sur `PrimeFicheLigneSaisie`  
  `resultatPrime`, `kpiPointMin`, `kpiPointMax`, `ponderationPrime`, `bonusAtteintPrime`, `montantPrime`
- **5 colonnes Challenge** :  
  `resultatChallenge`, `kpiChallenge`, `ponderationChallenge`, `bonusAtteintChallenge`, `montantChallenge`

Secteur `k` (0-based) occupe les colonnes d’indice `6 + k*11` à `6 + k*11 + 10`.

### Ligne 2 Excel — libellés attendus (comparaison normalisée)

Normalisation : minuscules, suppression des accents, espaces condensés, trim.

**Prime (6 colonnes)** — équivalents acceptés après normalisation :

1. `resultat` (ex. « Résultat »)
2. `kpi point min`
3. `kpi point max`
4. `ponderation` (« Pondération »)
5. `bonus atteint (%)` ou `bonus atteint %`
6. `montant`

**Challenge (5 colonnes)** :

1. `resultat`
2. `kpi challenge`
3. `ponderation`
4. `bonus atteint (%)` ou `bonus atteint %`
5. `montant`

Le parseur valide au moins **un** secteur (`k = 0`). Les secteurs suivants doivent répéter le même motif d’en-têtes sur la ligne 2.

### Ligne 1 Excel — libellés de secteur

Optionnel. Si présent, le texte au-dessus du bloc Prime (6 colonnes) est utilisé comme `label` du secteur ; sinon le libellé par défaut est `Secteur {n+1}`.

## Zones : saisie / formule / libellé

- **Lignes 1–2** : **libellés** (non importés comme données de défaut).
- **À partir de la ligne 3** : colonnes A–F + blocs secteur = **zones de valeurs par défaut ou formules**.
  - Si la cellule contient une **formule** (`cell.f`), le parseur enregistre la formule et la **valeur affichée / calculée** telle que stockée dans le fichier (`v` / `w`) comme valeur par défaut **MVP** (Option A : le superviseur enregistre le classeur depuis Excel avec les résultats calculés).
  - Option B (évolution) : recalcul côté serveur — hors scope v1 navigateur.

## Règles d’intégrité (échec import)

- `ID_UNIQUE` manquant ou en doublon.
- En-têtes de secteur 0 incorrects sur la ligne 2.
- Ligne de données sans contrat résolu (ni valeur, ni héritage).
- Colonnes insuffisantes pour au moins un secteur complet (17 colonnes minimum : A–F + 11).

## Lignes réservées / fin de tableau

- Si la cellule **ID_UNIQUE** est vide : **fin** des lignes de données (le reste du classeur est ignoré).
- Les lignes dont la colonne A contient uniquement des marqueurs du type `new contrat` / `nouveau contrat` **sans** `ID_UNIQUE` sont ignorées (fin logique possible).

## Fichier exemplaire

Placer une copie du classeur de référence sous [`docs/samples/`](samples/README.md) (voir README pour le nom de fichier attendu).

## Version JSON produite

Chaque import réussi produit un objet avec `templateFormatVersion: 1`. Toute évolution des positions de colonnes ou des en-têtes obligatoires doit incrémenter cette version et mettre à jour ce document.
