# ABYSSBOUND — Stats Model (Primary + Derived)

## Goals (this doc)
- Define the canonical OSRS-style **primary stats** list used by gear/affixes/sets.
- Define a **derived stats** list (DMG / HP / DR / etc.) computed from primaries.
- Establish rules for **where modifiers apply** and where **percent stacking** will be implemented.

Non-goals for this step:
- No UI changes.
- No combat behavior changes.
- No loot rolling changes.
- No set bonus value changes.

---

## 1) Canonical Primary Stats

These are the only stats that gear/affixes/sets should modify directly.

### Combat (primary)
- Attack
- Strength
- Defence
- Ranged
- Magic

### Skilling (primary)
- Alchemy
- Mining
- Woodcutting
- Forging
- Fishing
- Cooking

### Implementation notes (current code)
- Canonical primary stat type is represented by `Abyssbound.Loot.StatType`.
- Existing project naming is preserved to avoid breaking references:
  - Canonical “Defence” maps to `StatType.DefenseSkill`
  - Canonical “Ranged” maps to `StatType.RangedSkill`
  - Canonical “Magic” maps to `StatType.MagicSkill`
- Canonical mapping helpers live in `StatTypeCanonical` (same file as `StatType`).

---

## 2) Derived Stats

Derived stats are **computed from primary stats** (and tunable constants). They are not modified directly by gear/affixes/sets.

### Derived list (initial)
- **DMG** (outgoing damage scalar / bonus)
- **HP** (max health)
- **DR** (damage reduction)
- **Attack Speed**
- **Move Speed**

## Derived stats (runtime outputs)

### Option A minimal combat integration (Strength → Melee Damage)

Strength now contributes deterministically to melee damage (still no accuracy/miss/crit system).

- Tunable constant (single source of truth): `StatCalculator.kStrengthToMeleeDamage = 0.5f`
- Formula (uses **TotalPrimary** Strength = Leveled + GearBonus):

  $$\text{FinalMeleeDamage} = \text{BaseDamage} + \text{EquipMeleeDamageBonus} + \left\lfloor(\text{TotalStrength} - 1) \cdot k\right\rfloor$$

Notes:
- Strength 1 adds 0 damage.
- Existing `MeleeDamage` item bonuses continue to work and are added as `EquipMeleeDamageBonus`.

### Option A minimal combat integration (Attack → Accuracy)

- Tunable constants (single source of truth):
  - `StatCalculator.baseHitChance = 0.60f`
  - `StatCalculator.kAttackToHitChance = 0.03f`
  - `StatCalculator.minHitChance = 0.05f`
  - `StatCalculator.maxHitChance = 0.95f`

- Formula (uses **TotalPrimary** Attack):

  $$\text{hitChance} = \text{clamp}(0.05, 0.95, 0.60 + (\text{Attack} - \text{EnemyDefence}) \cdot 0.03)$$

EnemyDefence sourcing:
- Prefer an explicit enemy stats component implementing `Abyssbound.Combat.IEnemyDefenceProvider`.
- Fallback: `EnemyCombatProfile.defenceLevel` (auto-added at runtime by `EnemyHealth` if missing).

### Placeholder formulas (tunable; NOT implemented here)

All formulas below are intentionally placeholders. Constants live in a future tuning asset/code location (see section 4).

Let:
- $A$ = Attack
- $S$ = Strength
- $D$ = Defence
- $R$ = Ranged
- $M$ = Magic

Example placeholder computation plan:
- **DMG (melee)**: $DMG_{melee} = DMG_{base} + k_S \cdot S + k_A \cdot A$
- **DMG (ranged)**: $DMG_{ranged} = DMG_{base} + k_R \cdot R$
- **DMG (magic)**: $DMG_{magic} = DMG_{base} + k_M \cdot M$
- **HP**: $HP_{max} = HP_{base} + k_{HP} \cdot D$
- **DR (flat or %)**: 
  - Flat placeholder: $DR_{flat} = k_{DR} \cdot D$
  - Percent placeholder: $DR_{pct} = clamp(0, DR_{cap}, k'_{DR} \cdot D)$
- **Attack Speed**: $AS = AS_{base} + k_{AS} \cdot A$ (or a separate stat source)
- **Move Speed**: $MS = MS_{base} + k_{MS} \cdot D$ (or a separate stat source)

Notes:
- The game can choose one of the DR models (flat vs percent) later; both are listed as placeholders.
- If the project keeps “Attack Speed” and “Move Speed” as direct mods, they are treated as derived outputs even if their sources are “primary modifiers.”

---

## 3) Rule: Where Modifiers Apply

**Hard rule (target model):**
- Gear / affixes / sets modify **PRIMARY stats only**.
- Derived stats (DMG / HP / DR / etc.) are **computed** from primaries at runtime.

This enables:
- Consistent scaling across systems (loot, sets, progression).
- UI and combat to share a single derived-stat computation.
- Cleaner balancing by tuning constants instead of per-item derived modifiers.

Compatibility note:
- The current project already contains some direct modifiers (e.g., damage / max health / defense flat). This doc defines the desired final model; migrating legacy derived modifiers to primary-only is a separate step.

---

## 4) Percent Modifiers / Stacking (placeholder)

**Status:** Percent-mod stacking is **not enabled until defined**.

### Where it will live
- Data shape already exists in `StatMod` (`percent` boolean).
- The stacking rule should live in a single canonical place, ideally:
  - A new derived-stats evaluation module (future), or
  - A central `StatAccumulator`/`DerivedStatsCalculator` utility.

### Placeholder rule (to be decided)
Pick one (examples):
- **Additive percent:** total = base * (1 + p1 + p2 + ...)
- **Multiplicative percent:** total = base * (1 + p1) * (1 + p2) * ...
- **Hybrid:** additive within a category, multiplicative across categories

Until defined:
- Percent mods should be treated as ignored/unsupported by core runtime calculators.

---

## 5) Files

- Enum + canonical mapping: [Assets/Scripts/LootSystem/StatType.cs](Assets/Scripts/LootSystem/StatType.cs)
