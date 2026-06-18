# Traçabilité avancement

## Fichiers modifiés (à déployer)

| Date | Fichier | Description | Déployé |
|------|---------|-------------|---------|
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/PlanningService/PlanningService/Controllers/EmployeeImportController.cs | API import guidé v2 `/api/users/import/v2` | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/PlanningService/PlanningService/Services/EmployeeImport/ | Module import guidé (wizard backend, executor, matching org) | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/PlanningService/PlanningService/Services/DirectoryOrgWriteClient.cs | Client HTTP création org + affectations Directory | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/PlanningService/PlanningService/Services/UserService.cs | Import Directory-first, mot de passe, rollback Auth, isActive fichier | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/PlanningService/PlanningService/Data/AppDbContext.cs | DbSet tables EmployeeImport* | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/PlanningService/PlanningService/Migrations/20260617101405_AddEmployeeImportV2.cs | Migration EF régénérée avec ModelSnapshot | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/PlanningService/PlanningService/Program.cs | DI import v2 + seed config champs | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/PlanningService/PlanningService.Tests/ | Tests unitaires règles import (44 tests) | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/projet_kyntus_service_planning-frontend/src/app/features/users/pages/employee-import-guided/ | Wizard Angular import guidé RH | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/projet_kyntus_service_planning-frontend/src/app/features/users/services/employee-import.service.ts | Client HTTP wizard import v2 | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/projet_kyntus_service_planning-frontend/src/app/app.routes.ts | Route `/import` → wizard guidé | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/projet_kyntus_service_planning-frontend/src/app/app.config.ts | Provider `EMPLOYEE_IMPORT_HOST` | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/config/kyntus-public-urls.deploy.backup.json | Sauvegarde URLs LAN déploiement (10.10.10.25) | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/config/kyntus-public-urls.local.json | Profil URLs localhost pour tests locaux | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/scripts/switch-kyntus-urls.ps1 | Runtime JS + volume Docker + flag -RecreateDocker (sans rebuild image) | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/scripts/switch-kyntus-urls.cmd | Lanceur CMD pour switch URLs (sortie visible depuis invite cmd) | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/scripts/verify-kyntus-urls.cmd | Lanceur CMD pour verification URLs | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/config/kyntus-public-urls.runtime.js | URLs runtime montees dans les conteneurs frontend | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/docker-compose.yml | Restauration UTF-8 propre + volumes runtime URLs | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/scripts/restore-docker-compose.py | Restauration docker-compose depuis backup sans corruption | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/auth-frontend/src/index.html | Chargement kyntus-public-urls.js avant bootstrap Angular | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/projet_kyntus_service_planning-frontend/src/index.html | Chargement kyntus-public-urls.js avant bootstrap Angular | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/auth-frontend/src/app/config/kyntus-public-urls.ts | URLs centralisées auth-frontend (profil actif) | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/projet_kyntus_service_planning-frontend/src/app/config/kyntus-public-urls.ts | URLs centralisées planning-frontend (profil actif) | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/auth-frontend/src/app/components/login/login.component.ts | Redirection post-login via KYNTUS_PUBLIC_URLS | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/auth-frontend/src/app/services/auth.service.ts | Logout via KYNTUS_PUBLIC_URLS | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/projet_kyntus_service_planning-frontend/src/app/guard/guards/auth.ts | Guard auth via KYNTUS_PUBLIC_URLS | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/projet_kyntus_service_planning-frontend/src/app/features/shell/shell-layout.component.ts | Login/logout via KYNTUS_PUBLIC_URLS | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/init/ocelot.gateway.json | BaseUrl gateway selon profil actif (localhost) | ☐ |
| 2026-06-17 | projets_mykyntus--mykyntus_prod_v1/docker-compose.yml | public_gateway_base selon profil actif (localhost) | ☐ |

## Sessions de travail

| Date | Sujet | Résumé |
|------|-------|--------|
| 2026-06-17 | Import guidé RH v2 | Intégration synchro stricte Directory/Auth/MassTransit ; remplacement UI `/import` |
