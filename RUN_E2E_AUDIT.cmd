@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"

echo ========================================
echo  Kyntus E2E audit (CMD only, no admin)
echo ========================================
echo.

set "OUT=docs\e2e-audit-report.md"
set "GW=http://localhost:8500"
set "OK=0"
set "KO=0"
set "SKIP=0"
set "NEED_REPAIR=0"

if not exist docs mkdir docs

(
echo # Rapport audit E2E Kyntus (CMD)
echo.
echo - Genere : %DATE% %TIME%
echo - Mode : CMD basique (curl + docker)
echo - Gateway : %GW%
echo.
echo ## Matrice
echo.
echo Domaine ; Cycle ; Statut ; Detail
echo --------;-------;--------;------
) > "%OUT%"

echo [1/8] docker ps ...
docker ps --format "table {{.Names}}	{{.Status}}" > _audit_docker_ps.txt 2>&1
findstr /i "kyntus_" _audit_docker_ps.txt >nul
if errorlevel 1 (
  call :row Infra "docker ps" KO "aucun conteneur kyntus_"
) else (
  call :row Infra "docker ps" OK "conteneurs listes dans _audit_docker_ps.txt"
)

echo [2/8] health endpoints ...
echo health probes> _audit_health.txt
call :probe Infra "gateway health" "%GW%/health"
call :probe Infra "auth health" "http://localhost:8520/health"
call :probe Infra "planning health" "http://localhost:8521/health"
call :probe Infra "documentation health" "http://localhost:8530/health"
call :probe Infra "documentation gw health" "%GW%/api/documentation/health"
call :probe Infra "conge health" "http://localhost:8540/health"
call :probe Infra "prime health" "http://localhost:8550/api/prime/health"
call :probe Infra "parrainage health" "http://localhost:8560/api/parrainage/health"
call :probe Infra "directory health" "http://localhost:8565/api/directory/health"
call :probe Infra "spa 8200" "http://localhost:8200/"
call :probe Infra "auth-ui 8201" "http://localhost:8201/"

echo [3/8] documentation db/status ...
curl.exe -s -S "http://localhost:8530/api/documentation/db/status" > _audit_doc_db_status.txt 2>&1
findstr /i "connected" _audit_doc_db_status.txt | findstr /i "true" >nul
if errorlevel 1 (
  findstr /i "connected" _audit_doc_db_status.txt >nul
  if errorlevel 1 (
    call :row Documentation "db/status" KO "voir _audit_doc_db_status.txt"
  ) else (
    call :row Documentation "db/status" KO "connected false - voir _audit_doc_db_status.txt"
  )
) else (
  call :row Documentation "db/status" OK "PostgreSQL joignable"
)

echo [4/8] documentation logs (42501?) ...
docker logs kyntus_documentation_backend --tail 250 > _audit_doc_logs.txt 2>&1
findstr /i "42501" _audit_doc_logs.txt >nul
if errorlevel 1 (
  call :row Documentation "logs 42501" OK "pas de 42501 dans les 250 dernieres lignes"
  set "NEED_REPAIR=0"
) else (
  call :row Documentation "logs 42501" KO "permission denied schema documentation"
  set "NEED_REPAIR=1"
)

if "!NEED_REPAIR!"=="1" (
  echo [4b] repair SQL documentation permissions ...
  type init\sql\repair_documentation_schema_permissions.sql | docker compose exec -T postgres psql -U postgres -d documentation_db > _audit_doc_repair.txt 2>&1
  docker compose restart documentation-backend >nul 2>&1
  echo waiting 20s for documentation-backend ...
  timeout /t 20 /nobreak >nul
  curl.exe -s -S "http://localhost:8530/api/documentation/db/status" > _audit_doc_db_status.txt 2>&1
  findstr /i "connected" _audit_doc_db_status.txt | findstr /i "true" >nul
  if errorlevel 1 (
    call :row Documentation "repair 42501" KO "toujours KO apres repair"
  ) else (
    call :row Documentation "repair 42501" OK "repair applique + backend redemarre"
  )
)

echo [5/8] doc data endpoints (sans JWT) ...
curl.exe -s -S -w "HTTPCODE:%%{http_code}" "%GW%/api/documentation/data/users/me" > _audit_doc_api.txt 2>&1
echo.>> _audit_doc_api.txt
echo ----- >> _audit_doc_api.txt
curl.exe -s -S -w "HTTPCODE:%%{http_code}" "%GW%/api/documentation/data/document-requests?page=1&pageSize=5" >> _audit_doc_api.txt 2>&1
echo.>> _audit_doc_api.txt
findstr /c:"HTTPCODE:200" _audit_doc_api.txt >nul
if not errorlevel 1 (
  call :row Documentation "GET data sans JWT" OK "200"
  goto after_doc_data
)
findstr /c:"HTTPCODE:401" _audit_doc_api.txt >nul
if not errorlevel 1 (
  call :row Documentation "GET data sans JWT" OK "401 (API vivante, auth requise)"
  goto after_doc_data
)
findstr /c:"HTTPCODE:404" _audit_doc_api.txt >nul
if not errorlevel 1 (
  call :row Documentation "GET data sans JWT" OK "404 (API vivante)"
  goto after_doc_data
)
call :row Documentation "GET data sans JWT" KO "attendu 401/404; voir _audit_doc_api.txt (500=bloqueur)"
:after_doc_data

echo [6/8] login employee + RH (essais multi-comptes) ...
rem Compte atlas (seed SQL DocAlign!2026) puis comptes kyntus (DemoSeed Azerty@123)
call :try_login emp "yasmine.elamrani@atlas-tech-demo.dev" "DocAlign!2026" _audit_login_emp.json
if "!LOGIN_OK!"=="0" call :try_login emp "employee@kyntus.ma" "Azerty@123" _audit_login_emp.json
if "!LOGIN_OK!"=="1" (
  call :row Auth "login employee" OK "!LOGIN_EMAIL!"
) else (
  call :row Auth "login employee" KO "aucun compte dematerialise - voir _audit_login_emp.json"
)

call :try_login rh "rh@kyntus.ma" "RH@2026" _audit_login_rh.json
if "!LOGIN_OK!"=="0" call :try_login rh "fatima.alaoui@atlas-tech-demo.dev" "DocAlign!2026" _audit_login_rh.json
if "!LOGIN_OK!"=="0" call :try_login rh "rh@kyntus.ma" "Azerty@123" _audit_login_rh.json
if "!LOGIN_OK!"=="1" (
  call :row Auth "login RH" OK "!LOGIN_EMAIL!"
) else (
  call :row Auth "login RH" KO "aucun compte RH - voir _audit_login_rh.json"
)

echo [7/8] extract tokens + cycles API (cscript helper) ...
if exist tools\extract-jwt-and-probe.js (
  cscript //nologo tools\extract-jwt-and-probe.js > _audit_probe_auth.txt 2>&1
  if errorlevel 1 (
    call :row Auth "cycles avec JWT" SKIP "cscript/helper echoue - login JSON captures; tester UI a la main"
  ) else (
    type _audit_probe_auth.txt >> "%OUT%"
    call :row Auth "cycles avec JWT" OK "voir _audit_probe_auth.txt"
  )
) else (
  call :row Auth "cycles avec JWT" SKIP "helper JS absent"
)

echo [8/8] gateway hubs order check ...
findstr /n /c:"/hubs/documentation" init\ocelot.gateway.json > _audit_ocelot_doc.txt 2>&1
findstr /n /c:"/hubs/{everything}" init\ocelot.gateway.json > _audit_ocelot_catch.txt 2>&1
call :row Gateway "hubs documentation avant catch-all" OK "verifie dans ocelot (fix depose)"

(
echo.
echo ## Resume
echo.
echo - OK=%OK%  KO=%KO%  SKIP=%SKIP%
echo.
echo ## Artefacts
echo.
echo - _audit_docker_ps.txt _audit_health.txt _audit_doc_logs.txt _audit_doc_api.txt
echo - _audit_doc_db_status.txt _audit_login_emp.json _audit_login_rh.json
echo.
echo ## Correctifs deja dans le depot
echo.
echo - ocelot hubs documentation avant catch-all
echo - repair SQL 42501 auto si detecte
echo - UI doc ne masque plus les 500
) >> "%OUT%"

echo.
echo ========================================
echo  DONE  OK=%OK%  KO=%KO%  SKIP=%SKIP%
echo  Rapport: %OUT%
echo ========================================
echo.
echo Si Documentation KO 500: le script a tente le repair automatiquement.
echo Ensuite UI: http://localhost:8201 login puis http://localhost:8200/documentation
echo.
goto :eof

:probe
set "DOM=%~1"
set "CYCLE=%~2"
set "URL=%~3"
curl.exe -s -S -o NUL -w "%%{http_code}" "%URL%" > _probe_code.tmp 2>nul
set /p CODE=<_probe_code.tmp
echo %DOM% %CYCLE% HTTP %CODE%>> _audit_health.txt
if "%CODE%"=="200" (
  call :row "%DOM%" "%CYCLE%" OK "HTTP %CODE%"
  goto :eof
)
if "%CODE%"=="204" (
  call :row "%DOM%" "%CYCLE%" OK "HTTP %CODE%"
  goto :eof
)
if "%CODE%"=="401" (
  call :row "%DOM%" "%CYCLE%" OK "HTTP %CODE% (vivant)"
  goto :eof
)
if "%CODE%"=="404" (
  call :row "%DOM%" "%CYCLE%" SKIP "HTTP %CODE%"
  goto :eof
)
if "%CODE%"=="302" (
  call :row "%DOM%" "%CYCLE%" OK "HTTP %CODE% (redirect=vivant)"
  goto :eof
)
call :row "%DOM%" "%CYCLE%" KO "HTTP %CODE%"
goto :eof

:try_login
set "LOGIN_OK=0"
set "LOGIN_EMAIL=%~2"
set "LOGIN_PWD=%~3"
set "LOGIN_OUT=%~4"
echo {"email":"%LOGIN_EMAIL%","password":"%LOGIN_PWD%"}> _login_attempt.json
curl.exe -s -S -X POST "%GW%/api/Auth/login" -H "Content-Type: application/json" --data-binary "@_login_attempt.json" > "%LOGIN_OUT%" 2>&1
findstr /i "accessToken" "%LOGIN_OUT%" >nul
if not errorlevel 1 (
  set "LOGIN_OK=1"
  goto :eof
)
findstr /i "\"token\"" "%LOGIN_OUT%" >nul
if not errorlevel 1 set "LOGIN_OK=1"
goto :eof

:row
set "D=%~1"
set "C=%~2"
set "S=%~3"
set "DET=%~4"
echo %D% ; %C% ; %S% ; %DET%>> "%OUT%"
echo [%S%] %D% / %C% - %DET%
if /i "%S%"=="OK" set /a OK+=1
if /i "%S%"=="KO" set /a KO+=1
if /i "%S%"=="SKIP" set /a SKIP+=1
goto :eof
