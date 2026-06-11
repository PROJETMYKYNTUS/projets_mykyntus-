# Build Docker images one at a time — évite EIO / EOF / snapshots corrompus (8 builds parallèles).
# NE PAS utiliser : docker compose up --build  (lance 8 builds en parallèle)
#
# Usage :
#   .\scripts\docker-recover.ps1          # si erreur overlayfs / container already dead
#   .\scripts\docker-build-safe.ps1
#   .\scripts\docker-build-safe.ps1 -Services prime-frontend,prime-backend
#   .\scripts\docker-build-safe.ps1 -NoCache

param(
    [string[]]$Services = @(
        'auth-backend',
        'planning-backend',
        'formation-backend',
        'documentation-backend',
        'parrainage-backend',
        'prime-backend',
        'api-gateway',
        'prime-frontend',
        'parrainage-frontend'
    ),
    [switch]$NoCache
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $root

Write-Host "Docker build séquentiel ($($Services.Count) service(s))..." -ForegroundColor Cyan
Write-Host "Ne lancez pas 'docker compose up --build' — builds un par un uniquement." -ForegroundColor DarkGray

foreach ($svc in $Services) {
    Write-Host "`n=== $svc ===" -ForegroundColor Yellow
    if ($NoCache) {
        docker compose build --no-cache $svc
    } else {
        docker compose build $svc
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`nÉchec sur $svc (code $LASTEXITCODE)." -ForegroundColor Red
        Write-Host "1. Redémarrez Docker Desktop" -ForegroundColor Yellow
        Write-Host "2. .\scripts\docker-recover.ps1" -ForegroundColor Yellow
        Write-Host "3. .\scripts\docker-build-safe.ps1 -NoCache" -ForegroundColor Yellow
        exit $LASTEXITCODE
    }
}

Write-Host "`nBuild terminé. Lancez : docker compose up -d" -ForegroundColor Green
