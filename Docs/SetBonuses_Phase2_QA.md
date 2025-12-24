# Set Bonuses (Phase 2) QA Checklist

## Preconditions
- Enter **Play Mode**.
- Use the QA menu: **Tools → Abyssbound → QA → Give + Equip Starter Set (Abyssal Initiate) #F5**.

## Expected Set Definition
Set: **Abyssal Initiate** (`setId: AbyssalInitiate`)
- **2pc**: `+1 Defense` (flat DR equivalent)
- **3pc**: `+5 Max Health`

## Required Scenarios

### A) Tooltip / Details display
1. With **0 pieces equipped**: tooltip/details should show the set name and piece list; if the UI shows tiers, they should all be `LOCKED`.
2. Equip **1 piece**: tiers remain `LOCKED`.
3. Equip **2 pieces**:
   - `ACTIVE 2pc: +1 Defense`
   - `LOCKED 3pc: +5 Max Health`
4. Equip **3 pieces**:
   - `ACTIVE 2pc: +1 Defense`
   - `ACTIVE 3pc: +5 Max Health`

Where to verify:
- Hover the item in inventory (tooltip)
- Select the item (inventory details panel)

### B) Stat effects apply/remove cleanly (no stacking drift)
1. Equip 2 pieces → confirm mitigation increases by **+1** (flat).
2. Equip 3 pieces → confirm max health increases by **+5**.
3. Unequip back down to 2 pieces → max health bonus should drop back down (no cumulative growth).
4. Unequip to 0–1 pieces → both set bonuses should be inactive.
5. Repeat equip/unequip loop ~10 times → values should remain stable.

### C) Stable tier keys
Use: **Tools → Abyssbound → QA → Print Active Set Bonus Keys**

Expected keys:
- At 2 pieces: `SetBonus:AbyssalInitiate:2`
- At 3 pieces: `SetBonus:AbyssalInitiate:2` and `SetBonus:AbyssalInitiate:3`

## Notes
- Percent stat mods are currently ignored by the runtime stat recompute (flat-only).
- Set bonuses are evaluated via the equipped set tracker and folded into the same recompute pass as equipment stats.
