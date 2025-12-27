# ABYSSBOUND — Combat XP + Leveling (QA)

## What’s implemented (v1)
- OSRS-style combat XP based on **damage dealt** and **damage taken**.
- Skills supported:
  - Attack (accuracy)
  - Strength (melee damage)
  - Defence (by taking damage)
  - Ranged
  - Magic
- Flat leveling curve: **100 XP per level**.
- XP is awarded only when damage is **> 0**.

## Single source of truth (tuning)
Configured in:
- [Assets/Game/Player/Stats/CombatXpTuning.cs](Assets/Game/Player/Stats/CombatXpTuning.cs)

Defaults:
- `StyleXpPerDamage = 4` (Strength / Ranged / Magic)
- `AttackXpPerDamage = 2` (Attack)
- `DefenceXpPerDamage = 2`
- `DefenceXpMaxPerSecond = 40`
- `DefenceXpBatchWindowSeconds = 0.25`
- `TrashXpMult = 1.00`
- `EliteXpMult = 1.10`
- `BossXpMult = 1.25`
- `XpPerLevel = 100`

Tier source:
- Enemy tier is read from `EnemyCombatProfile.tier` (string: `Trash` / `Elite` / `Boss`).
- XP multiplier resolution lives in:
  - [Assets/Game/Combat/EnemyTierResolver.cs](Assets/Game/Combat/EnemyTierResolver.cs)

Tier multiplier table:
- Trash: `1.00x`
- Elite: `1.10x`
- Boss: `1.25x`

## XP awarding rules
### Damage dealt (successful hit)
On a successful hit where `finalDamage > 0`:
- Compute base damage XP:
  - `AttackXP_base = finalDamage * AttackXpPerDamage`
  - `StyleXP_base = finalDamage * StyleXpPerDamage`
- Apply tier multiplier and floor:
  - `AttackXP += floor(AttackXP_base * tierMult)`
  - `StyleXP += floor(StyleXP_base * tierMult)`
- Award **Style XP** based on attack type:
  - Melee → `StrengthXP += finalDamage * StyleXpPerDamage`
  - Ranged → `RangedXP += finalDamage * StyleXpPerDamage`
  - Magic → `MagicXP += finalDamage * StyleXpPerDamage`

Miss or `finalDamage <= 0`:
- Award **0 XP**.

### Damage taken (player)
When the player takes `D_taken > 0`:
- Convert damage to XP and accumulate in a batch.
- If the attacker is known (enemy melee attacks), use the attacker’s tier multiplier; otherwise default to Trash (`1.00x`).
- Every `DefenceXpBatchWindowSeconds`, award:
  - `DefenceXP += floor(damageBatchXp * tierMult)`
  - Clamped so the total awarded per second does not exceed `DefenceXpMaxPerSecond`.

## Where XP is stored
- Stored on the progression struct:
  - [Assets/Game/Player/Stats/PlayerLeveledStats.cs](Assets/Game/Player/Stats/PlayerLeveledStats.cs)
    - `attackXp`, `strengthXp`, `defenceXp`, `rangedXp`, `magicXp`
- XP/level logic lives in:
  - [Assets/Game/Player/Stats/PlayerStatsRuntime.cs](Assets/Game/Player/Stats/PlayerStatsRuntime.cs)

Saving to disk is intentionally **not** implemented yet.

## Floating XP text
- Damage dealt: one combined green line per hit:
  - `+{AttackXP} Attack XP  |  +{StyleXP} {Skill} XP`
- Defence XP (damage taken): one green line per batch window:
  - `+{amount} Defence XP`
- Level-up: `"{Skill} Level {newLevel}!"` spawns above all other combat text.

## QA menu paths
### Stats/XP tools
Under `Tools/Abyssbound/QA/Stats/XP/`:
- Add +200 Attack XP
- Add +200 Strength XP
- Add +200 Defence XP
- Add +200 Ranged XP
- Add +200 Magic XP
- Reset ALL Combat XP
- Print ALL Combat XP/Levels
- Toggle Award XP (On/Off)
- Toggle Defence XP From Damage Taken (On/Off)
- Toggle XP Floating Text (On/Off)

### Combat tools
Under `Tools/Abyssbound/QA/Combat/`:
- Spawn 1 Test Enemy (Trash)
- Toggle Always Hit (On/Off)
- Toggle Defence XP From Damage Taken (On/Off)

## Acceptance quick checks
- Deal 5 melee damage → `+10 Attack XP` and `+20 Strength XP`.
- Deal 5 ranged damage → `+10 Attack XP` and `+20 Ranged XP`.
- Deal 5 magic damage → `+10 Attack XP` and `+20 Magic XP`.
- Take 5 damage over multiple hits → Defence XP increases (capped per second).
- Miss → no XP.