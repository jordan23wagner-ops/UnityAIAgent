// QA checklist:
// - Use Bag Upgrade (T1): max inventory slots increases by +2 and persists.
// - Cap works: once at 24, using another bag upgrade does not consume and warns once.
// - Monster drop can occur (config + threat-tier mapping).
// - Fishing drop is rare and never rolls T5.
// - No inventory UI regressions: grid still behaves, no tab logic changes.

using System;
using Abyssbound.Loot;
using UnityEngine;

namespace Abyssbound.BagUpgrades
{
    public static class BagUpgradeDropRuntime
    {
        private const string ResourcesPath = "BagUpgrades/BagUpgradeDropConfig";
        private const string AssetPath = "Assets/Resources/BagUpgrades/BagUpgradeDropConfig.asset";

        private static BagUpgradeDropConfigSO _cfg;

        public static BagUpgradeDropConfigSO GetConfig()
        {
            if (_cfg != null)
                return _cfg;

            try { _cfg = Resources.Load<BagUpgradeDropConfigSO>(ResourcesPath); } catch { _cfg = null; }

#if UNITY_EDITOR
            if (_cfg == null)
            {
                try
                {
                    TryEnsureConfigAssetInEditor();
                    _cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<BagUpgradeDropConfigSO>(AssetPath);
                }
                catch { _cfg = null; }
            }
#endif

            return _cfg;
        }

#if UNITY_EDITOR
        private static void TryEnsureConfigAssetInEditor()
        {
            try
            {
                var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<BagUpgradeDropConfigSO>(AssetPath);
                if (existing != null)
                    return;

                EnsureEditorFolder("Assets/Resources");
                EnsureEditorFolder("Assets/Resources/BagUpgrades");

                var so = ScriptableObject.CreateInstance<BagUpgradeDropConfigSO>();
                UnityEditor.AssetDatabase.CreateAsset(so, AssetPath);
                UnityEditor.EditorUtility.SetDirty(so);
                UnityEditor.AssetDatabase.SaveAssets();
                UnityEditor.AssetDatabase.Refresh();
            }
            catch
            {
                // Intentionally swallow: runtime should just return null if not available.
            }
        }

        private static void EnsureEditorFolder(string path)
        {
            if (UnityEditor.AssetDatabase.IsValidFolder(path))
                return;

            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = System.IO.Path.GetFileName(path);

            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
                return;

            if (!UnityEditor.AssetDatabase.IsValidFolder(parent))
                EnsureEditorFolder(parent);

            if (!UnityEditor.AssetDatabase.IsValidFolder(path))
                UnityEditor.AssetDatabase.CreateFolder(parent, name);
        }
#endif

        public static string TryRollMonsterDropBaseId(float threat)
        {
            var cfg = GetConfig();
            if (cfg == null || !cfg.enableMonsterDrops)
            return null;

            int tier = ResolveTierForThreat(threat);
            if (tier <= 0)
                return null;

            float chance = Mathf.Clamp01(cfg.GetMonsterChanceForTier(tier));
            if (chance <= 0f)
                return null;

            if (UnityEngine.Random.value >= chance)
                return null;

            string id = BagUpgradeIds.GetIdForTier(tier);
            if (string.IsNullOrWhiteSpace(id))
                return null;

            return id;
        }

        public static ItemInstance TryRollMonsterDropAsInstance(float threat)
        {
            var id = TryRollMonsterDropBaseId(threat);
            if (string.IsNullOrWhiteSpace(id))
                return null;

            return new ItemInstance
            {
                baseItemId = id,
                rarityId = "Common",
                itemLevel = 1,
                baseScalar = 1f,
                affixes = new()
            };
        }

        public static string TryRollFishingDropBaseId()
        {
            var cfg = GetConfig();
            if (cfg == null || !cfg.enableFishingDrops)
                return null;

            float any = Mathf.Clamp01(cfg.fishingAnyChance);
            if (any <= 0f)
                return null;

            if (UnityEngine.Random.value >= any)
                return null;

            // Weighted tier selection (T1-T4 only). T5 must never drop from skilling.
            float w1 = Mathf.Max(0f, cfg.fishingWeightT1);
            float w2 = Mathf.Max(0f, cfg.fishingWeightT2);
            float w3 = Mathf.Max(0f, cfg.fishingWeightT3);
            float w4 = Mathf.Max(0f, cfg.fishingWeightT4);

            float sum = w1 + w2 + w3 + w4;
            if (sum <= 0.0001f)
                return BagUpgradeIds.BagUpgradeT1;

            float r = UnityEngine.Random.value * sum;
            if (r < w4) return BagUpgradeIds.BagUpgradeT4;
            r -= w4;
            if (r < w3) return BagUpgradeIds.BagUpgradeT3;
            r -= w3;
            if (r < w2) return BagUpgradeIds.BagUpgradeT2;
            return BagUpgradeIds.BagUpgradeT1;
        }

        private static int ResolveTierForThreat(float threat)
        {
            // ThreatService quantizes to 0.5 steps; map to tier bands.
            if (threat <= 1.5f) return 1;
            if (threat >= 2.0f && threat <= 2.5f) return 2;
            if (threat >= 3.0f && threat <= 3.5f) return 3;
            if (threat >= 4.0f && threat <= 4.5f) return 4;
            if (threat >= 5.0f) return 5;
            return 0;
        }

        public static bool IsBagUpgradeInstance(ItemInstance inst)
        {
            if (inst == null) return false;
            try { return BagUpgradeIds.IsBagUpgradeBaseId(inst.baseItemId); } catch { return false; }
        }
    }
}
