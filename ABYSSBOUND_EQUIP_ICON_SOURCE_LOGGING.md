# ABYSSBOUND — Equip Icon Source Logging

Generated: 2025-12-25

Goal: When equipping QA-spawned Loot V2 armor pieces, print the *exact* base `ItemDefinitionSO` asset path and the icon sprite source path so we can verify what the UI is really reading.

## 1) Runtime (Editor-only) equip log

Where it logs:
- `Assets/Abyss/Equipment/PlayerEquipment.cs`
- Method: `PlayerEquipment.TryEquipFromInventory(...)`
- Behavior: logs **one line per successful equip** in the Unity Editor (guarded by `#if UNITY_EDITOR`).

Console format (single line):
- `[EquipIconSrc] dn='...' slot=... icon='...' iconPath='...' baseSO='...'`

Notes:
- For Loot V2 rolled items (`ri_...`), `baseSO` is the `ItemDefinitionSO` resolved via `LootRegistryRuntime`.
- `iconPath` uses `AssetDatabase.GetAssetPath(Sprite)` (usually points at the underlying `.png`).
- The log is wrapped in try/catch so it cannot break equip flow.

## 2) Menu item to dump currently equipped icon sources

Menu:
- `Tools/Abyssbound/Debug/Log Equipped Item Icon Sources`

What it does:
- Finds `PlayerEquipment` in the active scene.
- For each equipped slot, prints a short line including:
  - equipped slot
  - equipped item id
  - resolved base `ItemDefinitionSO` asset path (when resolvable via `LootRegistryRuntime`)
  - icon sprite name and `AssetDatabase` path

Implementation:
- `Assets/Editor/Abyssbound/Debug/EquipIconSourceLoggingEditor.cs`

## 3) Usage for QA

1. Enter Play Mode.
2. Equip an armor piece (especially QA-spawned rolled items).
3. Look for `[EquipIconSrc] ...` lines in the Console.
4. If needed, run the menu item to dump all currently-equipped slots.
