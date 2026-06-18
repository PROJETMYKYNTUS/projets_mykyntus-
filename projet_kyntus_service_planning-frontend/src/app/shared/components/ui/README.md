# Kyntus UI — Design system partagé

Bibliothèque de composants standalone pour unifier l’interface de la plateforme RH Kyntus.

## Principes

- **Standalone** : chaque composant est importable individuellement via `@/shared/components/ui` ou `./index.ts`.
- **Tokens CSS** : `--text-primary`, `--text-muted`, `--border-default`, `--bg-card`, `--electric-blue`.
- **États** : privilégier `KyntusLoadingState`, `KyntusEmptyState`, `KyntusErrorState` plutôt que des spinners ad hoc.
- **En-têtes** : `KyntusPageHeader` (pages) ou `KyntusRoleDashboard` (tableaux de bord par rôle).

## Composants

| Composant | Usage |
|-----------|--------|
| `KyntusPageHeaderComponent` | Titre, sous-titre, eyebrow, slot `[actions]` |
| `KyntusKpiGridComponent` | Grille KPI (`KyntusKpiItem[]`) |
| `KyntusRoleDashboardComponent` | Dashboard rôle : header + KPI + alertes + liste récente + slots `[charts]` `[contextPanel]` |
| `KyntusDashboardRecentListComponent` | Liste récente standardisée (slot `[rows]`) |
| `KyntusDashboardAlertsComponent` | Bandeau alertes warn/info/error |
| `KyntusEmployeeInboxComponent` | Boîte employé : header + filtres + contenu + bouton créer |
| `KyntusFilterBarComponent` | Filtres chips (`filterChange`) + slot `[extraFilters]` |
| `KyntusDataTableComponent` | Table avec colonnes, loading, empty ; slot `[rows]` pour lignes custom |
| `KyntusStatusBadgeComponent` | Badges avec presets `parrainage`, `documentation`, `prime` |
| `KyntusAuditDrawerComponent` / `KyntusAuditLogPageComponent` | Journal d’audit + tiroir latéral |
| `KyntusRulesAdminPageComponent` | Admin règles : liste (gauche) + éditeur (droite) |
| `KyntusAccessDeniedComponent` | Accès refusé |
| `KyntusOrgDrillBarComponent` | Navigation hiérarchique org |
| `KyntusToastService` + `KyntusToastHostComponent` | Notifications toast |

## Navigation shell

`ShellPageTitleService` (`core/navigation/`) résout le titre affiché dans la topbar à partir de l’URL courante.

## Migration

Remplacer progressivement les en-têtes / KPI / filtres locaux par ces composants. Les wrappers métier (`FiltersBarComponent`, `PrimeFilterBarComponent`) délèguent à `KyntusFilterBarComponent`.
