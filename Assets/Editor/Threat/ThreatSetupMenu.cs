using System.IO;
using Abyssbound.Loot;
using Abyssbound.Threat;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Abyssbound.Editor.Threat
{
    public static class ThreatSetupMenu
    {
        private const string ResourcesThreatFolder = "Assets/Resources/Threat";
        private const string DistanceConfigAssetPath = "Assets/Resources/Threat/Threat_DistanceConfig.asset";
        private const string LootScalingAssetPath = "Assets/Resources/Threat/Threat_LootScaling.asset";

        [MenuItem("Abyssbound/Threat/One-click Setup")]
        public static void OneClickSetup()
        {
            EnsureThreatResourcesFolder();

            var distanceCfg = EnsureDistanceConfigAsset();
            var lootCfg = EnsureLootScalingAsset();

            EnsureDistanceProviderInScene();
            WireThreatServices(distanceCfg);
            EnsureHudDistanceTexts();

            EditorUtility.SetDirty(distanceCfg);
            if (lootCfg != null) EditorUtility.SetDirty(lootCfg);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ThreatSetup] Completed one-click setup.");
        }

        [MenuItem("Abyssbound/Threat/Print Distances")]
        public static void PrintDistances()
        {
            var svc = Object.FindFirstObjectByType<ThreatService>();
            var prov = ThreatDistanceProvider.Instance != null ? ThreatDistanceProvider.Instance : Object.FindFirstObjectByType<ThreatDistanceProvider>();

            float dist = 0f;
            float far = 0f;
            if (svc != null) { dist = svc.CurrentDistanceMeters; far = svc.FarthestDistanceMeters; }
            if (prov != null) { dist = Mathf.Max(dist, prov.CurrentDistanceMeters); far = Mathf.Max(far, prov.FarthestDistanceMeters); }

            Debug.Log($"[Threat] Dist={dist:0}m Max={far:0}m Threat={(svc != null ? svc.CurrentThreat : 0f):0.0}");
        }

        private static void EnsureThreatResourcesFolder()
        {
            if (AssetDatabase.IsValidFolder(ResourcesThreatFolder))
                return;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            AssetDatabase.CreateFolder("Assets/Resources", "Threat");
        }

        private static ThreatDistanceConfigSO EnsureDistanceConfigAsset()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<ThreatDistanceConfigSO>(DistanceConfigAssetPath);
            if (cfg != null)
                return cfg;

            cfg = ScriptableObject.CreateInstance<ThreatDistanceConfigSO>();
            cfg.step = 0.5f;
            cfg.metersThresholds = new[]
            {
                25f, 50f, 75f, 100f, 125f,
                150f, 175f, 200f, 225f, 250f,
            };

            AssetDatabase.CreateAsset(cfg, DistanceConfigAssetPath);
            return cfg;
        }

        private static ThreatLootScalingConfigSO EnsureLootScalingAsset()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<ThreatLootScalingConfigSO>(LootScalingAssetPath);
            if (cfg != null)
                return cfg;

            cfg = ScriptableObject.CreateInstance<ThreatLootScalingConfigSO>();
            cfg.tiers = new[]
            {
                new ThreatLootScalingConfigSO.Tier
                {
                    minThreat = 0f,
                    commonMultiplier = 1f,
                    uncommonMultiplier = 1f,
                    magicMultiplier = 1f,
                    rareMultiplier = 1f,
                    epicMultiplier = 1f,
                    legendaryMultiplier = 1f,
                    bonusRolls = 0,
                    bonusRollChance = 0f,
                },
                new ThreatLootScalingConfigSO.Tier
                {
                    minThreat = 2f,
                    commonMultiplier = 0.9f,
                    uncommonMultiplier = 1.05f,
                    magicMultiplier = 1.10f,
                    rareMultiplier = 1.20f,
                    epicMultiplier = 1.30f,
                    legendaryMultiplier = 1.40f,
                    bonusRolls = 0,
                    bonusRollChance = 0f,
                },
                new ThreatLootScalingConfigSO.Tier
                {
                    minThreat = 4f,
                    commonMultiplier = 0.8f,
                    uncommonMultiplier = 1.10f,
                    magicMultiplier = 1.20f,
                    rareMultiplier = 1.35f,
                    epicMultiplier = 1.50f,
                    legendaryMultiplier = 1.70f,
                    bonusRolls = 1,
                    bonusRollChance = 0.15f,
                },
            };

            AssetDatabase.CreateAsset(cfg, LootScalingAssetPath);
            return cfg;
        }

        private static void EnsureDistanceProviderInScene()
        {
            var provider = Object.FindFirstObjectByType<ThreatDistanceProvider>();
            if (provider != null)
                return;

            var go = new GameObject("[ThreatDistanceProvider]");
            go.AddComponent<ThreatDistanceProvider>();
            Undo.RegisterCreatedObjectUndo(go, "Create ThreatDistanceProvider");
        }

        private static void WireThreatServices(ThreatDistanceConfigSO distanceCfg)
        {
            var services = Object.FindObjectsByType<ThreatService>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (services == null || services.Length == 0)
            {
                Debug.LogWarning("[ThreatSetup] No ThreatService found in scene.");
                return;
            }

            var provider = Object.FindFirstObjectByType<ThreatDistanceProvider>();

            for (int i = 0; i < services.Length; i++)
            {
                var svc = services[i];
                if (svc == null) continue;

                var so = new SerializedObject(svc);
                so.FindProperty("distanceConfig").objectReferenceValue = distanceCfg;
                if (provider != null)
                    so.FindProperty("distanceProvider").objectReferenceValue = provider;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(svc);
            }
        }

        private static void EnsureHudDistanceTexts()
        {
            var huds = Object.FindObjectsByType<ThreatHUD>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (huds == null || huds.Length == 0)
            {
                Debug.LogWarning("[ThreatSetup] No ThreatHUD found in scene.");
                return;
            }

            foreach (var hud in huds)
            {
                if (hud == null) continue;

                var root = hud.transform as RectTransform;
                if (root == null) continue;

                EnsureContainers(root);

                var threatValueText = FindAnyChildTmp(root, "ThreatValueText");

                var distanceContainer = root.Find("DistanceContainer") as RectTransform;
                var dist = EnsureTmpChild(distanceContainer != null ? distanceContainer : root, "DistanceText", threatValueText);
                var far = EnsureTmpChild(distanceContainer != null ? distanceContainer : root, "FarthestDistanceText", threatValueText);

                LayoutUnderSkulls(root, dist.rectTransform, far.rectTransform);

                var so = new SerializedObject(hud);
                so.FindProperty("distanceText").objectReferenceValue = dist;
                so.FindProperty("farthestDistanceText").objectReferenceValue = far;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(hud);
            }
        }

        private static void EnsureContainers(RectTransform hudRoot)
        {
            if (hudRoot == null) return;

            // Ensure a bit more height so metadata has breathing room.
            var s = hudRoot.sizeDelta;
            s.y = Mathf.Max(s.y, 110f);
            hudRoot.sizeDelta = s;

            var skullContainer = hudRoot.Find("SkullContainer") as RectTransform;
            if (skullContainer == null)
            {
                var go = new GameObject("SkullContainer", typeof(RectTransform));
                skullContainer = go.GetComponent<RectTransform>();
                skullContainer.SetParent(hudRoot, false);
                skullContainer.SetSiblingIndex(0);
            }

            var distanceContainer = hudRoot.Find("DistanceContainer") as RectTransform;
            if (distanceContainer == null)
            {
                var go = new GameObject("DistanceContainer", typeof(RectTransform));
                distanceContainer = go.GetComponent<RectTransform>();
                distanceContainer.SetParent(hudRoot, false);
                distanceContainer.SetSiblingIndex(1);
            }

            ConfigureSection(skullContainer, yTop: -6f, height: 56f);
            ConfigureSection(distanceContainer, yTop: -6f - 56f - 8f, height: 40f);

            var skullRow = hudRoot.Find("SkullRow") as RectTransform;
            if (skullRow != null && skullRow.parent != skullContainer)
                skullRow.SetParent(skullContainer, false);

            if (skullRow != null)
            {
                skullRow.anchorMin = new Vector2(0.5f, 0.5f);
                skullRow.anchorMax = new Vector2(0.5f, 0.5f);
                skullRow.pivot = new Vector2(0.5f, 0.5f);
                skullRow.anchoredPosition = Vector2.zero;
            }
        }

        private static void ConfigureSection(RectTransform rt, float yTop, float height)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, yTop);
            rt.sizeDelta = new Vector2(320f, Mathf.Max(0f, height));
        }

        private static TMP_Text FindAnyChildTmp(Transform root, string name)
        {
            var t = root.Find(name);
            return t != null ? t.GetComponent<TMP_Text>() : null;
        }

        private static TMP_Text EnsureTmpChild(RectTransform parent, string name, TMP_Text template)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                var tmp = existing.GetComponent<TMP_Text>();
                if (tmp != null) return tmp;
            }

            GameObject go;
            if (template != null)
            {
                go = Object.Instantiate(template.gameObject, parent);
                go.name = name;
            }
            else
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<CanvasRenderer>();
                go.AddComponent<RectTransform>();
                go.AddComponent<TextMeshProUGUI>();
            }

            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");

            var created = go.GetComponent<TMP_Text>();
            created.raycastTarget = false;
            created.text = string.Empty;

            return created;
        }

        private static void LayoutUnderSkulls(RectTransform hudRoot, RectTransform dist, RectTransform far)
        {
            var skullRow = hudRoot.Find("SkullRow") as RectTransform;
            if (skullRow == null) return;

            var size = hudRoot.sizeDelta;
            size.y = Mathf.Max(size.y, 110f);
            hudRoot.sizeDelta = size;

            // Keep skulls primary: small, slightly muted labels beneath.
            dist.anchorMin = new Vector2(0.5f, 1f);
            dist.anchorMax = new Vector2(0.5f, 1f);
            dist.pivot = new Vector2(0.5f, 1f);

            // Within DistanceContainer: top aligned.
            dist.anchoredPosition = new Vector2(0f, 0f);
            dist.sizeDelta = new Vector2(300f, 16f);

            far.anchorMin = new Vector2(0.5f, 1f);
            far.anchorMax = new Vector2(0.5f, 1f);
            far.pivot = new Vector2(0.5f, 1f);
            far.anchoredPosition = new Vector2(0f, -16f);
            far.sizeDelta = new Vector2(300f, 16f);

            ConfigureText(dist.GetComponent<TMP_Text>(), TextAlignmentOptions.Center);
            ConfigureText(far.GetComponent<TMP_Text>(), TextAlignmentOptions.Center);
        }

        private static void ConfigureText(TMP_Text tmp, TextAlignmentOptions align)
        {
            if (tmp == null) return;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.alignment = align;

            // Target ~60-70% of the primary HUD label size; default to 14.
            tmp.fontSize = 12.5f;

            // Muted, neutral grey (actual runtime tinting is applied in ThreatHUD).
            var c = tmp.color;
            c.r = 0.85f;
            c.g = 0.85f;
            c.b = 0.85f;
            c.a = 0.65f;
            tmp.color = c;
        }
    }
}
