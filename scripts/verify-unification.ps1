# Verification croisée unification hiérarchie Prime v2
param(
    [string]$PlanningUrl = "http://localhost:8081",
    [string]$PrimeUrl = "http://localhost:8083"
)

Write-Host "=== Verification unification hiérarchie ===" -ForegroundColor Cyan

try {
    $verify = Invoke-RestMethod -Uri "$PlanningUrl/api/admin/org-reconciliation/verify" -Method Get
    Write-Host "Planning mirror IDs:" -ForegroundColor Yellow
    $verify | ConvertTo-Json
} catch {
    Write-Host "Planning verify endpoint unavailable: $_" -ForegroundColor Red
}

Write-Host "`nChecklist ecarts critiques:" -ForegroundColor Cyan
$checks = @(
    "3 Parrainage superviseur->MANAGER (kyntus-role-ui.config.ts)",
    "4 Conge Role in EmployeCreatedMessage",
    "5 UserService SubService Guid encoding",
    "6 Messaging.Contracts project",
    "8 PrimePoleId/PrimeCelluleId/PrimeServiceId columns",
    "1 Prime MassTransit RabbitMQ",
    "2 Org events after SaveChanges in PrimeControllers"
)
$checks | ForEach-Object { Write-Host "  [ ] $_" }

Write-Host "`nDone. Run dotnet build on PlanningService, Conge.API, PrimeBackend after freeing disk space." -ForegroundColor Green
