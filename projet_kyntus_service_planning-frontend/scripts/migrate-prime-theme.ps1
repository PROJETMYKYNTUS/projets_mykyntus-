# Prime theme migration — PowerShell equivalent of scripts/migrate-prime-theme.mjs
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot\..

$primeDir = Join-Path (Get-Location) 'src\app\features\prime'

function Get-TsFiles($dir) {
  Get-ChildItem -Path $dir -Recurse -Filter '*.ts' | ForEach-Object { $_.FullName }
}

function Migrate-TextWhite([string]$content) {
  $solidBtnRe = 'bg-(?:blue|emerald|indigo|cyan|rose|violet|red|green|slate-[67]|amber)-|ky-gradient|--ky-gradient|btn-primary|ky-btn-primary'
  $matches = [regex]::Matches($content, 'text-white')
  foreach ($m in ($matches | Sort-Object { $_.Index } -Descending)) {
    $start = [Math]::Max(0, $m.Index - 300)
    $len = [Math]::Min($content.Length - $start, 380)
    $ctx = $content.Substring($start, $len)
    if ($ctx -notmatch $solidBtnRe) {
      $content = $content.Remove($m.Index, $m.Length).Insert($m.Index, 'text-primary')
    }
  }
  return $content
}

$replacements = @(
  @('text-muted-foreground', 'text-muted'),
  @('text-gray-400', 'text-muted'),
  @('text-slate-50', 'text-primary'),
  @('placeholder:text-slate-500', 'placeholder:text-muted'),
  @('hover:text-slate-200', 'hover:text-primary'),
  @('hover:text-slate-300', 'hover:text-primary'),
  @('text-slate-700', 'text-muted'),
  @('text-slate-600', 'text-muted'),
  @('text-slate-500', 'text-muted'),
  @('text-slate-400', 'text-muted'),
  @('text-slate-300', 'text-muted'),
  @('text-slate-200', 'text-primary'),
  @('text-slate-100', 'text-primary'),
  @('border-white/20', ''),
  @('border-white/15', ''),
  @('border-white/10', ''),
  @('divide-navy-800/70', 'divide-default'),
  @('divide-navy-800/50', 'divide-default'),
  @('divide-navy-800/45', 'divide-default'),
  @('divide-navy-800/40', 'divide-default'),
  @('divide-y divide-navy-800', 'divide-y divide-default'),
  @('divide-navy-800', 'divide-default'),
  @('border-navy-800/80', 'border-default/80'),
  @('border-navy-800/70', 'border-default/70'),
  @('border-navy-800/55', 'border-default/55'),
  @('border-navy-800/50', 'border-default/50'),
  @('border-navy-800/45', 'border-default/45'),
  @('border-navy-800/40', 'border-default/40'),
  @('border-b border-navy-800', 'border-b border-default'),
  @('border-t border-navy-800', 'border-t border-default'),
  @('border-navy-800', 'border-default'),
  @('border-navy-700', 'border-default'),
  @('border-navy-600', 'border-default'),
  @('rounded border-navy-600', 'rounded border-default'),
  @('bg-navy-950/80', 'bg-input/80'),
  @('bg-navy-950/60', 'bg-input/60'),
  @('bg-navy-950/55', 'bg-input/55'),
  @('bg-navy-950/50', 'bg-input/50'),
  @('bg-navy-950/40', 'bg-input/40'),
  @('bg-navy-950/25', 'bg-input/25'),
  @('bg-navy-950', 'bg-input'),
  @('bg-navy-900/60', 'bg-card/60'),
  @('bg-navy-900/50', 'bg-card/50'),
  @('bg-navy-900/45', 'bg-card/45'),
  @('hover:bg-navy-800/60', 'hover:bg-input/60'),
  @('hover:bg-navy-800/50', 'hover:bg-input/50'),
  @('hover:bg-navy-800/45', 'hover:bg-input/45'),
  @('hover:bg-navy-800/40', 'hover:bg-input/40'),
  @('hover:bg-navy-800', 'hover:bg-input'),
  @('hover:bg-navy-700', 'hover:bg-input'),
  @('bg-navy-900', 'bg-card'),
  @('bg-navy-800', 'bg-input'),
  @('bg-slate-900', 'bg-card'),
  @('[class.bg-navy-900]', '[class.bg-card]'),
  @('[class.text-slate-300]', '[class.text-muted]'),
  @('bg-slate-500/15 text-slate-300', 'bg-slate-500/15 text-muted'),
  @('border border-default bg-card px-3 py-2', 'border border-default bg-input px-3 py-2'),
  @('border border-default bg-card pl-8', 'border border-default bg-input pl-8'),
  @('border border-default bg-card px-2', 'border border-default bg-input px-2')
)

$styleHexReplacements = @(
  @('var\(--text-muted,\s*#[0-9a-fA-F]+\)', 'var(--text-muted)'),
  @('var\(--text-primary,\s*#[0-9a-fA-F]+\)', 'var(--text-primary)'),
  @('var\(--bg-card,\s*#[0-9a-fA-F]+\)', 'var(--bg-card)'),
  @('var\(--bg-input,\s*#[0-9a-fA-F]+\)', 'var(--bg-input)'),
  @('var\(--border-default,\s*#[0-9a-fA-F]+\)', 'var(--border-color)'),
  @('border-radius:\s*0\.875rem', 'border-radius: var(--radius-card)'),
  @('border-radius:\s*0\.5rem', 'border-radius: var(--radius-md)'),
  @('border-radius:\s*9999px', 'border-radius: var(--radius-pill)'),
  @('border-radius:\s*999px', 'border-radius: var(--radius-pill)')
)

$changed = [System.Collections.Generic.List[string]]::new()
$textWhite = @{}
$remaining = 0

foreach ($file in (Get-TsFiles $primeDir)) {
  $original = [IO.File]::ReadAllText($file)
  $content = $original

  foreach ($pair in $replacements) {
    $content = $content.Replace($pair[0], $pair[1])
  }

  $content = Migrate-TextWhite $content

  foreach ($pair in $styleHexReplacements) {
    $content = [regex]::Replace($content, $pair[0], $pair[1])
  }

  $content = [regex]::Replace($content, 'class="([^"]*)"', {
    param($m)
    $cleaned = ($m.Groups[1].Value -replace '\s{2,}', ' ').Trim()
    "class=`"$cleaned`""
  })

  if ($content -ne $original) {
    [IO.File]::WriteAllText($file, $content)
    $rel = [IO.Path]::GetRelativePath($primeDir, $file) -replace '\\', '/'
    $changed.Add($rel)
  }

  $tw = ([regex]::Matches($content, 'text-white')).Count
  if ($tw -gt 0) {
    $rel = [IO.Path]::GetRelativePath($primeDir, $file) -replace '\\', '/'
    $textWhite[$rel] = $tw
  }
}

foreach ($file in (Get-TsFiles $primeDir)) {
  $content = [IO.File]::ReadAllText($file)
  $m = [regex]::Matches($content, 'text-slate-|bg-navy-|border-navy-|border-white/')
  if ($m.Count -gt 0) { $remaining += $m.Count }
}

$report = @{
  changed = @($changed)
  remaining = $remaining
  textWhite = $textWhite
}

$json = $report | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText((Join-Path (Get-Location) 'MIGRATION_AGENT_REPORT.txt'), $json)
Write-Output $json
