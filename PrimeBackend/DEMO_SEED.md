# Données de démonstration PRIME (Maroc)

L’enrichissement **v4** remplit `prime_db` avec des données **Bogus** (`fr`, emails `.ma`) en s’adaptant à **votre** arborescence Pôle → Cellule → Service déjà en base (IDs GUID), sans reset PostgreSQL.

## Prérequis Docker

Dans `docker-compose.yml`, le service `prime-backend` expose :

- `Prime__SeedDemoData=true` — seed initial si la base est vide (aucun pôle)
- `Prime__EnrichDemoData=true` — enrichissement automatique au démarrage
- `Prime__AllowDemoSeedEndpoint=true` — API manuelle ci-dessous

## Vérifier l’état

```powershell
curl http://localhost:5000/api/prime/demo/enrichment-status
```

## Régénérer / compléter (recommandé après changement d’org RH)

```powershell
curl -X POST "http://localhost:5000/api/prime/demo/enrich?force=true"
docker compose restart prime-backend
```

Puis ouvrir l’UI : http://localhost:4202 (Ctrl+F5).

## Contenu généré

- Collaborateurs `emp-ma-*` (superviseurs, référents, pilotes) par cellule/service si manquants
- Indicateurs KPI par service (NPS, AHT, QA, etc.)
- Brouillons cellule + fiches pilotes sur plusieurs périodes (`2026-01` … mois courant)
- Statuts de validation variés (pending, validé, rejeté, etc.)
- Logs d’audit, anomalies liées aux fiches, synthèse globale (Excel démo)

## Fichiers source

- [`Data/PrimeMoroccanDataFactory.cs`](Data/PrimeMoroccanDataFactory.cs) — Bogus, montants MAD, libellés métier
- [`Data/PrimeOrgSnapshot.cs`](Data/PrimeOrgSnapshot.cs) — lecture org + rôles pivots dynamiques
- [`Data/PrimeDbEnrichmentSeeder.cs`](Data/PrimeDbEnrichmentSeeder.cs) — orchestration v4
