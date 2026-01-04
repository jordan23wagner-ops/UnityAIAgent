#if UNITY_EDITOR
using System;
using System.IO;
using Abyss.Items;
using Abyssbound.WorldInteraction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyssbound.EditorTools.Fishing
{
    public static class AssignFishingSpotTypesSceneMenu
    {
        [MenuItem("Tools/Abyssbound/Fishing/Assign Spot Types in Scene")]
        public static void AssignSpotTypesInActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[Fishing] No active loaded scene.");
                return;
            }

            int changed = 0;

            changed += EnsurePerColliderInteractable("FishingSpot_0_Baked_0", "Shrimp Spot");
            changed += EnsurePerColliderInteractable("FishingSpot_1_Baked_1", "Trout Spot");

            // Ensure fish items/icons are present and distinct for inventory QA.
            try { EnsureFishItemDefinitionsAndIcons(); }
            catch { }

            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"[Fishing] Assigned SpotType on {changed} fishing spot(s) in scene '{scene.name}'.");
            }
            else
            {
                Debug.Log($"[Fishing] No SpotType changes needed in scene '{scene.name}'.");
            }
        }

        private static int EnsurePerColliderInteractable(string colliderGoName, string spotType)
        {
            var go = FindSceneGameObjectByName(colliderGoName);
            if (go == null)
            {
                Debug.LogWarning($"[Fishing] Could not find GameObject named '{colliderGoName}' in scene.");
                return 0;
            }

            var wi = go.GetComponent<FishingSpotWorldInteractable>();
            if (wi == null)
            {
                wi = Undo.AddComponent<FishingSpotWorldInteractable>(go);
                EditorUtility.SetDirty(go);
            }

            string before = null;
            try { before = wi.SpotType; }
            catch { before = null; }

            if (string.Equals(before, spotType, StringComparison.Ordinal))
                return 0;

            Undo.RecordObject(wi, "Assign Fishing SpotType");
            wi.SetSpotType(spotType);
            EditorUtility.SetDirty(wi);
            return 1;
        }

        private static GameObject FindSceneGameObjectByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Includes inactive objects.
            try
            {
                var all = Resources.FindObjectsOfTypeAll<Transform>();
                if (all != null)
                {
                    for (int i = 0; i < all.Length; i++)
                    {
                        var t = all[i];
                        if (t == null) continue;
                        var go = t.gameObject;
                        if (go == null) continue;

                        // Skip prefabs/assets; keep only scene objects.
                        if (!go.scene.IsValid() || !go.scene.isLoaded)
                            continue;

                        if (string.Equals(go.name, name, System.StringComparison.Ordinal))
                            return go;
                    }
                }
            }
            catch { }

            return null;
        }

        private static void EnsureFishItemDefinitionsAndIcons()
        {
            const string silhouettesFolder = "Assets/Art/Icons/Silhouettes";
            const string shrimpIconPath = "Assets/Art/Icons/Silhouettes/sil_fish.png";
            const string troutIconPath = "Assets/Art/Icons/Silhouettes/sil_trout.png";

            var shrimpIcon = AssetDatabase.LoadAssetAtPath<Sprite>(shrimpIconPath);
            if (shrimpIcon == null)
                Debug.LogWarning($"[Fishing] Missing shrimp icon at {shrimpIconPath} (will still create ItemDefinitions).");

            var troutIcon = AssetDatabase.LoadAssetAtPath<Sprite>(troutIconPath);
            if (troutIcon == null)
            {
                Directory.CreateDirectory(silhouettesFolder);
                GenerateTroutIconPng(troutIconPath);
                AssetDatabase.ImportAsset(troutIconPath, ImportAssetOptions.ForceUpdate);
                ConfigureAsSprite(troutIconPath);
                troutIcon = AssetDatabase.LoadAssetAtPath<Sprite>(troutIconPath);
            }

            EnsureResourceItemDefinition("fish_raw_shrimp", "Raw Shrimp", "A small raw shrimp.", shrimpIcon, "Assets/Resources/ItemDefinitions/FishRawShrimp.asset");
            EnsureResourceItemDefinition("fish_raw_trout", "Raw Trout", "A freshly caught trout.", troutIcon, "Assets/Resources/ItemDefinitions/FishRawTrout.asset");

            // Also update any other ItemDefinition assets already in the project so the icon stays distinct
            // even if the inventory index finds a different definition first.
            TryAssignIconOnAllItemDefinitionsById("fish_raw_shrimp", shrimpIcon);
            TryAssignIconOnAllItemDefinitionsById("fish_raw_trout", troutIcon);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureResourceItemDefinition(string itemId, string displayName, string description, Sprite icon, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return;
            if (string.IsNullOrWhiteSpace(assetPath)) return;

            var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(assetPath);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(def, assetPath);
            }

            Undo.RecordObject(def, "Ensure Fish ItemDefinition");
            try { def.itemId = itemId; } catch { }
            try { def.displayName = displayName; } catch { }
            try { def.description = description; } catch { }
            try { def.icon = icon; } catch { }
            EditorUtility.SetDirty(def);
        }

        private static void TryAssignIconOnAllItemDefinitionsById(string itemId, Sprite icon)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return;
            if (icon == null) return;

            try
            {
                var guids = AssetDatabase.FindAssets("t:Abyss.Items.ItemDefinition");
                if (guids == null) return;

                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                    if (def == null) continue;

                    string id = null;
                    try { id = def.itemId; } catch { id = null; }
                    if (!string.Equals(id, itemId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    Undo.RecordObject(def, "Assign Fish Icon");
                    try { def.icon = icon; } catch { }
                    EditorUtility.SetDirty(def);
                }
            }
            catch { }
        }

        private static void ConfigureAsSprite(string assetPath)
        {
            try
            {
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) return;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            catch { }
        }

        private static void GenerateTroutIconPng(string assetPath)
        {
            // Simple generated silhouette: long body + tail (distinct from the generic fish icon).
            const int w = 64;
            const int h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);

            var clear = new Color(0, 0, 0, 0);
            var ink = new Color(0f, 0f, 0f, 1f);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, clear);

            // Body ellipse.
            int cx = 36;
            int cy = 32;
            float rx = 22f;
            float ry = 10f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - cx) / rx;
                    float dy = (y - cy) / ry;
                    if (dx * dx + dy * dy <= 1f)
                        tex.SetPixel(x, y, ink);
                }
            }

            // Tail triangle.
            for (int y = 22; y <= 42; y++)
            {
                int t = Mathf.Abs(y - 32);
                int maxX = 16 + (t / 2);
                for (int x = 6; x <= maxX; x++)
                    tex.SetPixel(x, y, ink);
            }

            // Small fin.
            for (int y = 18; y <= 26; y++)
            {
                for (int x = 34; x <= 42; x++)
                {
                    if ((y - 18) + (x - 34) <= 8)
                        tex.SetPixel(x, y, ink);
                }
            }

            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            var bytes = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            File.WriteAllBytes(assetPath, bytes);
        }
    }
}
#endif
