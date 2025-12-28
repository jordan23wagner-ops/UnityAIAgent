#if UNITY_EDITOR
using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Abyssbound.QA
{
    public static class Zone1HpPass1Applier
    {
        private const int TrashHp = 42;
        private const int EliteHp = 166;
        private const int BossHp = 1010;

        [MenuItem("Tools/Abyssbound/QA/Zone1 HP/Apply Pass 2 (Trash 42 / Elite 166 / Boss 1010)")]
        private static void Apply()
        {
            int visited = 0;
            int enemyPrefabs = 0;
            int updated = 0;
            int updatedHealthComponents = 0;

            var sb = new StringBuilder(2048);
            sb.AppendLine("[Zone1 HP] Pass 2 apply");
            sb.AppendLine($"Targets: Trash={TrashHp} Elite={EliteHp} Boss={BossHp}");

            var guids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                string p = path.Replace('\\', '/');
                if (p.StartsWith("Assets/_Broken/", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (p.IndexOf("/UI/", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (p.StartsWith("Assets/TextMesh Pro/", StringComparison.OrdinalIgnoreCase))
                    continue;

                visited++;

                GameObject root = null;
                bool changed = false;

                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    if (root == null)
                        continue;

                    // Enemy identification: must have EnemyHealth.
                    var healths = root.GetComponentsInChildren<EnemyHealth>(true);
                    if (healths == null || healths.Length == 0)
                        continue;

                    // Zone1 scope: must have Loot V2 Zone1 table OR legacy Zone1 ZoneLootTable.
                    if (!IsZone1Scoped(root))
                        continue;

                    enemyPrefabs++;

                    int targetHp = ResolveTargetHp(root);

                    for (int h = 0; h < healths.Length; h++)
                    {
                        var eh = healths[h];
                        if (eh == null) continue;

                        var so = new SerializedObject(eh);
                        var maxHpProp = so.FindProperty("maxHealth");
                        if (maxHpProp == null) continue;

                        int old = maxHpProp.intValue;
                        if (old != targetHp)
                        {
                            maxHpProp.intValue = targetHp;
                            so.ApplyModifiedPropertiesWithoutUndo();
                            changed = true;
                            updatedHealthComponents++;

                            sb.AppendLine($"- {path} :: EnemyHealth.maxHealth {old} -> {targetHp}");
                        }
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        updated++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Zone1 HP] Skipped '{path}' due to error: {ex.Message}");
                }
                finally
                {
                    if (root != null)
                        PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();

            sb.AppendLine($"Done. Visited={visited} Zone1EnemyPrefabs={enemyPrefabs} UpdatedPrefabs={updated} UpdatedEnemyHealthComponents={updatedHealthComponents}");
            Debug.Log(sb.ToString());
        }

        private static bool IsZone1Scoped(GameObject root)
        {
            if (root == null) return false;

            try
            {
                var v2 = root.GetComponentsInChildren<LootDropOnDeath>(true);
                if (v2 != null)
                {
                    for (int i = 0; i < v2.Length; i++)
                    {
                        var lod = v2[i];
                        if (lod == null) continue;

                        var table = lod.lootTable;
                        if (table != null)
                        {
                            var name = table.name;
                            if (!string.IsNullOrWhiteSpace(name) && name.StartsWith("Zone1_", StringComparison.OrdinalIgnoreCase))
                                return true;
                        }
                        else
                        {
                            // Default LootDropOnDeath path is Zone1_Trash.
                            return true;
                        }
                    }
                }
            }
            catch { }

            try
            {
                var legacy = root.GetComponentsInChildren<DropOnDeath>(true);
                if (legacy != null)
                {
                    for (int i = 0; i < legacy.Length; i++)
                    {
                        var d = legacy[i];
                        if (d == null) continue;
                        if (d.zoneLootTable == null) continue;

                        var path = AssetDatabase.GetAssetPath(d.zoneLootTable);
                        if (!string.IsNullOrWhiteSpace(path) && path.Replace('\\', '/').IndexOf("/Zone1/", StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                        if (!string.IsNullOrWhiteSpace(d.zoneLootTable.name) && d.zoneLootTable.name.IndexOf("Zone1", StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                }
            }
            catch { }

            return false;
        }

        private static int ResolveTargetHp(GameObject root)
        {
            // Default to Trash.
            int hp = TrashHp;

            try
            {
                var drops = root.GetComponentsInChildren<DropOnDeath>(true);
                if (drops != null)
                {
                    for (int i = 0; i < drops.Length; i++)
                    {
                        var d = drops[i];
                        if (d == null) continue;

                        switch (d.tier)
                        {
                            case EnemyTier.Elite:
                                hp = Mathf.Max(hp, EliteHp);
                                break;
                            case EnemyTier.MiniBoss:
                                hp = Mathf.Max(hp, BossHp);
                                break;
                            case EnemyTier.Trash:
                            case EnemyTier.Normal:
                            default:
                                hp = Mathf.Max(hp, TrashHp);
                                break;
                        }
                    }
                }
            }
            catch { }

            try
            {
                var lod = root.GetComponentInChildren<LootDropOnDeath>(true);
                if (lod != null && lod.lootTable != null)
                {
                    var tn = lod.lootTable.name;
                    if (!string.IsNullOrWhiteSpace(tn) && tn.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0)
                        hp = BossHp;
                    else if (!string.IsNullOrWhiteSpace(tn) && tn.IndexOf("Elite", StringComparison.OrdinalIgnoreCase) >= 0)
                        hp = Mathf.Max(hp, EliteHp);
                }
            }
            catch { }

            return Mathf.Max(1, hp);
        }
    }
}
#endif
