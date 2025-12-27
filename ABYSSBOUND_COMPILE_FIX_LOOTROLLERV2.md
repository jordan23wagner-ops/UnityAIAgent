# ABYSSBOUND — Compile Fix: LootRollerV2 (CS0019)

## What was wrong
- `LootTableSO.WeightedRarityEntry` is declared as a `struct` (value type) in `Assets/Scripts/LootSystem/LootTableSO.cs`.
- In `Assets/Scripts/LootSystem/LootRollerV2.cs` the rarity-weight override path attempted to do `e != null` inside a lambda:
  - `weights.GetWeight(e != null && e.rarity != null ? e.rarity.id : null)`
- Because `WeightedRarityEntry` is a struct, it can’t be `null`, so the comparison triggers:
  - `CS0019: Operator '!=' cannot be applied to operands of type 'LootTableSO.WeightedRarityEntry' and '<null>'`

## What changed
- Removed the invalid `e != null` check and kept the real nullability check on the reference field:
  - Now uses `e.rarity != null ? e.rarity.id : null`.
- Added a small comment explaining the struct/null pitfall.

## Why this is correct
- Structs are non-nullable by default; the only meaningful “missing” state here is when the entry’s `rarity` reference is `null`.
- The updated logic preserves intent: map each table entry to an override weight using the rarity id (or `null` when no rarity is assigned) without changing selection behavior.
