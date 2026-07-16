@echo off
cd /d "%~dp0"
set GW=http://localhost:8500

echo === RH kyntus (RH@2026) ===
echo {"email":"rh@kyntus.ma","password":"RH@2026"}> _t.json
curl.exe -s -S -w "\nHTTP:%%{http_code}\n" -X POST "%GW%/api/Auth/login" -H "Content-Type: application/json" --data-binary "@_t.json"
echo.

echo === Employee kyntus (Azerty@123) ===
echo {"email":"employee@kyntus.ma","password":"Azerty@123"}> _t.json
curl.exe -s -S -w "\nHTTP:%%{http_code}\n" -X POST "%GW%/api/Auth/login" -H "Content-Type: application/json" --data-binary "@_t.json"
echo.

echo === Atlas RH (DocAlign!2026) ===
echo {"email":"fatima.alaoui@atlas-tech-demo.dev","password":"DocAlign!2026"}> _t.json
curl.exe -s -S -w "\nHTTP:%%{http_code}\n" -X POST "%GW%/api/Auth/login" -H "Content-Type: application/json" --data-binary "@_t.json"
echo.

echo === Atlas employee (DocAlign!2026) ===
echo {"email":"yasmine.elamrani@atlas-tech-demo.dev","password":"DocAlign!2026"}> _t.json
curl.exe -s -S -w "\nHTTP:%%{http_code}\n" -X POST "%GW%/api/Auth/login" -H "Content-Type: application/json" --data-binary "@_t.json"
echo.

echo Si tu as le mot de passe employee, indique-le et on l ajoute au script.
pause
