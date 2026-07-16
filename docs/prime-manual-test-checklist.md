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

## 5. Validation workflow (UI Angular)

Les transitions **approve / reject / bulk** sont exposées par `GET|POST /api/prime/validation` **et** branchées sur les pages Angular `prime-validation-page` / `prime-validation-history-page` (module Prime).

Parcours UI : module PRIME → écran validation → approve / reject / bulk → vérifier l’historique.

Détails API : [`prime-validation-api-scope.md`](prime-validation-api-scope.md).
