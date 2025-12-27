# ABYSSBOUND — Stats Visibility + QA

## HUD: where stats come from
- The HUD primary stats readout is implemented in [Assets/Game/UI/PlayerStatsHudPanel.cs](Assets/Game/UI/PlayerStatsHudPanel.cs).
- It shows **Leveled (progression) stats only** (Option A), plus HP/DMG/DR from the runtime combat/health components.
  - Runtime component: [Assets/Game/Player/Stats/PlayerStatsRuntime.cs](Assets/Game/Player/Stats/PlayerStatsRuntime.cs)
  - Leveled model: [Assets/Game/Player/Stats/PlayerLeveledStats.cs](Assets/Game/Player/Stats/PlayerLeveledStats.cs)

Important rules:
- HUD does **not** read derived stats.
- HUD does **not** change gameplay balance.

Refresh behavior:
- Uses existing `PlayerEquipment.Changed` event when available.
- Also polls safely at 0.25s intervals as a fallback.

## QA menu paths
Stat tools are under:
- `Tools/Abyssbound/QA/Stats/`

Combat tools are under:
- `Tools/Abyssbound/QA/Combat/`

### Print tools
- `Tools/Abyssbound/QA/Stats/Print Leveled Stats`
- `Tools/Abyssbound/QA/Stats/Print GearBonus Stats`
- `Tools/Abyssbound/QA/Stats/Print TotalPrimary Stats`
- `Tools/Abyssbound/QA/Stats/Print Derived Stats`
  - Logs derived totals and the active calculator mode string.

### Combat QA tools
- `Tools/Abyssbound/QA/Combat/Toggle Always Hit (On/Off)`
- `Tools/Abyssbound/QA/Combat/Spawn 1 Test Enemy (Trash)`

### Stat test kits (Give + Equip)
Each kit:
- Creates a **Loot V2** base item at runtime, registers it in `LootRegistryRuntime`, creates a rolled instance (`ri_...`),
  adds it to `PlayerInventory`, then equips it via `PlayerEquipment.TryEquipFromInventory`.
- This ensures the same **rolled instance id** pathway is used as normal loot.
- Kits are removable by unequipping (they return to inventory).

Menu items:
- `Tools/Abyssbound/QA/Stats/Give + Equip Stat Test Kit (Attack)`
- `Tools/Abyssbound/QA/Stats/Give + Equip Stat Test Kit (Strength)`
- `Tools/Abyssbound/QA/Stats/Give + Equip Stat Test Kit (Defence)`
- `Tools/Abyssbound/QA/Stats/Give + Equip Stat Test Kit (Ranged)`
- `Tools/Abyssbound/QA/Stats/Give + Equip Stat Test Kit (Magic)`
- `Tools/Abyssbound/QA/Stats/Give + Equip Stat Test Kit (Fishing)`
- `Tools/Abyssbound/QA/Stats/Give + Equip Stat Test Kit (Cooking)`

## Expected stat changes (per kit)
All kits currently apply **+5** to exactly one **primary** stat.

- Attack kit: `Attack +5` (two-handed; occupies both hands)
- Strength kit: `Strength +5` (Helm slot)
- Defence kit: `Defence +5` (Chest slot; implemented as `DefenseSkill +5`)
- Ranged kit: `Ranged +5` (Legs slot; implemented as `RangedSkill +5`)
- Magic kit: `Magic +5` (Belt slot; implemented as `MagicSkill +5`)
- Fishing kit: `Fishing +5` (Gloves slot)
- Cooking kit: `Cooking +5` (Boots slot)

Notes:
- If `Resources/Loot/Bootstrap.asset` is missing, the QA tools will log an error instructing you to run:
  `Tools/Abyssbound/Loot/Create Starter Loot Content.`

## Accuracy QA (Option A)

### Formula + constants
- Source of truth: [Assets/Game/Player/Stats/StatCalculator.cs](Assets/Game/Player/Stats/StatCalculator.cs)
- $$\text{hitChance} = \text{clamp}(0.05, 0.95, 0.60 + (\text{Attack} - \text{EnemyDefence}) \cdot 0.03)$$

### EnemyDefence sourcing
- Primary: an enemy component implementing `Abyssbound.Combat.IEnemyDefenceProvider`.
- Fallback: [Assets/Game/Enemies/EnemyCombatProfile.cs](Assets/Game/Enemies/EnemyCombatProfile.cs) (auto-added at runtime by [Assets/Game/Enemies/EnemyHealth.cs](Assets/Game/Enemies/EnemyHealth.cs)).
- Last resort: assume `EnemyDefence = 1` with a single warning.

### Verification steps
1) Spawn a test enemy: `Tools/Abyssbound/QA/Combat/Spawn 1 Test Enemy (Trash)`.
2) With no gear (Attack=1): you should see some "Miss" results over multiple swings.
3) Equip Attack kit (+5): misses should noticeably decrease.
4) Equip Strength kit (+5): damage per hit increases; miss rate should be unchanged.
5) Toggle Always Hit ON: no misses regardless of Attack.
