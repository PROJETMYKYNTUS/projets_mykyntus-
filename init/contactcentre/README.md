# Contact centre — seeds événements (employés / org intacts)

Source de vérité : [`roster.json`](roster.json) (miroir C# dans chaque service) + [`../demo/kyntus-users.manifest.json`](../demo/kyntus-users.manifest.json).

## Principe event-only

Les seeds d’enrichissement **n’upsertent jamais** l’organisation (floors / services / cellules) ni ne créent / modifient des employés Planning ou Directory.

Ils n’insèrent que des **événements métier** (congés, parrainages, formations, réclamations, documentation, fiches prime, notifications) qui référencent des `SubjectId` / GUID **déjà en base**. Si un GUID roster est absent → skip + log warning.

> `docker compose down -v` **n’est pas obligatoire** : le seed est idempotent sur une base déjà peuplée.

## Mapping Auth → persona

| Login Auth | SubjectId | Employé Prime |
|------------|-----------|---------------|
| `employee@kyntus.ma` | `…103` | `e1` Yasmine El Idrissi |
| `coach@kyntus.ma` | `…106` | `e8` Omar Tazi |
| `superviseur@kyntus.ma` | `…111` | `e9` Kenza Alami |
| `manager@kyntus.ma` | `…105` | `e10` Nadia Benchrif |
| `rp@kyntus.ma` | `…107` | `e3` Ghita Benkirane |
| `rh@kyntus.ma` | `…104` | `e5` Latifa Mansouri |
| `audit@kyntus.ma` | `…109` | `e7` Laila Zahidi |
| `admin@kyntus.ma` | `…108` | `e-admin` |
| `formation@kyntus.ma` | `…110` | `e6` Hicham Benjelloun |

Mot de passe démo Docker (défaut compose) : voir `DemoSeed__*` / `Azerty@123`.

## Flags Docker (désactivés par défaut — non présents dans `docker-compose.yml`)

Le remplissage démo des événements **n’est pas activé** au démarrage Compose. Pour le réactiver manuellement, ajouter sur le service concerné :

| Flag | Service | Effet |
|------|---------|--------|
| `KYNTUS_DEMO_ENRICHMENT=true` | tous (gate) | Active les enrichissements event-only |
| `KYNTUS_CONGE_DEMO_SEED=true` | conge-backend | Demandes + soldes (motif `%(démo)%`) |
| `KYNTUS_FORMATION_DEMO_SEED=true` | formation-backend | Sessions / assignments / parcours initiale (pas d’annuaire) |
| `KYNTUS_PLANNING_DEMO_SEED=true` | planning-backend | Réclamations + notifs (+ semaines / change-requests si cellule peuplée) |
| `Parrainage__SeedDemoData=true` | parrainage-backend | Referrals contact centre + pilotage |
| `Documentation__DemoDataSeed=true` | documentation-backend | DocumentRequests multi-statut (`ENRICH-DEMO-V1`) |
| `Prime__EnrichDemoData=true` | prime-backend | Fiches / indicateurs sur employés **existants** (pas de staff `emp-ma`) |

## Checklist de vérification dashboard

Après recreate des backends concernés (`docker compose up -d --build …`) :

1. Login `rh@kyntus.ma` → dashboard : congés en attente, parrainages, formations RH, doc, réclamations **non nuls**.
2. Login `employee@kyntus.ma` → mes congés / mes formations / mes parrainages **non vides**.
3. Relancer le même service → **pas de doublons** (marqueurs).
4. Compter les users Planning avant/après → **identique**.

## Marqueurs d’idempotence

| Domaine | Marqueur |
|---------|----------|
| Congé | motif contenant `(démo)` |
| Formation sessions | titre `Qualité softphone — Agents 1er niveau (contact centre)` |
| Planning | commentaire / réclamation `DockerPlanningEnrichmentV2` |
| Notifications formation | `WeekCode = TRAINING-SEED-MARK` |
| Documentation | type `ENRICH-DEMO-V1` |
| Prime | audit `DemoSeedApplied` |
| Parrainage | referrals contact centre ; pilotage `ref-pilot-*` |
