# DeathDrop_V1 — Handoff README

## Unity Version
- Unity `6000.3.0f1` (from `ProjectSettings/ProjectVersion.txt`)

## Scenes Used For Testing (current repo state)
- `Assets/Scenes/Abyssbound_Zone1.unity`
- `Assets/Scenes/SampleScene.unity`
- `Assets/Test.unity`

## Player Object (name / tag / death-relevant components)
### Scene instance
- **Scene object name:** `Player_Hero`
- **Scene tag:** `Player` (explicitly overridden in `Assets/Scenes/Abyssbound_Zone1.unity` via prefab modification)
- **Prefab source:** `Assets/Prefabs/Player/Player.prefab` (prefab root name `Player`, prefab tag `Untagged`)

### Components seen on `Player_Hero` in `Assets/Scenes/Abyssbound_Zone1.unity`
(Components most relevant to “death” / damage / inventory)
- `PlayerHealth` (health + damage intake)
- `PlayerInventory` (authoritative inventory container)
- `Rigidbody` (kinematic in scene)
- `SimplePlayerCombat`, `PlayerCombatStats` (combat stats/attack)
- `PlayerInteraction` (interactions; not death-specific, but common gameplay root component)

## Inventory System Summary
### Where inventory lives
- Inventory is a `MonoBehaviour` component: `PlayerInventory` on the player object (usually resolved via `GameObject.Find("Player_Hero")`).
- Lookup is centralized in `Game.Systems.PlayerInventoryResolver.GetOrFind()`.

### How items are added
- Primary API: `PlayerInventory.Add(string itemId, int amount = 1)`.
- Sources that add items:
  - World pickups:
    - Loot V2 rolled instances: `WorldItemPickup.TryPickup()` registers a rolled instance in `Abyssbound.Loot.LootRegistryRuntime` then calls `PlayerInventory.Add(rolledId, count)`.
    - Legacy item pickups: `WorldLootPickup.TryPickup()` calls `PlayerInventory.Add(itemDefinition.itemId, count)`.
  - Enemy death loot:
    - Legacy: `DropOnDeath.OnDeath()` → grants items directly (or spawns `WorldLootPickup` if configured).
    - Loot V2: `LootDropOnDeath.OnEnemyDeath()` → spawns `WorldItemPickup` or grants rolled ids directly into inventory.

### How items are removed
- Primary API: `PlayerInventory.TryConsume(string itemId, int amount = 1)` (alias: `TryRemove`).
- Storage model: `_items` is a dictionary of `itemId -> count`.
- Special-case policy:
  - Rolled instance ids prefixed `ri_` are treated as non-stackable.
  - Legacy equippable items are converted into rolled instances so each copy is unique (non-stackable).

## Existing Death / Respawn Handling
### Player death
- `PlayerHealth` defines `IsDead` and clamps health to 0, but **does not expose an `OnDeath` event** and does not call any respawn/death handler when `currentHealth` reaches 0.
- No explicit respawn system found in code (`Respawn` search returned no hits in `.cs`).

### Enemy death (existing pattern)
- `EnemyHealth` is authoritative and raises events (`OnDeath`, `Died`, `OnDied` UnityEvent).
- `EnemyHealth.Die()` calls `DropOnDeath.OnDeath()` if present.

Implication for DeathDrop_V1:
- Player-side death drop likely needs a new hook point (e.g., add an `OnDeath` event to `PlayerHealth` or a separate `PlayerDeathController` that polls `IsDead`).

## Item Value System (“most valuable”)
### Legacy items (`Abyss.Items.ItemDefinition`)
- Has `baseValue` (int) and `rarity` fields.
- Town Scroll example: `Assets/GameData/Items/Item_TownScroll.asset` has `baseValue: 25`.

### Loot V2 items (`Abyssbound.Loot.ItemDefinitionSO` + rolled `ItemInstance`)
- `ItemDefinitionSO` does **not** include an explicit value field.
- Rolled instances (`ItemInstance`) contain `baseItemId`, `rarityId`, `itemLevel`, `affixes`, but no explicit “gold value”.

If DeathDrop_V1 needs “most valuable” across both systems:
- Legacy: can use `ItemDefinition.baseValue` (and/or rarity).
- Loot V2: would need a new heuristic (e.g., rarity weight + itemLevel + sum of stat mods) or a new explicit value field.

## Missing / Not Found
- Player respawn system (no `Respawn` hits in runtime `.cs`).
- Player death event/callback (no `PlayerHealth.OnDeath` equivalent found).
- Inventory persistence save/load (PlayerPrefs/JsonUtility usage found for Waypoints + Loot QA, not for inventory).
- `InventorySlot` model class (inventory is dictionary-based in `PlayerInventory`).

## Snippets Exported
Files are copied into `Docs/ChatGPT_Handoff/DeathDrop_V1/Snippets/`:
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
