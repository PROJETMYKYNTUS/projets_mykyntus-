# PRIME — parcours de test manuel (Docker / seed)

Prérequis : stack `docker compose up -d` (gateway, `prime-backend`, Postgres seedé). Utilisateur superviseur de démo **`e9`** (Kenza Alami), période seed **`2026-04`** si base recréée avec le seeder actuel.

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

- Pôle sans cellule/service : créer la structure avant d’affecter un chef de projet.
- Pilote sans référent : affecter le référent technique avant d’ajouter un pilote.
- Profil RH / Admin / Audit : refus d’affectation structurelle.
