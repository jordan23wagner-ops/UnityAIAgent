// Assets/Editor/AIAssistant/UnityTools.cs
// Contract-compatible stable shell for AiAssistantWindow.

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AIAssistant
{
    public static class UnityTools
    {
        private static void AddLog(AiExecutionResult result, string op, string message)
        {
            if (result == null) return;
            result.logs.Add($"{op}: {message}");
        }

        private static void AddWarning(AiExecutionResult result, string op, string message)
        {
            if (result == null) return;
            result.warnings.Add($"{op}: {message}");
        }

        private static void AddError(AiExecutionResult result, string op, string message)
        {
            if (result == null) return;
            result.errors.Add($"{op}: {message}");
            result.success = false;
        }

#if UNITY_EDITOR
        private static List<T> DiscoverAssetsByType<T>(string unityTypeFilter) where T : UnityEngine.Object
        {
            var guids = UnityEditor.AssetDatabase.FindAssets(unityTypeFilter);
            var found = new List<T>();
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                    found.Add(asset);
            }
            return found;
        }

        private static List<UnityEngine.ScriptableObject> DiscoverAssetsByFilterAsScriptableObject(string unityTypeFilter)
        {
            var guids = UnityEditor.AssetDatabase.FindAssets(unityTypeFilter);
            var found = new List<UnityEngine.ScriptableObject>();
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.ScriptableObject>(path);
                if (asset != null)
                    found.Add(asset);
            }
            return found;
        }

        private static string GetAssetPath(UnityEngine.Object asset)
        {
            if (asset == null) return null;
            return UnityEditor.AssetDatabase.GetAssetPath(asset);
        }

        private static List<UnityEngine.ScriptableObject> DiscoverAllScriptableObjects()
        {
            return DiscoverAssetsByType<UnityEngine.ScriptableObject>("t:ScriptableObject");
        }

        private static List<UnityEngine.ScriptableObject> DiscoverScriptableObjectCandidatesByTypeName(Func<string, bool> typeNamePredicate)
        {
            var all = DiscoverAllScriptableObjects();
            var candidates = new List<UnityEngine.ScriptableObject>();
            foreach (var so in all)
            {
                if (so == null) continue;
                var typeName = so.GetType().Name;
                if (typeNamePredicate != null && typeNamePredicate(typeName))
                    candidates.Add(so);
            }
            return candidates;
        }

        private static void LogCandidateNamesAndTypes(AiExecutionResult result, string op, string label, List<UnityEngine.ScriptableObject> candidates, int maxToLog = 50)
        {
            if (candidates == null || candidates.Count == 0)
            {
                AddLog(result, op, $"{label}: (none)");
                return;
            }

            int count = Math.Min(maxToLog, candidates.Count);
            AddLog(result, op, $"{label}: {candidates.Count} candidate(s) (showing {count})");
            for (int i = 0; i < count; i++)
            {
                var so = candidates[i];
                if (so == null)
                {
                    AddLog(result, op, " - <null>");
                    continue;
                }
                AddLog(result, op, $" - {so.name} | {so.GetType().FullName}");
            }
        }

        private static void LogAssetsWithTypeAndPath(AiExecutionResult result, string op, string label, List<UnityEngine.ScriptableObject> assets, int maxToLog = 50)
        {
            if (assets == null || assets.Count == 0)
            {
                AddLog(result, op, $"{label}: (none)");
                return;
            }

            int count = Math.Min(maxToLog, assets.Count);
            AddLog(result, op, $"{label}: {assets.Count} asset(s) (showing {count})");
            for (int i = 0; i < count; i++)
            {
                var a = assets[i];
                if (a == null)
                {
                    AddLog(result, op, " - <null>");
                    continue;
                }
                AddLog(result, op, $" - {a.name} | {a.GetType().FullName} | {GetAssetPath(a)}");
            }
        }

        private static List<DropTable> DiscoverDropTablesWithFallback(AiExecutionResult result, string op, out List<UnityEngine.ScriptableObject> fallbackCandidates)
        {
            fallbackCandidates = null;
            var found = DiscoverAssetsByType<DropTable>("t:DropTable");
            if (found.Count > 0)
                return found;

            fallbackCandidates = DiscoverScriptableObjectCandidatesByTypeName(typeName =>
                typeName != null && typeName.IndexOf("DropTable", StringComparison.OrdinalIgnoreCase) >= 0
            );
            LogCandidateNamesAndTypes(result, op, "DropTable fallback candidates", fallbackCandidates);

            var casted = new List<DropTable>();
            foreach (var so in fallbackCandidates)
            {
                if (so is DropTable dt)
                    casted.Add(dt);
            }
            return casted;
        }

        private static List<UnityEngine.ScriptableObject> DiscoverItemDefinitionsWithFallback(AiExecutionResult result, string op)
        {
            var exact = DiscoverAssetsByType<ItemDefinition>("t:ItemDefinition");
            if (exact.Count > 0)
            {
                var list = new List<UnityEngine.ScriptableObject>();
                foreach (var x in exact)
                    if (x != null) list.Add(x);
                return list;
            }

            var candidates = DiscoverScriptableObjectCandidatesByTypeName(typeName =>
            {
                if (string.IsNullOrEmpty(typeName)) return false;
                if (typeName.IndexOf("Item", StringComparison.OrdinalIgnoreCase) < 0) return false;
                return typeName.IndexOf("Definition", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       typeName.IndexOf("Def", StringComparison.OrdinalIgnoreCase) >= 0;
            });
            LogCandidateNamesAndTypes(result, op, "ItemDefinition fallback candidates", candidates);
            return candidates;
        }

        private static List<UnityEngine.ScriptableObject> DiscoverEnemiesWithFallback(AiExecutionResult result, string op)
        {
            var exact = DiscoverAssetsByFilterAsScriptableObject("t:EnemyDefinition");
            if (exact.Count > 0)
            {
                LogAssetsWithTypeAndPath(result, op, "EnemyDefinition assets", exact);
                return exact;
            }

            var candidates = DiscoverScriptableObjectCandidatesByTypeName(typeName =>
            {
                if (string.IsNullOrEmpty(typeName)) return false;
                if (typeName.IndexOf("Enemy", StringComparison.OrdinalIgnoreCase) < 0) return false;
                return typeName.IndexOf("Definition", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       typeName.IndexOf("Config", StringComparison.OrdinalIgnoreCase) >= 0;
            });
            LogAssetsWithTypeAndPath(result, op, "Enemy fallback candidates", candidates);
            return candidates;
        }

        private static List<GateDefinition> DiscoverGateDefinitions()
        {
            return DiscoverAssetsByType<GateDefinition>("t:GateDefinition");
        }

        private static bool ItemDefinitionMatches(ItemDefinition item, string token)
        {
            if (item == null) return false;
            if (string.IsNullOrWhiteSpace(token)) return false;

            if (!string.IsNullOrWhiteSpace(item.itemId) && string.Equals(item.itemId, token, StringComparison.OrdinalIgnoreCase))
                return true;

            return string.Equals(item.name, token, StringComparison.OrdinalIgnoreCase);
        }

        private static ItemDefinition FindItemDefinitionByIdOrName(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            var items = DiscoverAssetsByType<ItemDefinition>("t:ItemDefinition");
            foreach (var item in items)
            {
                if (ItemDefinitionMatches(item, token))
                    return item;
            }
            return null;
        }

        private static string[] GetAssetNames<T>(List<T> assets) where T : UnityEngine.Object
        {
            if (assets == null || assets.Count == 0) return Array.Empty<string>();
            var names = new string[assets.Count];
            for (int i = 0; i < assets.Count; i++)
                names[i] = assets[i] != null ? assets[i].name : "<null>";
            return names;
        }

        private static bool IsProvidedMinDropChance(float minDropChance)
        {
            return minDropChance > 0.000001f;
        }

        private static Dictionary<string, List<DropEntry>> GetTierLists(DropTable table)
        {
            return new Dictionary<string, List<DropEntry>>
            {
                {"Fodder", table != null ? table.fodderDrops : null},
                {"Normal", table != null ? table.normalDrops : null},
                {"Elite", table != null ? table.eliteDrops : null},
                {"MiniBoss", table != null ? table.miniBossDrops : null}
            };
        }

        private static HashSet<ItemDefinition> CollectReferencedItemDefinitions(DropTable table)
        {
            var referenced = new HashSet<ItemDefinition>();
            if (table == null) return referenced;
            var tiers = GetTierLists(table);
            foreach (var kvp in tiers)
            {
                var drops = kvp.Value;
                if (drops == null) continue;
                foreach (var entry in drops)
                {
                    if (entry == null || entry.item == null) continue;
                    referenced.Add(entry.item);
                }
            }
            return referenced;
        }

        private static void ValidateDropTableNonMutating(AiExecutionResult result, string opName, DropTable table, string[] expectedItems, float minDropChance)
        {
            if (table == null)
            {
                AddError(result, opName, "Null DropTable.");
                return;
            }

            var tierLists = GetTierLists(table);

            float maxTierTotal = 0f;
            bool anyNonZeroTierTotal = false;
            foreach (var kvp in tierLists)
            {
                var tier = kvp.Key;
                var drops = kvp.Value;
                float total = 0f;
                int entryCount = 0;
                if (drops != null)
                {
                    foreach (var entry in drops)
                    {
                        if (entry == null) continue;
                        entryCount++;
                        total += entry.dropChance;
                    }
                }
                if (total > 0f) anyNonZeroTierTotal = true;
                if (total > maxTierTotal) maxTierTotal = total;
                AddLog(result, opName, $"{table.name} | Tier '{tier}' entries={entryCount} totalChance={total:0.###}");
            }

            if (!anyNonZeroTierTotal)
                AddWarning(result, opName, $"{table.name} | All tiers have totalChance == 0 (table effectively empty)");

            AddLog(result, opName, $"{table.name} | Max possible drop chance (max tier total) = {maxTierTotal:0.###}");
            if (IsProvidedMinDropChance(minDropChance) && maxTierTotal < minDropChance)
                AddWarning(result, opName, $"{table.name} | Max possible drop chance {maxTierTotal:0.###} is below minDropChance {minDropChance:0.###}");

            if (expectedItems == null || expectedItems.Length == 0)
                return;

            var foundItemDefs = DiscoverItemDefinitionsWithFallback(result, opName);
            var itemByName = new Dictionary<string, UnityEngine.ScriptableObject>(StringComparer.OrdinalIgnoreCase);
            foreach (var itemDef in foundItemDefs)
            {
                if (itemDef == null) continue;
                if (!itemByName.ContainsKey(itemDef.name))
                    itemByName.Add(itemDef.name, itemDef);
            }

            foreach (var itemName in expectedItems)
            {
                if (string.IsNullOrWhiteSpace(itemName))
                {
                    AddWarning(result, opName, $"{table.name} | Encountered empty expectedItems entry; skipped.");
                    continue;
                }

                if (!itemByName.TryGetValue(itemName, out var itemDefAsset) || itemDefAsset == null)
                {
                    AddError(result, opName, $"{table.name} | Expected item '{itemName}' is missing ItemDefinition asset (matched by asset.name)");
                    continue;
                }

                var itemId = TryGetStringMember(itemDefAsset, "itemId");
                if (!string.IsNullOrEmpty(itemId) && !string.Equals(itemId, itemDefAsset.name, StringComparison.OrdinalIgnoreCase))
                    AddWarning(result, opName, $"{table.name} | ItemDefinition '{itemDefAsset.name}' itemId '{itemId}' does not match asset name");

                float maxChance = 0f;
                var perTier = new List<string>();
                bool foundInTable = false;
                foreach (var kvp in tierLists)
                {
                    var tier = kvp.Key;
                    var drops = kvp.Value;
                    if (drops == null) continue;
                    foreach (var entry in drops)
                    {
                        if (entry != null && entry.item != null && string.Equals(entry.item.name, itemName, StringComparison.OrdinalIgnoreCase))
                        {
                            foundInTable = true;
                            if (entry.dropChance > maxChance) maxChance = entry.dropChance;
                            perTier.Add($"{tier}: {entry.dropChance:0.###}");
                        }
                    }
                }

                if (!foundInTable)
                {
                    AddError(result, opName, $"{table.name} | Expected item '{itemName}' not referenced by any tier entry");
                    continue;
                }

                if (IsProvidedMinDropChance(minDropChance) && maxChance < minDropChance)
                    AddWarning(result, opName, $"{table.name} | '{itemName}' max entry chance {maxChance:0.###} < minDropChance {minDropChance:0.###}");

                AddLog(result, opName, $"{table.name} | '{itemName}' | {string.Join(", ", perTier)} | maxEntryChance={maxChance:0.###}");
            }
        }

        private class DropTableRef
        {
            public DropTable table;
            public string id;
            public string source;
        }

#if UNITY_EDITOR
        private class EnemyPrefabCandidate
        {
            public GameObject prefab;
            public string path;
            public List<string> matchingComponentTypeFullNames;
        }

        private static List<EnemyPrefabCandidate> DiscoverEnemyPrefabCandidates(AiExecutionResult result, string op)
        {
            var candidates = new List<EnemyPrefabCandidate>();
            var guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab");
            foreach (var guid in guids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                bool nameMatch = prefab.name.IndexOf("Enemy", StringComparison.OrdinalIgnoreCase) >= 0;
                var matchingComponentTypes = new HashSet<string>(StringComparer.Ordinal);
                try
                {
                    var comps = prefab.GetComponentsInChildren<Component>(true);
                    if (comps != null)
                    {
                        foreach (var c in comps)
                        {
                            if (c == null) continue;
                            var t = c.GetType();
                            var typeName = t.Name;
                            if (!NameContainsAny(typeName, "Enemy", "Mob", "Monster"))
                                continue;
                            matchingComponentTypes.Add(t.FullName ?? t.Name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddWarning(result, op, $"Failed to inspect components for prefab '{prefab.name}' at '{path}': {ex.GetType().Name}");
                }

                if (!nameMatch && matchingComponentTypes.Count == 0)
                    continue;

                candidates.Add(new EnemyPrefabCandidate
                {
                    prefab = prefab,
                    path = path,
                    matchingComponentTypeFullNames = new List<string>(matchingComponentTypes)
                });
            }

            return candidates;
        }

        private static List<DropTableRef> ExtractDropTableRefsFromComponent(Component component, HashSet<string> matchedMemberNames)
        {
            var refs = new List<DropTableRef>();
            if (component == null) return refs;

            void HandleValue(object value, string source)
            {
                if (value == null) return;

                if (value is DropTable dt)
                {
                    refs.Add(new DropTableRef { table = dt, id = null, source = source });
                    return;
                }

                if (value is string s)
                {
                    if (!string.IsNullOrWhiteSpace(s))
                        refs.Add(new DropTableRef { table = null, id = s, source = source });
                    return;
                }

                if (value is System.Collections.IEnumerable enumerable)
                {
                    foreach (var element in enumerable)
                        HandleValue(element, source);
                }
            }

            var t = component.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            foreach (var f in t.GetFields(flags))
            {
                if (!NameContainsAny(f.Name, "dropTable", "dropTables", "loot")) continue;
                matchedMemberNames?.Add($"{t.FullName}.{f.Name}");
                try { HandleValue(f.GetValue(component), $"{t.FullName}.field:{f.Name}"); } catch { }
            }

            foreach (var p in t.GetProperties(flags))
            {
                if (!p.CanRead) continue;
                if (p.GetIndexParameters().Length > 0) continue;
                if (!NameContainsAny(p.Name, "dropTable", "dropTables", "loot")) continue;
                matchedMemberNames?.Add($"{t.FullName}.{p.Name}");
                try { HandleValue(p.GetValue(component), $"{t.FullName}.prop:{p.Name}"); } catch { }
            }

            return refs;
        }
#endif

        private static bool NameContainsAny(string name, params string[] tokens)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var t in tokens)
            {
                if (string.IsNullOrEmpty(t)) continue;
                if (name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static List<DropTableRef> ExtractDropTableRefsFromEnemy(UnityEngine.ScriptableObject enemy)
        {
            var refs = new List<DropTableRef>();
            if (enemy == null) return refs;

            void HandleValue(object value, string source)
            {
                if (value == null) return;

                if (value is DropTable dt)
                {
                    refs.Add(new DropTableRef { table = dt, id = null, source = source });
                    return;
                }

                if (value is string s)
                {
                    if (!string.IsNullOrWhiteSpace(s))
                        refs.Add(new DropTableRef { table = null, id = s, source = source });
                    return;
                }

                if (value is System.Collections.IEnumerable enumerable)
                {
                    foreach (var element in enumerable)
                        HandleValue(element, source);
                }
            }

            var t = enemy.GetType();
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            foreach (var f in t.GetFields(flags))
            {
                if (!NameContainsAny(f.Name, "dropTable", "dropTables", "loot")) continue;
                try { HandleValue(f.GetValue(enemy), $"field:{f.Name}"); } catch { }
            }

            foreach (var p in t.GetProperties(flags))
            {
                if (!p.CanRead) continue;
                if (p.GetIndexParameters().Length > 0) continue;
                if (!NameContainsAny(p.Name, "dropTable", "dropTables", "loot")) continue;
                try { HandleValue(p.GetValue(enemy), $"prop:{p.Name}"); } catch { }
            }

            return refs;
        }
#endif

        private static string TryGetStringMember(object obj, string memberName)
        {
            if (obj == null || string.IsNullOrEmpty(memberName)) return null;
            var t = obj.GetType();

            var field = t.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(string))
                return field.GetValue(obj) as string;

            var prop = t.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.PropertyType == typeof(string) && prop.CanRead)
                return prop.GetValue(obj) as string;

            return null;
        }

        private static void ValidateCommandSchemaForList(AiExecutionResult result, AiCommandList list)
        {
            const string opName = "validateCommandSchema";

            if (list == null || list.commands == null)
            {
                AddError(result, opName, "Null command list.");
                return;
            }

            for (int i = 0; i < list.commands.Length; i++)
            {
                var cmd = list.commands[i];
                if (cmd == null)
                {
                    AddWarning(result, opName, $"Command[{i}] is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(cmd.op))
                {
                    AddError(result, opName, $"Command[{i}] missing required field 'op'.");
                    continue;
                }

                var requiredMissing = new List<string>();
                var allowedFields = new HashSet<string>(StringComparer.Ordinal) { "op" };

                switch (cmd.op)
                {
                    case "ping":
                    case "listDropTables":
                    case "listItemDefinitions":
                    case "listGates":
                    case "listScriptableObjectTypes":
                    case "listEnemies":
                    case "listEnemyPrefabs":
                    case "validateCommandSchema":
                    case "validateAllDropTables":
                    case "validateOrphanItemDefinitions":
                        break;

                    case "validateDropTable":
                        allowedFields.Add("dropTableId");
                        allowedFields.Add("expectedItems");
                        allowedFields.Add("minDropChance");
                        if (string.IsNullOrWhiteSpace(cmd.dropTableId)) requiredMissing.Add("dropTableId");
                        break;

                    case "validateEnemyDrops":
                        allowedFields.Add("enemyId");
                        allowedFields.Add("dropTableId");
                        allowedFields.Add("expectedItems");
                        allowedFields.Add("minDropChance");
                        break;

                    case "validateEnemyPrefabDrops":
                        allowedFields.Add("prefabId");
                        allowedFields.Add("dropTableId");
                        allowedFields.Add("expectedItems");
                        allowedFields.Add("minDropChance");
                        break;

                    case "validateGate":
                        allowedFields.Add("gateId");
                        allowedFields.Add("expectedKeyItem");
                        allowedFields.Add("requiredAmount");
                        if (string.IsNullOrWhiteSpace(cmd.gateId)) requiredMissing.Add("gateId");
                        if (string.IsNullOrWhiteSpace(cmd.expectedKeyItem)) requiredMissing.Add("expectedKeyItem");
                        if (cmd.requiredAmount <= 0) requiredMissing.Add("requiredAmount");
                        break;

                    default:
                        AddError(result, opName, $"Command[{i}] unknown op '{cmd.op}'.");
                        continue;
                }

                var setButUnused = new List<string>();
                if (!allowedFields.Contains("name") && !string.IsNullOrWhiteSpace(cmd.name)) setButUnused.Add("name");
                if (!allowedFields.Contains("path") && !string.IsNullOrWhiteSpace(cmd.path)) setButUnused.Add("path");
                if (!allowedFields.Contains("parentPath") && !string.IsNullOrWhiteSpace(cmd.parentPath)) setButUnused.Add("parentPath");
                if (!allowedFields.Contains("enemyId") && !string.IsNullOrWhiteSpace(cmd.enemyId)) setButUnused.Add("enemyId");
                if (!allowedFields.Contains("prefabId") && !string.IsNullOrWhiteSpace(cmd.prefabId)) setButUnused.Add("prefabId");
                if (!allowedFields.Contains("dropTableId") && !string.IsNullOrWhiteSpace(cmd.dropTableId)) setButUnused.Add("dropTableId");
                if (!allowedFields.Contains("expectedItems") && cmd.expectedItems != null && cmd.expectedItems.Length > 0) setButUnused.Add("expectedItems");
                if (!allowedFields.Contains("minDropChance") && Math.Abs(cmd.minDropChance) > 0.000001f) setButUnused.Add("minDropChance");
                if (!allowedFields.Contains("gateId") && !string.IsNullOrWhiteSpace(cmd.gateId)) setButUnused.Add("gateId");
                if (!allowedFields.Contains("expectedKeyItem") && !string.IsNullOrWhiteSpace(cmd.expectedKeyItem)) setButUnused.Add("expectedKeyItem");
                if (!allowedFields.Contains("requiredAmount") && cmd.requiredAmount != 0) setButUnused.Add("requiredAmount");

                if (requiredMissing.Count > 0)
                    AddError(result, opName, $"Command[{i}] ({cmd.op}) missing required: {string.Join(", ", requiredMissing)}");
                else
                    AddLog(result, opName, $"Command[{i}] ({cmd.op}) required fields OK.");

                if (setButUnused.Count > 0)
                    AddWarning(result, opName, $"Command[{i}] ({cmd.op}) contains unused fields: {string.Join(", ", setButUnused)}");

                if ((cmd.op == "validateDropTable" || cmd.op == "validateEnemyDrops" || cmd.op == "validateEnemyPrefabDrops") && cmd.expectedItems != null)
                {
                    for (int j = 0; j < cmd.expectedItems.Length; j++)
                    {
                        if (string.IsNullOrWhiteSpace(cmd.expectedItems[j]))
                            AddWarning(result, opName, $"Command[{i}] expectedItems[{j}] is empty.");
                    }
                    if (cmd.minDropChance < 0f || cmd.minDropChance > 1f)
                        AddWarning(result, opName, $"Command[{i}] minDropChance {cmd.minDropChance:0.###} is outside [0,1].");
                }
            }
        }

        public static AiExecutionResult ExecuteCommands(AiCommandList list, ExecutionMode mode = ExecutionMode.DryRun)
        {
            // Example JSON:
            // {
            //   "commands": [ { "op": "listEnemyPrefabs" } ]
            // }
            //
            // {
            //   "commands": [ { "op": "validateEnemyPrefabDrops" } ]
            // }
            //
            // {
            //   "commands": [ { "op": "validateEnemyPrefabDrops", "prefabId": "Enemy_Goblin" } ]
            // }
            //
            // {
            //   "commands": [
            //     {
            //       "op": "validateEnemyPrefabDrops",
            //       "dropTableId": "Drops_Zone1_Sigil",
            //       "expectedItems": ["Item_AbyssalSigil"],
            //       "minDropChance": 0.1
            //     }
            //   ]
            // }

            var result = new AiExecutionResult
            {
                mode = mode,
                opsPlanned = (list?.commands?.Length) ?? 0,
                opsExecuted = 0,
                success = true
            };

            // Log mode, command count, and scope if present
            string scopeMsg = "";
            if (list?.commands != null && list.commands.Length > 0)
            {
                var first = list.commands[0];
                if (first != null && first.GetType().GetProperty("scope") != null)
                {
                    var scopeObj = first.GetType().GetProperty("scope").GetValue(first);
                    if (scopeObj != null && scopeObj is SafetyScope scope)
                    {
                        scopeMsg = $" | allowedRoots: [{string.Join(",", scope.allowedRoots)}] deniedRoots: [{string.Join(",", scope.deniedRoots)}] maxOps: {scope.maxOperations}";
                    }
                }
            }
            result.logs.Add($"[UnityTools] Mode: {mode} | Commands: {(list?.commands?.Length ?? 0)}{scopeMsg}");

            if (list?.commands == null)
            {
                AddError(result, "ExecuteCommands", "Null command list.");
                return result;
            }

            for (int i = 0; i < list.commands.Length; i++)
            {
                var cmd = list.commands[i];
                if (cmd == null || string.IsNullOrWhiteSpace(cmd.op))
                {
                    AddWarning(result, "ExecuteCommands", $"Command[{i}] missing op. Skipped.");
                    continue;
                }

                // --- Pre-execution scope safety check (preserve existing behavior) ---
                var pathsToCheck = new[] { cmd.parentPath, cmd.name, cmd.prefabId, cmd.enemyId, cmd.dropTableId, cmd.gateId, cmd.expectedKeyItem, cmd.path };
                bool denied = false;
                if (list.commands != null && list.commands.Length > 0)
                {
                    var deniedRoots = new List<string>();
                    if (cmd is AiCommand c && c != null && c.GetType().GetProperty("scope") != null)
                    {
                        var scopeObj = c.GetType().GetProperty("scope").GetValue(c);
                        if (scopeObj != null && scopeObj is SafetyScope scope && scope.deniedRoots != null)
                            deniedRoots.AddRange(scope.deniedRoots);
                    }

                    foreach (var deniedRoot in deniedRoots)
                    {
                        foreach (var p in pathsToCheck)
                        {
                            if (!string.IsNullOrEmpty(deniedRoot) && !string.IsNullOrEmpty(p) && p.StartsWith(deniedRoot, StringComparison.OrdinalIgnoreCase))
                            {
                                AddError(result, "ExecuteCommands", $"Command[{i}] references denied root '{deniedRoot}' in field value '{p}'. Blocked.");
                                denied = true;
                                break;
                            }
                        }
                        if (denied) break;
                    }
                }

                if (denied)
                    continue;

                switch (cmd.op)
                {
                    case "ping":
                        AddLog(result, "ping", "ok");
                        result.opsExecuted++;
                        break;

                    case "listScriptableObjectTypes":
                    {
#if UNITY_EDITOR
                        var all = DiscoverAllScriptableObjects();
                        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                        foreach (var so in all)
                        {
                            if (so == null) continue;
                            var fullName = so.GetType().FullName ?? so.GetType().Name;
                            if (counts.TryGetValue(fullName, out var c))
                                counts[fullName] = c + 1;
                            else
                                counts.Add(fullName, 1);
                        }
                        var entries = new List<KeyValuePair<string, int>>(counts);
                        entries.Sort((a, b) => b.Value.CompareTo(a.Value));
                        int take = Math.Min(50, entries.Count);
                        AddLog(result, "listScriptableObjectTypes", $"Total ScriptableObjects loaded: {all.Count}. Distinct types: {entries.Count}. Top {take}:");
                        for (int j = 0; j < take; j++)
                            AddLog(result, "listScriptableObjectTypes", $" - {entries[j].Key} = {entries[j].Value}");
#else
                        AddError(result, "listScriptableObjectTypes", "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "listDropTables":
                    {
#if UNITY_EDITOR
                        var foundTables = DiscoverDropTablesWithFallback(result, "listDropTables", out var fallbackCandidates);
                        var names = GetAssetNames(foundTables);
                        if (names.Length == 0)
                        {
                            if (fallbackCandidates != null && fallbackCandidates.Count > 0)
                                AddWarning(result, "listDropTables", "No assets matched exact type 'DropTable'. Fallback candidates found; see logs for names and type full names.");
                            else
                                AddWarning(result, "listDropTables", "No DropTable assets found (exact or fallback).");
                        }
                        else
                        {
                            AddLog(result, "listDropTables", $"Found {names.Length}: [{string.Join(", ", names)}]");
                        }
#else
                        AddError(result, "listDropTables", "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "listItemDefinitions":
                    {
#if UNITY_EDITOR
                        var foundItems = DiscoverItemDefinitionsWithFallback(result, "listItemDefinitions");
                        var names = GetAssetNames(foundItems);
                        if (names.Length == 0)
                            AddWarning(result, "listItemDefinitions", "No ItemDefinition assets found (exact or fallback).");
                        else
                            AddLog(result, "listItemDefinitions", $"Found {names.Length}: [{string.Join(", ", names)}]");
#else
                        AddError(result, "listItemDefinitions", "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "listGates":
                    {
                        const string opName = "listGates";
#if UNITY_EDITOR
                        var gates = DiscoverGateDefinitions();
                        if (gates.Count == 0)
                        {
                            AddWarning(result, opName, "No GateDefinition assets found.");
                        }
                        else
                        {
                            AddLog(result, opName, $"Found {gates.Count} GateDefinition asset(s).");
                            foreach (var g in gates)
                            {
                                if (g == null) continue;
                                var required = g.requiredItem;
                                var requiredLabel = required == null
                                    ? "<null>"
                                    : (!string.IsNullOrWhiteSpace(required.itemId) ? required.itemId : required.name);
                                AddLog(result, opName, $"{g.name} | requiredItem={requiredLabel} | path={GetAssetPath(g)}");
                            }
                        }
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "validateGate":
                    {
                        const string opName = "validateGate";
                        var gateId = cmd.gateId;
                        var expectedKeyItem = cmd.expectedKeyItem;
                        var requiredAmount = cmd.requiredAmount;

                        if (requiredAmount <= 0)
                        {
                            AddError(result, opName, $"requiredAmount must be > 0 (got {requiredAmount}).");
                            result.opsExecuted++;
                            break;
                        }

#if UNITY_EDITOR
                        var gates = DiscoverGateDefinitions();
                        GateDefinition gate = null;
                        foreach (var g in gates)
                        {
                            if (g == null) continue;
                            if (string.Equals(g.name, gateId, StringComparison.OrdinalIgnoreCase))
                            {
                                gate = g;
                                break;
                            }
                        }

                        if (gate == null)
                        {
                            AddError(result, opName, $"GateDefinition not found for gateId '{gateId}' (matched by asset name, case-insensitive).");
                            result.opsExecuted++;
                            break;
                        }

                        AddLog(result, opName, $"GateDefinition found: {gate.name} | path={GetAssetPath(gate)}");

                        var expectedItem = FindItemDefinitionByIdOrName(expectedKeyItem);
                        if (expectedItem == null)
                        {
                            AddError(result, opName, $"Expected key item not found in ItemDefinition assets: '{expectedKeyItem}' (matched by itemId or asset name, case-insensitive).");
                            result.opsExecuted++;
                            break;
                        }

                        AddLog(result, opName, $"Expected key item resolved: {expectedItem.name} (itemId='{expectedItem.itemId}') | path={GetAssetPath(expectedItem)}");

                        if (gate.requiredItem == null)
                        {
                            AddError(result, opName, $"GateDefinition '{gate.name}' has requiredItem = null.");
                            result.opsExecuted++;
                            break;
                        }

                        if (!ItemDefinitionMatches(gate.requiredItem, expectedKeyItem))
                        {
                            var actualLabel = !string.IsNullOrWhiteSpace(gate.requiredItem.itemId) ? gate.requiredItem.itemId : gate.requiredItem.name;
                            AddError(result, opName, $"GateDefinition '{gate.name}' requires '{actualLabel}', which does not match expected '{expectedKeyItem}'.");
                            result.opsExecuted++;
                            break;
                        }

                        if (requiredAmount != 1)
                        {
                            AddWarning(result, opName, $"GateDefinition has no quantity field; runtime gate treats requiredAmount as 1. You requested {requiredAmount}.");
                        }

                        AddLog(result, opName, "GateDefinition requirements validated.");
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif

                        result.opsExecuted++;
                        break;
                    }

                    case "listEnemies":
                    {
                        const string opName = "listEnemies";
#if UNITY_EDITOR
                        var enemies = DiscoverEnemiesWithFallback(result, opName);
                        AddLog(result, opName, $"Found {enemies.Count} enemy asset(s).");
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "listEnemyPrefabs":
                    {
                        const string opName = "listEnemyPrefabs";
#if UNITY_EDITOR
                        var candidates = DiscoverEnemyPrefabCandidates(result, opName);
                        AddLog(result, opName, $"Found {candidates.Count} enemy prefab candidate(s). Detection heuristics: prefab.name contains 'Enemy' OR component type name contains 'Enemy'/'Mob'/'Monster'.");
                        foreach (var c in candidates)
                        {
                            if (c == null || c.prefab == null) continue;
                            var types = (c.matchingComponentTypeFullNames != null && c.matchingComponentTypeFullNames.Count > 0)
                                ? string.Join(", ", c.matchingComponentTypeFullNames)
                                : "(none)";
                            AddLog(result, opName, $"{c.prefab.name} | {c.path} | matchingComponents=[{types}]");
                        }
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "validateCommandSchema":
                    {
                        ValidateCommandSchemaForList(result, list);
                        result.opsExecuted++;
                        break;
                    }

                    case "validateDropTable":
                    {
                        const string opName = "validateDropTable";
                        string dropTableId = cmd.dropTableId;
                        string[] expectedItems = cmd.expectedItems;
                        float minDropChance = cmd.minDropChance;

#if UNITY_EDITOR
                        var foundTables = DiscoverDropTablesWithFallback(result, opName, out var dropTableFallbackCandidates);
                        var discoveredTableNames = GetAssetNames(foundTables);
                        AddLog(result, opName, $"Discovered DropTables ({discoveredTableNames.Length}): [{string.Join(", ", discoveredTableNames)}]");

                        if (foundTables.Count == 0)
                        {
                            if (dropTableFallbackCandidates != null && dropTableFallbackCandidates.Count > 0)
                                AddError(result, opName, "No assets were loadable as DropTable (exact type search returned 0; fallback candidates exist but were not DropTable instances). See logs for candidate names/types.");
                            else
                                AddError(result, opName, "No DropTable assets found (exact or fallback).");
                            result.opsExecuted++;
                            break;
                        }

                        if (string.IsNullOrWhiteSpace(dropTableId))
                        {
                            AddError(result, opName, "Missing required field dropTableId.");
                            result.opsExecuted++;
                            break;
                        }

                        DropTable table = null;
                        foreach (var t in foundTables)
                        {
                            if (t != null && string.Equals(t.name, dropTableId, StringComparison.OrdinalIgnoreCase))
                            {
                                table = t;
                                break;
                            }
                        }

                        if (table == null)
                        {
                            AddError(result, opName, $"DropTable '{dropTableId}' not found (match is case-insensitive). Available: [{string.Join(", ", discoveredTableNames)}]");
                            result.opsExecuted++;
                            break;
                        }

                        // Hardened validation: tier totals, empty-table warning, optional expectations.
                        ValidateDropTableNonMutating(result, opName, table, expectedItems, minDropChance);
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "validateAllDropTables":
                    {
                        const string opName = "validateAllDropTables";
#if UNITY_EDITOR
                        int warn0 = result.warnings != null ? result.warnings.Count : 0;
                        int err0 = result.errors != null ? result.errors.Count : 0;

                        var tables = DiscoverDropTablesWithFallback(result, opName, out var fallbackCandidates);
                        if (tables.Count == 0)
                        {
                            if (fallbackCandidates != null && fallbackCandidates.Count > 0)
                                AddError(result, opName, "No assets were loadable as DropTable (fallback candidates exist; see logs).");
                            else
                                AddError(result, opName, "No DropTable assets found.");
                            result.opsExecuted++;
                            break;
                        }

                        AddLog(result, opName, $"Validating {tables.Count} DropTable(s)...");
                        foreach (var t in tables)
                        {
                            if (t == null) continue;
                            ValidateDropTableNonMutating(result, opName, t, expectedItems: null, minDropChance: cmd.minDropChance);
                        }

                        int warn1 = result.warnings != null ? result.warnings.Count : 0;
                        int err1 = result.errors != null ? result.errors.Count : 0;
                        AddLog(result, opName, $"Summary: tables={tables.Count} warningsAdded={warn1 - warn0} errorsAdded={err1 - err0}");
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "validateOrphanItemDefinitions":
                    {
                        const string opName = "validateOrphanItemDefinitions";
#if UNITY_EDITOR
                        var itemCandidates = DiscoverItemDefinitionsWithFallback(result, opName);
                        var items = new List<ItemDefinition>();
                        foreach (var c in itemCandidates)
                        {
                            if (c is ItemDefinition id)
                                items.Add(id);
                        }

                        if (items.Count == 0)
                        {
                            if (itemCandidates.Count > 0)
                                AddWarning(result, opName, "Found ItemDefinition-like candidates but none were ItemDefinition instances (see logs).");
                            else
                                AddWarning(result, opName, "No ItemDefinition assets found.");
                            result.opsExecuted++;
                            break;
                        }

                        var tables = DiscoverDropTablesWithFallback(result, opName, out _);
                        var referenced = new HashSet<ItemDefinition>();
                        foreach (var t in tables)
                        {
                            if (t == null) continue;
                            foreach (var it in CollectReferencedItemDefinitions(t))
                                referenced.Add(it);
                        }

                        var orphans = new List<ItemDefinition>();
                        foreach (var it in items)
                        {
                            if (it == null) continue;
                            if (!referenced.Contains(it))
                                orphans.Add(it);
                        }

                        if (orphans.Count == 0)
                        {
                            AddLog(result, opName, $"No orphan ItemDefinitions found. Total={items.Count}");
                        }
                        else
                        {
                            AddWarning(result, opName, $"Found {orphans.Count} orphan ItemDefinition(s) not referenced by any DropTable:");
                            foreach (var o in orphans)
                                AddLog(result, opName, $" - {o.name} | {GetAssetPath(o)}");
                        }
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "validateEnemyDrops":
                    {
                        const string opName = "validateEnemyDrops";
                        string enemyId = cmd.enemyId;
                        string expectedDropTableId = cmd.dropTableId;
                        string[] expectedItems = cmd.expectedItems;
                        float minDropChance = cmd.minDropChance;

#if UNITY_EDITOR
                        var enemies = DiscoverEnemiesWithFallback(result, opName);
                        if (!string.IsNullOrWhiteSpace(enemyId))
                            enemies = enemies.FindAll(e => e != null && string.Equals(e.name, enemyId, StringComparison.OrdinalIgnoreCase));

                        if (enemies.Count == 0)
                        {
                            AddWarning(result, opName, string.IsNullOrWhiteSpace(enemyId)
                                ? "No enemy assets found."
                                : $"No enemy assets found matching enemyId '{enemyId}'.");
                            result.opsExecuted++;
                            break;
                        }

                        var tables = DiscoverDropTablesWithFallback(result, opName, out _);
                        var tableByName = new Dictionary<string, DropTable>(StringComparer.OrdinalIgnoreCase);
                        foreach (var t in tables)
                        {
                            if (t == null) continue;
                            if (!tableByName.ContainsKey(t.name))
                                tableByName.Add(t.name, t);
                        }

                        foreach (var enemy in enemies)
                        {
                            if (enemy == null) continue;
                            AddLog(result, opName, $"Enemy '{enemy.name}' | {enemy.GetType().FullName} | {GetAssetPath(enemy)}");

                            var refs = ExtractDropTableRefsFromEnemy(enemy);
                            if (refs.Count == 0)
                            {
                                AddWarning(result, opName, $"Enemy '{enemy.name}' has no dropTable/dropTables/loot fields or properties found via reflection.");
                                continue;
                            }

                            var resolved = new List<DropTable>();
                            foreach (var r in refs)
                            {
                                if (r.table != null)
                                {
                                    resolved.Add(r.table);
                                    AddLog(result, opName, $"Enemy '{enemy.name}' ref {r.source} -> DropTable asset '{r.table.name}'");
                                    continue;
                                }

                                if (!string.IsNullOrWhiteSpace(r.id))
                                {
                                    if (tableByName.TryGetValue(r.id, out var dt) && dt != null)
                                    {
                                        resolved.Add(dt);
                                        AddLog(result, opName, $"Enemy '{enemy.name}' ref {r.source} -> DropTableId '{r.id}' resolved to '{dt.name}'");
                                    }
                                    else
                                    {
                                        AddError(result, opName, $"Enemy '{enemy.name}' ref {r.source} -> DropTableId '{r.id}' could not be resolved to an asset");
                                    }
                                }
                            }

                            var uniqueTables = new List<DropTable>();
                            var seen = new HashSet<DropTable>();
                            foreach (var t in resolved)
                            {
                                if (t == null) continue;
                                if (seen.Add(t)) uniqueTables.Add(t);
                            }

                            if (uniqueTables.Count == 0)
                            {
                                AddWarning(result, opName, $"Enemy '{enemy.name}' had drop table references but none could be resolved to assets.");
                                continue;
                            }

                            if (!string.IsNullOrWhiteSpace(expectedDropTableId))
                            {
                                bool matched = false;
                                foreach (var t in uniqueTables)
                                {
                                    if (t != null && string.Equals(t.name, expectedDropTableId, StringComparison.OrdinalIgnoreCase))
                                    {
                                        matched = true;
                                        break;
                                    }
                                }
                                if (!matched)
                                    AddError(result, opName, $"Enemy '{enemy.name}' does not reference expected dropTableId '{expectedDropTableId}'.");

                                uniqueTables = uniqueTables.FindAll(t => t != null && string.Equals(t.name, expectedDropTableId, StringComparison.OrdinalIgnoreCase));
                            }

                            foreach (var t in uniqueTables)
                            {
                                if (t == null) continue;
                                ValidateDropTableNonMutating(result, opName, t, expectedItems, minDropChance);
                            }
                        }
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    case "validateEnemyPrefabDrops":
                    {
                        const string opName = "validateEnemyPrefabDrops";
                        string prefabId = cmd.prefabId;
                        string expectedDropTableId = cmd.dropTableId;
                        string[] expectedItems = cmd.expectedItems;
                        float minDropChance = cmd.minDropChance;

#if UNITY_EDITOR
                        // Discover candidates via heuristics; if prefabId is specified, we will still validate it even if it doesn't match.
                        var candidates = DiscoverEnemyPrefabCandidates(result, opName);
                        var prefabsToValidate = new List<EnemyPrefabCandidate>(candidates);

                        if (!string.IsNullOrWhiteSpace(prefabId))
                        {
                            prefabsToValidate = prefabsToValidate.FindAll(p => p != null && p.prefab != null &&
                                string.Equals(p.prefab.name, prefabId, StringComparison.OrdinalIgnoreCase));

                            if (prefabsToValidate.Count == 0)
                            {
                                var allPrefabGuids = UnityEditor.AssetDatabase.FindAssets("t:Prefab");
                                foreach (var guid in allPrefabGuids)
                                {
                                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                                    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                                    if (prefab == null) continue;
                                    if (!string.Equals(prefab.name, prefabId, StringComparison.OrdinalIgnoreCase))
                                        continue;

                                    AddWarning(result, opName, $"prefabId '{prefabId}' did not match enemy heuristics, but will be validated because it was explicitly requested.");
                                    prefabsToValidate.Add(new EnemyPrefabCandidate
                                    {
                                        prefab = prefab,
                                        path = path,
                                        matchingComponentTypeFullNames = new List<string>()
                                    });
                                    break;
                                }
                            }
                        }

                        if (prefabsToValidate.Count == 0)
                        {
                            AddWarning(result, opName, string.IsNullOrWhiteSpace(prefabId)
                                ? "No enemy prefab candidates found."
                                : $"No prefabs found matching prefabId '{prefabId}'.");
                            result.opsExecuted++;
                            break;
                        }

                        var allTables = DiscoverDropTablesWithFallback(result, opName, out _);
                        var tableByName = new Dictionary<string, DropTable>(StringComparer.OrdinalIgnoreCase);
                        foreach (var t in allTables)
                        {
                            if (t == null) continue;
                            if (!tableByName.ContainsKey(t.name))
                                tableByName.Add(t.name, t);
                        }

                        AddLog(result, opName, "Matched member name tokens: dropTable, dropTables, loot");

                        foreach (var candidate in prefabsToValidate)
                        {
                            if (candidate == null || candidate.prefab == null) continue;
                            var prefab = candidate.prefab;
                            AddLog(result, opName, $"Prefab '{prefab.name}' | {candidate.path}");

                            var matchedMembers = new HashSet<string>(StringComparer.Ordinal);
                            var resolved = new List<DropTable>();
                            var unresolvedStringIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                            Component[] comps = null;
                            try { comps = prefab.GetComponentsInChildren<Component>(true); } catch { comps = null; }
                            if (comps == null || comps.Length == 0)
                            {
                                AddWarning(result, opName, $"Prefab '{prefab.name}' has no components to inspect.");
                                continue;
                            }

                            foreach (var comp in comps)
                            {
                                if (comp == null) continue; // missing script
                                var refs = ExtractDropTableRefsFromComponent(comp, matchedMembers);
                                foreach (var r in refs)
                                {
                                    if (r == null) continue;
                                    if (r.table != null)
                                    {
                                        resolved.Add(r.table);
                                        continue;
                                    }

                                    if (!string.IsNullOrWhiteSpace(r.id))
                                    {
                                        if (tableByName.TryGetValue(r.id, out var dt) && dt != null)
                                            resolved.Add(dt);
                                        else
                                            unresolvedStringIds.Add(r.id);
                                    }
                                }
                            }

                            if (matchedMembers.Count > 0)
                                AddLog(result, opName, $"Prefab '{prefab.name}' matched members: [{string.Join(", ", matchedMembers)}]");
                            else
                                AddLog(result, opName, $"Prefab '{prefab.name}' matched members: (none)");

                            var uniqueTables = new List<DropTable>();
                            var seen = new HashSet<DropTable>();
                            foreach (var t in resolved)
                            {
                                if (t == null) continue;
                                if (seen.Add(t)) uniqueTables.Add(t);
                            }

                            if (uniqueTables.Count == 0 && unresolvedStringIds.Count == 0)
                            {
                                AddWarning(result, opName, $"Prefab '{prefab.name}' | No DropTable reference found on prefab.");
                                continue;
                            }

                            foreach (var badId in unresolvedStringIds)
                                AddError(result, opName, $"Prefab '{prefab.name}' | DropTableId '{badId}' could not be resolved to an asset.");

                            if (!string.IsNullOrWhiteSpace(expectedDropTableId))
                            {
                                bool matched = false;
                                foreach (var t in uniqueTables)
                                {
                                    if (t != null && string.Equals(t.name, expectedDropTableId, StringComparison.OrdinalIgnoreCase))
                                    {
                                        matched = true;
                                        break;
                                    }
                                }
                                if (!matched)
                                    AddError(result, opName, $"Prefab '{prefab.name}' does not reference expected dropTableId '{expectedDropTableId}'.");

                                uniqueTables = uniqueTables.FindAll(t => t != null && string.Equals(t.name, expectedDropTableId, StringComparison.OrdinalIgnoreCase));
                            }

                            bool shouldValidateDropTables = (expectedItems != null && expectedItems.Length > 0) || IsProvidedMinDropChance(minDropChance);
                            if (shouldValidateDropTables)
                            {
                                foreach (var t in uniqueTables)
                                {
                                    if (t == null) continue;
                                    ValidateDropTableNonMutating(result, opName, t, expectedItems, minDropChance);
                                }
                            }
                            else
                            {
                                foreach (var t in uniqueTables)
                                {
                                    if (t == null) continue;
                                    AddLog(result, opName, $"Prefab '{prefab.name}' references DropTable '{t.name}'");
                                }
                            }
                        }
#else
                        AddError(result, opName, "Only supported in Unity Editor.");
#endif
                        result.opsExecuted++;
                        break;
                    }

                    default:
                        AddWarning(result, "ExecuteCommands", $"Unknown op: {cmd.op}. Skipped.");
                        result.opsExecuted++;
                        break;
                }
            }

            if (result.errors != null && result.errors.Count > 0)
                result.success = false;

            return result;
        }
    }
}
