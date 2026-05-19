# PRIME — parcours de test manuel (Docker / seed)

Prérequis : stack `docker compose up -d` (gateway, `prime-backend`, Postgres seedé). Variables `Prime__SeedDemoData` et `Prime__EnrichDemoData` à `true` sur `prime-backend` (voir `docker-compose.yml`).

## Comptes et périodes (données enrichies en base)

| Rôle | Id | Usage |
|------|-----|--------|
| Superviseur | `e9` (Kenza Alami) | Fiches communes, brouillons cellule |
| Référent technique | `e8` (Omar Tazi) | Validation 1re ligne |
| Chef de projet / RP | `e6` (Hicham Benjelloun) | Dashboard RP, validation finale |
| Pilotes seed | `e1`–`e4`, `e-enrich-01`…`e-enrich-15` | Pilotage, fiches par service |

Périodes avec brouillons / fiches fictives : **`2026-01`** à **`2026-04`** et le **mois calendaire courant** (UTC). Template enrichi : `enrich-template-v2`.

### Re-enrichir sans effacer la base

**Important** : un simple `dotnet build` ne met pas à jour l’image Docker. Après modification du code :

```bash
docker compose build prime-backend --no-cache
docker compose up -d prime-backend
```

Vérifier dans les logs : `PRIME enrichissement v3 terminé` ou `déjà appliqué`.

Depuis la machine hôte (Postgres local port **5433**) :

```bash
cd PrimeBackend
dotnet run -- enrich-demo
dotnet run -- enrich-demo --force
```

**Diagnostic** (backend direct `:5250` ou gateway `:5000`) :

```http
GET /api/prime/demo/enrichment-status
POST /api/prime/demo/enrich?force=true
```

Réponse attendue si tout va bien : `hasEnrichmentData: true`, `counts.fiches` > 20, `counts.enrichEmployees` = 15.

### Si vous ne voyez aucun changement dans l’UI

1. **Période** : choisir le **mois courant** (UTC) ou `2026-04` — les graphiques dashboard utilisent le mois en cours.
2. **Utilisateur** : superviseur `e9`, chef de projet `e6` (pas seulement `e1` pilote).
3. **Brouillons** : template `enrich-template-v2` (pas seulement `demo-template`).
4. **Connexion DB** : si `enrichment-status` renvoie `databaseConfigured: false`, la chaîne `DefaultConnection` n’est pas chargée (rebuild Docker avec les variables `docker-compose.yml`).

Contrôles API rapides (gateway `:5000`) :

- `GET /api/prime/dashboard-stats` — montants sur le mois courant
- `GET /api/prime/validation/summary` — plusieurs statuts
- `GET /api/prime/admin/audit-logs?take=20`
- `GET /api/prime/admin/anomalies?take=20`
- `GET /api/rp/dashboard-stats?rpUserId=e6`

## 1. Fiches communes (liste)

1. Ouvrir le module PRIME, écran **« Fiches communes — en cours »**.
2. Vérifier que la liste charge sans erreur (`GET /api/prime/supervisor-pole-prime-drafts/list-active?supervisorUserId=e9` via gateway).
3. **Rafraîchir** puis ouvrir une fiche existante ou **Ajouter** une période + template.

## 2. Saisie partie commune (RACC / SAV)

1. **Fiche PRIME — saisie** : choisir la même période, renseigner / importer Excel si besoin, **enregistrer** le brouillon cellule (`PUT .../supervisor-pole-prime-drafts`).
2. Vérifier qu’aucune erreur « périmètre organisationnel » ne s’affiche.

## 3. Fiches pilotes (pilotage)

1. **Fiches PRIME — pilotage** : période **`2026-04`** (ou celle du brouillon).
2. Vérifier une ligne par **équipe (service)** avec effectifs cohérents.
3. Sélectionner un pilote : la saisie cellule se charge ; **Enregistrer** une fiche pilote.
4. Si fiche pilote **Complete** + template lié : tester **Aperçu fusionné** et export XLSX.

## 4. Indicateurs par équipe

1. **Indicateurs PRIME** : la liste déroulante reflète les **services** supervisés ; enregistrement sans 404.

## 5. Validation workflow (hors UI Angular)

Les transitions **approve / reject / bulk** exposées par `GET|POST /api/prime/validation` ne sont **pas** branchées sur une page Angular dédiée dans ce dépôt. Les tester via **Swagger / curl / Postman** ou un outil API si besoin métier.

Détails : [`prime-validation-api-scope.md`](prime-validation-api-scope.md).

## 6. Organisation RH — affectations structurelles

Prérequis : écran **Organisation RH** (module Prime), utilisateur RH ; API `prime-backend` avec PostgreSQL (`DefaultConnection` configurée).

Pour chaque action : le bouton doit renvoyer **204** (ou **200** pour création), puis **Actualiser** doit refléter le changement ; après redémarrage de `prime-backend`, l’état doit être identique.

| Bouton UI | Endpoint | Succès attendu |
|-----------|----------|----------------|
| Créer (pôle) | `POST /api/prime/org/structure/departments` | Nouveau pôle en liste ; ligne en base `prime_pole` |
| Créer la cellule | `POST .../structure/departments/{poleId}/poles` | Cellule sous le pôle |
| Créer le service | `POST .../structure/poles/{celluleId}/cellules` | Service feuille sous la cellule |
| Enregistrer chef de projet | `POST .../structure/departments/{poleId}/manager` | Employé : `role = Chef de projet`, `pole_id` = pôle |
| Retirer chef de projet | `DELETE .../structure/departments/{poleId}/manager` | Titulaire repasse `Pilote` (ou rôle opérationnel de repli) |
| Enregistrer superviseur | `POST .../structure/poles/{celluleId}/supervisor` | Employé : `role = Superviseur`, `cellule_id` = cellule |
| Retirer superviseur | `DELETE .../structure/poles/{celluleId}/supervisor` | Ancien superviseur rétrogradé en base |
| Enregistrer référent technique | `POST .../structure/cellules/{serviceId}/coach` | `role = Référent technique`, `service_id` = service feuille |
| Retirer référent | `DELETE .../structure/cellules/{serviceId}/coach` | Titulaire rétrogradé |
| Ajouter pilote | `POST .../structure/cellules/{serviceId}/pilots` | `role = Pilote`, `parent_id` = référent du service |
| Retirer pilote | `DELETE .../structure/cellules/{serviceId}/pilots/{employeeId}` | Lien coach–pilote retiré |

Contrôles rapides API après **Enregistrer** superviseur (ex. employé `e1`, cellule `p1`) :

1. `GET /api/prime/employees` → l’employé a `role: "Superviseur"`.
2. `GET /api/prime/org/assignments/supervisor-service` → une ligne avec `userId` et `celluleId` cohérents.

Cas d’erreur attendus (message rouge, pas bouton silencieux) :

- Pilote sans référent : affecter le référent technique avant d’ajouter un pilote.
- Profil RH / Admin / Audit : refus d’affectation structurelle (sauf navigation Admin sur Organisation pour test).

### Listes déroulantes (titulaire visible)

| Étape | Attendu |
|-------|---------|
| Rôle **RH** sur `/rh/organisation` | Le rôle démo passe en RH (sessionStorage) ; les `<select>` chef de projet / superviseur / référent affichent le titulaire actuel même s’il est « protégé » |
| Changer de titulaire puis recharger | La valeur sélectionnée reste dans la liste des options (pas de retour visuel vers un autre employé) |
| Topbar | Nom de l’employé courant affiché à côté du sélecteur de rôle |

### Fiche commune — une par pôle racine et période

| Étape | Attendu |
|-------|---------|
| Superviseur (ex. `e9`), période `YYYY-MM` | `GET .../supervisor-pole-prime-drafts/list-active` : au plus **une** fiche active par couple `(rootPoleId, period)` |
| Upsert sur deux cellules du même pôle | Un seul brouillon conservé pour le pôle racine et la période |

### Erreur HTTP 502 sur `/api/prime/...` (ex. `org/etages`)

Le front Prime (`http://localhost:4202`) proxifie `/api/` vers la **gateway** (`kyntus_gateway:8080`), qui route vers **prime-backend**. Un 502 signifie en général que le backend PRIME ne répond pas encore ou a crashé au démarrage.

| Vérification | Commande / URL attendue |
|--------------|-------------------------|
| Santé directe backend | `http://localhost:5250/api/prime/health` → `{ "status": "ok" }` |
| Santé via gateway | `http://localhost:5000/api/prime/health` → idem |
| Logs backend | `docker logs kyntus_prime_backend --tail 100` |
| Rebuild après correctifs | `docker compose build prime-backend && docker compose up -d prime-backend api-gateway prime-frontend` |

**Dev local Angular** (`ng serve`) : le proxy doit cibler la gateway sur le port **5000** (`prime-angular/proxy.conf.json`), pas 5001.

### Erreur « An error occurred while saving the entity changes »

Si l’affectation RH ou l’enregistrement d’une fiche échoue avec ce message générique :

1. **Redémarrer le conteneur `prime-backend`** (au démarrage, le correctif de schéma `OrgOptional` rend `CelluleId` / `ServiceId` nullable et ajoute `RootPoleId` sur les brouillons).
2. Vérifier les logs backend : le message PostgreSQL interne (contrainte NOT NULL, clé étrangère, doublon) est désormais renvoyé dans `{ "error": "…" }` sur les API organisation / fiches communes.
3. En dernier recours : `docker compose down -v` puis `up` (perte des données locales).

### Indicateurs — cellule RH puis service

| Étape | Attendu |
|-------|---------|
| Rôle **Superviseur** (`e9`), page indicateurs | `GET /api/prime/org/supervisor-scope?supervisorUserId=e9` alimente le select **Cellule (RH)** |
| Choisir une cellule | Le select **Service** liste les `prime_service` enfants ; l’édition appelle `GET/PUT .../services/{serviceId}/prime-indicators` |
| Aperçu en bas | Une ligne par service (libellé `Cellule — Service` si plusieurs services) |
