# Live Docker/HTTP audit for Kyntus stack — writes _audit_*.txt in workspace root
$ErrorActionPreference = 'Continue'
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $Root

$psOut = Join-Path $Root '_audit_docker_ps.txt'
$healthOut = Join-Path $Root '_audit_health.txt'
$logsOut = Join-Path $Root '_audit_doc_logs.txt'
$apiOut = Join-Path $Root '_audit_doc_api.txt'
$summaryOut = Join-Path $Root '_audit_summary.txt'

function Probe-Url {
  param([string]$Name, [string]$Url, [switch]$FullBody)
  try {
    $r = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop
    $code = [int]$r.StatusCode
    $body = if ($null -eq $r.Content) { '' } else { [string]$r.Content }
    if ($FullBody) {
      return "${Name}: HTTP $code`n$body"
    }
    $snip = if ($body.Length -gt 120) { $body.Substring(0, 120) + '...' } else { $body }
    $snip = ($snip -replace "`r|`n", ' ')
    return "${Name}: HTTP $code | $snip"
  } catch {
    $resp = $_.Exception.Response
    if ($resp) {
      $code = [int]$resp.StatusCode
      $stream = $resp.GetResponseStream()
      $reader = New-Object System.IO.StreamReader($stream)
      $body = $reader.ReadToEnd()
      $reader.Close()
      if ($FullBody) {
        return "${Name}: HTTP $code`n$body"
      }
      $snip = if ($body.Length -gt 120) { $body.Substring(0, 120) + '...' } else { $body }
      $snip = ($snip -replace "`r|`n", ' ')
      return "${Name}: HTTP $code | $snip"
    }
    return "${Name}: ERROR | $($_.Exception.Message)"
  }
}

# 1) docker ps
try {
  docker ps --format "table {{.ID}}\t{{.Names}}\t{{.Status}}\t{{.Ports}}" 2>&1 | Out-File -FilePath $psOut -Encoding utf8
} catch {
  "docker ps FAILED: $($_.Exception.Message)" | Out-File -FilePath $psOut -Encoding utf8
}

# 2) health endpoints
$healthLines = @()
$ports = @(
  @{ Name = 'gateway:8500'; Url = 'http://localhost:8500/health' },
  @{ Name = 'auth:8520'; Url = 'http://localhost:8520/health' },
  @{ Name = 'planning:8521'; Url = 'http://localhost:8521/health' },
  @{ Name = 'documentation:8530'; Url = 'http://localhost:8530/health' },
  @{ Name = 'conge:8540'; Url = 'http://localhost:8540/health' },
  @{ Name = 'prime:8550'; Url = 'http://localhost:8550/health' },
  @{ Name = 'parrainage:8560'; Url = 'http://localhost:8560/health' },
  @{ Name = 'directory:8565'; Url = 'http://localhost:8565/health' },
  @{ Name = 'spa:8200'; Url = 'http://localhost:8200/' },
  @{ Name = 'login:8201'; Url = 'http://localhost:8201/' }
)
foreach ($p in $ports) {
  $healthLines += (Probe-Url -Name $p.Name -Url $p.Url)
  # fallback root if /health fails hard
  if ($healthLines[-1] -match 'ERROR' -and $p.Url -like '*/health') {
    $rootUrl = $p.Url -replace '/health$', '/'
    $healthLines += (Probe-Url -Name "$($p.Name)/" -Url $rootUrl)
  }
}
$healthLines -join "`r`n" | Out-File -FilePath $healthOut -Encoding utf8

# 3) documentation db status (full JSON)
$dbStatus = Probe-Url -Name 'doc-db-status' -Url 'http://localhost:8530/api/documentation/db/status' -FullBody
$dbStatus | Out-File -FilePath (Join-Path $Root '_audit_doc_db_status.txt') -Encoding utf8
Add-Content -Path $healthOut -Value "`r`n---`r`n$dbStatus" -Encoding utf8

# 4) documentation backend logs
try {
  docker logs kyntus_documentation_backend --tail 200 2>&1 | Out-File -FilePath $logsOut -Encoding utf8
} catch {
  "docker logs FAILED: $($_.Exception.Message)" | Out-File -FilePath $logsOut -Encoding utf8
}

$logsText = Get-Content -Raw -Path $logsOut -ErrorAction SilentlyContinue
$has42501 = $false
if ($logsText -and ($logsText -match '42501')) { $has42501 = $true }

# 5) gateway documentation data endpoints
$apiLines = @()
$apiUrls = @(
  @{ Name = 'gw-document-requests'; Url = 'http://localhost:8500/api/documentation/data/document-requests' },
  @{ Name = 'gw-users-me'; Url = 'http://localhost:8500/api/documentation/data/users/me' },
  @{ Name = 'direct-document-requests'; Url = 'http://localhost:8530/api/documentation/data/document-requests' },
  @{ Name = 'direct-users-me'; Url = 'http://localhost:8530/api/documentation/data/users/me' },
  @{ Name = 'gw-db-status'; Url = 'http://localhost:8500/api/documentation/db/status' }
)
foreach ($a in $apiUrls) {
  $apiLines += (Probe-Url -Name $a.Name -Url $a.Url -FullBody)
  $apiLines += '---'
}
$apiLines -join "`r`n" | Out-File -FilePath $apiOut -Encoding utf8

# 6) repair if 42501
$repairApplied = $false
$repairOutput = ''
if ($has42501) {
  $sqlPath = Join-Path $Root 'init\sql\repair_documentation_schema_permissions.sql'
  try {
    $repairOutput = Get-Content -Raw -Path $sqlPath | docker compose exec -T postgres psql -U postgres -d documentation_db 2>&1 | Out-String
    $repairApplied = $true
    # re-probe after repair
    Start-Sleep -Seconds 2
    $apiLines += 'AFTER REPAIR:'
    $apiLines += (Probe-Url -Name 'post-repair-db-status' -Url 'http://localhost:8530/api/documentation/db/status' -FullBody)
    $apiLines += (Probe-Url -Name 'post-repair-document-requests' -Url 'http://localhost:8530/api/documentation/data/document-requests' -FullBody)
    $apiLines -join "`r`n" | Out-File -FilePath $apiOut -Encoding utf8
  } catch {
    $repairOutput = "REPAIR FAILED: $($_.Exception.Message)"
  }
}

# 7) summary
$psText = Get-Content -Raw -Path $psOut -ErrorAction SilentlyContinue
$containerCount = 0
if ($psText) {
  $containerCount = ([regex]::Matches($psText, 'kyntus_')).Count
}

$summary = @"
Kyntus Docker/HTTP live audit
Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Workspace: $Root

== Docker ==
Containers with kyntus_ prefix visible: $containerCount
See: _audit_docker_ps.txt

== Health (8500,8520,8521,8530,8540,8550,8560,8565,8200,8201) ==
$((Get-Content $healthOut | Select-Object -First 20) -join "`n")

== Documentation DB status ==
$($dbStatus.Substring(0, [Math]::Min(800, $dbStatus.Length)))

== Logs: 42501 permission error ==
Detected SqlState 42501 in kyntus_documentation_backend logs: $has42501
Repair SQL applied: $repairApplied
$(if ($repairOutput) { "Repair output:`n$repairOutput" } else { '' })

== Documentation API probes ==
$((Get-Content $apiOut | Select-Object -First 40) -join "`n")

== Findings ==
- docker ps captured to _audit_docker_ps.txt
- Health probes in _audit_health.txt
- Doc backend logs (tail 200) in _audit_doc_logs.txt
- Gateway/direct doc API responses in _audit_doc_api.txt
- 42501 present: $has42501; repair applied: $repairApplied
"@

$summary | Out-File -FilePath $summaryOut -Encoding utf8
Write-Host "AUDIT_DONE"
Write-Host $summary
