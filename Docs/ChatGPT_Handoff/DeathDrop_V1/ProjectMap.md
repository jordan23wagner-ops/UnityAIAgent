# DeathDrop_V1 — Project Map (paths)

## Inventory Model
- `Assets/Scripts/Player/PlayerInventory.cs`
- `Assets/Game/Systems/PlayerInventoryResolver.cs`
- Inventory-slot style model: **Not found** (no `InventorySlot` type in runtime scripts; inventory is dictionary-based)

## Item Definitions
### Legacy (used by many systems + asset auth)
- `Assets/Abyss/Items/ItemDefinition.cs`
- `Assets/Abyss/Items/EquipmentSlot.cs`
- `Assets/Abyss/Items/ItemRarity.cs`
- `Assets/Abyss/Items/ItemType.cs`
- `Assets/Abyss/Items/WeaponHandedness.cs`

### Loot V2 (rolled instances)
- `Assets/Scripts/LootSystem/ItemDefinitionSO.cs`
- `Assets/Scripts/LootSystem/ItemInstance.cs`

## Item Database / Registries
- `Assets/Scripts/LootSystem/ItemRegistrySO.cs`
- `Assets/Scripts/LootSystem/RarityRegistrySO.cs`
- `Assets/Scripts/LootSystem/AffixRegistrySO.cs`
- `Assets/Scripts/LootSystem/LootRegistryBootstrapSO.cs`
- `Assets/Scripts/LootSystem/LootRegistryRuntime.cs`

Related authored assets:
- `Assets/Resources/Loot/ItemRegistry.asset`
- `Assets/Resources/Loot/Bootstrap.asset` (expected by `LootRegistryRuntime`, may be created by editor tooling)

## Player Health / Death / Damage
- Player health:
  - `Assets/Game/Player/PlayerHealth.cs`
- Player combat loop (enemy target death tracking):
  - `Assets/Game/Player/CombatLoopController.cs`
- Enemy → player damage path:
  - `Assets/Game/Enemies/EnemyMeleeAttack.cs` (calls `PlayerHealth.TakeDamage(...)`)
- Enemy health/death events (existing death pattern):
  - `Assets/Game/Enemies/EnemyHealth.cs`

## Respawn
- **Not found** (no runtime `.cs` mentioning `Respawn`)

## Death-driven UI (if any)
- Enemy death-driven UI (subscribes to `EnemyHealth` events):
  - `Assets/Game/UI/EnemyHealthBar.cs`
  - `Assets/Game/UI/EnemyHealthBarManager.cs`
  - `Assets/Game/UI/FloatingDamageTextManager.cs`
- Player death UI:
  - **Not found** (no obvious subscriber to player death; `PlayerHealth` does not raise `OnDeath`)

## Drop / Loot Spawn Code
- Legacy enemy drops:
  - `Assets/Scripts/Loot/DropOnDeath.cs`
  - `Assets/Scripts/Loot/WorldLootPickup.cs`
- Loot V2 enemy drops:
  - `Assets/Scripts/LootSystem/LootDropOnDeath.cs`
  - `Assets/Scripts/LootSystem/WorldItemPickup.cs`
- Enemy-side binders (likely wiring):
  - `Assets/Game/Enemies/DropOnDeathBinder.cs`
  - `Assets/Game/Enemies/EnemyLoot.cs`

## Save/Load touching inventory
- **Not found** for inventory.
- Existing PlayerPrefs/JsonUtility persistence (not inventory):
  - `Assets/Scripts/Waypoints/WaypointManager.cs`
  - `Assets/Scripts/Waypoints/WaypointSaveData.cs`
  - `Assets/Scripts/LootSystem/LootQaSettings.cs`

## “Town Scroll” definition (or equivalent)
- Asset:
  - `Assets/GameData/Items/Item_TownScroll.asset` (`itemId: scroll_town`)
- Definition type:
  - `Assets/Abyss/Items/ItemDefinition.cs`
