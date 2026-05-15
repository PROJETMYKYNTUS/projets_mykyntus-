# Script de renommage Phase 0 (PRIME taxonomy migration)
# Renomme uniquement le code non-migration, dans l'ordre Cellule -> Service, Pole -> Cellule, Department -> Pole
# IMPORTANT: Exclut les dossiers Migrations/ pour preserver l'historique EF

$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
$root = $workspaceRoot

Write-Host "Working from root: $root"

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

# Liste ordonnee (tableau de paires) pour preserver l'ordre et la casse
function Build-Pairs($pairs) {
    $list = @()
    for ($i = 0; $i -lt $pairs.Length; $i += 2) {
        $list += ,@($pairs[$i], $pairs[$i+1])
    }
    # Sort by key length desc to avoid prefix-substring conflicts
    $sorted = $list | Sort-Object -Property @{Expression={ $_[0].Length }; Descending=$true}
    return ,$sorted
}

# PASS 1 : Cellule -> Service
$pass1 = Build-Pairs @(
    'PutCellulePrimeIndicatorsRequest', 'PutServicePrimeIndicatorsRequest',
    'CellulePrimeIndicatorsController', 'ServicePrimeIndicatorsController',
    'CellulePrimeIndicatorEntity',      'ServicePrimeIndicatorEntity',
    'PutCellulePrimeIndicatorItem',     'PutServicePrimeIndicatorItem',
    'CellulePrimeIndicatorDto',         'ServicePrimeIndicatorDto',
    'CellulePrimeIndicators',           'ServicePrimeIndicators',
    'CelluleEntity',                    'ServiceEntity',
    'celluleId',                        'serviceId',
    'CelluleId',                        'ServiceId',
    'celluleName',                      'serviceName',
    'CelluleName',                      'ServiceName'
)

# PASS 2 : Pole -> Cellule (entites/DTOs longues d'abord)
$pass2 = Build-Pairs @(
    'UpsertEmployeePrimeCellFicheRequest', 'UpsertEmployeePrimeServiceFicheRequest',
    'EmployeePrimeCellFicheResponseDto', 'EmployeePrimeServiceFicheResponseDto',
    'EmployeePrimeCellFicheListItemDto', 'EmployeePrimeServiceFicheListItemDto',
    'EmployeePrimeCellFicheController',  'EmployeePrimeServiceFicheController',
    'EmployeePrimeCellFicheEntity',      'EmployeePrimeServiceFicheEntity',
    'EmployeePrimeCellFiches',           'EmployeePrimeServiceFiches',
    'UpsertSupervisorPolePrimeDraftRequest', 'UpsertSupervisorCellulePrimeDraftRequest',
    'SupervisorPolePrimeDraftListItemDto',   'SupervisorCellulePrimeDraftListItemDto',
    'SupervisorPolePrimeDraftResponseDto',   'SupervisorCellulePrimeDraftResponseDto',
    'SupervisorPolePrimeDraftController',    'SupervisorCellulePrimeDraftController',
    'SupervisorPolePrimeDraftEntity',        'SupervisorCellulePrimeDraftEntity',
    'SupervisorPolePrimeDrafts',             'SupervisorCellulePrimeDrafts',
    'PrimeCellFicheStatusHelper',     'PrimeServiceFicheStatusHelper',
    'PoleDraftPayloadNormalizer',     'CelluleDraftPayloadNormalizer',
    'NormalizePoleSaisieJson',        'NormalizeCelluleSaisieJson',
    'AssignSupervisorServiceRequest', 'AssignSupervisorCelluleRequest',
    'AssignCoachSousServiceRequest',  'AssignReferentTechniqueServiceRequest',
    'AssignManagerEtageRequest',      'AssignChefProjetPoleRequest',
    'SupervisorServiceAssignment',    'SupervisorCelluleAssignment',
    'CoachSousServiceAssignment',     'ReferentTechniqueServiceAssignment',
    'ManagerEtageAssignment',         'ChefProjetPoleAssignment',
    'AddPilotToCelluleBody',          'AddPilotToServiceBody',
    'CreateOrgDepartmentBody',        'CreateOrgPoleBody',
    'AssignCoachPilotRequest',        'AssignReferentTechniquePilotRequest',
    'UpdateRpValidationStatusRequest','UpdateChefProjetValidationStatusRequest',
    'RpTeamMemberPerformance',        'ChefProjetTeamMemberPerformance',
    'RpValidationItem',               'ChefProjetValidationItem',
    'RpDashboardStats',               'ChefProjetDashboardStats',
    'AdminByDepartmentPoint',         'AdminByPolePoint',
    'CellPilotageSummaryDto',         'ServicePilotageSummaryDto',
    'CoachPilotLink',                 'ReferentTechniquePilotLink',
    'CoachUserId',                    'ReferentTechniqueUserId',
    'SupervisorOwnsPole',             'SupervisorOwnsCellule',
    'GetSupervisedPoleIds',           'GetSupervisedCelluleIds',
    'PolePrimeDraftId',               'CellulePrimeDraftId',
    'PolePrimeDraft',                 'CellulePrimeDraft',
    'ManagerValidated',               'SuperviseurValidated',
    'ByDepartment',                   'ByPole',
    'PoleSaisieJson',                 'CelluleSaisieJson',
    'poleSaisieJson',                 'celluleSaisieJson',
    'CellSaisieJson',                 'ServiceSaisieJson',
    'cellSaisieJson',                 'serviceSaisieJson',
    'PoleEntity',                     'CelluleEntity',
    'poleId',                         'celluleId',
    'PoleId',                         'CelluleId',
    'poleName',                       'celluleName',
    'PoleName',                       'CelluleName'
)

# PASS 3 : Department/Departement -> Pole
$pass3 = Build-Pairs @(
    'DepartmentEntity',  'PoleEntity',
    'Departments',       'Poles',
    'departmentId',      'poleId',
    'DepartmentId',      'PoleId',
    'departementId',     'poleId',
    'DepartementId',     'PoleId',
    'departmentName',    'poleName',
    'DepartmentName',    'PoleName'
)

# PASS 4 : Role string literals
$pass4 = Build-Pairs @(
    '"RP"',       '"Chef de projet"',
    '"Coach"',    '"Référent technique"'
)

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

Write-Host "`n=== PASS 1: Cellule -> Service ==="
foreach ($f in $backendFiles) { Apply-Pairs -path (Join-Path $root $f) -pairs $pass1 }

Write-Host "`n=== PASS 2: Pole -> Cellule ==="
foreach ($f in $backendFiles) { Apply-Pairs -path (Join-Path $root $f) -pairs $pass2 }

Write-Host "`n=== PASS 3: Department -> Pole ==="
foreach ($f in $backendFiles) { Apply-Pairs -path (Join-Path $root $f) -pairs $pass3 }

Write-Host "`n=== PASS 4: Roles literals ==="
foreach ($f in $backendFiles) { Apply-Pairs -path (Join-Path $root $f) -pairs $pass4 }

Write-Host "`n=== DONE ==="
