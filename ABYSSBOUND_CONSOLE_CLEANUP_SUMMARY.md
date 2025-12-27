# ABYSSBOUND — Console Cleanup Summary (CS0618 + SourceAssetDB mismatch)

## A) CS0618 (Editor warning)
**Issue**
- `Assets/Editor/Abyssbound/Debug/EquipIconSourceLoggingEditor.cs` used `Object.FindObjectOfType<T>()`, which is obsolete in newer Unity versions.

**Fix**
- Replaced it with `Object.FindAnyObjectByType<PlayerEquipment>()` to preserve “grab any instance” behavior.
- Added a brief comment explaining the deprecation.

## B) Import Error Code:(4) SourceAssetDB vs disk mod time mismatch
**Issue**
- Unity import pipeline reported a build asset version / SourceAssetDB timestamp mismatch for:
  - `Assets/GameData/Loot/StarterSet/Item_Starter_Legs.asset`

**Fix (Editor menu tools)**
Added menu items in `Assets/Editor/Abyssbound/Maintenance/SourceAssetDbTimestampMismatchTools.cs`:
- `Tools/Abyssbound/Maintenance/Fix SourceAssetDB Timestamp Mismatch`
  - Targets `Assets/GameData/Loot/StarterSet/Item_Starter_Legs.asset`.
- `Tools/Abyssbound/Maintenance/Fix All SourceAssetDB Timestamp Mismatch (Safe)`
  - Runs the same fix on a small explicit list (currently just Starter Legs).

**What the menu does (short + safe)**
- `AssetDatabase.Refresh()`
- `AssetDatabase.ImportAsset(path, ForceUpdate | ForceSynchronousImport)`
- `AssetDatabase.ForceReserializeAssets([path])`
- `AssetDatabase.ImportAsset(path, ForceUpdate | ForceSynchronousImport)`
- `AssetDatabase.SaveAssets()` + `AssetDatabase.Refresh()`

## Notes
- Console output is intentionally short (single success/failure line per run).
