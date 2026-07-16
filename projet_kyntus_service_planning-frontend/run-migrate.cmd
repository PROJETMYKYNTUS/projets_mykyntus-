@echo off
cd /d "%~dp0"
node scripts\migrate-prime-theme.mjs > migrate-console.log 2>&1
echo EXIT:%ERRORLEVEL%>> migrate-console.log
