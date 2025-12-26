# ABYSSBOUND — FIND WHICH SHOP INVENTORY THE SKILLING MERCHANT USES (HERBS NOT SHOWING)

Date: 2025-12-25

## 1) Skilling merchant -> ShopInventory reference

### In-scene MerchantShop (this is what drives the shop UI)
- Scene: `Assets/Scenes/Abyssbound_Zone1.unity`
- GameObject: `SkillingSuppliesMerchant`
- Component: `Abyss.Shop.MerchantShop`
- Inspector field: `shopInventory`
- Assigned ShopInventory GUID: `c59fc707efc477b43ad53e147daa44d5`
- Resolved asset path: `Assets/Abyss/Shops/Inventories/ShopInventory_Skilling.asset`

Evidence snippet (scene YAML):
- `shopInventory: {fileID: 11400000, guid: c59fc707efc477b43ad53e147daa44d5, type: 2}`

### Notes on "SkillingSuppliesMerchant" script
- Script file: `Assets/Game/Town/Merchants/SkillingSuppliesMerchant.cs`
- The script currently logs a placeholder in `Interact()` and does not reference a `ShopInventory`.

## 2) ShopInventory assets (known folder)
All found under `Assets/Abyss/Shops/Inventories/`:
- `Assets/Abyss/Shops/Inventories/ShopInventory_Alchemy.asset`
- `Assets/Abyss/Shops/Inventories/ShopInventory_Consumables.asset`
- `Assets/Abyss/Shops/Inventories/ShopInventory_Skilling.asset`
- `Assets/Abyss/Shops/Inventories/ShopInventory_Weapons.asset`
- `Assets/Abyss/Shops/Inventories/ShopInventory_Workshop.asset`

## 3) Does ShopInventory_Alchemy exist, and does it contain herbs?

### ShopInventory_Alchemy exists
- Asset: `Assets/Abyss/Shops/Inventories/ShopInventory_Alchemy.asset`
- Meta GUID: `5c78720bb0a7b404da6e64d020b36eb3`

### Herbs exist (legacy item definitions)
Folder exists and contains the created herb items:
- `Assets/GameData/Items/Alchemy/Item_herb_guam.asset` (meta GUID `b16c0624fb25c0f459ac4458d4954e20`)
- plus `Item_herb_marrentill.asset`, `Item_herb_taradromin.asset`, `Item_herb_harralander.asset`, `Item_herb_irit.asset`, `Item_herb_avantoe.asset`

### ShopInventory_Alchemy contains herb entries
- `ShopInventory_Alchemy.asset` contains an entry referencing `Item_herb_guam` by GUID:
  - `item: {fileID: 11400000, guid: b16c0624fb25c0f459ac4458d4954e20, type: 2}`

## Key conclusion
- The Skilling merchant shop UI uses `Assets/Abyss/Shops/Inventories/ShopInventory_Skilling.asset`.
- Herbs were added to `ShopInventory_Alchemy.asset`, not to `ShopInventory_Skilling.asset`.
- Therefore herbs will not show for the Skilling merchant until `ShopInventory_Skilling.asset` (or the merchant’s `shopInventory` reference) is updated.

(Per request: no changes were made to inventories or scene.)
