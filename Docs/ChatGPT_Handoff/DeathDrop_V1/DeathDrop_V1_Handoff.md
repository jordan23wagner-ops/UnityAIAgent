# DeathDrop_V1 — Single-file Handoff

This file combines the README + Project Map for quick ChatGPT attachment.

---

## README

### Unity Version
- Unity `6000.3.0f1` (from `ProjectSettings/ProjectVersion.txt`)

### Scenes Used For Testing (current repo state)
- `Assets/Scenes/Abyssbound_Zone1.unity`
- `Assets/Scenes/SampleScene.unity`
- `Assets/Test.unity`

### Player Object (name / tag / death-relevant components)
- **Scene object name:** `Player_Hero`
- **Scene tag:** `Player` (overridden in `Assets/Scenes/Abyssbound_Zone1.unity`)
- **Prefab source:** `Assets/Prefabs/Player/Player.prefab` (prefab root name `Player`, prefab tag `Untagged`)

Components seen on `Player_Hero` in `Assets/Scenes/Abyssbound_Zone1.unity` (death-relevant subset):
- `PlayerHealth`
- `PlayerInventory`
- `Rigidbody` (kinematic)
- `SimplePlayerCombat`, `PlayerCombatStats`
- `PlayerInteraction`

### Inventory System Summary
- Inventory lives on-player as `PlayerInventory` and is resolved via `Game.Systems.PlayerInventoryResolver.GetOrFind()`.
- Storage is dictionary-based: `itemId -> count`.
- Add: `PlayerInventory.Add(itemId, amount)`
- Remove: `PlayerInventory.TryConsume(itemId, amount)` / `TryRemove`
- Rolled instances: `ri_...` IDs are non-stackable and reference `Abyssbound.Loot.ItemInstance` stored in `Abyssbound.Loot.LootRegistryRuntime`.

### Existing Death / Respawn Handling
- Player: `PlayerHealth` sets `IsDead` when health reaches 0, but has no `OnDeath` event and no respawn logic found.
- Enemies: `EnemyHealth.Die()` fires `OnDeath` and calls `DropOnDeath.OnDeath()` if present.

### Item Value System (“most valuable”)
- Legacy `Abyss.Items.ItemDefinition` includes `baseValue` and `rarity`.
- Loot V2 `ItemDefinitionSO` / `ItemInstance` do **not** include an explicit value field; “most valuable” heuristic is not currently implemented.

### Missing / Not Found
- Player respawn system.
- Player death callback/event.
- Inventory save/load persistence.
- InventorySlot-style model class.

---

## Project Map (paths)

### Inventory Model
- `Assets/Scripts/Player/PlayerInventory.cs`
- `Assets/Game/Systems/PlayerInventoryResolver.cs`

### Item Definitions
- Legacy: `Assets/Abyss/Items/ItemDefinition.cs`
- Loot V2: `Assets/Scripts/LootSystem/ItemDefinitionSO.cs`
- Rolled instance data: `Assets/Scripts/LootSystem/ItemInstance.cs`

### Item Database / Registries
- `Assets/Scripts/LootSystem/ItemRegistrySO.cs`
- `Assets/Scripts/LootSystem/RarityRegistrySO.cs`
- `Assets/Scripts/LootSystem/AffixRegistrySO.cs`
- `Assets/Scripts/LootSystem/LootRegistryBootstrapSO.cs`
- `Assets/Scripts/LootSystem/LootRegistryRuntime.cs`

### Player Health / Death / Damage
- `Assets/Game/Player/PlayerHealth.cs`
- `Assets/Game/Enemies/EnemyMeleeAttack.cs`
- `Assets/Game/Enemies/EnemyHealth.cs`

### Respawn
- Not found.

### Death-driven UI
- `Assets/Game/UI/EnemyHealthBar.cs`
- `Assets/Game/UI/EnemyHealthBarManager.cs`
- `Assets/Game/UI/FloatingDamageTextManager.cs`

### Drop / Loot Spawn
- `Assets/Scripts/Loot/DropOnDeath.cs`
- `Assets/Scripts/Loot/WorldLootPickup.cs`
- `Assets/Scripts/LootSystem/LootDropOnDeath.cs`
- `Assets/Scripts/LootSystem/WorldItemPickup.cs`

### Save/Load touching inventory
- Not found.

### Town Scroll
- `Assets/GameData/Items/Item_TownScroll.asset` (`itemId: scroll_town`)

---

## Snippet Copies
Snippets are exported into `Docs/ChatGPT_Handoff/DeathDrop_V1/Snippets/` as `.txt` copies of current scripts.

- `Snippets/PlayerHealth.cs.txt`
- `Snippets/PlayerInventory.cs.txt`
- `Snippets/PlayerInventoryResolver.cs.txt`
- `Snippets/ItemDefinition.cs.txt`
- `Snippets/ItemDefinitionSO.cs.txt`
- `Snippets/ItemInstance.cs.txt`
- `Snippets/DropOnDeath.cs.txt`
- `Snippets/LootDropOnDeath.cs.txt`
- `Snippets/WorldLootPickup.cs.txt`
- `Snippets/WorldItemPickup.cs.txt`
- `Snippets/EnemyHealth.cs.txt`
- `Snippets/EnemyMeleeAttack.cs.txt`
- `Snippets/CombatLoopController.cs.txt`
