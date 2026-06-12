# Verification E2E interface Organisation RH (via gateway SPA :8200)
param(
    [string]$BaseUrl = "http://localhost:8200",
    [string]$Email = "rh@kyntus.ma",
    [string]$Password = "RH@2026"
)

$ErrorActionPreference = "Stop"

function Test-Endpoint {
    param(
        [string]$Method,
        [string]$Path,
        [hashtable]$Headers,
        [object]$Body = $null
    )
    $uri = "$BaseUrl$Path"
    try {
        $params = @{
            Uri             = $uri
            Method          = $Method
            Headers         = $Headers
            UseBasicParsing = $true
        }
        if ($null -ne $Body) {
            $params.Body = ($Body | ConvertTo-Json -Compress)
            $params.ContentType = "application/json"
        }
        $resp = Invoke-WebRequest @params
        $code = [int]$resp.StatusCode
        $len = if ($resp.Content) { $resp.Content.Length } else { 0 }
        return [pscustomobject]@{ Method = $Method; Path = $Path; Status = $code; Bytes = $len; Ok = ($code -ge 200 -and $code -lt 300) }
    }
    catch {
        $code = 0
        if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
            $code = [int]$_.Exception.Response.StatusCode
        }
        return [pscustomobject]@{ Method = $Method; Path = $Path; Status = $code; Bytes = 0; Ok = ($code -ge 200 -and $code -lt 300); Error = $_.Exception.Message }
    }
}

Write-Host "=== Verification Organisation RH ===" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl"

# Login (token kept in memory only)
$loginBody = @{ email = $Email; password = $Password }
try {
    $login = Invoke-RestMethod -Uri "$BaseUrl/api/auth/login" -Method Post -Body ($loginBody | ConvertTo-Json) -ContentType "application/json"
}
catch {
    Write-Host "ECHEC login: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) { Write-Host $_.ErrorDetails.Message -ForegroundColor Red }
    exit 1
}

$token = $login.accessToken
if (-not $token) { $token = $login.AccessToken }
if (-not $token) { $token = $login.token }
if (-not $token) {
    Write-Host "ECHEC login: pas de token dans la reponse" -ForegroundColor Red
    exit 1
}

Write-Host "Login OK (token recu, non affiche)" -ForegroundColor Green

$headers = @{
    Authorization = "Bearer $token"
    "X-Prime-Role" = "RH"
}

$getPaths = @(
    "/api/prime/org/etages",
    "/api/prime/org/services",
    "/api/prime/org/sous-services",
    "/api/prime/employees",
    "/api/prime/departments",
    "/api/prime/org/assignments/manager-etage",
    "/api/prime/org/assignments/supervisor-service",
    "/api/prime/org/assignments/coach-sous-service",
    "/api/prime/org/assignments/coach-pilot"
)

Write-Host "`n--- GET loadOverview() ---" -ForegroundColor Yellow
$results = @()
foreach ($p in $getPaths) {
    $r = Test-Endpoint -Method GET -Path $p -Headers $headers
    $results += $r
    $color = if ($r.Ok) { "Green" } else { "Red" }
    Write-Host ("  {0,-6} {1,-55} -> {2}" -f $r.Method, $r.Path, $r.Status) -ForegroundColor $color
}

$failGet = $results | Where-Object { -not $_.Ok }
if ($failGet) {
    Write-Host "`nGET en echec - arret avant mutations." -ForegroundColor Red
    exit 2
}

# Parse departments for mutation smoke test
$depts = Invoke-RestMethod -Uri "$BaseUrl/api/prime/departments" -Headers $headers
$deptId = $null
if ($depts -is [array] -and $depts.Count -gt 0) {
    $deptId = $depts[0].id
}
elseif ($depts.id) {
    $deptId = $depts.id
}

Write-Host "`n--- POST mutations (smoke) ---" -ForegroundColor Yellow
$testName = "Test-Org-RH-$(Get-Date -Format 'yyyyMMddHHmmss')"
$postTests = @()

if ($deptId) {
    $r = Test-Endpoint -Method POST -Path "/api/prime/org/structure/departments/$deptId/poles" -Headers $headers -Body @{ name = $testName }
    $postTests += $r
    Write-Host ("  {0,-6} {1,-55} -> {2}" -f $r.Method, $r.Path, $r.Status) -ForegroundColor $(if ($r.Ok) { "Green" } else { "Red" })

    if ($r.Ok -and $r.Bytes -gt 2) {
        try {
            $poleResp = Invoke-RestMethod -Uri "$BaseUrl/api/prime/org/structure/departments/$deptId/poles" -Method Post -Headers $headers -Body (@{ name = "$testName-2" } | ConvertTo-Json) -ContentType "application/json"
            $poleId = $poleResp.id
            if ($poleId) {
                $r2 = Test-Endpoint -Method POST -Path "/api/prime/org/structure/poles/$poleId/cellules" -Headers $headers -Body @{ name = "$testName-cell" }
                $postTests += $r2
                Write-Host ("  {0,-6} {1,-55} -> {2}" -f $r2.Method, $r2.Path, $r2.Status) -ForegroundColor $(if ($r2.Ok) { "Green" } else { "Red" })
            }
        }
        catch { Write-Host "  POST cellule: $($_.Exception.Message)" -ForegroundColor Red }
    }
}
else {
    $r = Test-Endpoint -Method POST -Path "/api/prime/org/structure/departments" -Headers $headers -Body @{ name = $testName }
    $postTests += $r
    Write-Host ("  {0,-6} {1,-55} -> {2}" -f $r.Method, $r.Path, $r.Status) -ForegroundColor $(if ($r.Ok) { "Green" } else { "Red" })
}

Write-Host "`n--- Resume ---" -ForegroundColor Cyan
$allOk = ($results + $postTests) | Where-Object { -not $_.Ok }
if ($allOk) {
    Write-Host "ECHECS:" -ForegroundColor Red
    $allOk | ForEach-Object { Write-Host "  $($_.Method) $($_.Path) -> $($_.Status)" -ForegroundColor Red }
    exit 3
}

Write-Host "Tous les endpoints testes OK." -ForegroundColor Green
exit 0
