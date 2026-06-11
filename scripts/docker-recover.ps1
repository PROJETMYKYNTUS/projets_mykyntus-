# Répare les erreurs Docker Desktop :
#   - overlayfs/snapshots/... no such file or directory
#   - container process is already dead
#   - EIO / EOF pendant builds parallèles
#
# Usage :
#   1. Fermer tous les conteneurs du projet
#   2. Redémarrer Docker Desktop (obligatoire si snapshots corrompus)
#   3. .\scripts\docker-recover.ps1
#   4. .\scripts\docker-build-safe.ps1

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $root

Write-Host "=== Récupération Docker ===" -ForegroundColor Cyan
Write-Host "Arrêt des conteneurs du projet..." -ForegroundColor Yellow
docker compose down --remove-orphans 2>$null

Write-Host "Purge du cache BuildKit (snapshots corrompus)..." -ForegroundColor Yellow
docker builder prune -af

Write-Host "Purge des images dangling..." -ForegroundColor Yellow
docker image prune -f

Write-Host "`nTerminé." -ForegroundColor Green
Write-Host "Si l'erreur persiste : Docker Desktop → Troubleshoot → Clean / Purge data, puis redémarrer." -ForegroundColor Yellow
Write-Host "Ensuite : .\scripts\docker-build-safe.ps1" -ForegroundColor Cyan
