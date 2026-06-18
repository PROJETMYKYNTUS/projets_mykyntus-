# Dashboard legacy — retrait planifié

Les composants suivants ne sont **plus routés** (redirection vers `/home`) et ont été remplacés par `UnifiedDashboardComponent` + `GlobalDashboardService` :

| Fichier | Raison |
|---------|--------|
| `pages/dashboard-home/` | Ancienne page RH avec sidebar dupliquée et KPIs statiques |
| `pages/dashboard-employee/` | Mini-app employé avec navigation dupliquant le shell |

**Ne pas réintroduire** de grille « microservices » ni de liens rapides copiant le menu latéral sur `/home`.

Suppression physique de ces dossiers : à faire dans une PR dédiée après validation QA.
