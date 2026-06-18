# Smoke test intégration Directory (nécessite stack Docker démarrée)
param(
    [string]$GatewayUrl = "http://localhost:8500",
    [string]$AdminToken = ""
)

$headers = @{}
if ($AdminToken) { $headers["Authorization"] = "Bearer $AdminToken" }

Write-Host "=== Integration smoke: Directory master ===" -ForegroundColor Cyan

$health = Invoke-RestMethod -Uri "$GatewayUrl/api/directory/health" -Method Get
if ($health.status -ne "healthy") { throw "Directory unhealthy" }
Write-Host "[OK] Directory health" -ForegroundColor Green

$overview = Invoke-RestMethod -Uri "$GatewayUrl/api/directory/org/overview" -Method Get -Headers $headers
Write-Host "[OK] Org overview: $($overview.etages.Count) pôle(s), $($overview.employees.Count) employé(s)" -ForegroundColor Green

if ($AdminToken) {
    $verify = Invoke-RestMethod -Uri "$GatewayUrl/api/directory/reconcile/verify" -Method Get -Headers $headers
    Write-Host "[OK] Reconcile verify" -ForegroundColor Green
    $verify | ConvertTo-Json -Depth 3
}

Write-Host "Smoke test passed." -ForegroundColor Green
