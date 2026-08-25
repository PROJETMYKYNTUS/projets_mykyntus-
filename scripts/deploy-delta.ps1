<#
.SYNOPSIS
  Suivi des fichiers à déployer (delta) vers le serveur Kyntus.

.DESCRIPTION
  - status  : liste les fichiers en attente (pending + optionnellement git depuis le mark)
  - add     : ajoute un ou plusieurs chemins à deploy/pending-files.txt
  - export  : génère un tar Windows + commandes scp / extract serveur
  - mark    : après déploiement réussi, vide pending et met à jour last-deploy.json

.EXAMPLE
  .\scripts\deploy-delta.ps1 -Action status
  .\scripts\deploy-delta.ps1 -Action add -Path 'PlanningService\Planning.Infrastructure\Persistence\PlanningSchemaPatches.cs'
  .\scripts\deploy-delta.ps1 -Action export
  .\scripts\deploy-delta.ps1 -Action mark -Note 'fix exceptional requests + schema patch'
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('status', 'add', 'export', 'mark')]
    [string]$Action,

    [string[]]$Path = @(),

    [string]$Note = '',

    [switch]$IncludeGitDiff,

    [string]$OutDir = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$deployDir = Join-Path $root 'deploy'
$pendingFile = Join-Path $deployDir 'pending-files.txt'
$lastDeployFile = Join-Path $deployDir 'last-deploy.json'
$historyFile = Join-Path $deployDir 'history.jsonl'

if (-not (Test-Path $deployDir)) {
    New-Item -ItemType Directory -Path $deployDir | Out-Null
}

function Normalize-RelPath([string]$p) {
    $p = $p.Trim().TrimStart('.', '\', '/')
    return ($p -replace '/', '\').Trim()
}

function Read-Pending {
    $list = [System.Collections.Generic.List[string]]::new()
    if (-not (Test-Path $pendingFile)) { return ,$list }
    Get-Content $pendingFile | ForEach-Object {
        $line = $_.Trim()
        if (-not $line -or $line.StartsWith('#')) { return }
        $n = Normalize-RelPath $line
        if ($n -and -not $list.Contains($n)) { [void]$list.Add($n) }
    }
    return ,$list
}

function Write-Pending([System.Collections.Generic.List[string]]$items) {
    if ($null -eq $items) { $items = [System.Collections.Generic.List[string]]::new() }
    $header = @(
        '# Fichiers a deployer depuis le dernier mark (un chemin relatif par ligne).',
        '# Mis a jour par: scripts\deploy-delta.ps1 -Action add -Path ...',
        '# Ou manuellement / par l''agent Cursor apres chaque correctif.',
        '# Ignorer les lignes vides et celles qui commencent par #.',
        '#'
    )
    $body = @($items | Sort-Object -Unique)
    Set-Content -Path $pendingFile -Value ($header + $body) -Encoding UTF8
}

function Test-IgnoredPath([string]$rel) {
    $lower = $rel.ToLowerInvariant()
    $parts = $lower -split '[\\/]'
    $skipDirs = @('bin', 'obj', 'node_modules', '.angular', 'dist', '.git', 'TestResults', 'coverage')
    foreach ($d in $skipDirs) {
        if ($parts -contains $d) { return $true }
    }
    if ($lower -match '\.(dll|pdb|cache|user)$') { return $true }
    return $false
}

function Get-GitChangedSinceMark {
    $files = [System.Collections.Generic.List[string]]::new()
    if (-not (Test-Path $lastDeployFile)) { return ,$files }
    $mark = Get-Content -Raw $lastDeployFile | ConvertFrom-Json
    $commit = $mark.gitCommit
    if (-not $commit) { return ,$files }

    Push-Location $root
    try {
        $diff = git diff --name-only $commit -- 2>$null
        $untracked = git ls-files --others --exclude-standard 2>$null
        foreach ($f in @($diff) + @($untracked)) {
            if (-not $f) { continue }
            $n = Normalize-RelPath $f
            if (Test-IgnoredPath $n) { continue }
            if ($n -and -not $files.Contains($n)) { [void]$files.Add($n) }
        }
    } finally {
        Pop-Location
    }
    return ,$files
}

function Get-DeploySet {
    $set = Read-Pending
    if ($null -eq $set) { $set = [System.Collections.Generic.List[string]]::new() }
    if ($IncludeGitDiff) {
        foreach ($f in (Get-GitChangedSinceMark)) {
            if (-not $set.Contains($f)) { [void]$set.Add($f) }
        }
    }
    $existing = [System.Collections.Generic.List[string]]::new()
    foreach ($f in $set) {
        $full = Join-Path $root $f
        if (Test-Path -LiteralPath $full -PathType Leaf) {
            [void]$existing.Add($f)
        } elseif (Test-Path -LiteralPath $full -PathType Container) {
            Get-ChildItem -LiteralPath $full -Recurse -File | ForEach-Object {
                $rel = Normalize-RelPath $_.FullName.Substring($root.Length)
                if (-not (Test-IgnoredPath $rel) -and -not $existing.Contains($rel)) {
                    [void]$existing.Add($rel)
                }
            }
        } else {
            Write-Warning "Introuvable (ignore): $f"
        }
    }
    return ,$existing
}

function Read-LastDeploy {
    if (-not (Test-Path $lastDeployFile)) {
        return [pscustomobject]@{
            markedAt = $null
            gitCommit = $null
            serverHost = 'o@10.10.10.25'
            serverPath = '/home/o/projets_mykyntus-'
        }
    }
    return Get-Content -Raw $lastDeployFile | ConvertFrom-Json
}

switch ($Action) {
    'status' {
        $mark = Read-LastDeploy
        Write-Host "Dernier mark: $($mark.markedAt)  commit=$($mark.gitCommit)"
        Write-Host "Serveur: $($mark.serverHost):$($mark.serverPath)"
        Write-Host ''
        $pending = Read-Pending
        Write-Host "Pending ($($pending.Count)):"
        $pending | ForEach-Object { Write-Host "  $_" }
        if ($IncludeGitDiff) {
            Write-Host ''
            $git = Get-GitChangedSinceMark
            Write-Host "Git depuis mark ($($git.Count), hors bin/obj):"
            $git | Select-Object -First 80 | ForEach-Object { Write-Host "  $_" }
            if ($git.Count -gt 80) { Write-Host "  ... +$($git.Count - 80) autres" }
        }
    }

    'add' {
        if (-not $Path -or $Path.Count -eq 0) {
            throw 'Utiliser -Path avec un ou plusieurs chemins relatifs au repo.'
        }
        $list = Read-Pending
        $expanded = [System.Collections.Generic.List[string]]::new()
        foreach ($raw in $Path) {
            foreach ($piece in ($raw -split ',')) {
                $piece = $piece.Trim().Trim("'").Trim('"')
                if ($piece) { [void]$expanded.Add($piece) }
            }
        }
        foreach ($p in $expanded) {
            $n = Normalize-RelPath $p
            $full = Join-Path $root $n
            if (-not (Test-Path -LiteralPath $full)) {
                Write-Warning "Chemin introuvable: $n"
                continue
            }
            if (-not $list.Contains($n)) {
                [void]$list.Add($n)
                Write-Host "Ajoute: $n"
            } else {
                Write-Host "Deja liste: $n"
            }
        }
        Write-Pending $list
    }

    'export' {
        $mark = Read-LastDeploy
        $files = Get-DeploySet
        if ($files.Count -eq 0) {
            Write-Host 'Aucun fichier à exporter. Ajoutez des chemins (add) ou -IncludeGitDiff.'
            return
        }

        if (-not $OutDir) {
            $OutDir = Join-Path $deployDir 'out'
        }
        if (-not (Test-Path $OutDir)) {
            New-Item -ItemType Directory -Path $OutDir | Out-Null
        }

        $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $tarName = "kyntus-delta-$stamp.tar"
        $tarPath = Join-Path $OutDir $tarName
        $listPath = Join-Path $OutDir "kyntus-delta-$stamp.files.txt"

        $unixPaths = @($files | ForEach-Object { ($_ -replace '\\', '/') })
        $unixPaths | Set-Content -Path $listPath -Encoding ascii

        Push-Location $root
        try {
            Remove-Item $tarPath -ErrorAction SilentlyContinue
            & tar -acf $tarPath --files-from=$listPath
            if ($LASTEXITCODE -ne 0 -or -not (Test-Path $tarPath)) {
                Remove-Item $tarPath -ErrorAction SilentlyContinue
                & tar -acf $tarPath @unixPaths
            }
        } finally {
            Pop-Location
        }

        if (-not (Test-Path $tarPath)) {
            throw "Échec création archive: $tarPath"
        }

        $hostUser = $mark.serverHost
        if (-not $hostUser) { $hostUser = 'o@10.10.10.25' }
        $remoteRoot = $mark.serverPath
        if (-not $remoteRoot) { $remoteRoot = '/home/o/projets_mykyntus-' }

        $remoteTar = "/tmp/$tarName"
        $sizeMb = [math]::Round((Get-Item $tarPath).Length / 1MB, 2)

        Write-Host ''
        Write-Host ("Archive: {0} ({1} Mo) - {2} fichier(s)" -f $tarPath, $sizeMb, $files.Count)
        Write-Host ("Liste:   {0}" -f $listPath)
        Write-Host ''
        Write-Host '--- Commandes deploiement ---'
        Write-Host ('scp "{0}" {1}:{2}' -f $tarPath, $hostUser, $remoteTar)
        $extractCmd = 'cd {0}; tar -xf {1}; rm -f {1}' -f $remoteRoot, $remoteTar
        Write-Host ('ssh {0} "{1}"' -f $hostUser, $extractCmd)
        Write-Host ''
        Write-Host 'Puis rebuild Docker des services touches, ex.:'
        $rebuildCmd = 'cd {0}; docker compose up -d --build planning-backend' -f $remoteRoot
        Write-Host ('ssh {0} "{1}"' -f $hostUser, $rebuildCmd)
        Write-Host ''
        Write-Host 'Apres succes: .\scripts\deploy-delta.ps1 -Action mark -Note ''...'''
    }

    'mark' {
        Push-Location $root
        try {
            $commit = (git rev-parse HEAD).Trim()
        } finally {
            Pop-Location
        }
        $mark = Read-LastDeploy
        $pending = Read-Pending
        $now = (Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz')
        $newMark = [ordered]@{
            markedAt            = $now
            note                = if ($Note) { $Note } else { 'Deploiement delta applique' }
            gitCommit           = $commit
            serverHost          = if ($mark.serverHost) { $mark.serverHost } else { 'o@10.10.10.25' }
            serverPath          = if ($mark.serverPath) { $mark.serverPath } else { '/home/o/projets_mykyntus-' }
            lastServicesRebuilt = @()
            fileCount           = $pending.Count
        }
        ($newMark | ConvertTo-Json -Depth 5) | Set-Content -Path $lastDeployFile -Encoding UTF8

        $hist = [ordered]@{
            at        = $now
            event     = 'deployed'
            gitCommit = $commit
            note      = $newMark.note
            files     = @($pending)
        }
        Add-Content -Path $historyFile -Value (($hist | ConvertTo-Json -Compress -Depth 5)) -Encoding UTF8

        Write-Pending ([System.Collections.Generic.List[string]]::new())
        Write-Host ("Mark enregistre ({0}). Pending vide. Historique: deploy/history.jsonl" -f $now)
    }
}
