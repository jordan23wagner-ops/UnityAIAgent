#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Abyss.Legacy;

namespace Abyssbound.EditorTools
{
    public static class SetupBossLootEditor
    {
        private const string MenuPath = "Abyssbound/Setup/Ensure Boss Guaranteed Equipment Pool";

        private const string ItemsFolder = "Assets/Game/Items";
        private const string LootFolder = "Assets/Game/Loot";

        private const string RareSwordName = "Test_Rare_Sword";
        private const string GlobalEquipmentTableName = "Global_Equipment_DropTable";
        private const string BossTableName = "Zone1_Boss_DropTable";

        [MenuItem(MenuPath)]
        public static void EnsureBossGuaranteedEquipmentPool()
        {
            var summary = new List<string>();

            Debug.Log("[SetupBossLootEditor] Starting: Ensure Boss Guaranteed Equipment Pool");

            // Never throw / never stop execution: catch per-step and continue.
            EnsureFolderExists("Assets/Game");
            EnsureFolderExists(ItemsFolder);
            EnsureFolderExists(LootFolder);

            LegacyItemDefinition rareSword = null;
            DropTable globalEquipmentTable = null;

            LogStep(summary, "Find/create Test_Rare_Sword", () =>
            {
                rareSword = EnsureRareSword(summary);
            });

            LogStep(summary, "Find/create Global_Equipment_DropTable", () =>
            {
                globalEquipmentTable = EnsureGlobalEquipmentDropTable(summary, rareSword);
            });

            LogStep(summary, "Find and wire Zone1_Boss_DropTable", () =>
            {
                EnsureBossDropTable(summary, rareSword, globalEquipmentTable);
            });

            // Always save & refresh at the end.
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SetupBossLootEditor] Summary:\n - " + string.Join("\n - ", summary));
        }

        private static LegacyItemDefinition EnsureRareSword(List<string> summary)
        {
            Debug.Log("[SetupBossLootEditor] Step: Find/create Test_Rare_Sword (begin)");
            var item = FindAssetByName<LegacyItemDefinition>(RareSwordName);
            if (item == null)
            {
                string path = $"{ItemsFolder}/{RareSwordName}.asset";
                item = ScriptableObject.CreateInstance<LegacyItemDefinition>();
                item.name = RareSwordName;
                AssetDatabase.CreateAsset(item, path);
                summary.Add($"Created {RareSwordName}");
            }
            else
            {
                summary.Add($"Found {RareSwordName}");
            }

            if (item == null)
            {
                Debug.LogWarning("[SetupBossLootEditor] Could not create or load LegacyItemDefinition 'Test_Rare_Sword'.");
                summary.Add("WARN: Could not create or load Test_Rare_Sword");
                Debug.Log("[SetupBossLootEditor] Step: Find/create Test_Rare_Sword (end)");
                return null;
            }

            // Configure fields defensively via SerializedObject to tolerate field renames.
            var so = new SerializedObject(item);
            SetStringIfPresent(so, "itemId", RareSwordName);
            SetStringIfPresent(so, "displayName", "Test Rare Sword");
            SetEnumIfPresent(so, "itemType", ItemType.Equipment);
            SetEnumIfPresent(so, "rarity", ItemRarity.Rare);

            // If the data model has any tier eligibility fields, we attempt to set them.
            // This project currently gates eligibility by putting the item into the Normal drops list.
            bool setTierFlag = false;
            setTierFlag |= SetBoolIfPresent(so, "eligibleForNormal", true);
            setTierFlag |= SetBoolIfPresent(so, "allowNormalTier", true);
            setTierFlag |= SetBoolIfPresent(so, "isNormalTierEligible", true);
            setTierFlag |= SetIntIfPresent(so, "minEnemyTier", (int)EnemyTier.Normal);

            if (!setTierFlag)
                summary.Add("No LegacyItemDefinition tier-eligibility fields found (OK: eligibility comes from DropTable tier lists)");

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);

            Debug.Log("[SetupBossLootEditor] Step: Find/create Test_Rare_Sword (end)");

            return item;
        }

        private static DropTable EnsureGlobalEquipmentDropTable(List<string> summary, LegacyItemDefinition rareSword)
        {
            Debug.Log("[SetupBossLootEditor] Step: Find/create Global_Equipment_DropTable (begin)");
            var table = FindAssetByName<DropTable>(GlobalEquipmentTableName);
            if (table == null)
            {
                string path = $"{LootFolder}/{GlobalEquipmentTableName}.asset";
                table = ScriptableObject.CreateInstance<DropTable>();
                table.name = GlobalEquipmentTableName;
                AssetDatabase.CreateAsset(table, path);
                summary.Add($"Created {GlobalEquipmentTableName}");
            }
            else
            {
                summary.Add($"Found {GlobalEquipmentTableName}");
            }

            if (table == null)
            {
                Debug.LogWarning($"[SetupBossLootEditor] Could not create or load DropTable '{GlobalEquipmentTableName}'.");
                summary.Add($"WARN: Could not create or load {GlobalEquipmentTableName}");
                Debug.Log("[SetupBossLootEditor] Step: Find/create Global_Equipment_DropTable (end)");
                return null;
            }

            if (rareSword == null)
            {
                Debug.LogWarning("[SetupBossLootEditor] Test_Rare_Sword is missing; cannot add it to Global_Equipment_DropTable Normal Drops.");
                summary.Add("WARN: Skipped adding Normal entry (Test_Rare_Sword missing)");
                EditorUtility.SetDirty(table);
                Debug.Log("[SetupBossLootEditor] Step: Find/create Global_Equipment_DropTable (end)");
                return table;
            }

            Debug.Log("[SetupBossLootEditor] Step: Add Test_Rare_Sword to Global_Equipment_DropTable Normal Drops (begin)");
            bool added = EnsureDropEntryWithCandidates(table, new[] { "normalDrops", "Normal Drops", "normal" }, rareSword, 1f, out var usedListField);
            if (string.IsNullOrEmpty(usedListField))
            {
                summary.Add("WARN: Could not locate Normal Drops list on Global_Equipment_DropTable");
            }
            else
            {
                summary.Add(added ? $"Added entry under Normal Drops ({usedListField})" : $"Normal Drops already contained Test_Rare_Sword ({usedListField})");
            }
            Debug.Log("[SetupBossLootEditor] Step: Add Test_Rare_Sword to Global_Equipment_DropTable Normal Drops (end)");

            EditorUtility.SetDirty(table);
            Debug.Log("[SetupBossLootEditor] Step: Find/create Global_Equipment_DropTable (end)");
            return table;
        }

        private static void EnsureBossDropTable(List<string> summary, LegacyItemDefinition rareSword, DropTable globalEquipmentTable)
        {
            Debug.Log("[SetupBossLootEditor] Step: Find Zone1_Boss_DropTable (begin)");
            var bossTable = FindAssetByName<DropTable>(BossTableName);
            if (bossTable == null)
            {
                var msg = $"[SetupBossLootEditor] Could not find DropTable asset named '{BossTableName}'. Create it or rename to match.";
                Debug.LogWarning(msg);
                summary.Add($"WARN: Could not find {BossTableName}");
                Debug.Log("[SetupBossLootEditor] Step: Find Zone1_Boss_DropTable (end)");
                return;
            }

            summary.Add($"Found {BossTableName}");

            Debug.Log("[SetupBossLootEditor] Step: Ensure guaranteed equipment drop enabled + min rarity Rare (begin)");

            var so = new SerializedObject(bossTable);

            // Ensure guaranteed drop settings exist and are configured.
            var guaranteed = so.FindProperty("guaranteedEquipmentDrop");
            if (guaranteed != null)
            {
                SetBoolIfPresentRelative(guaranteed, "enabled", true);
                SetEnumIfPresentRelative(guaranteed, "category", ItemType.Equipment);
                SetEnumIfPresentRelative(guaranteed, "minRarity", ItemRarity.Rare);

                // Alternative field names supported by some models.
                SetEnumIfPresentRelative(guaranteed, "minimumRarity", ItemRarity.Rare);

                var rollsProp = guaranteed.FindPropertyRelative("rolls") ?? guaranteed.FindPropertyRelative("rollCount");
                if (rollsProp != null && rollsProp.propertyType == SerializedPropertyType.Integer)
                    rollsProp.intValue = Mathf.Max(1, rollsProp.intValue);
                else if (rollsProp == null)
                    Debug.LogWarning("[SetupBossLootEditor] Could not find 'rolls' or 'rollCount' under guaranteedEquipmentDrop.");

                summary.Add("Set guaranteedEquipmentDrop enabled + min rarity Rare");
            }
            else
            {
                Debug.LogWarning("[SetupBossLootEditor] Boss DropTable has no 'guaranteedEquipmentDrop' property. Available top-level properties: " + DescribeTopLevelProperties(so));
                summary.Add("WARN: Boss DropTable has no guaranteedEquipmentDrop field (cannot configure guarantee)");
            }

            Debug.Log("[SetupBossLootEditor] Step: Ensure guaranteed equipment drop enabled + min rarity Rare (end)");

            // Try to wire a dedicated equipment pool reference if the data model supports it.
            // If no such field exists, we fall back to ensuring the boss table itself has an eligible Rare+ Equipment entry.
            Debug.Log("[SetupBossLootEditor] Step: Wire an equipment pool/table reference if possible (begin)");
            bool wiredPool = false;
            if (globalEquipmentTable != null)
            {
                wiredPool = TryAssignDropTableReference(so, globalEquipmentTable, out var wiredFieldPath);
                if (wiredPool)
                    summary.Add($"Wired guaranteed equipment pool reference ({wiredFieldPath}) -> {GlobalEquipmentTableName}");
                else
                    summary.Add("No guaranteed equipment pool reference field found (OK for this project model)");
            }
            else
            {
                Debug.LogWarning("[SetupBossLootEditor] Global_Equipment_DropTable is null; cannot wire equipment pool reference.");
                summary.Add("WARN: Global_Equipment_DropTable missing; cannot wire pool reference");
            }

            Debug.Log("[SetupBossLootEditor] Step: Wire an equipment pool/table reference if possible (end)");

            so.ApplyModifiedPropertiesWithoutUndo();

            // BossEncounter validation in this project checks the boss table's tier list directly.
            // Ensure Normal tier has at least one eligible Rare+ Equipment entry.
            if (rareSword != null)
            {
                if (!HasEligibleRareEquipmentInNormalTier(bossTable))
                {
                    Debug.Log("[SetupBossLootEditor] Step: Ensure Zone1_Boss_DropTable has eligible Normal entry (begin)");
                    bool added = EnsureDropEntryWithCandidates(bossTable, new[] { "normalDrops", "Normal Drops", "normal" }, rareSword, 1f, out var usedListField);
                    summary.Add(added
                        ? $"Added eligible Rare+ Equipment entry to {BossTableName} Normal Drops ({usedListField})"
                        : $"{BossTableName} Normal Drops already contained Test_Rare_Sword ({usedListField})");
                    Debug.Log("[SetupBossLootEditor] Step: Ensure Zone1_Boss_DropTable has eligible Normal entry (end)");
                }
                else
                {
                    summary.Add("Zone1_Boss_DropTable Normal Drops already had an eligible Rare+ Equipment entry");
                }
            }
            else
            {
                Debug.LogWarning("[SetupBossLootEditor] Test_Rare_Sword is null; cannot ensure boss table eligible entry.");
                summary.Add("WARN: Test_Rare_Sword missing; cannot ensure boss eligible entry");
            }

            EditorUtility.SetDirty(bossTable);
            Debug.Log("[SetupBossLootEditor] Step: Find Zone1_Boss_DropTable (end)");
        }

        private static bool EnsureDropEntryWithCandidates(DropTable table, string[] listFieldCandidates, LegacyItemDefinition item, float chance, out string usedListField)
        {
            usedListField = null;
            if (table == null || item == null) return false;

            var so = new SerializedObject(table);
            var listProp = FindFirstArrayProperty(so, listFieldCandidates);
            if (listProp == null)
            {
                Debug.LogWarning($"[SetupBossLootEditor] Could not find Normal Drops array on DropTable '{table.name}'. Tried: [{string.Join(", ", listFieldCandidates)}]. Available top-level properties: {DescribeTopLevelProperties(so)}");
                return false;
            }

            usedListField = listProp.propertyPath;

            for (int i = 0; i < listProp.arraySize; i++)
            {
                var element = listProp.GetArrayElementAtIndex(i);
                var itemProp = element != null ? element.FindPropertyRelative("item") : null;
                if (itemProp != null && itemProp.propertyType == SerializedPropertyType.ObjectReference)
                {
                    if (itemProp.objectReferenceValue == item)
                    {
                        // Ensure chance is non-zero.
                        var chanceProp = element.FindPropertyRelative("dropChance");
                        if (chanceProp != null && chanceProp.propertyType == SerializedPropertyType.Float)
                            chanceProp.floatValue = Mathf.Clamp01(Mathf.Max(chanceProp.floatValue, chance));
                        else if (chanceProp == null)
                            Debug.LogWarning($"[SetupBossLootEditor] Could not find 'dropChance' on DropEntry element for table '{table.name}'.");

                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(table);
                        return false;
                    }
                }
                else if (itemProp == null)
                {
                    Debug.LogWarning($"[SetupBossLootEditor] DropEntry element missing 'item' property for table '{table.name}'.");
                }
            }

            int idx = listProp.arraySize;
            listProp.InsertArrayElementAtIndex(idx);
            var newElement = listProp.GetArrayElementAtIndex(idx);
            if (newElement != null)
            {
                var itemProp = newElement.FindPropertyRelative("item");
                if (itemProp != null && itemProp.propertyType == SerializedPropertyType.ObjectReference)
                    itemProp.objectReferenceValue = item;
                else
                    Debug.LogWarning($"[SetupBossLootEditor] New DropEntry element missing 'item' property for table '{table.name}'.");

                var chanceProp = newElement.FindPropertyRelative("dropChance");
                if (chanceProp != null && chanceProp.propertyType == SerializedPropertyType.Float)
                    chanceProp.floatValue = Mathf.Clamp01(chance);
                else
                    Debug.LogWarning($"[SetupBossLootEditor] New DropEntry element missing 'dropChance' property for table '{table.name}'.");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(table);
            return true;
        }

        private static SerializedProperty FindFirstArrayProperty(SerializedObject so, string[] candidates)
        {
            if (so == null || candidates == null) return null;
            foreach (var c in candidates)
            {
                if (string.IsNullOrWhiteSpace(c)) continue;
                var p = so.FindProperty(c);
                if (p != null && p.isArray)
                    return p;
            }
            return null;
        }

        private static void LogStep(List<string> summary, string label, Action action)
        {
            Debug.Log($"[SetupBossLootEditor] Step: {label} (begin)");
            summary.Add($"BEGIN: {label}");
            try
            {
                action?.Invoke();
                summary.Add($"END: {label}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SetupBossLootEditor] Step failed: {label}. {ex.GetType().Name}: {ex.Message}");
                summary.Add($"WARN: Step failed: {label} ({ex.GetType().Name}: {ex.Message})");
            }
            Debug.Log($"[SetupBossLootEditor] Step: {label} (end)");
        }

        private static string DescribeTopLevelProperties(SerializedObject so)
        {
            if (so == null) return "<null>";
            try
            {
                var it = so.GetIterator();
                var names = new List<string>();
                bool enterChildren = true;
                while (it.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (!string.IsNullOrWhiteSpace(it.name))
                        names.Add(it.name);
                }
                return names.Count == 0 ? "<none>" : string.Join(", ", names);
            }
            catch
            {
                return "<unavailable>";
            }
        }

        private static void EnsureFolderExists(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int lastSlash = path.LastIndexOf('/');
            if (lastSlash <= 0)
                return;

            string parent = path.Substring(0, lastSlash);
            string name = path.Substring(lastSlash + 1);

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolderExists(parent);

            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static T FindAssetByName<T>(string assetName) where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(assetName)) return null;

            string typeName = typeof(T).Name;
            var guids = AssetDatabase.FindAssets($"{assetName} t:{typeName}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null) continue;

                if (string.Equals(asset.name, assetName, StringComparison.OrdinalIgnoreCase))
                    return asset;
            }

            // Fallback: try any asset of the type and match by name.
            guids = AssetDatabase.FindAssets($"t:{typeName}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null) continue;

                if (string.Equals(asset.name, assetName, StringComparison.OrdinalIgnoreCase))
                    return asset;
            }

            return null;
        }

        private static void SetStringIfPresent(SerializedObject so, string propertyName, string value)
        {
            var p = so.FindProperty(propertyName);
            if (p == null || p.propertyType != SerializedPropertyType.String)
                return;
            p.stringValue = value;
        }

        private static bool SetBoolIfPresent(SerializedObject so, string propertyName, bool value)
        {
            var p = so.FindProperty(propertyName);
            if (p == null || p.propertyType != SerializedPropertyType.Boolean)
                return false;
            p.boolValue = value;
            return true;
        }

        private static bool SetIntIfPresent(SerializedObject so, string propertyName, int value)
        {
            var p = so.FindProperty(propertyName);
            if (p == null || p.propertyType != SerializedPropertyType.Integer)
                return false;
            p.intValue = value;
            return true;
        }

        private static void SetEnumIfPresent<TEnum>(SerializedObject so, string propertyName, TEnum value) where TEnum : Enum
        {
            var p = so.FindProperty(propertyName);
            if (p == null) return;
            if (p.propertyType != SerializedPropertyType.Enum) return;

            var enumName = Enum.GetName(typeof(TEnum), value);
            if (string.IsNullOrWhiteSpace(enumName)) return;

            int idx = Array.IndexOf(p.enumNames, enumName);
            if (idx < 0) return;

            p.enumValueIndex = idx;
        }

        private static void SetBoolIfPresentRelative(SerializedProperty root, string relativeName, bool value)
        {
            var p = root.FindPropertyRelative(relativeName);
            if (p == null || p.propertyType != SerializedPropertyType.Boolean) return;
            p.boolValue = value;
        }

        private static void SetEnumIfPresentRelative<TEnum>(SerializedProperty root, string relativeName, TEnum value) where TEnum : Enum
        {
            var p = root.FindPropertyRelative(relativeName);
            if (p == null || p.propertyType != SerializedPropertyType.Enum) return;

            var enumName = Enum.GetName(typeof(TEnum), value);
            if (string.IsNullOrWhiteSpace(enumName)) return;

            int idx = Array.IndexOf(p.enumNames, enumName);
            if (idx < 0) return;

            p.enumValueIndex = idx;
        }

        private static bool HasEligibleRareEquipmentInNormalTier(DropTable table)
        {
            if (table == null) return false;
            if (table.normalDrops == null) return false;

            foreach (var entry in table.normalDrops)
            {
                var item = entry != null ? entry.item : null;
                if (item == null) continue;
                if (item.itemType != ItemType.Equipment) continue;
                if (item.rarity < ItemRarity.Rare) continue;
                return true;
            }

            return false;
        }

        private static bool TryAssignDropTableReference(SerializedObject so, DropTable pool, out string fieldPath)
        {
            fieldPath = null;
            if (so == null || pool == null) return false;

            // Common candidate field names (root-level)
            var candidates = new[]
            {
                "equipmentPool",
                "equipmentDropTable",
                "equipmentTable",
                "guaranteedEquipmentPool",
                "guaranteedEquipmentDropTable",
                "guaranteedEquipmentTable",
                "guaranteedPool",
            };

            foreach (var name in candidates)
            {
                var p = so.FindProperty(name);
                if (TrySetObjectRefDropTable(p, pool))
                {
                    fieldPath = name;
                    return true;
                }
            }

            // Candidate nested under guaranteedEquipmentDrop
            var guaranteed = so.FindProperty("guaranteedEquipmentDrop");
            if (guaranteed != null)
            {
                var nestedCandidates = new[] { "pool", "dropTable", "table", "equipmentPool", "equipmentTable" };
                foreach (var rel in nestedCandidates)
                {
                    var p = guaranteed.FindPropertyRelative(rel);
                    if (TrySetObjectRefDropTable(p, pool))
                    {
                        fieldPath = $"guaranteedEquipmentDrop.{rel}";
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TrySetObjectRefDropTable(SerializedProperty p, DropTable value)
        {
            if (p == null) return false;
            if (p.propertyType != SerializedPropertyType.ObjectReference) return false;

            // We can only safely assign if the target can hold this reference.
            // If it isn't a DropTable field, Unity will ignore or clear the assignment.
            p.objectReferenceValue = value;
            return p.objectReferenceValue == value;
        }
    }
}
#endif
