@echo off
cd /d "%~dp0.."
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0switch-kyntus-urls.ps1" %*
exit /b %ERRORLEVEL%
