# Vérification croisée unification (Planning, Prime, Employee Directory)
param(
    [string]$GatewayUrl = "http://localhost:8500",
    [string]$PlanningUrl = "",
    [string]$PrimeUrl = "",
    [string]$DirectoryUrl = ""
)

if (-not $PlanningUrl) { $PlanningUrl = $GatewayUrl }
if (-not $PrimeUrl) { $PrimeUrl = $GatewayUrl }
if (-not $DirectoryUrl) { $DirectoryUrl = $GatewayUrl }

$failed = $false
Write-Host "=== Verification unification (Gateway: $GatewayUrl) ===" -ForegroundColor Cyan

function Test-Endpoint {
    param([string]$Label, [string]$Uri, [hashtable]$Headers = @{})
    try {
        $r = Invoke-RestMethod -Uri $Uri -Method Get -Headers $Headers -ErrorAction Stop
        Write-Host "[OK] $Label" -ForegroundColor Green
        $r | ConvertTo-Json -Depth 4
        return $r
    } catch {
        Write-Host "[FAIL] $Label : $_" -ForegroundColor Red
        $script:failed = $true
        return $null
    }
}

Write-Host "`n--- Directory ---" -ForegroundColor Yellow
Test-Endpoint "Directory health" "$DirectoryUrl/api/directory/health"
# verify requires auth in production; health is enough for smoke test

Write-Host "`n--- Planning org mirror ---" -ForegroundColor Yellow
$planVerify = Test-Endpoint "Planning org verify" "$PlanningUrl/api/admin/org-reconciliation/verify"

Write-Host "`n--- Prime employees (smoke) ---" -ForegroundColor Yellow
try {
    $primeEmps = Invoke-RestMethod -Uri "$PrimeUrl/api/prime/employees" -Method Get -ErrorAction Stop
    Write-Host "[OK] Prime employees count: $($primeEmps.Count)" -ForegroundColor Green
} catch {
    Write-Host "[FAIL] Prime employees: $_" -ForegroundColor Red
    $failed = $true
}

if ($planVerify -and $planVerify.subServicesWithoutPrimeServiceId -gt 0) {
    Write-Host "`n[WARN] Planning subs without PrimeServiceId: $($planVerify.subServicesWithoutPrimeServiceId)" -ForegroundColor Yellow
    Write-Host "  Run: POST $PlanningUrl/api/admin/org-reconciliation/sync-from-prime" -ForegroundColor Yellow
}

Write-Host "`n--- Checklist architecture ---" -ForegroundColor Cyan
@(
    "Employee Directory service in docker-compose",
    "outbox_messages in Planning, Prime, Directory",
    "Org mirror PrimePoleId / PrimeCelluleId / PrimeServiceId",
    "DirectoryEmployeeChanged consumers in Conge/Formation",
    "POST /api/directory/reconcile for drift repair"
) | ForEach-Object { Write-Host "  [ ] $_" }

if ($failed) {
    Write-Host "`nVerification completed with failures." -ForegroundColor Red
    exit 1
}
Write-Host "`nVerification completed." -ForegroundColor Green
