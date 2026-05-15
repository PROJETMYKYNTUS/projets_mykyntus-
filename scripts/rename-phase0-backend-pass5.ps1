# PASS 5 : nettoyage post-rename (routes API, Team, Etage, SousService, etc.)
$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$root = $workspaceRoot

$backendFiles = @(
    "PrimeBackend\Services\PrimeInMemoryStore.cs",
    "PrimeBackend\Controllers\SupervisorPolePrimeDraftController.cs",
    "PrimeBackend\Controllers\EmployeePrimeCellFicheController.cs",
    "PrimeBackend\Controllers\CellulePrimeIndicatorsController.cs",
    "PrimeBackend\Controllers\PrimePilotageController.cs",
    "PrimeBackend\Controllers\SupervisorPrimeFicheController.cs",
    "PrimeBackend\Controllers\PrimeControllers.cs",
    "PrimeBackend\Data\PrimeDbSeeder.cs",
    "PrimeBackend\Data\PrimeDatabaseInitializer.cs"
)

function Build-Pairs($pairs) {
    $list = @()
    for ($i = 0; $i -lt $pairs.Length; $i += 2) {
        $list += ,@($pairs[$i], $pairs[$i+1])
    }
    $sorted = $list | Sort-Object -Property @{Expression={ $_[0].Length }; Descending=$true}
    return ,$sorted
}

function Apply-Pairs {
    param([string]$path, $pairs)
    if (-not (Test-Path $path)) { Write-Host "SKIP (missing) $path"; return }
    $content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    $changed = $false
    foreach ($p in $pairs) {
        $k = $p[0]
        $v = $p[1]
        if ($content.Contains($k)) {
            $content = $content.Replace($k, $v)
            $changed = $true
        }
    }
    if ($changed) {
        $utf8 = New-Object System.Text.UTF8Encoding $false
        [System.IO.File]::WriteAllText($path, $content, $utf8)
        Write-Host "  Updated $path"
    } else {
        Write-Host "  No change $path"
    }
}

# Routes API et chemins specifiques
$pass5 = Build-Pairs @(
    'api/prime/supervisor-pole-prime-drafts', 'api/prime/supervisor-cellule-prime-drafts',
    'api/prime/employee-prime-cell-fiches',   'api/prime/employee-prime-service-fiches',
    'api/prime/cellule-prime-indicators',     'api/prime/service-prime-indicators',
    'api/prime/employees-by-cellule',         'api/prime/employees-by-service',
    'api/prime/employees-by-pole',            'api/prime/employees-by-cellule',
    'api/prime/cell-pilotage',                'api/prime/service-pilotage',
    'api/prime/cells',                        'api/prime/services',
    'api/prime/cellules',                     'api/prime/services'
)

# Etage / SousService / Manager / RP / Coach (variables, IDs)
$pass6 = Build-Pairs @(
    'EtageId',           'PoleId',
    'etageId',           'poleId',
    'SousServiceId',     'ServiceId',
    'sousServiceId',     'serviceId',
    'ServiceNode',       'CelluleNode',
    'EtageNode',         'PoleNode',
    'SousServiceNode',   'ServiceNode',
    'departement ',      'pole ',
    'Departement',       'Pole',
    'departement',       'pole'
)

# Team entity / TeamId : ces references doivent disparaitre car Team est supprime
# On les remplace par Service car le pilote est rattache au service maintenant
$pass7 = Build-Pairs @(
    'TeamEntity',  'ServiceEntity',
    'teamId',      'serviceId',
    'TeamId',      'ServiceId',
    'teamName',    'serviceName',
    'TeamName',    'ServiceName',
    'Teams',       'Services'
)

# Manager role : on le remplace par chaine vide ou autre selon contexte
# Pour la securite on conserve "Manager" dans les literals string pour migration ulterieure
# Mais on renomme dans les noms de variables specifiques
$pass8 = Build-Pairs @(
    'isManager',     'isChefProjet',
    'managerName',   'chefProjetName',
    'managerNode',   'chefProjetNode'
)

Write-Host "=== PASS 5: Routes API ==="
foreach ($f in $backendFiles) { Apply-Pairs -path (Join-Path $root $f) -pairs $pass5 }
Write-Host "`n=== PASS 6: Etage / SousService / Departement ==="
foreach ($f in $backendFiles) { Apply-Pairs -path (Join-Path $root $f) -pairs $pass6 }
Write-Host "`n=== PASS 7: Team ==="
foreach ($f in $backendFiles) { Apply-Pairs -path (Join-Path $root $f) -pairs $pass7 }
Write-Host "`n=== PASS 8: Manager noms variables ==="
foreach ($f in $backendFiles) { Apply-Pairs -path (Join-Path $root $f) -pairs $pass8 }

Write-Host "`n=== DONE ==="
