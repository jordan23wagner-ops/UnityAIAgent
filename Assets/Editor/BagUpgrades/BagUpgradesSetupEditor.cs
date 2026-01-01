#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Abyssbound.BagUpgrades;
using Abyssbound.Loot;
using UnityEditor;
using UnityEngine;

namespace Abyssbound.Editor.BagUpgrades
{
    public static class BagUpgradesSetupEditor
    {
        private const string DropConfigAssetPath = "Assets/Resources/BagUpgrades/BagUpgradeDropConfig.asset";
        private const string BagUpgradesFolder = "Assets/Resources/Loot/Items/BagUpgrades";
        private const string BagUpgradeIconsFolder = "Assets/Resources/Loot/Icons/BagUpgrades";
        private const string ItemRegistryAssetPath = "Assets/Resources/Loot/ItemRegistry.asset";

        [MenuItem("Tools/Bag Upgrades/Setup Bag Upgrades v1 (One-Click)")]
        public static void Setup()
        {
            try
            {
                EnsureFolder("Assets/Resources");
                EnsureFolder("Assets/Resources/BagUpgrades");
                EnsureFolder("Assets/Resources/Loot");
                EnsureFolder("Assets/Resources/Loot/Items");
                EnsureFolder(BagUpgradesFolder);

                var created = new List<UnityEngine.Object>(8);

                var cfg = AssetDatabase.LoadAssetAtPath<BagUpgradeDropConfigSO>(DropConfigAssetPath);
                if (cfg == null)
                {
                    cfg = ScriptableObject.CreateInstance<BagUpgradeDropConfigSO>();
                    AssetDatabase.CreateAsset(cfg, DropConfigAssetPath);
                    created.Add(cfg);
                }

                var t1 = EnsureBagUpgradeItem("Item_BagUpgrade_T1", BagUpgradeIds.BagUpgradeT1, "Bag Upgrade I", "Permanently increases max inventory slots by +2.");
                var t2 = EnsureBagUpgradeItem("Item_BagUpgrade_T2", BagUpgradeIds.BagUpgradeT2, "Bag Upgrade II", "Permanently increases max inventory slots by +2.");
                var t3 = EnsureBagUpgradeItem("Item_BagUpgrade_T3", BagUpgradeIds.BagUpgradeT3, "Bag Upgrade III", "Permanently increases max inventory slots by +2.");
                var t4 = EnsureBagUpgradeItem("Item_BagUpgrade_T4", BagUpgradeIds.BagUpgradeT4, "Bag Upgrade IV", "Permanently increases max inventory slots by +4.");
                var t5 = EnsureBagUpgradeItem("Item_BagUpgrade_T5", BagUpgradeIds.BagUpgradeT5, "Bag Upgrade V", "Permanently increases max inventory slots by +4.");

                // Icons (generated placeholders). Only assigns if icon is missing.
                EnsureFolder("Assets/Resources/Loot/Icons");
                EnsureFolder("Assets/Resources/Loot/Icons/BagUpgrades");
                EnsureBagUpgradeIcon(t1, tier: 1);
                EnsureBagUpgradeIcon(t2, tier: 2);
                EnsureBagUpgradeIcon(t3, tier: 3);
                EnsureBagUpgradeIcon(t4, tier: 4);
                EnsureBagUpgradeIcon(t5, tier: 5);

                if (t1 != null) created.Add(t1);
                if (t2 != null) created.Add(t2);
                if (t3 != null) created.Add(t3);
                if (t4 != null) created.Add(t4);
                if (t5 != null) created.Add(t5);

                var itemRegistry = AssetDatabase.LoadAssetAtPath<ItemRegistrySO>(ItemRegistryAssetPath);
                if (itemRegistry == null)
                {
                    Debug.LogWarning($"[BagUpgradesSetupEditor] Missing ItemRegistry at {ItemRegistryAssetPath}. Run Tools/Abyssbound/Loot/Create Starter Loot Content first.");
                }
                else
                {
                    bool changed = false;
                    changed |= EnsureRegistryContains(itemRegistry, t1);
                    changed |= EnsureRegistryContains(itemRegistry, t2);
                    changed |= EnsureRegistryContains(itemRegistry, t3);
                    changed |= EnsureRegistryContains(itemRegistry, t4);
                    changed |= EnsureRegistryContains(itemRegistry, t5);

                    if (changed)
                        EditorUtility.SetDirty(itemRegistry);
                }

                EditorUtility.SetDirty(cfg);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (created.Count > 0)
                    Selection.objects = created.ToArray();

                Debug.Log("[BagUpgradesSetupEditor] Bag Upgrades v1 setup complete.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BagUpgradesSetupEditor] Setup failed: {ex.Message}\n{ex}");
            }
        }

        private static void EnsureBagUpgradeIcon(ItemDefinitionSO item, int tier)
        {
            if (item == null)
                return;

            // Keep authored icons.
            if (item.icon != null)
                return;

            tier = Mathf.Clamp(tier, 1, 5);
            string pngPath = $"{BagUpgradeIconsFolder}/Icon_BagUpgrade_T{tier}.png";

            if (!File.Exists(pngPath))
            {
                var tex = GenerateBagUpgradeIconTexture(tier, 128);
                var bytes = tex != null ? tex.EncodeToPNG() : null;
                if (bytes != null && bytes.Length > 0)
                    File.WriteAllBytes(pngPath, bytes);
            }

            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);

            try
            {
                var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.filterMode = FilterMode.Point;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.spritePixelsPerUnit = 100f;
                    importer.SaveAndReimport();
                }
            }
            catch { }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            if (sprite != null)
            {
                item.icon = sprite;
                EditorUtility.SetDirty(item);
            }
        }

        private static Texture2D GenerateBagUpgradeIconTexture(int tier, int size)
        {
            size = Mathf.Clamp(size, 64, 256);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = $"BagUpgradeIcon_T{tier}";

            Color clear = new Color(0f, 0f, 0f, 0f);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
            tex.SetPixels(pixels);

            // Tier colors (simple, readable).
            Color fill = tier switch
            {
                1 => new Color(0.36f, 0.60f, 0.86f, 1f),
                2 => new Color(0.36f, 0.86f, 0.55f, 1f),
                3 => new Color(0.86f, 0.82f, 0.36f, 1f),
                4 => new Color(0.86f, 0.55f, 0.36f, 1f),
                _ => new Color(0.78f, 0.44f, 0.86f, 1f),
            };
            Color outline = new Color(0.08f, 0.08f, 0.10f, 1f);
            Color highlight = new Color(1f, 1f, 1f, 0.18f);

            // Bag body (rounded rect)
            int left = Mathf.RoundToInt(size * 0.26f);
            int right = Mathf.RoundToInt(size * 0.74f);
            int bottom = Mathf.RoundToInt(size * 0.18f);
            int top = Mathf.RoundToInt(size * 0.82f);
            int radius = Mathf.RoundToInt(size * 0.09f);

            FillRoundedRect(tex, left, bottom, right, top, radius, fill);
            DrawRoundedRectOutline(tex, left, bottom, right, top, radius, 2, outline);

            // Bag neck
            int neckLeft = Mathf.RoundToInt(size * 0.36f);
            int neckRight = Mathf.RoundToInt(size * 0.64f);
            int neckBottom = Mathf.RoundToInt(size * 0.68f);
            int neckTop = Mathf.RoundToInt(size * 0.88f);
            FillRoundedRect(tex, neckLeft, neckBottom, neckRight, neckTop, Mathf.RoundToInt(radius * 0.6f), fill);
            DrawRoundedRectOutline(tex, neckLeft, neckBottom, neckRight, neckTop, Mathf.RoundToInt(radius * 0.6f), 2, outline);

            // Plus sign
            int cx = size / 2;
            int cy = Mathf.RoundToInt(size * 0.46f);
            int w = Mathf.RoundToInt(size * 0.08f);
            int h = Mathf.RoundToInt(size * 0.22f);
            FillRect(tex, cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2, Color.white);
            FillRect(tex, cx - h / 2, cy - w / 2, cx + h / 2, cy + w / 2, Color.white);

            // Small highlight strip
            FillRoundedRect(tex, left + 6, top - Mathf.RoundToInt(size * 0.18f), right - 10, top - Mathf.RoundToInt(size * 0.10f), Mathf.RoundToInt(radius * 0.5f), highlight);

            tex.Apply();
            return tex;
        }

        private static void FillRect(Texture2D tex, int x0, int y0, int x1, int y1, Color c)
        {
            if (tex == null) return;
            int w = tex.width;
            int h = tex.height;
            x0 = Mathf.Clamp(x0, 0, w - 1);
            x1 = Mathf.Clamp(x1, 0, w - 1);
            y0 = Mathf.Clamp(y0, 0, h - 1);
            y1 = Mathf.Clamp(y1, 0, h - 1);
            if (x1 < x0) (x0, x1) = (x1, x0);
            if (y1 < y0) (y0, y1) = (y1, y0);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                    tex.SetPixel(x, y, c);
            }
        }

        private static void FillRoundedRect(Texture2D tex, int left, int bottom, int right, int top, int radius, Color c)
        {
            if (tex == null) return;
            int w = tex.width;
            int h = tex.height;

            left = Mathf.Clamp(left, 0, w - 1);
            right = Mathf.Clamp(right, 0, w - 1);
            bottom = Mathf.Clamp(bottom, 0, h - 1);
            top = Mathf.Clamp(top, 0, h - 1);
            radius = Mathf.Max(0, radius);

            int r2 = radius * radius;

            for (int y = bottom; y <= top; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    bool inside = true;

                    // Corner tests
                    if (radius > 0)
                    {
                        if (x < left + radius && y < bottom + radius)
                            inside = ((x - (left + radius)) * (x - (left + radius)) + (y - (bottom + radius)) * (y - (bottom + radius))) <= r2;
                        else if (x > right - radius && y < bottom + radius)
                            inside = ((x - (right - radius)) * (x - (right - radius)) + (y - (bottom + radius)) * (y - (bottom + radius))) <= r2;
                        else if (x < left + radius && y > top - radius)
                            inside = ((x - (left + radius)) * (x - (left + radius)) + (y - (top - radius)) * (y - (top - radius))) <= r2;
                        else if (x > right - radius && y > top - radius)
                            inside = ((x - (right - radius)) * (x - (right - radius)) + (y - (top - radius)) * (y - (top - radius))) <= r2;
                    }

                    if (inside)
                        tex.SetPixel(x, y, c);
                }
            }
        }

        private static void DrawRoundedRectOutline(Texture2D tex, int left, int bottom, int right, int top, int radius, int thickness, Color c)
        {
            thickness = Mathf.Clamp(thickness, 1, 6);
            for (int t = 0; t < thickness; t++)
            {
                FillRoundedRectEdge(tex, left + t, bottom + t, right - t, top - t, Mathf.Max(0, radius - t), c);
            }
        }

        private static void FillRoundedRectEdge(Texture2D tex, int left, int bottom, int right, int top, int radius, Color c)
        {
            if (tex == null) return;

            // Draw edge by filling and subtracting inner fill (cheap, small textures).
            var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            copy.SetPixels(tex.GetPixels());

            FillRoundedRect(copy, left, bottom, right, top, radius, c);
            FillRoundedRect(copy, left + 2, bottom + 2, right - 2, top - 2, Mathf.Max(0, radius - 2), new Color(0, 0, 0, 0));

            // Composite onto tex: only apply non-transparent pixels.
            for (int y = 0; y < tex.height; y++)
            {
                for (int x = 0; x < tex.width; x++)
                {
                    var p = copy.GetPixel(x, y);
                    if (p.a > 0.01f)
                        tex.SetPixel(x, y, p);
                }
            }
        }

        private static ItemDefinitionSO EnsureBagUpgradeItem(string assetName, string id, string displayName, string description)
        {
            if (string.IsNullOrWhiteSpace(assetName) || string.IsNullOrWhiteSpace(id))
                return null;

            string path = $"{BagUpgradesFolder}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(path);
            if (existing != null)
            {
                // Keep existing icon/fields if someone has already authored them.
                bool dirty = false;
                if (!string.Equals(existing.id, id, StringComparison.OrdinalIgnoreCase)) { existing.id = id; dirty = true; }
                if (string.IsNullOrWhiteSpace(existing.displayName)) { existing.displayName = displayName; dirty = true; }
                if (string.IsNullOrWhiteSpace(existing.description)) { existing.description = description; dirty = true; }
                if (!existing.stackable) { existing.stackable = true; dirty = true; }
                if (!existing.untradeable) { existing.untradeable = true; dirty = true; }
                if (dirty) EditorUtility.SetDirty(existing);
                return existing;
            }

            var it = ScriptableObject.CreateInstance<ItemDefinitionSO>();
            it.id = id;
            it.displayName = displayName;
            it.description = description;
            it.stackable = true;
            it.untradeable = true;

            AssetDatabase.CreateAsset(it, path);
            EditorUtility.SetDirty(it);
            return it;
        }

        private static bool EnsureRegistryContains(ItemRegistrySO registry, ItemDefinitionSO item)
        {
            if (registry == null || item == null)
                return false;

            registry.items ??= new List<ItemDefinitionSO>();
            for (int i = 0; i < registry.items.Count; i++)
            {
                if (registry.items[i] == item)
                    return false;
            }

            registry.items.Add(item);
            return true;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);

            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
                return;

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
