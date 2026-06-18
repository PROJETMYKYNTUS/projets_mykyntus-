# Orphelins employés (Planning / Auth / Directory)

## Détection

1. **Via API Directory** (JWT Admin/RH) :
   - `GET /api/directory/reconcile/verify` — compte les écarts Directory vs Planning/Prime
   - `POST /api/directory/reconcile` — fusion doublons email, import Prime, republish projections

2. **Via Planning** (JWT Admin/RH) :
   - `GET /api/admin/org-reconciliation/verify` — miroir org Planning

3. **Script** :
   ```powershell
   .\scripts\verify-unification.ps1 -GatewayUrl http://localhost:8500
   ```

## Symptômes courants

| Symptôme | Cause probable | Action |
|----------|----------------|--------|
| Employé visible Planning, pas de login | `AuthUserId` null | `POST /api/users/sync-auth` ou recréer sync Auth |
| Employé Planning, absent Congé | Event RabbitMQ perdu | `POST /api/directory/reconcile` |
| Email doublon | Création partielle | `POST /api/directory/reconcile` (dedupe) |
| Org warning formulaire | SubService sans `PrimeServiceId` | `POST /api/admin/org-reconciliation/sync-from-prime` |

## Suppression manuelle

1. Supprimer via UI Employés (supprime Planning + Auth + Directory si endpoints OK)
2. Sinon SQL ciblé sur `kyntus_db.Users`, `auth_db.Users`, `employee_directory_db.employees`

## Feature flag

`Directory:RequireEnsureOnWrite=true` dans `planning-backend` : échec Directory → rollback création Planning (évite orphelins).
