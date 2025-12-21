$ErrorActionPreference = 'Stop'

$files = @(
  'Assets/Abyss/Equipment/PlayerEquipment.cs',
  'Assets/Abyss/Equipment/PlayerEquipmentUI.cs',
  'Assets/Abyss/Equipment/EquipmentSlotFeedback.cs',
  'Assets/Abyss/Equipment/InventoryEquipButtonMvpAdapter.cs',
  'Assets/Abyss/Inventory/PlayerInventoryUI.cs',
  'Assets/Abyss/Inventory/PlayerInventoryDetailsUI.cs',
  'Assets/Abyss/Inventory/PlayerInventoryRowUI.cs',
  'Assets/Abyss/Inventory/InventoryRarityColors.cs',
  'Assets/Editor/BuildPlayerEquipmentUIEditor.cs',
  'Assets/Editor/BuildPlayerInventoryUIEditor.cs',
  'Assets/Editor/AutoAssignMissingItemIconsEditor.cs',
  'Assets/Editor/GenerateBasicItemIcons.cs',
  'Assets/Editor/GenerateUiBorderSprite.cs',
  'Assets/Editor/TownInteractionRestorer.cs',
  'Assets/Editor/TownLegacyRootCleaner.cs',
  'Assets/Editor/ValidateUiIconsEditor.cs',
  'Assets/Abyss/Shop/MerchantClickRaycaster.cs',
  'Assets/Abyss/Shop/MerchantDoorHoverHighlighter.cs',
  'Assets/Game/Dev/DevCheats.cs',
  'Assets/Game/Input/PlayerInputAuthority.cs'
)

New-Item -ItemType Directory -Force -Path 'Docs' | Out-Null
$out = 'Docs/EquipmentUI_MVP_CodeDump.md'

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$sb = New-Object System.Text.StringBuilder

$null = $sb.AppendLine('# Equipment UI MVP - Full C# File Contents')
$null = $sb.AppendLine('')
$null = $sb.AppendLine('Auto-generated code dump for the Equipment UI MVP implementation.')
$null = $sb.AppendLine(('Generated: ' + (Get-Date).ToString('s')))
$null = $sb.AppendLine('')

$missing = @()

foreach ($rel in $files) {
  if (-not (Test-Path -LiteralPath $rel)) {
    $missing += $rel
    continue
  }

  $null = $sb.AppendLine(('## ' + $rel))
  $null = $sb.AppendLine('')
  $null = $sb.AppendLine('```csharp')

  $text = Get-Content -Raw -LiteralPath $rel
  if ($text -and -not $text.EndsWith("`n")) {
    $text += "`n"
  }

  $null = $sb.AppendLine($text.TrimEnd("`r", "`n"))
  $null = $sb.AppendLine('```')
  $null = $sb.AppendLine('')
}

if ($missing.Count -gt 0) {
  $null = $sb.AppendLine('---')
  $null = $sb.AppendLine('Missing files (not found on disk at generation time):')
  foreach ($m in $missing) { $null = $sb.AppendLine(('- ' + $m)) }
  $null = $sb.AppendLine('')
}

[System.IO.File]::WriteAllText($out, $sb.ToString(), $utf8NoBom)
Write-Host ("Wrote $out files=" + ($files.Count - $missing.Count) + " missing=" + $missing.Count)
