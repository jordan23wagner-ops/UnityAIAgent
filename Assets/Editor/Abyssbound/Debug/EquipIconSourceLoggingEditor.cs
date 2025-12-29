#if UNITY_EDITOR
using System;
using Abyss.Equipment;
using Abyss.Items;
using UnityEditor;
using UnityEngine;

// Legacy QA tools are gated behind a compile define.
// Enable via: Project Settings > Player > Scripting Define Symbols: ABYSS_LEGACY_QA_TOOLS

namespace Abyssbound.EditorTools
{
    public static class EquipIconSourceLoggingEditor
    {
        #if ABYSS_LEGACY_QA_TOOLS
        // Old menu: Tools/Abyssbound/Debug/Log Equipped Item Icon Sources
        [MenuItem("Tools/Legacy QA/Abyssbound/Debug/Log Equipped Item Icon Sources")]
        private static void LogEquippedItemIconSources()
        {
            // FindObjectOfType<T>() is obsolete in newer Unity versions; we only need any instance.
            var equipment = UnityEngine.Object.FindAnyObjectByType<PlayerEquipment>();
            if (equipment == null)
            {
                UnityEngine.Debug.LogWarning("[EquipIconSrc] No PlayerEquipment found in scene.");
                return;
            }

            var reg = Abyssbound.Loot.LootRegistryRuntime.GetOrCreate();

            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
            {
                if (slot == EquipmentSlot.None)
                    continue;

                string itemId = null;
                try { itemId = equipment.Get(slot); } catch { itemId = null; }

                if (string.IsNullOrWhiteSpace(itemId))
                    continue;

                Abyssbound.Loot.ItemDefinitionSO baseItem = null;
                Sprite icon = null;
                string displayName = "";

                try
                {
                    if (reg != null)
                    {
                        if (reg.TryGetRolledInstance(itemId, out var inst) && inst != null)
                        {
                            reg.TryGetItem(inst.baseItemId, out baseItem);
                        }
                        else
                        {
                            // Some equipped ids may already be base item ids.
                            reg.TryGetItem(itemId, out baseItem);
                        }
                    }
                }
                catch
                {
                    baseItem = null;
                }

                if (baseItem != null)
                {
                    displayName = string.IsNullOrWhiteSpace(baseItem.displayName) ? baseItem.id : baseItem.displayName;
                    icon = baseItem.icon;
                }

                var baseSoPath = baseItem != null ? AssetDatabase.GetAssetPath(baseItem) : "";
                var iconName = icon != null ? icon.name : "<null>";
                var iconPath = icon != null ? AssetDatabase.GetAssetPath(icon) : "";

                UnityEngine.Debug.Log($"[EquipIconSrc] equippedSlot={slot} itemId='{itemId}' dn='{displayName}' icon='{iconName}' iconPath='{iconPath}' baseSO='{baseSoPath}'");
            }
        }
        #endif
    }
}
#endif
