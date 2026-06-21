# Parrainage Service Backend

API .NET 8 du module parrainage MyKyntus (PostgreSQL, mode développeur sans JWT — calqué sur Prime).

## Démarrage Docker

```bash
docker compose up -d parrainage-db-bootstrap parrainage-backend api-gateway planning-frontend
```

- API directe : `http://localhost:5260/api/parrainage/health`
- Via gateway : `http://localhost:5000/api/parrainage/health`
- UI : `http://localhost:8200/parrainage`

## Variables d'environnement

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__DefaultConnection` | Chaîne Npgsql (`parrainage_db` / `parrainage_user`) |
| `Parrainage__SeedDemoData` | `true` : seed 15 parrainages si tables vides |
| `Parrainage__AllowDemoSeedEndpoint` | `true` : autorise `POST /api/parrainage/dev/seed` |

## Schéma base

Les tables sont créées par **EF Core Migrations** au démarrage (`Database.Migrate()`).  
`init/sql/parrainage_database.sql` ne crée que la base et l'utilisateur PostgreSQL.

Si la base a déjà été créée par l’ancien `EnsureCreated()` (erreur `42P07 relation "parrainage_audit_log" already exists`), le backend **marque automatiquement** les migrations déjà présentes dans `__EFMigrationsHistory` puis applique uniquement les migrations manquantes (ex. colonne `Notes`). Aucun `docker compose down -v` n’est nécessaire dans ce cas.

Pour repartir de zéro sur Postgres uniquement : `docker compose down -v` puis `docker compose up -d`.

## Mode développeur (sans JWT)

Le frontend envoie des en-têtes HTTP (comme Prime) :

- `X-Parrainage-Role` : `PILOTE`, `RH`, `ADMIN`, `MANAGER`, `COACH`, `RP`, `AUDIT`
- `X-Parrainage-User-Id` : ex. `emp-1`, `rh-1`, `admin-1`
- `X-Parrainage-Project-Id` : optionnel

Si absents, le backend utilise `PILOTE` / `emp-1` (log debug).

## Endpoints principaux

| Méthode | Chemin | Description |
|---------|--------|-------------|
| GET | `/api/parrainage/referrals` | Liste parrainages |
| POST | `/api/parrainage/referrals` | Création (limite / employé) |
| PATCH | `/api/parrainage/referrals/{id}` | Édition (+ audit si manuel) |
| POST | `/api/parrainage/referrals/{id}/status` | Changement statut |
| POST | `/api/parrainage/referrals/{id}/reward` | Attribution prime |
| GET | `/api/parrainage/admin/export` | Export JSON snapshot |
| GET | `/api/parrainage/dev/seed-status` | Diagnostic seed |
| POST | `/api/parrainage/dev/seed` | Seed manuel |

## Migrations EF

```bash
cd parrainage_service_backend
dotnet ef migrations add NomMigration --output-dir Data/Migrations
dotnet ef database update
```

Design-time : `Data/ParrainageDbContextFactory.cs` (PostgreSQL localhost:**8433**, aligné sur le port hôte du `docker-compose`).

## Frontend mock (secours)

Comme Prime : `localStorage.setItem('parrainage.demoMockData', 'true')` puis recharger l'app Angular.
