# Importe le module Prime depuis origin/mykyntus_v3 sur la branche courante (ex. mykyntus_v2).
# Usage (PowerShell, à la racine du dépôt) :
#   .\scripts\import-prime-from-mykyntus_v3.ps1
#
# Git Bash : utiliser scripts/import-prime-from-mykyntus_v3.sh (ne pas utiliser ` pour couper les lignes).
# Option : -DryRun pour afficher les commandes sans les exécuter.

param(
    [switch]$DryRun,
    [string]$Remote = "origin",
    [string]$Branch = "mykyntus_v3"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

$ref = "${Remote}/${Branch}"
$paths = @(
    "PrimeBackend",
    "prime-angular",
    "docs/prime-fiche-template-v1.md",
    "docs/prime-fiche-template-v2.md",
    "docs/prime-manual-test-checklist.md",
    "docs/prime-validation-api-scope.md",
    "init/sql/prime_database.sql",
    "docker-compose.yml"
)

function Invoke-Git {
    param([string[]]$Args)
    $line = "git " + ($Args -join " ")
    Write-Host ">> $line" -ForegroundColor Cyan
    if (-not $DryRun) {
        & git @Args
        if ($LASTEXITCODE -ne 0) { throw "git a échoué (code $LASTEXITCODE): $line" }
    }
}

Write-Host "Dépôt : $RepoRoot" -ForegroundColor Green
Invoke-Git @("fetch", $Remote, $Branch)
Invoke-Git @("checkout", $ref, "--") + $paths

Write-Host ""
Write-Host "Terminé. Vérifiez :" -ForegroundColor Green
Write-Host "  git status"
Write-Host "  git diff --stat"
Write-Host ""
Write-Host "Si docker-compose.yml écrase des services hors Prime, comparez :" -ForegroundColor Yellow
Write-Host "  git diff HEAD -- docker-compose.yml"
Write-Host ""
Write-Host "Puis commit ciblé (si souhaité) :" -ForegroundColor Yellow
Write-Host '  git commit -m "feat(prime): import module Prime depuis mykyntus_v3"'
