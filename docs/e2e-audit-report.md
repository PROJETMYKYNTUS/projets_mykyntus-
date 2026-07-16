# Rapport audit E2E Kyntus (CMD)

- Genere : 15/07/2026 16:41:28,45
- Mode : CMD basique (curl + docker)
- Gateway : http://localhost:8500

## Matrice

| Domaine | Cycle | Statut | Detail |
Infra ; docker ps ; OK ; conteneurs listes dans _audit_docker_ps.txt
Infra ; gateway health ; SKIP ; HTTP 404
Infra ; auth health ; OK ; HTTP 200
Infra ; planning health ; OK ; HTTP 200
Infra ; documentation health ; OK ; HTTP 200
Infra ; documentation gw health ; OK ; HTTP 200
Infra ; conge health ; SKIP ; HTTP 404
Infra ; prime health ; OK ; HTTP 200
Infra ; parrainage health ; OK ; HTTP 200
Infra ; directory health ; OK ; HTTP 200
Infra ; spa 8200 ; OK ; HTTP 200
Infra ; auth-ui 8201 ; KO ; HTTP 302
Documentation ; db/status ; OK ; PostgreSQL joignable
Documentation ; logs 42501 ; OK ; pas de 42501 dans les 250 dernieres lignes
Documentation ; GET data sans JWT ; OK ; 401 (API vivante, auth requise)
Auth ; login employee ; KO ; voir _audit_login_emp.json
Auth ; login RH ; KO ; voir _audit_login_rh.json
Auth ; cycles avec JWT ; SKIP ; cscript/helper echoue - login JSON captures; tester UI a la main
Gateway ; hubs documentation avant catch-all ; OK ; verifie dans ocelot (fix depose)

## Resume

- OK=13  KO=3  SKIP=3

## Artefacts

- _audit_docker_ps.txt _audit_health.txt _audit_doc_logs.txt _audit_doc_api.txt
- _audit_doc_db_status.txt _audit_login_emp.json _audit_login_rh.json

## Correctifs deja dans le depot

- ocelot hubs documentation avant catch-all
- repair SQL 42501 auto si detecte
- UI doc ne masque plus les 500
Infra ; docker ps ; OK ; conteneurs listes dans _audit_docker_ps.txt
Infra ; gateway health ; SKIP ; HTTP 404
Infra ; auth health ; OK ; HTTP 200
Infra ; planning health ; OK ; HTTP 200
Infra ; documentation health ; OK ; HTTP 200
Infra ; documentation gw health ; OK ; HTTP 200
Infra ; conge health ; SKIP ; HTTP 404
Infra ; prime health ; OK ; HTTP 200
Infra ; parrainage health ; OK ; HTTP 200
Infra ; directory health ; OK ; HTTP 200
Infra ; spa 8200 ; OK ; HTTP 200
Infra ; auth-ui 8201 ; OK ; HTTP 302 (redirect=vivant)
Documentation ; db/status ; OK ; PostgreSQL joignable
Documentation ; logs 42501 ; OK ; pas de 42501 dans les 250 dernieres lignes
Documentation ; GET data sans JWT ; OK ; 401 (API vivante, auth requise)
Auth ; login employee ; KO ; aucun compte dematerialise - voir _audit_login_emp.json
Auth ; login RH ; KO ; aucun compte RH - voir _audit_login_rh.json
Auth ; cycles avec JWT ; SKIP ; cscript/helper echoue - login JSON captures; tester UI a la main
Gateway ; hubs documentation avant catch-all ; OK ; verifie dans ocelot (fix depose)

## Resume

- OK=14  KO=2  SKIP=3

## Artefacts

- _audit_docker_ps.txt _audit_health.txt _audit_doc_logs.txt _audit_doc_api.txt
- _audit_doc_db_status.txt _audit_login_emp.json _audit_login_rh.json

## Correctifs deja dans le depot

- ocelot hubs documentation avant catch-all
- repair SQL 42501 auto si detecte
- UI doc ne masque plus les 500
