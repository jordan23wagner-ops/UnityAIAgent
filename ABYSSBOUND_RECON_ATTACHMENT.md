# ABYSSBOUND — RECON (A–G)

This is a **read-only, evidence-based inventory** of what exists in the repo right now.

## Method
- Sources are **Unity YAML assets/scenes/prefabs** (`.asset`, `.unity`, `.prefab`) and **C# scripts**.
- Every claim below is backed by a concrete **path** plus the relevant **class/type** and **field/property names**.
- When something is missing, it is recorded as **NOT FOUND** plus what was searched.

---

## A) Loot Systems + Drop Tables (Loot V2 + Legacy)

### A1. Loot V2 (Abyssbound.Loot) — data + runtime

**Drop trigger component**
- Script: `Assets/Scripts/LootSystem/LootDropOnDeath.cs`
  - Type: `LootDropOnDeath : MonoBehaviour`
  - Fields:
    - `LootTableSO lootTable`
    - `int itemLevel`
    - `int? seed`
    - `WorldItemPickup pickupPrefab`
    - `float scatterRadius`
    - `bool logDrop`
  - Default table constant: `DefaultLootTableResourcesPath = "Loot/Tables/Zone1_Trash"`
  - Legacy interop guard: checks `DropOnDeath` via `GetComponentInParent<DropOnDeath>()` and returns early if present + enabled.

**Loot table schema**
- Script: `Assets/Scripts/LootSystem/LootTableSO.cs`
  - Type: `Abyssbound.Loot.LootTableSO : ScriptableObject`
  - Fields:
    - `string id`
    - `List<WeightedItemEntry> items` where `WeightedItemEntry` has `ItemDefinitionSO item` and `float weight`
    - `List<WeightedRarityEntry> rarities` where `WeightedRarityEntry` has `RarityDefinitionSO rarity` and `float weight`
    - `List<AffixDefinitionSO> affixPoolOverride`

**Zone1 Loot V2 tables (assets)**
- `Assets/Resources/Loot/Tables/Zone1_Trash.asset`
  - Type: `Abyssbound.Loot.LootTableSO`
  - Fields present: `id`, `items[]`, `rarities[]`, `affixPoolOverride[]`
  - Rarity weights: `rarities[].weight` = 60 / 25 / 12 / 3 / 0 / 0
- `Assets/Resources/Loot/Tables/Zone1_Elite.asset`
  - Type: `Abyssbound.Loot.LootTableSO`
  - Rarity weights: `rarities[].weight` = 45 / 30 / 20 / 5 / 0 / 0
- `Assets/Resources/Loot/Tables/Zone1_Boss.asset`
  - Type: `Abyssbound.Loot.LootTableSO`
  - Rarity weights: `rarities[].weight` = 5 / 15 / 55 / 25 / 0 / 0
- `Assets/Resources/Loot/Tables/LootTable_Starter.asset`
  - Type: `Abyssbound.Loot.LootTableSO`
  - Rarity weights: `rarities[].weight` = 70 / 22 / 6 / 1.8 / 0.18 / 0.02

**Enemy hookup(s) for Loot V2**
- Prefab: `Assets/Prefabs/Enemy_Dummy/Enemy_Dummy.prefab`
  - Component block includes `m_EditorClassIdentifier: Assembly-CSharp::LootDropOnDeath`
  - Serialized fields:
    - `lootTable` references GUID `5d1c0c4dc0f24e41a4bb4e6cc4f4e1b2` → `Assets/Resources/Loot/Tables/Zone1_Trash.asset`
    - `itemLevel: 0`, `pickupPrefab: {fileID: 0}`, `scatterRadius: 0.35`, `logDrop: 0`
  - Also contains a **disabled** legacy `DropOnDeath` component (`m_Enabled: 0`) with `zoneLootTable` set (see A2).
- Prefab: `Assets/Abyssbound/Prefabs/Actors/Enemies/Enemy_Dummy.prefab`
  - Same `LootDropOnDeath` fields and same `lootTable` GUID `5d1c0c4dc0f24e41a4bb4e6cc4f4e1b2`.

**Search coverage (Loot V2 placements)**
- Searched (including ignored):
  - `LootDropOnDeath` in `**/*.prefab` and `**/*.unity`
  - `Assembly-CSharp::LootDropOnDeath` in `**/*.prefab` and `**/*.unity`
- Result: **Only the two Enemy_Dummy prefabs above** were found containing `Assembly-CSharp::LootDropOnDeath`.

### A2. Legacy loot (DropOnDeath + ZoneLootTable + DropTable)

**Drop trigger component**
- Script: `Assets/Scripts/Loot/DropOnDeath.cs`
  - Type: `DropOnDeath : MonoBehaviour`
  - Loot fields:
    - `DropTable dropTable`
    - `ZoneLootTable zoneLootTable` (comment: “preferred”)
    - `EnemyTier tier`
  - Optional: `grantGold`, `goldMin`, `goldMax`
  - Optional: world pickup path `spawnWorldPickups`, `pickupPrefab`, `pickupScatterRadius`, `AffixPool affixPool`, `AffixRollRules affixRules`
  - Destination: `PlayerInventory playerInventory`
  - Multi-drop behavior:
    - Always rolls base table via `DropTableRoller.Roll(dropTable, tier, ...)`.
    - If `dropTable.matsTable != null` then adds `DropTableRoller.Roll(dropTable.matsTable, ...)`.
    - If `dropTable.specialDropsTable != null` then adds `DropTableRoller.Roll(dropTable.specialDropsTable, ...)`.

**ZoneLootTable schema + roller (preferred legacy path)**
- Script: `Assets/Abyss/Loot/ZoneLootTable.cs`
  - Type: `Abyss.Loot.ZoneLootTable : ScriptableObject`
  - Pools:
    - `List<Abyss.Items.ItemDefinition> zonePool`
    - `List<Abyss.Items.ItemDefinition> materialsPool`
    - `List<Abyss.Items.ItemDefinition> bossUniquesPool`
  - Rarity chances per tier (`trashChances`, `normalChances`, `eliteChances`, `miniBossChances`) with fields:
    - `common`, `uncommon`, `magic`, `rare`, `epic`, `legendary`, `set`, `radiant`
  - Roll counts:
    - `zoneRollsTrash`, `zoneRollsNormal`, `zoneRollsElite`, `zoneRollsMiniBoss`
  - Materials:
    - `bool rollMaterials`, plus `materialsRollsTrash`, `materialsRollsNormal`, `materialsRollsElite`, `materialsRollsMiniBoss`
  - Boss overrides:
    - `bool enableBossOverrides`, `bossZoneRolls`, `bossMaterialsRolls`, `bossUniqueRolls`, `bossGuaranteedMinRarity`, `bossGuaranteedAttempts`
- Script: `Assets/Abyss/Loot/ZoneLootRoller.cs`
  - Type: `Abyss.Loot.ZoneLootRoller` (static)
  - Core APIs:
    - `RollZone(ZoneLootTable table, EnemyTier tier, ...)` rolls `table.zonePool` and (optionally) `table.materialsPool`.
    - `ApplyBossOverrides(...)` adds `bossZoneRolls`, `bossMaterialsRolls`, `bossUniqueRolls` and applies guaranteed minimum rarity.

**Zone1 legacy ZoneLootTable data**
- Asset: `Assets/GameData/Loot/Zone1/Zone1_LootTable.asset`
  - Type: `Abyss.Loot.ZoneLootTable`
  - `zonePool` GUID references include:
    - `Assets/GameData/Items/Item_ApprenticeStaff.asset` (GUID `f014540a17ccdc640b67f4e423848a2f`)
    - `Assets/GameData/Items/Item_BronzeSword.asset` (GUID `9d79b1bd40201174bb340ff1f0a1885b`)
    - `Assets/GameData/Items/Item_HealthPotion.asset` (GUID `367dc5bae70bd7e49bcd6c3f0933a32f`)
    - `Assets/GameData/Items/Item_ManaPotion.asset` (GUID `7285dd1bb89f0a24bba904496c44a94c`)
    - `Assets/GameData/Items/Item_TownScroll.asset` (GUID `07fe535a06f31774ab8b35794a821266`)
    - `Assets/GameData/Items/Item_TrainingBow.asset` (GUID `1d5bc59c49a186d498caa6bb391e4549`)
  - `materialsPool` GUID references include:
    - `Assets/GameData/Items/Item_BronzePickaxe.asset`
    - `Assets/GameData/Items/Item_FishingRod.asset`
    - `Assets/GameData/Items/Item_Hatchet.asset`
    - `Assets/GameData/Items/Item_IronOre.asset`
    - `Assets/GameData/Items/Item_Leather.asset`
    - `Assets/GameData/Items/Item_WoodPlank.asset`
  - Example knobs present:
    - `zoneRollsTrash`, `zoneRollsMiniBoss`, `rollMaterials`, `materialsRollsElite`, `enableBossOverrides`, `bossGuaranteedMinRarity`

**DropTable schema + roller (legacy fallback)**
- Script: `Assets/Scripts/Legacy/Loot/DropTable.cs`
  - Type: `DropTable : ScriptableObject`
  - Tiered lists:
    - `List<DropEntry> trashDrops`, `normalDrops`, `eliteDrops`, `miniBossDrops`
  - Extensions:
    - `GuaranteedDropRule guaranteedEquipmentDrop`
    - `DropTable matsTable`
    - `DropTable specialDropsTable`
  - `DropEntry.item` type: `Abyss.Legacy.LegacyItemDefinition`
- Script: `Assets/Scripts/Legacy/Loot/DropTableRoller.cs`
  - Type: `DropTableRoller` (static)
  - API: `Roll(DropTable table, EnemyTier tier, ...)` returns `List<LegacyItemDefinition>` and applies `guaranteedEquipmentDrop`.

**Zone1 DropTable assets**
- Asset: `Assets/GameData/Loot/Zone1_Boss_DropTable.asset`
  - Type: `DropTable`
  - `normalDrops[]` contains one `DropEntry` with:
    - `item` GUID `876b0021a2bdf10488cde9e20d580d56` → `Assets/Abyssbound/ScriptableObjects/Items/Test_Rare_Sword.asset`
    - `dropChance: 1`
  - `guaranteedEquipmentDrop` fields present: `enabled`, `category`, `minRarity`, `rolls`
  - Extension tables:
    - `matsTable` GUID `71f5810abd7028f47948ee2c5e444061` → `Assets/GameData/Loot/Zone1_Mats_DropTable.asset`
    - `specialDropsTable` GUID `532102f532e75a04aa1f6c185ee163df` → `Assets/GameData/Loot/Zone1_Boss_SpecialDrops_DropTable.asset`
- Asset: `Assets/GameData/Loot/Zone1_Mats_DropTable.asset`
  - Type: `DropTable`
  - All tier drop lists are empty (`fodderDrops/normalDrops/eliteDrops/miniBossDrops: []`).
- Asset: `Assets/GameData/Loot/Zone1_Boss_SpecialDrops_DropTable.asset`
  - Type: `DropTable`
  - All tier drop lists are empty.

**Scene placement (legacy DropOnDeath)**
- Scene: `Assets/Scenes/Abyssbound_Zone1.unity`
  - GameObject: `Boss_Zone1`
  - Component: `m_EditorClassIdentifier: Assembly-CSharp::DropOnDeath`
  - Serialized fields include:
    - `dropTable` GUID `bf87ceb6052904a42b76010b788f9a21` → `Assets/GameData/Loot/Zone1_Boss_DropTable.asset`
    - `zoneLootTable: {fileID: 0}`
    - `tier: 1` (see `EnemyTier` enum in `Assets/Scripts/Legacy/Loot/DropTable.cs`)
    - `grantGold`, `spawnWorldPickups`, `pickupScatterRadius`, `logNoDrop`

**Search coverage (legacy DropOnDeath placements)**
- Searched (including ignored) for `Assembly-CSharp::DropOnDeath`:
  - In `**/*.prefab`: **NOT FOUND**
  - In `Assets/Scenes/**/*.unity`: found in `Assets/Scenes/Abyssbound_Zone1.unity` and `Assets/Scenes/SampleScene.unity`

---

## B) Items (3 parallel item “worlds”)

### B1. Abyss.Items.ItemDefinition (used by merchants + ZoneLootTable)

**Definition**
- Script: `Assets/Abyss/Items/ItemDefinition.cs`
  - Type: `Abyss.Items.ItemDefinition : ScriptableObject`
  - Identity fields: `itemId`, `displayName`, `description`, `Sprite icon`
  - Classification fields: `ItemRarity rarity`, `ItemType itemType`, `int baseValue`
  - Equipment fields: `EquipmentSlot equipmentSlot`, `WeaponHandedness weaponHandedness`
  - Stat fields: `int DamageBonus`, `int MaxHealthBonus`, `int DamageReductionFlat`

**Examples (assets referenced by ShopInventory and ZoneLootTable)**
- `Assets/GameData/Items/Item_BronzeSword.asset` (`itemId: weapon_bronze_sword`, `equipmentSlot: 7`, `DamageBonus: 3`)
- `Assets/GameData/Items/Item_HealthPotion.asset` (`itemId: potion_health`, `itemType: 1`, `equipmentSlot: 0`)
- `Assets/GameData/Items/Item_IronOre.asset` (`itemId: mat_iron_ore`, `itemType: 3`, `equipmentSlot: 0`)

### B2. Loot V2 ItemDefinitionSO (Abyssbound.Loot)

**Definition**
- Script: `Assets/Scripts/LootSystem/ItemDefinitionSO.cs`
  - Type: `Abyssbound.Loot.ItemDefinitionSO : ScriptableObject`
  - Fields:
    - `string id`, `string displayName`, `Sprite icon`
    - `EquipmentSlot slot`, `List<EquipmentSlot> occupiesSlots`
    - `List<StatMod> baseStats`
    - `List<AffixTag> allowedAffixTags`
    - `ItemSetDefinitionSO set`, `string setId`

**Examples**
- `Assets/Resources/Loot/Items/Item_Starter_Sword.asset`
  - Type: `Abyssbound.Loot.ItemDefinitionSO`
  - Fields present: `id`, `displayName`, `slot`, `baseStats[]`, `allowedAffixTags`, `set`
- Starter set pieces:
  - `Assets/GameData/Loot/StarterSet/Item_Starter_Helm.asset` references `set` GUID `4cc6f7fb824361a42ae5990a1057814d`.

### B3. Abyss.Legacy.LegacyItemDefinition (preserved old assets)

**Definition**
- Script: `Assets/Scripts/Items/Legacy/LegacyItemDefinition.cs`
  - Type: `Abyss.Legacy.LegacyItemDefinition : ScriptableObject`
  - Fields: `itemId`, `displayName`, `ItemType itemType`, `ItemRarity rarity`, `Sprite icon`
  - File comment states it exists to preserve previously-created loot assets that referenced a former global `ItemDefinition` type.

**Example**
- `Assets/Abyssbound/ScriptableObjects/Items/Test_Rare_Sword.asset`
  - Serialized as `m_EditorClassIdentifier: Assembly-CSharp::ItemDefinition` but uses script GUID `b8677b5b7ac9eca44b075408a25d5ff8` which maps to `Assets/Scripts/Items/Legacy/LegacyItemDefinition.cs`.

---

## C) Sets (Loot V2)

**Set definition schema**
- Script: `Assets/Scripts/LootSystem/ItemSetDefinitionSO.cs`
  - Type: `Abyssbound.Loot.ItemSetDefinitionSO : ScriptableObject`
  - Fields:
    - `string setId`, `string displayName`
    - `List<ItemDefinitionSO> pieces`
    - `List<SetBonusTier> bonuses`
  - Tier fields (`SetBonusTier`): `int requiredPieces`, `string description`, `List<StatMod> modifiers`

**Set asset (Abyssal Initiate)**
- Asset: `Assets/GameData/Sets/ItemSet_AbyssalInitiate.asset`
  - Type: `Abyssbound.Loot.ItemSetDefinitionSO`
  - Fields:
    - `pieces[]` references:
      - `Assets/GameData/Loot/StarterSet/Item_Starter_Helm.asset`
      - `Assets/GameData/Loot/StarterSet/Item_Starter_Chest.asset`
      - `Assets/GameData/Loot/StarterSet/Item_Starter_Legs.asset`
    - `bonuses[]` tiers:
      - `requiredPieces: 2` → `description: +1 Defense` → `modifiers[].stat: 3`, `value: 1`, `percent: 0`
      - `requiredPieces: 3` → `description: +5 Max Health` → `modifiers[].stat: 4`, `value: 5`, `percent: 0`

**Runtime application**
- Script: `Assets/Scripts/LootSystem/SetBonusRuntime.cs`
  - Type: `Abyssbound.Loot.SetBonusRuntime` (static)
  - Entry points used by player stats:
    - `AccumulateActiveSetBonusesForDamage(ref int damageBonus)`
    - `AccumulateActiveSetBonusesForHealth(ref int maxHealthBonus, ref int defenseFlatBonus)`
  - Important behavior: skips percent mods (`if (m.percent) continue; // percent not applied yet`).

---

## D) Materials / Alchemy

### D1. Materials that exist as assets

**Workshop merchant sells these ItemDefinition assets**
- From `Assets/Abyss/Shops/Inventories/ShopInventory_Workshop.asset` (type `Abyss.Shop.ShopInventory`, field `entries[]`):
  - `Assets/GameData/Items/Item_IronOre.asset` (`itemId: mat_iron_ore`)
  - `Assets/GameData/Items/Item_Leather.asset` (`itemId: mat_leather`)
  - `Assets/GameData/Items/Item_WoodPlank.asset` (`itemId: mat_wood_plank`)

**ZoneLootTable also contains a “materialsPool”**
- Asset: `Assets/GameData/Loot/Zone1/Zone1_LootTable.asset`
  - Field: `materialsPool` includes item assets listed in A2 (IronOre/Leather/WoodPlank) plus tools (Pickaxe/FishingRod/Hatchet).
  - Roll toggles: `bool rollMaterials`, `materialsRollsElite`, `materialsRollsMiniBoss`, etc.

### D2. Alchemy-specific items

**Evidence of alchemy category strings (code only)**
- Script: `Assets/Game/Town/Merchants/SkillingSuppliesMerchant.cs`
  - Field: `public string[] items` contains strings `"Vials"`, `"T1 Alchemy Mats"`, `"Empty Flask"`.

**NOT FOUND: authored alchemy item ScriptableObjects**
- Searched (including ignored) in `Assets/**/*.asset` for regex: `Alchemy|Flask|Vial|Herb|Reagent`.
- Result: **No matches found** in `.asset` content.

---

## E) Merchants + Shop Inventories

### E1. MerchantShop component schema
- Script: `Assets/Abyss/Shop/MerchantShop.cs`
  - Type: `Abyss.Shop.MerchantShop : MonoBehaviour`
  - Key fields:
    - `ShopInventory shopInventory`
    - `float buybackPercent`
    - `List<StockEntry> stock` (fallback list)
    - `string _merchantName`
  - Stock resolution logic: `GetResolvedStock()` prefers `shopInventory.entries` when present.

### E2. ShopInventory asset schema
- Script: `Assets/Abyss/Shop/ShopInventory.cs`
  - Type: `Abyss.Shop.ShopInventory : ScriptableObject`
  - Fields:
    - `List<ShopInventory.Entry> entries`
    - Entry fields: `Abyss.Items.ItemDefinition item`, `int price`

### E3. Zone1 merchants (scene placements)

Scene: `Assets/Scenes/Abyssbound_Zone1.unity`
- Merchant component blocks include `m_EditorClassIdentifier: Assembly-CSharp::Abyss.Shop.MerchantShop` with fields `shopInventory` and `_merchantName`:
  - Consumables:
    - `shopInventory` GUID `e38f024bb6adfa348b539c7f36107e61` → `Assets/Abyss/Shops/Inventories/ShopInventory_Consumables.asset`
    - `_merchantName: Consumables`
  - Weapons & Gear:
    - `shopInventory` GUID `41b4de403ffb4c448972aad6245d25b1` → `Assets/Abyss/Shops/Inventories/ShopInventory_Weapons.asset`
    - `_merchantName: Weapons & Gear`
  - Workshop:
    - `shopInventory` GUID `a60f3645e24937842bffd00fccd4b939` → `Assets/Abyss/Shops/Inventories/ShopInventory_Workshop.asset`
    - `_merchantName: Workshop`
  - Skilling Supplies:
    - `shopInventory` GUID `c59fc707efc477b43ad53e147daa44d5` → `Assets/Abyss/Shops/Inventories/ShopInventory_Skilling.asset`
    - `_merchantName: Skilling Supplies`

### E4. ShopInventory contents → concrete ItemDefinition assets

- `Assets/Abyss/Shops/Inventories/ShopInventory_Consumables.asset`
  - `entries[0].item` → `Assets/GameData/Items/Item_HealthPotion.asset` (price 10)
  - `entries[1].item` → `Assets/GameData/Items/Item_ManaPotion.asset` (price 12)
  - `entries[2].item` → `Assets/GameData/Items/Item_TownScroll.asset` (price 25)

- `Assets/Abyss/Shops/Inventories/ShopInventory_Weapons.asset`
  - `Assets/GameData/Items/Item_BronzeSword.asset` (price 80)
  - `Assets/GameData/Items/Item_TrainingBow.asset` (price 95)
  - `Assets/GameData/Items/Item_ApprenticeStaff.asset` (price 110)
  - `Assets/GameData/Items/Item_BasicHelm.asset` (price 35)
  - `Assets/GameData/Items/Item_BasicChest.asset` (price 60)
  - `Assets/GameData/Items/Item_BasicLegs.asset` (price 50)

- `Assets/Abyss/Shops/Inventories/ShopInventory_Workshop.asset`
  - `Assets/GameData/Items/Item_IronOre.asset` (price 15)
  - `Assets/GameData/Items/Item_Leather.asset` (price 18)
  - `Assets/GameData/Items/Item_WoodPlank.asset` (price 12)

- `Assets/Abyss/Shops/Inventories/ShopInventory_Skilling.asset`
  - `Assets/GameData/Items/Item_BronzePickaxe.asset` (price 60)
  - `Assets/GameData/Items/Item_Hatchet.asset` (price 55)
  - `Assets/GameData/Items/Item_FishingRod.asset` (price 65)

---

## F) Equipment UI Hooks (slots + rarity visuals)

### F1. PlayerEquipmentUI

**UI controller**
- Script: `Assets/Abyss/Equipment/PlayerEquipmentUI.cs`
  - Type: `Abyss.Equipment.PlayerEquipmentUI : MonoBehaviour`
  - Serialized SlotWidget fields:
    - `EquipmentSlot slot`
    - `Button button`
    - `Image iconImage`
    - `Image rarityStrip`
    - `TMP_Text labelText`
    - `Sprite emptyIcon`
  - In `Refresh()`:
    - Resolves equipped `itemId` via `_equipment.Get(w.slot)`.
    - If legacy item: uses `ItemRarityVisuals.Normalize(def.rarity)` + `RarityColorMap.GetColorOrDefault(normalized, ...)`.
    - If Loot V2 rolled instance: uses `LootRegistryRuntime.TryGetRolledInstance(itemId, out inst)` and colors via `RarityColorMap.GetColorOrDefault(inst.rarityId, ...)`.

**Scene wiring**
- Scene: `Assets/Scenes/Abyssbound_Zone1.unity`
  - GameObject: `PlayerEquipmentUI`
  - Component: `m_EditorClassIdentifier: Assembly-CSharp::Abyss.Equipment.PlayerEquipmentUI`
  - Field: `slots[]` contains many slot enums and per-slot `emptyIcon` sprite references.

### F2. Shared rarity color mapping
- Script: `Assets/Scripts/LootSystem/RarityColorMap.cs`
  - Type: `Abyssbound.Loot.RarityColorMap` (static)
  - APIs:
    - `GetColorOrDefault(string rarityId, Color defaultColor)`
    - `GetColorOrDefault(Abyss.Items.ItemRarity rarity, Color defaultColor)`
- Script: `Assets/Abyss/Items/ItemRarityVisuals.cs`
  - Type: `Abyss.Items.ItemRarityVisuals` (static)
  - APIs: `Normalize`, `ToDisplayString`, `GetColor`
- Script: `Assets/Abyss/Inventory/InventoryRarityColors.cs`
  - Type: `Abyss.Inventory.InventoryRarityColors` (static)
  - Uses `ItemRarityVisuals.Normalize` then delegates to `RarityColorMap.GetColorOrDefault(...)`.

---

## G) Stats / Combat Integration (equipment + rolled loot + set bonuses)

### G1. PlayerEquipment stores equipped item IDs
- Script: `Assets/Abyss/Equipment/PlayerEquipment.cs`
  - Type: `Abyss.Equipment.PlayerEquipment : MonoBehaviour`
  - Stores equipped IDs as strings per slot (`helm`, `chest`, `legs`, ...).
  - Read API: `string Get(EquipmentSlot slot)`
  - Write API: `void Set(EquipmentSlot slot, string itemId)`

### G2. Damage calculation (includes rolled loot + set bonuses)
- Script: `Assets/Game/Player/PlayerCombatStats.cs`
  - Type: `PlayerCombatStats : MonoBehaviour`
  - Fields/properties:
    - `baseDamage` (serialized), `BaseDamage`, `EquipmentDamageBonus`, `DamageFinal`
  - Equipment aggregation:
    - `AccumulateSlot(EquipmentSlot.RightHand, ...)` and `AccumulateSlot(EquipmentSlot.LeftHand, ...)`
    - Rolled Loot V2 support: `LootRegistryRuntime.TryGetRolledInstance(itemId, out inst)` → `inst.GetAllStatMods(reg)`
      - Applies only non-percent stat mods (`if (m.percent) continue`)
      - Accepts damage stats: `StatType.MeleeDamage`, `RangedDamage`, `MagicDamage`
    - Legacy ItemDefinition fallback: uses `def.DamageBonus` when `def.itemType == Abyss.Items.ItemType.Weapon`
  - Set bonuses: calls `Abyssbound.Loot.SetBonusRuntime.AccumulateActiveSetBonusesForDamage(ref bonus)`

### G3. Max HP and flat DR (includes rolled loot + set bonuses)
- Script: `Assets/Game/Player/PlayerHealth.cs`
  - Type: `PlayerHealth : MonoBehaviour`
  - Key fields/properties:
    - `baseMaxHealth` (serialized), `currentHealth`
    - `EquipmentMaxHealthBonus`, `EquipmentDamageReductionFlat`, `TotalDamageReductionFlat`, `MaxHealth`
  - Equipment aggregation:
    - Iterates `s_EquipSlots` (includes helm/chest/legs/belt/gloves/boots/cape/ammo/hands/rings/amulet/artifact)
    - Rolled Loot V2 support: `LootRegistryRuntime.TryGetRolledInstance(itemId, out inst)` → `inst.GetAllStatMods(reg)`
      - Applies `StatType.MaxHealth` and `StatType.Defense` (flat), skipping percent mods.
    - Legacy ItemDefinition fallback: adds `def.MaxHealthBonus` and `def.DamageReductionFlat`.
  - Set bonuses: calls `Abyssbound.Loot.SetBonusRuntime.AccumulateActiveSetBonusesForHealth(ref maxHpBonus, ref flatMitigation)`

### G4. HUD display
- Script: `Assets/Game/UI/PlayerStatsHudPanel.cs`
  - In `Refresh()`:
    - `dmgText.text = $"DMG: {combatStats.DamageFinal}"`
    - `hpText.text = $"HP: {playerHealth.CurrentHealth}/{playerHealth.MaxHealth}"`
    - `drText.text = $"DR: {playerHealth.TotalDamageReductionFlat}"`

---

## Notes / Repo Layout Observations (non-functional)
- Duplicate asset/script trees exist under `UnityAIAgent/UnityAIAgent/...` with matching GUIDs for several scripts/assets (example: `DropOnDeath.cs.meta` appears in both trees). This report cites the **project-root** `Assets/...` paths whenever the same GUID exists in multiple locations.
