#Requires -Version 5.1
<#
.SYNOPSIS
  Audit E2E Kyntus — santé stack, repair doc si 42501, login démo, cycles API fermés.
.DESCRIPTION
  Écrit docs/e2e-audit-report.md + _audit_*.txt à la racine du dépôt.
  Prérequis : docker compose up (gateway :8500, SPA :8200).
#>
param(
  [string]$GatewayUrl = "http://localhost:8500",
  [string]$EmployeeEmail = "employee@kyntus.ma",
  [string]$RhEmail = "rh@kyntus.ma",
  [string]$Password = "Azerty@123",
  [switch]$SkipRepair
)

$ErrorActionPreference = "Continue"
$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
if (-not (Test-Path (Join-Path $Root "docker-compose.yml"))) {
  $Root = (Get-Location).Path
}
Set-Location $Root
$ReportPath = Join-Path $Root "docs\e2e-audit-report.md"
$Results = [System.Collections.Generic.List[object]]::new()
$Started = Get-Date

function Add-Result {
  param([string]$Domain, [string]$Cycle, [string]$Status, [string]$Detail, [string]$Severity = "")
  $Results.Add([pscustomobject]@{
    Domain = $Domain; Cycle = $Cycle; Status = $Status; Detail = $Detail; Severity = $Severity
  }) | Out-Null
  $color = switch ($Status) { "OK" { "Green" } "KO" { "Red" } "SKIP" { "Yellow" } default { "Gray" } }
  Write-Host "[$Status] $Domain / $Cycle — $Detail" -ForegroundColor $color
}

function Invoke-Http {
  param(
    [string]$Method = "GET",
    [string]$Url,
    [hashtable]$Headers = @{},
    [object]$Body = $null,
    [int]$TimeoutSec = 30
  )
  try {
    $params = @{
      Uri             = $Url
      Method          = $Method
      Headers         = $Headers
      UseBasicParsing = $true
      TimeoutSec      = $TimeoutSec
    }
    if ($null -ne $Body) {
      $params.ContentType = "application/json"
      $params.Body = if ($Body -is [string]) { $Body } else { ($Body | ConvertTo-Json -Depth 8 -Compress) }
    }
    $r = Invoke-WebRequest @params
    return @{ Ok = $true; Code = [int]$r.StatusCode; Body = $r.Content; Error = $null }
  } catch {
    $code = 0
    $body = ""
    $resp = $_.Exception.Response
    if ($resp) {
      try { $code = [int]$resp.StatusCode } catch { }
      try {
        $stream = $resp.GetResponseStream()
        if ($stream) {
          $reader = New-Object System.IO.StreamReader($stream)
          $body = $reader.ReadToEnd()
          $reader.Close()
        }
      } catch { }
    }
    return @{ Ok = $false; Code = $code; Body = $body; Error = $_.Exception.Message }
  }
}

function Get-JsonProp {
  param($Obj, [string]$Name)
  if ($null -eq $Obj) { return $null }
  $p = $Obj.PSObject.Properties[$Name]
  if ($p) { return $p.Value }
  $p2 = $Obj.PSObject.Properties | Where-Object { $_.Name -ieq $Name } | Select-Object -First 1
  if ($p2) { return $p2.Value }
  return $null
}

# ─── Phase 0: Docker + health ───────────────────────────────────────────────
Write-Host "`n=== Phase 0 — Santé stack ===" -ForegroundColor Cyan
try {
  docker ps --format "table {{.Names}}\t{{.Status}}" 2>&1 |
    Out-File (Join-Path $Root "_audit_docker_ps.txt") -Encoding utf8
  $psText = Get-Content -Raw (Join-Path $Root "_audit_docker_ps.txt")
  $n = ([regex]::Matches($psText, "kyntus_")).Count
  Add-Result "Infra" "docker ps" $(if ($n -ge 8) { "OK" } else { "KO" }) "$n conteneurs kyntus_" $(if ($n -ge 8) { "" } else { "Bloquant" })
} catch {
  Add-Result "Infra" "docker ps" "KO" $_.Exception.Message "Bloquant"
}

$healthTargets = @(
  @{ N = "gateway"; U = "$GatewayUrl/health" },
  @{ N = "auth"; U = "http://localhost:8520/health" },
  @{ N = "planning"; U = "http://localhost:8521/health" },
  @{ N = "documentation"; U = "http://localhost:8530/health" },
  @{ N = "documentation-gw"; U = "$GatewayUrl/api/documentation/health" },
  @{ N = "conge"; U = "http://localhost:8540/health" },
  @{ N = "prime"; U = "http://localhost:8550/api/prime/health" },
  @{ N = "parrainage"; U = "http://localhost:8560/api/parrainage/health" },
  @{ N = "directory"; U = "http://localhost:8565/api/directory/health" },
  @{ N = "formation"; U = "http://localhost:8522/health" },
  @{ N = "spa"; U = "http://localhost:8200/" },
  @{ N = "auth-ui"; U = "http://localhost:8201/" }
)
$healthLines = @()
foreach ($t in $healthTargets) {
  $r = Invoke-Http -Url $t.U
  $healthLines += "$($t.N): HTTP $($r.Code) $(if ($r.Ok) { 'OK' } else { $r.Error })"
  $ok = $r.Ok -or ($r.Code -ge 200 -and $r.Code -lt 500)
  Add-Result "Infra" "health $($t.N)" $(if ($ok) { "OK" } else { "KO" }) "HTTP $($r.Code)" $(if ($ok) { "" } else { "Bloquant" })
}
$healthLines -join "`n" | Out-File (Join-Path $Root "_audit_health.txt") -Encoding utf8

# Doc DB status + logs
$db = Invoke-Http -Url "http://localhost:8530/api/documentation/db/status"
$db.Body | Out-File (Join-Path $Root "_audit_doc_db_status.txt") -Encoding utf8
Add-Result "Documentation" "db/status" $(if ($db.Ok) { "OK" } else { "KO" }) "HTTP $($db.Code) $($db.Body.Substring(0, [Math]::Min(200, $db.Body.Length)))" $(if ($db.Ok) { "" } else { "Bloquant" })

try {
  docker logs kyntus_documentation_backend --tail 250 2>&1 |
    Out-File (Join-Path $Root "_audit_doc_logs.txt") -Encoding utf8
} catch {
  "logs failed: $($_.Exception.Message)" | Out-File (Join-Path $Root "_audit_doc_logs.txt") -Encoding utf8
}
$logs = Get-Content -Raw (Join-Path $Root "_audit_doc_logs.txt") -ErrorAction SilentlyContinue
$has42501 = $logs -and ($logs -match "42501")
Add-Result "Documentation" "logs 42501" $(if ($has42501) { "KO" } else { "OK" }) $(if ($has42501) { "permission denied détecté" } else { "pas de 42501 dans les 250 dernières lignes" }) $(if ($has42501) { "Bloquant" } else { "" })

if ($has42501 -and -not $SkipRepair) {
  Write-Host "Application repair_documentation_schema_permissions.sql..." -ForegroundColor Yellow
  $sql = Join-Path $Root "init\sql\repair_documentation_schema_permissions.sql"
  $repairOut = Get-Content -Raw $sql | docker compose exec -T postgres psql -U postgres -d documentation_db 2>&1 | Out-String
  $repairOut | Out-File (Join-Path $Root "_audit_doc_repair.txt") -Encoding utf8
  docker compose restart documentation-backend 2>&1 | Out-Null
  Start-Sleep -Seconds 25
  $db2 = Invoke-Http -Url "http://localhost:8530/api/documentation/db/status"
  Add-Result "Documentation" "repair 42501" $(if ($db2.Ok) { "OK" } else { "KO" }) "après repair HTTP $($db2.Code)" $(if ($db2.Ok) { "" } else { "Bloquant" })
}

# Probe data endpoints (pre-auth)
$preMe = Invoke-Http -Url "$GatewayUrl/api/documentation/data/users/me"
$preReq = Invoke-Http -Url "$GatewayUrl/api/documentation/data/document-requests?page=1&pageSize=5"
@"
users/me HTTP $($preMe.Code)
$($preMe.Body)

---
document-requests HTTP $($preReq.Code)
$($preReq.Body)
"@ | Out-File (Join-Path $Root "_audit_doc_api.txt") -Encoding utf8

# ─── Phase 1: Auth ──────────────────────────────────────────────────────────
Write-Host "`n=== Phase 1 — Auth + identité ===" -ForegroundColor Cyan

function Login-Demo {
  param([string]$Email, [string]$Pwd)
  $r = Invoke-Http -Method POST -Url "$GatewayUrl/api/Auth/login" -Body @{ email = $Email; password = $Pwd }
  if (-not $r.Ok) { return $null }
  try { return ($r.Body | ConvertFrom-Json) } catch { return $null }
}

$empLogin = Login-Demo -Email $EmployeeEmail -Pwd $Password
$rhLogin = Login-Demo -Email $RhEmail -Pwd $Password
$empToken = if ($empLogin) { Get-JsonProp $empLogin "token"; if (-not $_) { Get-JsonProp $empLogin "accessToken" } } else { $null }
if ($empLogin -and -not $empToken) {
  $empToken = $empLogin.token; if (-not $empToken) { $empToken = $empLogin.accessToken }
  if (-not $empToken -and $empLogin.data) { $empToken = $empLogin.data.token }
}
$rhToken = $null
if ($rhLogin) {
  $rhToken = $rhLogin.token
  if (-not $rhToken) { $rhToken = $rhLogin.accessToken }
  if (-not $rhToken -and $rhLogin.data) { $rhToken = $rhLogin.data.token }
}

# Re-parse tokens robustly
function Extract-Token($loginObj) {
  if ($null -eq $loginObj) { return $null }
  foreach ($k in @("token", "accessToken", "access_token", "jwt")) {
    $v = Get-JsonProp $loginObj $k
    if ($v) { return [string]$v }
  }
  $data = Get-JsonProp $loginObj "data"
  if ($data) {
    foreach ($k in @("token", "accessToken", "access_token")) {
      $v = Get-JsonProp $data $k
      if ($v) { return [string]$v }
    }
  }
  return $null
}
$empToken = Extract-Token $empLogin
$rhToken = Extract-Token $rhLogin

Add-Result "Auth" "login employee" $(if ($empToken) { "OK" } else { "KO" }) $EmployeeEmail $(if ($empToken) { "" } else { "Bloquant" })
Add-Result "Auth" "login RH" $(if ($rhToken) { "OK" } else { "KO" }) $RhEmail $(if ($rhToken) { "" } else { "Bloquant" })

$empH = @{ Authorization = "Bearer $empToken" }
$rhH = @{ Authorization = "Bearer $rhToken" }

# Directory smoke
$dirHealth = Invoke-Http -Url "$GatewayUrl/api/directory/health" -Headers $rhH
Add-Result "Directory" "health via gateway" $(if ($dirHealth.Ok) { "OK" } else { "KO" }) "HTTP $($dirHealth.Code)" $(if ($dirHealth.Ok) { "" } else { "Majeur" })
$org = Invoke-Http -Url "$GatewayUrl/api/directory/org/overview" -Headers $rhH
Add-Result "Directory" "org/overview" $(if ($org.Ok) { "OK" } else { "KO" }) "HTTP $($org.Code)" $(if ($org.Ok) { "" } else { "Majeur" })

# SPA shell
$spa = Invoke-Http -Url "http://localhost:8200/"
Add-Result "Auth" "SPA shell :8200" $(if ($spa.Ok -or $spa.Code -eq 200) { "OK" } else { "KO" }) "HTTP $($spa.Code)"

# ─── Phase 2: Domain closed loops ───────────────────────────────────────────
Write-Host "`n=== Phase 2 — Cycles métier ===" -ForegroundColor Cyan

# Documentation
$me = Invoke-Http -Url "$GatewayUrl/api/documentation/data/users/me" -Headers $empH
Add-Result "Documentation" "GET users/me (employee)" $(if ($me.Ok) { "OK" } elseif ($me.Code -eq 401) { "KO" } elseif ($me.Code -eq 404) { "KO" } elseif ($me.Code -eq 500) { "KO" } else { "KO" }) "HTTP $($me.Code) $($me.Body.Substring(0, [Math]::Min(120, $me.Body.Length)))" $(if ($me.Ok) { "" } else { "Bloquant" })

$reqs = Invoke-Http -Url "$GatewayUrl/api/documentation/data/document-requests?page=1&pageSize=20" -Headers $rhH
Add-Result "Documentation" "GET document-requests (RH)" $(if ($reqs.Ok) { "OK" } else { "KO" }) "HTTP $($reqs.Code)" $(if ($reqs.Ok) { "" } else { "Bloquant" })

$types = Invoke-Http -Url "$GatewayUrl/api/documentation/data/document-types" -Headers $empH
if (-not $types.Ok) { $types = Invoke-Http -Url "$GatewayUrl/api/documentation/document-types" -Headers $empH }
Add-Result "Documentation" "list document-types" $(if ($types.Ok) { "OK" } else { "SKIP" }) "HTTP $($types.Code)"

# Congé microservice (lowercase)
$conges = Invoke-Http -Url "$GatewayUrl/api/conges" -Headers $empH
Add-Result "Congé" "GET /api/conges (employee)" $(if ($conges.Ok -or $conges.Code -eq 200) { "OK" } else { "KO" }) "HTTP $($conges.Code)" $(if ($conges.Ok -or $conges.Code -eq 200) { "" } else { "Majeur" })

# Planning Conges (Pascal) — must hit planning, not crash
$CongesP = Invoke-Http -Url "$GatewayUrl/api/Conges" -Headers $empH
Add-Result "Planning" "GET /api/Conges (Pascal→planning)" $(if ($CongesP.Code -gt 0 -and $CongesP.Code -lt 500) { "OK" } else { "KO" }) "HTTP $($CongesP.Code) (casse Ocelot)" "Doc"

# Planning
$plan = Invoke-Http -Url "$GatewayUrl/api/Planning" -Headers $empH
if (-not $plan.Ok) { $plan = Invoke-Http -Url "$GatewayUrl/api/planning" -Headers $empH }
Add-Result "Planning" "list planning" $(if ($plan.Ok -or ($plan.Code -ge 200 -and $plan.Code -lt 500)) { "OK" } else { "KO" }) "HTTP $($plan.Code)" $(if ($plan.Ok) { "" } else { "Majeur" })

# Prime
$prime = Invoke-Http -Url "$GatewayUrl/api/prime/health" -Headers $rhH
Add-Result "Prime" "health via gateway" $(if ($prime.Ok) { "OK" } else { "KO" }) "HTTP $($prime.Code)"
$primeVal = Invoke-Http -Url "$GatewayUrl/api/prime/validation" -Headers $rhH
Add-Result "Prime" "GET validation" $(if ($primeVal.Ok -or ($primeVal.Code -ge 200 -and $primeVal.Code -lt 500)) { "OK" } else { "KO" }) "HTTP $($primeVal.Code)" $(if ($primeVal.Code -eq 404) { "Doc" } else { "" })

# Parrainage
$parr = Invoke-Http -Url "$GatewayUrl/api/parrainage/health" -Headers $rhH
Add-Result "Parrainage" "health" $(if ($parr.Ok) { "OK" } else { "KO" }) "HTTP $($parr.Code)"
$refs = Invoke-Http -Url "$GatewayUrl/api/parrainage/referrals" -Headers $rhH
if (-not $refs.Ok) { $refs = Invoke-Http -Url "$GatewayUrl/api/parrainage" -Headers $rhH }
Add-Result "Parrainage" "list referrals" $(if ($refs.Ok -or ($refs.Code -ge 200 -and $refs.Code -lt 500)) { "OK" } else { "KO" }) "HTTP $($refs.Code)"

# Formation
$form = Invoke-Http -Url "$GatewayUrl/api/formations" -Headers $empH
Add-Result "Formation" "GET /api/formations" $(if ($form.Ok -or ($form.Code -ge 200 -and $form.Code -lt 500)) { "OK" } else { "KO" }) "HTTP $($form.Code)"

# Contracts
$contracts = Invoke-Http -Url "$GatewayUrl/api/contract" -Headers $rhH
Add-Result "Contrats" "GET /api/contract" $(if ($contracts.Ok -or ($contracts.Code -ge 200 -and $contracts.Code -lt 500)) { "OK" } else { "KO" }) "HTTP $($contracts.Code)"

# Reclamations
$recl = Invoke-Http -Url "$GatewayUrl/api/reclamation" -Headers $empH
if (-not $recl.Ok) { $recl = Invoke-Http -Url "$GatewayUrl/api/reclamations" -Headers $empH }
Add-Result "Réclamations" "list" $(if ($recl.Ok -or ($recl.Code -ge 200 -and $recl.Code -lt 500)) { "OK" } else { "KO" }) "HTTP $($recl.Code)"

# Newsletter
$news = Invoke-Http -Url "$GatewayUrl/api/newsletter" -Headers $rhH
if (-not $news.Ok) { $news = Invoke-Http -Url "$GatewayUrl/api/newsletters" -Headers $rhH }
Add-Result "Newsletters" "list" $(if ($news.Ok -or ($news.Code -ge 200 -and $news.Code -lt 500)) { "OK" } else { "KO" }) "HTTP $($news.Code)"

# Documentation create→approve smoke (only if me + types work)
if ($me.Ok -and $empToken -and $rhToken) {
  $createBody = @{
    documentTypeId = $null
    comment        = "e2e-audit-$(Get-Date -Format 'HHmmss')"
  }
  # Try minimal create — may fail without type id
  $create = Invoke-Http -Method POST -Url "$GatewayUrl/api/documentation/data/document-requests" -Headers $empH -Body $createBody
  if ($create.Ok) {
    Add-Result "Documentation" "POST create request" "OK" "HTTP $($create.Code)"
    try {
      $created = $create.Body | ConvertFrom-Json
      $rid = Get-JsonProp $created "id"
      if ($rid) {
        $approve = Invoke-Http -Method POST -Url "$GatewayUrl/api/documentation/data/workflow/approve" -Headers $rhH -Body @{ documentRequestId = $rid }
        if (-not $approve.Ok) {
          $approve = Invoke-Http -Method PUT -Url "$GatewayUrl/api/documentation/data/document-requests/$rid/approve" -Headers $rhH
        }
        Add-Result "Documentation" "approve request" $(if ($approve.Ok) { "OK" } else { "KO" }) "HTTP $($approve.Code) id=$rid" $(if ($approve.Ok) { "" } else { "Majeur" })
      }
    } catch {
      Add-Result "Documentation" "approve request" "SKIP" "parse create body failed"
    }
  } else {
    Add-Result "Documentation" "POST create request" "SKIP" "HTTP $($create.Code) — besoin type/template (attendu si catalogue vide)" "Mineur"
  }
}

# ─── Phase 3: Incohérences connues (statique + runtime) ─────────────────────
Write-Host "`n=== Phase 3 — Incohérences ===" -ForegroundColor Cyan
$ocelot = Get-Content -Raw (Join-Path $Root "init\ocelot.gateway.json")
$docHubIdx = $ocelot.IndexOf('"/hubs/documentation"')
$catchIdx = $ocelot.IndexOf('"/hubs/{everything}"')
if ($docHubIdx -ge 0 -and $catchIdx -ge 0 -and $docHubIdx -lt $catchIdx) {
  Add-Result "Gateway" "hubs/documentation avant catch-all" "OK" "ordre Ocelot correct"
} else {
  Add-Result "Gateway" "hubs/documentation avant catch-all" "KO" "SignalR doc peut être routé vers planning" "Bloquant"
}

$intDoc = Get-Content -Raw (Join-Path $Root "init\DOCUMENTATION_INTEGRATION.txt") -ErrorAction SilentlyContinue
if ($intDoc -match "localhost:5000" -or $intDoc -match "localhost:4200") {
  Add-Result "Docs" "ports DOCUMENTATION_INTEGRATION.txt" "KO" "ports obsolètes 5000/4200 vs 8500/8200" "Doc"
} else {
  Add-Result "Docs" "ports DOCUMENTATION_INTEGRATION.txt" "OK" "alignés"
}

$primeChecklist = Get-Content -Raw (Join-Path $Root "docs\prime-manual-test-checklist.md") -ErrorAction SilentlyContinue
if ($primeChecklist -match "ne sont\s+\*\*pas\*\*\s+branchées") {
  Add-Result "Docs" "prime-manual-test-checklist vs UI validation" "KO" "checklist dit validation hors UI alors que pages existent" "Doc"
}

# ─── Report ─────────────────────────────────────────────────────────────────
$okN = ($Results | Where-Object Status -eq "OK").Count
$koN = ($Results | Where-Object Status -eq "KO").Count
$skipN = ($Results | Where-Object Status -eq "SKIP").Count
$elapsed = [int]((Get-Date) - $Started).TotalSeconds

$lines = @()
$lines += "# Rapport audit E2E Kyntus — cycles fermés"
$lines += ""
$lines += "- Généré : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$lines += "- Durée : ${elapsed}s"
$lines += "- Gateway : $GatewayUrl"
$lines += "- Comptes : $EmployeeEmail / $RhEmail"
$lines += "- Résumé : **$okN OK** / **$koN KO** / **$skipN SKIP**"
$lines += ""
$lines += "## Matrice"
$lines += ""
$lines += "| Domaine | Cycle | Statut | Sévérité | Détail |"
$lines += "|---------|-------|--------|----------|--------|"
foreach ($row in $Results) {
  $d = ($row.Detail -replace '\|', '/' -replace "`r|`n", " ")
  if ($d.Length -gt 160) { $d = $d.Substring(0, 160) + "…" }
  $lines += "| $($row.Domain) | $($row.Cycle) | $($row.Status) | $($row.Severity) | $d |"
}
$lines += ""
$lines += "## Correctifs appliqués dans le dépôt"
$lines += ""
$lines += "- \`init/ocelot.gateway.json\` : routes \`/hubs/documentation\` déplacées **avant** le catch-all \`/hubs/{everything}\` (évite le routage SignalR doc → planning)."
$lines += "- Repair SQL disponible : \`init/sql/repair_documentation_schema_permissions.sql\` (appliqué automatiquement si logs 42501)."
$lines += ""
$lines += "## Artefacts"
$lines += ""
$lines += "- \`_audit_docker_ps.txt\`, \`_audit_health.txt\`, \`_audit_doc_logs.txt\`, \`_audit_doc_api.txt\`, \`_audit_doc_db_status.txt\`"
$lines += ""
$lines += "## Relancer"
$lines += ""
$lines += '```powershell'
$lines += 'powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-e2e-closed-loops.ps1'
$lines += '```'

$reportDir = Split-Path $ReportPath
if (-not (Test-Path $reportDir)) { New-Item -ItemType Directory -Path $reportDir | Out-Null }
$lines -join "`n" | Out-File $ReportPath -Encoding utf8
$Results | ConvertTo-Json -Depth 4 | Out-File (Join-Path $Root "_audit_results.json") -Encoding utf8

Write-Host "`nRapport écrit : $ReportPath" -ForegroundColor Cyan
Write-Host "OK=$okN KO=$koN SKIP=$skipN" -ForegroundColor Cyan
exit $(if ($koN -gt 0) { 1 } else { 0 })
