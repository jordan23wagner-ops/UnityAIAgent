# ABYSSBOUND — Final Legs Icon Fix (Art + Data)

Generated: 2025-12-26 04:07:09Z

Context:
- Merchant Basic Legs already references sil_legs.png
- sil_legs.png artwork previously resembled a belt
- Loot V2 Abyssal Initiate Legs previously referenced sil_boots.png

## A) sil_legs sprite ART
- Result: UPDATED
- Details: Replaced pixel content (generated pants silhouette) at Assets/Abyss/Equipment/Icons/sil_legs.png (96x96).

## B) Loot V2 starter legs DATA
- UPDATED: Item_Starter_Legs.icon
- Asset: Assets/GameData/Loot/StarterSet/Item_Starter_Legs.asset
- Before: sil_boots (Assets/Abyss/Equipment/Icons/sil_boots.png)
- After:  sil_legs (Assets/Abyss/Equipment/Icons/sil_legs.png)

## C) Refresh
- AssetDatabase.SaveAssets() + Refresh() executed.

## Reminder
- Restart Play Mode (and ensure Domain Reload is enabled) so LootRegistryRuntime rebuilds and picks up updated base item icons.

## QA checklist
1) Weapons & Gear shop: Basic Legs shows legs silhouette (not belt).
2) Equipment UI: Abyssal Initiate Legs shows legs silhouette.
3) Boots still show boots silhouette.
