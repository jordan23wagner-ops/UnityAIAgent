# ABYSSBOUND — QUICK CHECK: Loot V2 stackable / material items (Recon)

Date: 2025-12-25

## 1) Does `Abyssbound.Loot.ItemDefinitionSO` or the rolled instance type include stackability fields?

### Loot V2 base definition: `Abyssbound.Loot.ItemDefinitionSO`
- File: `Assets/Scripts/LootSystem/ItemDefinitionSO.cs`
- Type: `Abyssbound.Loot.ItemDefinitionSO : ScriptableObject`
- Fields present (stack-related **NOT FOUND**):
  - Identity: `id`, `displayName`, `icon`
  - Equipment: `slot`, `occupiesSlots`
  - Stats: `baseStats`
  - Affix constraints: `allowedAffixTags`
  - Set: `set`, `setId`
- Stackability fields **NOT FOUND** on this type (no `stackable`, `maxStack`, `quantity`, `count`, etc.).

### Loot V2 rolled instance: `Abyssbound.Loot.ItemInstance`
- File: `Assets/Scripts/LootSystem/ItemInstance.cs`
- Type: `Abyssbound.Loot.ItemInstance`
- Fields present (stack-related **NOT FOUND**):
  - `baseItemId`, `rarityId`, `itemLevel`, `baseScalar`, `affixes`
- Quantity/stack fields **NOT FOUND** on this type.

## 2) If not on `ItemDefinitionSO`, where is stack handling implemented (if anywhere)?

### Primary stacking model: `PlayerInventory` stores counts by itemId key
- File: `Assets/Scripts/Player/PlayerInventory.cs`
- Type: `PlayerInventory : MonoBehaviour`
- Storage:
  - `_items : Dictionary<string,int>`
  - Comment: “Number of distinct stacks/entries (rolled instances count as their own stack).” on `GetStackCount()`
- Stack operations:
  - `Add(string itemId, int amount = 1)` adds `amount` to `_items[itemId]` **unless** the item is treated as non-stackable.
  - `Has(string itemId, int amount = 1)`, `Count(string itemId)`, `TryConsume(string itemId, int amount = 1)`, `TryRemove(string itemId, int amount = 1)`
- Max-stack enforcement **NOT FOUND**: there is no `maxStack` / cap logic inside `Add`.

### Explicit non-stackable policy for Loot V2 rolled items (`ri_...`) and for legacy equippable items
- File: `Assets/Scripts/Player/PlayerInventory.cs`
- Method: `TryAddAsNonStackable(string itemId, int amount)`
- Rules:
  1) Rolled Loot V2 IDs (prefix `"ri_"`) are treated as **non-stackable**.
     - If `amount == 1`: stores `_items[itemId] = 1`
     - If `amount > 1`: clones the rolled instance into multiple unique rolled IDs via:
       - `LootRegistryRuntime.TryGetRolledInstance(itemId, out inst)`
       - `CloneInstance(inst)`
       - `LootRegistryRuntime.RegisterRolledInstance(clone)`
       - Each clone is added as `_items[cloneId] = 1`
  2) Legacy equippable items (resolved via `Abyss.Items.ItemDefinition.equipmentSlot != EquipmentSlot.None`) are also treated as **non-stackable**.
     - Converted into Loot V2 rolled instances:
       - `EnsureLootV2BaseItemRegistered(LootRegistryRuntime registry, ItemDefinition legacy)`
       - `CreateInstanceFromLegacy(ItemDefinition legacy)`
       - `LootRegistryRuntime.RegisterRolledInstance(inst)`
       - Added as `_items[rolledId] = 1`

### Loot V2 registry does not implement stacking
- File: `Assets/Scripts/LootSystem/LootRegistryRuntime.cs`
- Type: `Abyssbound.Loot.LootRegistryRuntime : MonoBehaviour`
- Storage relevant to rolled items:
  - `_instancesByRolledId : Dictionary<string, ItemInstance>`
- Methods:
  - `RegisterRolledInstance(ItemInstance instance, string preferredRolledId = null)` returns a string rolledId (generated as `ri_{Guid}` when not provided).
  - `TryGetRolledInstance(string rolledId, out ItemInstance instance)`
- Quantity/stack fields **NOT FOUND**.

### Pickup & drop paths reinforce “non-stackable rolled instances”
- File: `Assets/Scripts/LootSystem/LootDropOnDeath.cs`
  - When granting to inventory directly: always registers a rolled instance and calls `inv.Add(rolledId, 1)`.
  - Debug log literally emits: `stackable=false merged=false equipable=true`.
- File: `Assets/Scripts/LootSystem/WorldItemPickup.cs`
  - Field: `inventoryCount : int` (serialized)
  - On pickup: registers a rolled instance and calls `inv.Add(rolledId, Mathf.Max(1, inventoryCount))`.
  - With current `PlayerInventory` policy, `inventoryCount > 1` does **not** create a stack; it creates multiple distinct rolled IDs.

## 3) Safest way to represent droppable stackable materials

### Key observation
Today, *stacking exists only at the `PlayerInventory` level as “same string key => higher `int` count”*, and Loot V2 items are generally stored as unique rolled IDs (`ri_...`) which are explicitly treated as non-stackable.

### Option A) Loot V2 item defs (`Abyssbound.Loot.ItemDefinitionSO`)
- Current state: **not stack-friendly**.
- Why:
  - No `stackable/maxStack/quantity` fields on `ItemDefinitionSO` or `ItemInstance`.
  - Inventory policy treats rolled IDs as non-stackable, and the drop/pickup flow always registers rolled instances.
- What would be required (not implemented here): change drop/pickup/inventory rules so materials are stored by a stable base ID (or add explicit stackability metadata to `ItemDefinitionSO` and teach `PlayerInventory.Add` to stack when allowed).

### Option B) Legacy `Abyss.Items.ItemDefinition`
- File: `Assets/Abyss/Items/ItemDefinition.cs`
- Type: `Abyss.Items.ItemDefinition : ScriptableObject`
- Fields relevant to “material-like” items:
  - Identity: `itemId`, `displayName`, `description`, `icon`, `rarity`, `itemType`, `baseValue`
  - Equipment gating: `equipmentSlot`, `weaponHandedness`
- Stackability fields **NOT FOUND** on the legacy definition either.
- However, **the existing inventory supports stacking legacy non-equippables implicitly**:
  - If `equipmentSlot == EquipmentSlot.None` (non-equippable), `PlayerInventory.Add(itemId, amount)` will stack counts into `_items[itemId]`.

### Option C) A separate `MaterialDefinition` system
- Would require new code/data model and integration work.
- Given existing behavior, this is *not* the lowest-risk path unless there are strong upcoming requirements (e.g., per-material metadata, crafting categories, max stack limits, special UI rules) that don’t fit the current `ItemDefinition` model.

## Recommendation

**Use Option B (Legacy `Abyss.Items.ItemDefinition` non-equippable items) for droppable stackable materials, represented in inventory by their `itemId` key.**

Reasoning grounded in existing code:
- The only stacking mechanism currently implemented is `PlayerInventory._items : Dictionary<string,int>` keyed by string id (see `PlayerInventory.Add`, `Count`, `TryConsume`).
- Loot V2’s core identity in inventory is a rolled ID (`ri_...`) registered in `LootRegistryRuntime`, and `PlayerInventory` explicitly prevents these from stacking (it clones them into unique IDs).
- Legacy non-equippables (where `Abyss.Items.ItemDefinition.equipmentSlot == EquipmentSlot.None`) naturally follow the “stack by key” path with no special-case conversion.

### Practical implication
- If you need a “material drop” that stacks today, the safest current representation is: **an inventory key that is NOT a rolled `ri_...` id and is NOT an equippable legacy item id**.
- The cleanest way to accomplish that with existing assets and minimal plumbing is a legacy `ItemDefinition` whose `itemId` is used as the stack key.

## Searches / negative evidence
- Searched Loot V2 scripts (`Assets/Scripts/LootSystem/**/*.cs`) for: `stack`, `stackable`, `maxStack`, `quantity`, `merged`.
- Confirmed “stackable=false” in `LootDropOnDeath` logs and `inventoryCount` behavior in `WorldItemPickup`.
- No explicit max-stack or stackability metadata was found in the inspected definitions.
