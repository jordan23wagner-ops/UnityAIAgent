param(
  [string]$OutputPath = "Docs/EquipmentDamage_MVP_CodeDump.md"
)

$ErrorActionPreference = "Stop"

# Collect changed + untracked C# files (relative paths)
$changed = @(git diff --name-only -- "*.cs")
$untracked = @(git ls-files --others --exclude-standard -- "*.cs")

$files = @($changed + $untracked | Where-Object { $_ -and $_.Trim().Length -gt 0 } | Sort-Object -Unique)

$nl = "`n"
$sb = New-Object System.Text.StringBuilder

[void]$sb.AppendLine("# Equipment Damage MVP - Code Dump")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("## Files")
foreach ($f in $files) {
  [void]$sb.AppendLine("- $f")
}

foreach ($f in $files) {
  [void]$sb.AppendLine("")
  [void]$sb.AppendLine("---")
  [void]$sb.AppendLine("")
  [void]$sb.AppendLine("## $f")
  [void]$sb.AppendLine("")
  [void]$sb.AppendLine('```csharp')

  if (-not (Test-Path -LiteralPath $f)) {
    [void]$sb.AppendLine("// ERROR: file not found")
  } else {
    $content = Get-Content -LiteralPath $f -Raw
    # Normalize newlines for readability in markdown
    $content = $content -replace "`r`n", "`n"
    [void]$sb.Append($content)
    if (-not $content.EndsWith("`n")) {
      [void]$sb.AppendLine("")
    }
  }

  [void]$sb.AppendLine('```')
}

$outDir = Split-Path -Parent $OutputPath
if ($outDir -and -not (Test-Path -LiteralPath $outDir)) {
  New-Item -ItemType Directory -Path $outDir | Out-Null
}

# Write UTF8 without BOM to avoid weird encoding artifacts
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($OutputPath, $sb.ToString(), $utf8NoBom)

Write-Host "Wrote $OutputPath"