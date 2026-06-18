# Verifier profil URLs actif (fichiers + conteneurs Docker)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$activePath = Join-Path $root 'config\kyntus-public-urls.active.json'
$runtimePath = Join-Path $root 'config\kyntus-public-urls.runtime.js'

[Console]::Out.WriteLine('=== Kyntus URLs - verification ===')
[Console]::Out.WriteLine('')

[Console]::Out.WriteLine('=== Fichiers ===')
if (Test-Path $activePath) {
    $active = Get-Content -Raw -Path $activePath | ConvertFrom-Json
    [Console]::Out.WriteLine("active.json  -> profile=$($active.profile), host=$($active.host)")
} else {
    [Console]::Out.WriteLine('active.json  -> MANQUANT')
}

if (Test-Path $runtimePath) {
    $runtime = Get-Content -Raw -Path $runtimePath
    if ($runtime -match "host:\s*'([^']+)'") {
        [Console]::Out.WriteLine("runtime.js   -> host=$($Matches[1])")
    } else {
        [Console]::Out.WriteLine('runtime.js   -> present (host non detecte)')
    }
} else {
    [Console]::Out.WriteLine('runtime.js   -> MANQUANT (lancer switch-kyntus-urls)')
}

[Console]::Out.WriteLine('')
[Console]::Out.WriteLine('=== Docker (si demarre) ===')

$dockerOk = $false
try {
    $null = docker version 2>&1
    $dockerOk = $LASTEXITCODE -eq 0
} catch {
    $dockerOk = $false
}

if (-not $dockerOk) {
    [Console]::Out.WriteLine('Docker non disponible ou non demarre.')
    exit 0
}

foreach ($pair in @(
    @{ Name = 'kyntus_planning_frontend'; Path = '/usr/share/nginx/html/kyntus-public-urls.js' },
    @{ Name = 'kyntus_auth_frontend'; Path = '/app/dist/auth-frontend/browser/kyntus-public-urls.js' }
)) {
    $running = docker ps --filter "name=$($pair.Name)" --format '{{.Names}}' 2>$null
    if ($running) {
        [Console]::Out.WriteLine("--- $($pair.Name) ---")
        docker exec $pair.Name sh -c "head -5 $($pair.Path) 2>/dev/null || echo FICHIER ABSENT" 2>$null
    } else {
        [Console]::Out.WriteLine("$($pair.Name) -> non demarre")
    }
}

[Console]::Out.WriteLine('')
[Console]::Out.WriteLine('OK.')
