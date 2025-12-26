# ABYSSBOUND — Silhouette Icon Inventory + UI Icon Pipeline Report

Generated: 2025-12-26 02:46:23Z

Goal: ensure all weapons/gear use white silhouette pictogram sprites (no letters).

## 1) Sprite search results

Search rule (case-insensitive name match): `(sil_|icon|pictogram|glyph|weapon|bow|staff|sword|helm|chest|legs|belt|shield|amulet|ring|cape|arrows|orb)`

### 1A) Image/Sprite source files (png/psd/tga/jpg/jpeg) + SpriteAtlases

| Name | Asset Path |
|---|---|
| sil_amulet | Assets/Abyss/Equipment/Icons/sil_amulet.png |
| sil_arrows | Assets/Abyss/Equipment/Icons/sil_arrows.png |
| sil_belt | Assets/Abyss/Equipment/Icons/sil_belt.png |
| sil_boots | Assets/Abyss/Equipment/Icons/sil_boots.png |
| sil_cape | Assets/Abyss/Equipment/Icons/sil_cape.png |
| sil_chest | Assets/Abyss/Equipment/Icons/sil_chest.png |
| sil_gloves | Assets/Abyss/Equipment/Icons/sil_gloves.png |
| sil_helm | Assets/Abyss/Equipment/Icons/sil_helm.png |
| sil_legs | Assets/Abyss/Equipment/Icons/sil_legs.png |
| sil_orb | Assets/Abyss/Equipment/Icons/sil_orb.png |
| sil_ring | Assets/Abyss/Equipment/Icons/sil_ring.png |
| sil_shield | Assets/Abyss/Equipment/Icons/sil_shield.png |
| sil_sword | Assets/Abyss/Equipment/Icons/sil_sword.png |
| URP | Assets/TutorialInfo/Icons/URP.png |
| Icon_HealthPotion | Assets/UI/Generated/Icons/Icon_HealthPotion.png |
| Icon_ManaPotion | Assets/UI/Generated/Icons/Icon_ManaPotion.png |
| Icon_TownScroll | Assets/UI/Generated/Icons/Icon_TownScroll.png |

(Note: this lists source assets that *may* be imported as Sprites; Unity import settings ultimately decide Sprite vs Texture.)

## 2) Required silhouette existence check

- sil_sword:
  - Assets/Abyss/Equipment/Icons/sil_sword.png
- sil_bow: NOT FOUND
- sil_staff: NOT FOUND
- sil_2h: NOT FOUND
- sil_helm:
  - Assets/Abyss/Equipment/Icons/sil_helm.png
- sil_chest:
  - Assets/Abyss/Equipment/Icons/sil_chest.png
- sil_legs:
  - Assets/Abyss/Equipment/Icons/sil_legs.png
- sil_gloves:
  - Assets/Abyss/Equipment/Icons/sil_gloves.png
- sil_boots:
  - Assets/Abyss/Equipment/Icons/sil_boots.png
- sil_belt:
  - Assets/Abyss/Equipment/Icons/sil_belt.png
- sil_shield:
  - Assets/Abyss/Equipment/Icons/sil_shield.png
- sil_amulet:
  - Assets/Abyss/Equipment/Icons/sil_amulet.png
- sil_ring:
  - Assets/Abyss/Equipment/Icons/sil_ring.png
- sil_cape:
  - Assets/Abyss/Equipment/Icons/sil_cape.png
- sil_arrows:
  - Assets/Abyss/Equipment/Icons/sil_arrows.png
- sil_orb:
  - Assets/Abyss/Equipment/Icons/sil_orb.png

## 3) Icon pipeline

### 3A) Loot V2 equipment drops / inventory / equipment UI

**Primary icon data sources**
- Loot V2 base item: `Abyssbound.Loot.ItemDefinitionSO.icon`
- Legacy base item: `Abyss.Items.ItemDefinition.icon`

**Where the UI reads icons**
- Inventory list/grid rows: `Assets/Abyss/Inventory/PlayerInventoryRowUI.cs`
  - Uses `ItemDefinition.icon` when a legacy `ItemDefinition` is available.
  - If the row is backed by a rolled Loot V2 instance id (`ri_...`) and there is no legacy `ItemDefinition`, it calls `LootRegistryRuntime.TryResolveDisplay(fallbackItemId, ..., out Sprite lootIcon)` and uses that as the icon.
- Equipment window slots: `Assets/Abyss/Equipment/PlayerEquipmentUI.cs`
  - Uses `ItemDefinition.icon` for legacy items.
  - If the equipped item id is a rolled Loot V2 instance without a legacy `ItemDefinition`, it calls `LootRegistryRuntime.TryResolveDisplay(itemId, ..., out Sprite lootIcon)` and uses that.
- Tooltips:
  - Loot V2 world drops (rolled instances): `Assets/Scripts/LootSystem/WorldItemPickup.cs` -> `ItemTooltipUI.ShowLootInstance` uses `ItemDefinitionSO.icon`.
  - Legacy world drops: `Assets/Scripts/Loot/WorldLootPickup.cs` -> `ItemTooltipUI.ShowExtended` uses `ItemDefinition.icon`.

**Key resolver**
- `Assets/Scripts/LootSystem/LootRegistryRuntime.cs`: `TryResolveDisplay(itemId, out displayName, out Sprite icon)` returns `baseItem.icon` for the rolled instance's base item.

**Important bridge (legacy -> Loot V2 instances)**
- `Assets/Scripts/Player/PlayerInventory.cs`: when legacy equippable items are added, they are converted into Loot V2 rolled instances and a Loot V2 base item is registered via `EnsureLootV2BaseItemRegistered`, which copies `legacy.icon` into `ItemDefinitionSO.icon`.

### 3B) Legacy shop list rows

**Primary icon data source**
- `Abyss.Items.ItemDefinition.icon` for items referenced by the shop inventory.

**Where the UI reads icons**
- `Assets/Abyss/Shop/MerchantShop.cs`: `GetResolvedStock()` builds a list of entries with `ResolvedStock.icon = def.icon` (when using `ShopInventory`).
- `Assets/Abyss/Shop/MerchantShopUI.cs`: uses `ResolvedStock.icon` for row icons and details panel icon (`detailIconImage.sprite = icon`).
- `Assets/Abyss/Shop/MerchantShopRowUI.cs`: binds the provided `Sprite icon` to its `iconImage` and hides the image if icon is null.

**Fallback behavior that can hide icons**
- If a merchant has no `ShopInventory` (or it has no entries), `MerchantShop.GetResolvedStock()` falls back to a legacy string `stock` list and sets `icon = null` for those entries. The UI then hides the icon image rather than generating a fallback.

### 3C) Anything that could generate “letter-like” icons (fallbacks)

Based on code inspection in this repo, the *UI code paths above do not generate letter sprites at runtime when an icon is missing*; they generally hide the Image when `Sprite` is null.

However, there is an explicit editor-time generator that creates **letter icons** as PNG sprites:
- `Assets/Editor/GenerateBasicItemIcons.cs`
  - Writes letter sprites to `Assets/UI/Generated/Icons` (examples: `Icon_HealthPotion.png`, `Icon_ManaPotion.png`, `Icon_TownScroll.png`)
  - Assigns those sprites to specific legacy `ItemDefinition.icon` entries (itemIds: `potion_health`, `potion_mana`, `scroll_town`).

So if you are seeing letters in UI, it is most likely because the item’s `icon` Sprite asset itself contains a letter (generated/placeholder), not because the UI is synthesizing letters.

## 4) Recommendation (weapon silhouette mapping)

### 4A) Target mapping (ideal)

| Weapon Type | Target Sprite |
|---|---|
| Sword (and generic 1H melee) | sil_sword |
| Bow | sil_bow |
| Staff/Wand | sil_staff |
| 2H melee (optional) | sil_2h |

### 4B) Temporary fallback plan (if bow/staff sprites are missing)

- Bow -> use `sil_arrows` temporarily
- Staff/Wand -> use `sil_orb` temporarily
- Generic weapon -> use `sil_sword`

### 4C) Assets to add later (to fully meet the goal)

- Add silhouette sprites:
  - `Assets/Abyss/Equipment/Icons/sil_bow.png`
  - `Assets/Abyss/Equipment/Icons/sil_staff.png`
  - (Optional) `Assets/Abyss/Equipment/Icons/sil_2h.png`

## Notes
- This report is read-only: no project assets were modified to generate it.
