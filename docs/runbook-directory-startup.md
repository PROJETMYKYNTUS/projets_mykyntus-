# Runbook — démarrage et réconciliation (Directory maître)

## Ordre de démarrage Docker

1. `postgres`, `rabbitmq`
2. `prime-db-bootstrap`, bases métier
3. `employee-directory-backend` (attendre health `/api/directory/health`)
4. `planning-backend`, `prime-backend`, `conge-backend`, `formation-backend`
5. `gateway` (Ocelot `:8500`)
6. Frontends (`:8200` shell, `:8201` auth)

## Vérification post-démarrage

```powershell
./scripts/verify-unification.ps1 -GatewayUrl http://localhost:8500
```

## Après incident (dérive données)

1. Vérifier RabbitMQ et outbox pending (tables `outbox_messages` dans Directory / Planning / Prime).
2. Exécuter la réconciliation (JWT Admin/RH requis) :

```http
POST /api/directory/reconcile
GET  /api/directory/reconcile/verify
```

3. Si miroir Planning incomplet :

```http
POST /api/admin/org-reconciliation/sync-from-prime
GET  /api/admin/org-reconciliation/verify
```

## Flags importants

| Service | Variable | Défaut Docker | Rôle |
|---------|----------|---------------|------|
| Planning | `Directory__WriteMaster` | `true` | Création employé : Directory d'abord |
| Planning | `Directory__EnablePlanningBootstrap` | `false` | Ne republie plus EmployeUpdated au startup |
| Directory | `Directory__EnablePrimeBootstrap` | `false` | Ne tire plus Prime au runtime |
| Planning | `Directory__RequireEnsureOnWrite` | `false` | Rollback si ensure échoue (mode legacy) |

## Migration one-shot Prime → Directory (environnement existant)

Si Directory est vide après upgrade, activer temporairement `Directory__EnablePrimeBootstrap=true`, redémarrer Directory, puis `POST /api/directory/reconcile` et remettre le flag à `false`.

## Tag stable

Release migration : `mykyntus_directory_stable`
