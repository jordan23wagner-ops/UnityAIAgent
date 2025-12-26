# ABYSSBOUND — Icon Runtime Source Report (Merchant + Equip)

Generated: 2025-12-25

Goal: identify the exact asset instances the UI *reads from* (merchant + inventory/equipment), and what Sprite each of those assets currently references.

This report is read-only: it does not modify assets.

## A) Merchant (legacy)

### A1) Shop inventory asset
- ShopInventory asset path: Assets/Abyss/Shops/Inventories/ShopInventory_Weapons.asset
- ShopInventory asset GUID: 41b4de403ffb4c448972aad6245d25b1

In Assets/Abyss/Shops/Inventories/ShopInventory_Weapons.asset, the “Basic Legs” entry is the item reference with GUID:
- Entry item GUID: 4cf011cfcf8e441d9f886002386b5e22

### A2) Referenced ItemDefinition (“Basic Legs”)
Resolved from GUID 4cf011cfcf8e441d9f886002386b5e22:
- ItemDefinition asset path: Assets/GameData/Items/Item_BasicLegs.asset
- ItemDefinition asset GUID: 4cf011cfcf8e441d9f886002386b5e22
- Serialized equipmentSlot: 3 (expected: Legs)
- Serialized icon reference:
  - Sprite GUID: 4816c84c5bf7d3d499ab2bf46db1d699
  - Sprite asset path: Assets/Abyss/Equipment/Icons/sil_legs.png
  - Sprite name: sil_legs

So: for “Basic Legs” shown via ShopInventory_Weapons, the icon source-of-truth is ItemDefinition.icon on Assets/GameData/Items/Item_BasicLegs.asset, and it currently points at the Sprite Assets/Abyss/Equipment/Icons/sil_legs.png.

### A3) Exact UI binding path (shop row icon)
**Primary icon field read:** ItemDefinition.icon (copied into MerchantShop.ResolvedStock.icon)

- Merchant builds the list and copies icon from the referenced ItemDefinition:
  - MerchantShop.GetResolvedStock(): [Assets/Abyss/Shop/MerchantShop.cs](Assets/Abyss/Shop/MerchantShop.cs#L55)
  - The exact assignment: `icon = def.icon` is at [Assets/Abyss/Shop/MerchantShop.cs](Assets/Abyss/Shop/MerchantShop.cs#L78)

- MerchantShopUI passes that icon into the row prefab:
  - Buy-mode list population: [Assets/Abyss/Shop/MerchantShopUI.cs](Assets/Abyss/Shop/MerchantShopUI.cs#L255)
  - The exact bind call: `row.Bind(..., captured.icon, ...)` at [Assets/Abyss/Shop/MerchantShopUI.cs](Assets/Abyss/Shop/MerchantShopUI.cs#L268)

- MerchantShopRowUI renders the sprite onto the row Image:
  - `iconImage.sprite = icon;` at [Assets/Abyss/Shop/MerchantShopRowUI.cs](Assets/Abyss/Shop/MerchantShopRowUI.cs#L89)

- Details panel icon also binds from the selected icon:
  - `detailIconImage.sprite = icon;` at [Assets/Abyss/Shop/MerchantShopUI.cs](Assets/Abyss/Shop/MerchantShopUI.cs#L467)

### A4) Important fallback note
If the MerchantShop component has no ShopInventory assigned (or entries are empty), it falls back to the legacy `stock` string list and sets `icon = null` for those entries.
- See fallback section in [Assets/Abyss/Shop/MerchantShop.cs](Assets/Abyss/Shop/MerchantShop.cs#L94-L127)

If you are seeing a non-null icon in the merchant list but it doesn’t match Item_BasicLegs.icon, that strongly suggests either:
1) The in-scene MerchantShop is *not* using ShopInventory_Weapons (different inventory), OR
2) The Sprite asset at Assets/Abyss/Equipment/Icons/sil_legs.png contains unexpected artwork (e.g., it visually resembles a belt).

## B) Equipment/Inventory (Loot V2)

### B1) Loot V2 ItemDefinitionSO (“Abyssal Initiate Legs”)
Found by displayName match:
- ItemDefinitionSO asset path: Assets/GameData/Loot/StarterSet/Item_Starter_Legs.asset
- ItemDefinitionSO asset GUID: 3dcab2cb9fc297b44b60afa0b58a594e
- id: Starter_Legs
- Serialized slot: 3 (expected: Legs)
- Serialized icon reference:
  - Sprite GUID: 061ce2abcde0bcb408e07238c0042597
  - Sprite asset path: Assets/Abyss/Equipment/Icons/sil_boots.png
  - Sprite name: sil_boots

This means: the exact ItemDefinitionSO you asked about is currently configured to show the boots silhouette even though it is a Legs-slot item.

### B2) Exact UI binding path (inventory & equipment icons for Loot V2 instances)

**Inventory grid/list row (rolled Loot V2):**
- PlayerInventoryRowUI attempts to resolve Loot V2 display (name + icon) when `def == null`:
  - `reg.TryResolveDisplay(fallbackItemId, out var lootName, out var lootIcon)` at [Assets/Abyss/Inventory/PlayerInventoryRowUI.cs](Assets/Abyss/Inventory/PlayerInventoryRowUI.cs#L280)
  - It later uses the resolved icon when no legacy def icon is available:
    - `if (reg.TryResolveDisplay(..., out _, out var lootIcon) && lootIcon != null) icon = lootIcon;` at [Assets/Abyss/Inventory/PlayerInventoryRowUI.cs](Assets/Abyss/Inventory/PlayerInventoryRowUI.cs#L362)

**Equipment UI slots (rolled Loot V2):**
- PlayerEquipmentUI resolves Loot V2 icons when the equipped id has no legacy ItemDefinition:
  - `reg.TryResolveDisplay(itemId, out var dn, out var ic)` at [Assets/Abyss/Equipment/PlayerEquipmentUI.cs](Assets/Abyss/Equipment/PlayerEquipmentUI.cs#L186)
  - It binds the Image sprite as either legacy `def.icon` or resolved `lootIcon`:
    - `w.iconImage.sprite = ... (def != null ? def.icon : lootIcon)` at [Assets/Abyss/Equipment/PlayerEquipmentUI.cs](Assets/Abyss/Equipment/PlayerEquipmentUI.cs#L200)

**LootRegistryRuntime source-of-truth:**
- The resolved Sprite is directly the base item’s icon:
  - `icon = baseItem.icon;` at [Assets/Scripts/LootSystem/LootRegistryRuntime.cs](Assets/Scripts/LootSystem/LootRegistryRuntime.cs#L234)
  - Method entry: [Assets/Scripts/LootSystem/LootRegistryRuntime.cs](Assets/Scripts/LootSystem/LootRegistryRuntime.cs#L222)

So for Loot V2 rolled items, the UI icon is ultimately whatever the registered base ItemDefinitionSO’s `.icon` is.

## C) Dupes check (critical)

### C1) displayName duplicates
- ItemDefinition with `displayName: Basic Legs`
  - Found: 1
  - Assets/GameData/Items/Item_BasicLegs.asset

- ItemDefinitionSO with `displayName: Abyssal Initiate Legs`
  - Found: 1
  - Assets/GameData/Loot/StarterSet/Item_Starter_Legs.asset

### C2) id / itemId duplicates (sanity)
- ItemDefinition with `itemId: armor_basic_legs`: Found 1 (Assets/GameData/Items/Item_BasicLegs.asset)
- ItemDefinitionSO with `id: Starter_Legs`: Found 1 (Assets/GameData/Loot/StarterSet/Item_Starter_Legs.asset)

## Recommended fix (targets the exact assets used)

1) Merchant “Basic Legs”
- The merchant pipeline reads Assets/GameData/Items/Item_BasicLegs.asset → ItemDefinition.icon → Assets/Abyss/Equipment/Icons/sil_legs.png.
- If it still visually looks like a belt, the most likely cause is that Assets/Abyss/Equipment/Icons/sil_legs.png contains belt-like artwork.
  - Replace the *texture content* of sil_legs.png with correct legs artwork (keep the filename so all references stay valid).
- Also verify the in-scene MerchantShop is actually using ShopInventory_Weapons (otherwise it may be using the legacy `stock` list).

2) Equipment UI “Abyssal Initiate Legs”
- The ItemDefinitionSO Assets/GameData/Loot/StarterSet/Item_Starter_Legs.asset currently points to sil_boots.png.
- Update that asset’s icon to sil_legs.png so LootRegistryRuntime.TryResolveDisplay returns the legs sprite for rolled instances.

3) If changes still don’t appear in Play Mode
- LootRegistryRuntime caches its bootstrap-derived registry on first build; restarting Play Mode (with Domain Reload enabled) or restarting Unity ensures the registry reloads the updated ItemDefinitionSO.icon.
