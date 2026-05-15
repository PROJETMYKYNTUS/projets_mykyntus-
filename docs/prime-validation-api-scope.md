# API validation PRIME (hors module Angular)

Les transitions de workflow sur les fiches employé (`EmployeePrimeServiceFicheEntity`) sont exposées par le backend uniquement :

- Préfixe : `GET|POST /api/prime/validation` (voir [`PrimeValidationController.cs`](../PrimeBackend/Controllers/PrimeValidationController.cs)).
- Cas d’usage : liste filtrée, résumé par statut, approbation / rejet / bulk.

Le module **prime-angular** ne contient pas d’écran dédié branché sur ces endpoints au moment de l’implémentation du plan de reverification. Pour tester le flux complet validation, utiliser la gateway (`http://localhost:5000`) ou un client HTTP (curl, Postman, Swagger si activé).

Voir aussi le parcours manuel : [`prime-manual-test-checklist.md`](prime-manual-test-checklist.md).
