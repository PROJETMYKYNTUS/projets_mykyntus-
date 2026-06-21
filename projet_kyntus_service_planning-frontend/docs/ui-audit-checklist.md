# Checklist audit visuel Kyntus

Référence design : module **Prime** (`prime-page-shell`, `prime-page-title`, `ky-card`, `prime-table`).

## Critères par écran

- [ ] Padding page uniforme (`p-8` / `32px`) — contenu non collé aux bords
- [ ] Un seul titre principal (`h1` / `KyntusPageHeader`) — pas d'eyebrow groupe
- [ ] Un seul item sidebar actif (pas de double sur `/users/fields` vs `/users`)
- [ ] Cartes : `rounded-xl`, bordure `--border-color`, fond `--bg-card`
- [ ] Tables : en-têtes uppercase muted, cellules `px-6 py-4`
- [ ] Boutons : `ky-btn-primary` / `ky-btn-secondary` ou `prime-btn-*`
- [ ] Alertes : classes `ky-alert-*` ou `alert-error` standardisées
- [ ] Responsive : actions header wrap, tables scroll horizontal
- [ ] Mode clair + mode sombre

## Plateforme

| Route | Titre attendu | Statut |
|-------|---------------|--------|
| `/home` | Accueil | ☐ |
| `/notifications` | Notifications | ☐ |
| `/settings` | Paramètres | ☐ |

## Organisation

| Route | Titre attendu | Statut |
|-------|---------------|--------|
| `/organisation?tab=home` | Organisation RH | ☐ |
| `/organisation?tab=departments` | Pôles | ☐ |
| `/organisation?tab=poles` | Cellules | ☐ |
| `/organisation?tab=cellules` | Services | ☐ |

## Ressources Humaines

| Route | Titre attendu | Statut |
|-------|---------------|--------|
| `/users` | Employés | ☐ |
| `/users/fields` | Champs employés | ☐ |
| `/users/create` | Nouvel employé | ☐ |
| `/users/edit/:id` | Modifier l'employé | ☐ |
| `/users/:id` | Détail employé | ☐ |
| `/import` | Import employés | ☐ |
| `/contracts` | Contrats | ☐ |
| `/contracts/new` | Nouveau contrat | ☐ |
| `/contracts/:id` | Détail du contrat | ☐ |
| `/new-employees` | Nouveaux Employés | ☐ |

## Congés

| Route | Titre attendu | Statut |
|-------|---------------|--------|
| `/conge-gestion` | Gestion des congés | ☐ |
| `/conge-historique` | Historique des congés | ☐ |
| `/conge` | Gestion des absences | ☐ |
| `/mes-conges` | Mes congés | ☐ |

## Planification

| Route | Titre attendu | Statut |
|-------|---------------|--------|
| `/planning` | Plannings | ☐ |
| `/planning/equipe` | Planning Équipe | ☐ |
| `/planning/shift-config` | Configuration Shifts | ☐ |
| `/planning/saturday-history` | Historique Samedis | ☐ |

## Formation

| Route | Titre attendu | Statut |
|-------|---------------|--------|
| `/formations` | Gestion des formations | ☐ |
| `/mes-formations` | Mes formations | ☐ |

## Communication

| Route | Titre attendu | Statut |
|-------|---------------|--------|
| `/mes-newsletters` | Mes newsletters | ☐ |
| `/newsletter` | Gestion newsletters | ☐ |

## Qualité & Amélioration

| Route | Titre attendu | Statut |
|-------|---------------|--------|
| `/reclamations-admin` | Réclamations (gestion) | ☐ |
| `/reclamations` | Mes réclamations | ☐ |

## Documentation

| Route | Titre attendu | Statut |
|-------|---------------|--------|
| `/documentation` | Tableau de bord | ☐ |
| `/documentation/my-docs` | Mes documents | ☐ |
| `/documentation/doc-gen` | Génération de documents | ☐ |
| `/documentation/hr-doc-history` | Historique documents générés | ☐ |
| `/documentation/*` (autres) | Selon menu sidebar | ☐ |

## Prime (`/prime`)

| Vue | Statut |
|-----|--------|
| Tableau de bord | ☐ |
| Validation | ☐ |
| Configuration | ☐ |
| Résultats / Règles / Historique | ☐ |
| Vues Admin / RP / Audit / Employé | ☐ |

## Parrainage (`/parrainage`)

| Vue | Statut |
|-----|--------|
| Tableaux de bord (pilote, RH, PM, admin) | ☐ |
| Gestion / Règles / Historique | ☐ |
| Configuration / Audit / Paiements | ☐ |

## Notes de migration (2026-06)

- Fondations : `ky-page-shell` unifié avec `prime-page-shell`, alertes `ky-alert-*`
- `KyntusPageHeaderComponent` aligné sur `prime-page-title` (sans eyebrow)
- Fix sidebar : matching route exact ou préfixe `/`
- `ParrainageHeaderComponent` : barre utilitaire sans titre dupliqué
- Documentation : shell global `ky-page-shell`
- Build Angular : **OK** (`npm run build` — 2026-06-18)

### Statut migration code (automatique)

Les routes ci-dessous ont été migrées vers `ky-page-shell` + `KyntusPageHeader` / `prime-page-title`. Cocher manuellement après revue visuelle en clair/sombre.

| Groupe | Migration code |
|--------|----------------|
| RH + Contrats | OK |
| Planning + Congés | OK |
| Formation + Newsletter + Réclamations | OK |
| Documentation (shell + pages clés) | OK |
| Prime (titres inline) | OK |
| Parrainage (titres + layout) | OK |
