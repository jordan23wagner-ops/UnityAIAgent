# ABYSSBOUND — QA Set Legs Icon Fix (sil_legs)

Date: 2025-12-25

## Change

Updated the Loot V2 Starter Set legs base item so its `icon` points to the legs silhouette sprite.

Also updated the QA starter set authoring menu so it no longer overwrites legs to the boots silhouette.

- Target base item (ItemDefinitionSO):
  - `Assets/GameData/Loot/StarterSet/Item_Starter_Legs.asset`
  - `id: Starter_Legs`
  - `displayName: Abyssal Initiate Legs`

- Icon set to:
  - `Assets/Abyss/Equipment/Icons/sil_legs.png`

- QA authoring script fix:
  - `Assets/Editor/Abyssbound/Sets/QA/StarterSetQaMenu.cs`
  - Changed `IconLegsPath` from `sil_boots.png` to `sil_legs.png`

## What was wrong

`Item_Starter_Legs.asset` was referencing the boots silhouette GUID (so legs displayed as boots in any UI path that resolves icons from `ItemDefinitionSO.icon`).

Additionally, `StarterSetQaMenu` was configured to re-author the Starter Set legs piece using `sil_boots.png` on editor load / when running the QA starter set commands, which could revert the asset back to boots even after manual edits.

## Verification

1. In the Unity Editor, ensure Play Mode uses **Reload Domain and Scene**.
2. Enter Play Mode.
3. Equip the QA-spawned legs item.
4. Confirm the console line shows:
   - `slot=Legs`
   - `icon='sil_legs'` (or similar)
   - `baseSO='Assets/GameData/Loot/StarterSet/Item_Starter_Legs.asset'`

You can also run:
- `Tools/Abyssbound/Debug/Log Equipped Item Icon Sources`

Expected for legs slot:
- `baseSO='Assets/GameData/Loot/StarterSet/Item_Starter_Legs.asset'`
- `iconPath='Assets/Abyss/Equipment/Icons/sil_legs.png'`
