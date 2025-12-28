#if UNITY_EDITOR
using System;
using System.IO;
using Abyss.Items;
using Abyssbound.Loot;
using UnityEditor;
using UnityEngine;

namespace Abyssbound.EditorTools.UI
{
    public static class AssignFishIconsEditor
    {
        private const string FishIconPath = "Assets/Art/Icons/Silhouettes/sil_fish.png";

        [MenuItem("Tools/Abyssbound/UI/Assign Fish Icons")]
        public static void AssignFishIcons()
        {
            try
            {
                var sprite = EnsureFishSilhouetteSprite();
                if (sprite == null)
                {
                    Debug.LogError($"[AssignFishIcons] Missing fish icon sprite at '{FishIconPath}'.");
                    return;
                }

                int soTouched = AssignToLootV2ItemDefinitionSOs(sprite);
                int legacyTouched = AssignToLegacyItemDefinitions(sprite);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"[AssignFishIcons] Done. Updated LootV2 ItemDefinitionSO: {soTouched}, Legacy ItemDefinition: {legacyTouched}.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static Sprite EnsureFishSilhouetteSprite()
        {
            EnsureFolderFor(FishIconPath);

            if (!File.Exists(FishIconPath))
            {
                GeneratePlaceholderFishPng(FishIconPath, 128, 128);
                AssetDatabase.ImportAsset(FishIconPath, ImportAssetOptions.ForceSynchronousImport);
            }

            ConfigureFishIconImporter(FishIconPath);

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FishIconPath);
            if (sprite == null)
            {
                // Sometimes Unity will import as Texture2D first; try a reimport.
                AssetDatabase.ImportAsset(FishIconPath, ImportAssetOptions.ForceUpdate);
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FishIconPath);
            }

            return sprite;
        }

        private static int AssignToLootV2ItemDefinitionSOs(Sprite fishIcon)
        {
            int touched = 0;

            string[] guids;
            try { guids = AssetDatabase.FindAssets("t:Abyssbound.Loot.ItemDefinitionSO"); }
            catch { guids = AssetDatabase.FindAssets("t:ItemDefinitionSO"); }

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(path);
                if (item == null) continue;

                bool isFish = MatchesFish(item.id) || MatchesFish(item.name);
                bool isShrimpId = string.Equals(item.id, "fish_raw_shrimp", StringComparison.OrdinalIgnoreCase);

                if (!isFish && !isShrimpId)
                    continue;

                if (item.icon != null && !isShrimpId)
                    continue;

                item.icon = fishIcon;
                EditorUtility.SetDirty(item);
                touched++;
            }

            return touched;
        }

        private static int AssignToLegacyItemDefinitions(Sprite fishIcon)
        {
            int touched = 0;

            var guids = AssetDatabase.FindAssets("t:Abyss.Items.ItemDefinition");
            if (guids == null || guids.Length == 0)
                guids = AssetDatabase.FindAssets("t:ItemDefinition");

            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                if (item == null) continue;

                bool isFish = MatchesFish(item.itemId) || MatchesFish(item.name) || MatchesFish(item.displayName);
                bool isShrimpId = string.Equals(item.itemId, "fish_raw_shrimp", StringComparison.OrdinalIgnoreCase);

                if (!isFish && !isShrimpId)
                    continue;

                if (item.icon != null && !isShrimpId)
                    continue;

                item.icon = fishIcon;
                EditorUtility.SetDirty(item);
                touched++;
            }

            return touched;
        }

        private static bool MatchesFish(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            return s.IndexOf("fish_", StringComparison.OrdinalIgnoreCase) >= 0
                   || s.IndexOf("fish_raw", StringComparison.OrdinalIgnoreCase) >= 0
                   || s.IndexOf("shrimp", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void EnsureFolderFor(string assetPath)
        {
            // assetPath is like Assets/.../file.png
            var dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(dir)) return;

            if (AssetDatabase.IsValidFolder(dir))
                return;

            // Create folders recursively under Assets
            var parts = dir.Split('/');
            string cur = parts[0]; // Assets
            for (int i = 1; i < parts.Length; i++)
            {
                var next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        private static void ConfigureFishIconImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            bool changed = false;

            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
            if (Math.Abs(importer.spritePixelsPerUnit - 100f) > 0.01f) { importer.spritePixelsPerUnit = 100f; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
            if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }

            // No compression.
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();
        }

        private static void GeneratePlaceholderFishPng(string assetPath, int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
            var clear = new Color32(0, 0, 0, 0);
            var white = new Color32(255, 255, 255, 255);

            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            // Crude silhouette: ellipse body + triangle tail.
            int cx = (int)(width * 0.52f);
            int cy = (int)(height * 0.52f);
            float rx = width * 0.22f;
            float ry = height * 0.14f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (x - cx) / rx;
                    float dy = (y - cy) / ry;
                    if ((dx * dx + dy * dy) <= 1f)
                        pixels[y * width + x] = white;
                }
            }

            // Tail: left-side triangle.
            int tx = (int)(width * 0.28f);
            int midY = cy;
            int tailW = (int)(width * 0.16f);
            int tailH = (int)(height * 0.20f);

            for (int y = -tailH; y <= tailH; y++)
            {
                int yy = midY + y;
                if (yy < 0 || yy >= height) continue;

                float t = 1f - (Mathf.Abs(y) / (float)tailH);
                int span = Mathf.RoundToInt(tailW * t);
                for (int x = 0; x <= span; x++)
                {
                    int xx = tx - x;
                    if (xx < 0 || xx >= width) continue;
                    pixels[yy * width + xx] = white;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            var png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            File.WriteAllBytes(assetPath, png);
        }
    }
}
#endif
