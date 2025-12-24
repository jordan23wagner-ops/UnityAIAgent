using UnityEngine;

namespace Abyssbound.Loot
{
    [CreateAssetMenu(menuName = "Abyssbound/Loot/QA Selected Item Settings", fileName = "LootQaSelectedItemSettings")]
    public sealed class LootQaSelectedItemSettingsSO : ScriptableObject
    {
        [Header("Selection")]
        [Tooltip("The current QA selected item. Can be an ItemDefinitionSO or (legacy) Abyss.Items.ItemDefinition.")]
        public Object selectedItemDefinition;

        [Tooltip("Fallback used when Selected is null at runtime.")]
        public Object defaultSelectedItemDefinition;

        private const string ResourcesPath = "LootQaSelectedItemSettings";

        private static bool s_WarnedMissingAsset;
        private static bool s_LoggedAutoDefault;

        public static LootQaSelectedItemSettingsSO LoadOrNull()
        {
            var asset = Resources.Load<LootQaSelectedItemSettingsSO>(ResourcesPath);
            if (asset == null && !s_WarnedMissingAsset)
            {
                s_WarnedMissingAsset = true;
                Debug.LogWarning("[LootQA] Missing LootQaSelectedItemSettings asset in Resources. Create via Tools/Abyssbound/QA/Selected Item/Create Settings Asset.");
            }
            return asset;
        }

        public Object GetSelectedOrDefaultRuntime()
        {
            var selected = selectedItemDefinition;
            if (selected != null)
                return selected;

            var fallback = defaultSelectedItemDefinition;
            if (fallback != null)
            {
                if (!s_LoggedAutoDefault)
                {
                    s_LoggedAutoDefault = true;
                    Debug.Log("[LootQA] Selected QA item is null; using DefaultSelectedItemDefinition.");
                }
                return fallback;
            }

            return null;
        }

        public static Object GetSelectedItemOrNull()
        {
            var settings = LoadOrNull();
            return settings != null ? settings.GetSelectedOrDefaultRuntime() : null;
        }
    }
}
