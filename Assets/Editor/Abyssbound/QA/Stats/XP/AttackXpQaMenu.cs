#if UNITY_EDITOR
using Abyssbound.Stats;
using Abyssbound.Loot;
using UnityEditor;
using UnityEngine;

namespace Abyssbound.Editor.QA.Stats.XP
{
    public static class AttackXpQaMenu
    {
        private const string Root = "Tools/Abyssbound/QA/Stats/XP/";

        [MenuItem(Root + "Add +50 Attack XP")]
        public static void Add50() => AddXp(50);

        [MenuItem(Root + "Add +200 Attack XP")]
        public static void Add200() => AddXp(200);

        [MenuItem(Root + "Add +200 Strength XP")]
        public static void Add200Strength() => AddXp(StatType.Strength, 200);

        [MenuItem(Root + "Add +200 Defence XP")]
        public static void Add200Defence() => AddXp(StatType.DefenseSkill, 200);

        [MenuItem(Root + "Add +200 Ranged XP")]
        public static void Add200Ranged() => AddXp(StatType.RangedSkill, 200);

        [MenuItem(Root + "Add +200 Magic XP")]
        public static void Add200Magic() => AddXp(StatType.MagicSkill, 200);

        [MenuItem(Root + "Reset Attack XP")]
        public static void ResetXp()
        {
            if (!EnsurePlayMode()) return;
            if (!TryFindStats(out var stats)) return;

            stats.AddAttackXp(-stats.Leveled.attackXp);
            stats.RecalculateAttackLevelFromXp();
            stats.MarkDirty();
            Debug.Log("[QA][XP] Reset AttackXP=0 AttackLevel=1");
        }

        [MenuItem(Root + "Reset ALL Combat XP")]
        public static void ResetAllCombatXp()
        {
            if (!EnsurePlayMode()) return;
            if (!TryFindStats(out var stats)) return;

            stats.AddXp(StatType.Attack, -stats.GetXp(StatType.Attack));
            stats.AddXp(StatType.Strength, -stats.GetXp(StatType.Strength));
            stats.AddXp(StatType.DefenseSkill, -stats.GetXp(StatType.DefenseSkill));
            stats.AddXp(StatType.RangedSkill, -stats.GetXp(StatType.RangedSkill));
            stats.AddXp(StatType.MagicSkill, -stats.GetXp(StatType.MagicSkill));

            stats.MarkDirty();
            Debug.Log("[QA][XP] Reset ALL combat XP (Attack/Strength/Defence/Ranged/Magic) to 0 and levels to 1");
        }

        [MenuItem(Root + "Print Attack XP/Level")]
        public static void Print()
        {
            if (!EnsurePlayMode()) return;
            if (!TryFindStats(out var stats)) return;

            var p = stats.Leveled;
            Debug.Log($"[QA][XP] AttackXP={p.attackXp} AttackLevel={p.attack}");
        }

        [MenuItem(Root + "Print ALL Combat XP/Levels")]
        public static void PrintAll()
        {
            if (!EnsurePlayMode()) return;
            if (!TryFindStats(out var stats)) return;

            Debug.Log(
                "[QA][XP] Combat XP/Levels\n" +
                $"Attack:  XP={stats.GetXp(StatType.Attack)}  Lvl={stats.GetLevel(StatType.Attack)}\n" +
                $"Strength: XP={stats.GetXp(StatType.Strength)}  Lvl={stats.GetLevel(StatType.Strength)}\n" +
                $"Defence:  XP={stats.GetXp(StatType.DefenseSkill)}  Lvl={stats.GetLevel(StatType.DefenseSkill)}\n" +
                $"Ranged:   XP={stats.GetXp(StatType.RangedSkill)}  Lvl={stats.GetLevel(StatType.RangedSkill)}\n" +
                $"Magic:    XP={stats.GetXp(StatType.MagicSkill)}  Lvl={stats.GetLevel(StatType.MagicSkill)}"
            );
        }

        [MenuItem(Root + "Toggle Award XP (On/Off)")]
        public static void ToggleAwardXp()
        {
            XpAwardFlags.AwardAttackXp = !XpAwardFlags.AwardAttackXp;
            Debug.Log($"[QA][XP] AwardAttackXp={(XpAwardFlags.AwardAttackXp ? "ON" : "OFF")}");
        }

        [MenuItem(Root + "Toggle Defence XP From Damage Taken (On/Off)")]
        public static void ToggleDefenceXpFromDamageTaken()
        {
            XpAwardFlags.AwardDefenceXpFromDamageTaken = !XpAwardFlags.AwardDefenceXpFromDamageTaken;
            Debug.Log($"[QA][XP] AwardDefenceXpFromDamageTaken={(XpAwardFlags.AwardDefenceXpFromDamageTaken ? "ON" : "OFF")}");
        }

        [MenuItem(Root + "Toggle XP Floating Text (On/Off)")]
        public static void ToggleXpFloatingText()
        {
            XpFloatingTextFlags.ShowXpFloatingText = !XpFloatingTextFlags.ShowXpFloatingText;
            Debug.Log($"[QA][XP] ShowXpFloatingText={(XpFloatingTextFlags.ShowXpFloatingText ? "ON" : "OFF")}");
        }

        private static void AddXp(int amount)
        {
            if (!EnsurePlayMode()) return;
            if (!TryFindStats(out var stats)) return;

            stats.AddAttackXp(amount);
            var p = stats.Leveled;
            Debug.Log($"[QA][XP] Added {amount} AttackXP => AttackXP={p.attackXp} AttackLevel={p.attack}");
        }

        private static void AddXp(StatType stat, int amount)
        {
            if (!EnsurePlayMode()) return;
            if (!TryFindStats(out var stats)) return;

            stats.AddXp(stat, amount);
            Debug.Log($"[QA][XP] Added {amount} {StatTypeCanonical.ToCanonicalPrimaryName(stat)}XP => XP={stats.GetXp(stat)} Level={stats.GetLevel(stat)}");
        }

        private static bool EnsurePlayMode()
        {
            if (Application.isPlaying)
                return true;

            Debug.LogWarning("[QA][XP] Enter Play Mode first.");
            return false;
        }

        private static bool TryFindStats(out PlayerStatsRuntime stats)
        {
            stats = null;
            try
            {
#if UNITY_2023_1_OR_NEWER
                stats = Object.FindAnyObjectByType<PlayerStatsRuntime>(FindObjectsInactive.Exclude);
#else
                stats = Object.FindObjectOfType<PlayerStatsRuntime>();
#endif
            }
            catch { stats = null; }

            if (stats != null)
                return true;

            // Fallback: if combat stats exists, it will auto-add PlayerStatsRuntime.
            PlayerCombatStats combat = null;
            try
            {
#if UNITY_2023_1_OR_NEWER
                combat = Object.FindAnyObjectByType<PlayerCombatStats>(FindObjectsInactive.Exclude);
#else
                combat = Object.FindObjectOfType<PlayerCombatStats>();
#endif
            }
            catch { combat = null; }

            if (combat != null)
            {
                try { stats = combat.GetComponent<PlayerStatsRuntime>(); } catch { stats = null; }
                if (stats == null)
                {
                    try { stats = combat.gameObject.AddComponent<PlayerStatsRuntime>(); } catch { stats = null; }
                }
            }

            if (stats != null)
                return true;

            Debug.LogError("[QA][XP] No PlayerStatsRuntime found.");
            return false;
        }
    }
}
#endif
