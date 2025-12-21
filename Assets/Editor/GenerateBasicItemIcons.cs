using System;
using System.Collections.Generic;
using System.IO;
using Abyss.Items;
using UnityEditor;
using UnityEngine;

public static class GenerateBasicItemIcons
{
    private const int Size = 64;
    private const string OutputFolder = "Assets/UI/Generated/Icons";

    [MenuItem("Tools/UI/Generate Basic Item Icons")]
    public static void Generate()
    {
        // Requirement: generate PNG bytes from an in-memory Texture2D only (never encode an imported asset).
        EnsureFolders(OutputFolder);

        var outputs = new List<(Color32 bg, Color32 fg, string letter, string fileName)>
        {
            (new Color32(150, 30, 30, 255),   new Color32(255, 255, 255, 255), "H", "Icon_HealthPotion.png"),
            (new Color32(35, 75, 170, 255),   new Color32(255, 255, 255, 255), "M", "Icon_ManaPotion.png"),
            (new Color32(210, 180, 130, 255), new Color32(45, 30, 10, 255),    "S", "Icon_TownScroll.png"),
        };

        var generatedAssetPaths = new List<string>(outputs.Count);
        foreach (var o in outputs)
        {
            var assetPath = (OutputFolder + "/" + o.fileName).Replace('\\', '/');

            Texture2D tex = null;
            try
            {
                tex = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: false, linear: false)
                {
                    name = o.fileName,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };

                Fill(tex, o.bg);
                DrawBorder(tex, new Color32(0, 0, 0, 60));
                DrawLetter(tex, o.letter, o.fg);

                // IMPORTANT: keep readable for EncodeToPNG.
                tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);

                var png = ImageConversion.EncodeToPNG(tex);
                File.WriteAllBytes(assetPath, png);
            }
            finally
            {
                if (tex != null)
                    UnityEngine.Object.DestroyImmediate(tex);
            }

            // Requirement: explicitly import after writing.
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureImportedSprite(assetPath);
            generatedAssetPaths.Add(assetPath);
        }

        var iconPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "potion_health", OutputFolder + "/Icon_HealthPotion.png" },
            { "potion_mana",   OutputFolder + "/Icon_ManaPotion.png" },
            { "scroll_town",   OutputFolder + "/Icon_TownScroll.png" },
        };

        AssignIconsToItems(iconPaths);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[GenerateBasicItemIcons] Done.");
    }

    private static void ConfigureImportedSprite(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
            return;

        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.sRGBTexture = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
    }

    private static void EnsureFolders(string assetFolder)
    {
        // Creates nested folders under Assets/ using AssetDatabase so Unity recognizes them.
        if (string.IsNullOrWhiteSpace(assetFolder))
            return;

        assetFolder = assetFolder.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(assetFolder))
            return;

        var parts = assetFolder.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;

        string current = parts[0];
        if (!string.Equals(current, "Assets", StringComparison.Ordinal))
            return;

        current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void AssignIconsToItems(Dictionary<string, string> itemIdToIconPath)
    {
        if (itemIdToIconPath == null || itemIdToIconPath.Count == 0)
            return;

        int updated = 0;
        int missing = 0;

        foreach (var kv in itemIdToIconPath)
        {
            var itemId = kv.Key;
            var iconPath = kv.Value;
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            if (icon == null)
            {
                Debug.LogWarning($"[GenerateBasicItemIcons] Could not load sprite at '{iconPath}'");
                continue;
            }

            var def = FindItemDefinitionById(itemId);
            if (def == null)
            {
                missing++;
                Debug.LogWarning($"[GenerateBasicItemIcons] Could not find ItemDefinition with itemId='{itemId}'");
                continue;
            }

            def.icon = icon;
            EditorUtility.SetDirty(def);
            updated++;
            Debug.Log($"[GenerateBasicItemIcons] Assigned icon '{icon.name}' to ItemDefinition '{def.displayName}' ({def.itemId})");
        }

        Debug.Log($"[GenerateBasicItemIcons] Icon assignment complete. Updated={updated} MissingDefs={missing}");
    }

    private static ItemDefinition FindItemDefinitionById(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        var guids = AssetDatabase.FindAssets("t:ItemDefinition");
        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (def == null) continue;
            if (string.Equals(def.itemId, itemId, StringComparison.OrdinalIgnoreCase))
                return def;
        }

        return null;
    }

    private static void Fill(Texture2D tex, Color32 c)
    {
        var pixels = new Color32[Size * Size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = c;
        tex.SetPixels32(pixels);
    }

    private static void DrawBorder(Texture2D tex, Color32 c)
    {
        for (int x = 0; x < Size; x++)
        {
            tex.SetPixel(x, 0, c);
            tex.SetPixel(x, Size - 1, c);
        }

        for (int y = 0; y < Size; y++)
        {
            tex.SetPixel(0, y, c);
            tex.SetPixel(Size - 1, y, c);
        }
    }

    private static void DrawLetter(Texture2D tex, string letter, Color32 color)
    {
        // Simple 5x7 block font. Each "1" becomes a filled square.
        var glyph = GetGlyph(letter);
        int glyphW = glyph[0].Length;
        int glyphH = glyph.Length;

        // Scale up so it looks crisp at 64x64.
        int cell = 8; // 5*8=40, 7*8=56
        int w = glyphW * cell;
        int h = glyphH * cell;
        int startX = (Size - w) / 2;
        int startY = (Size - h) / 2;

        for (int gy = 0; gy < glyphH; gy++)
        {
            for (int gx = 0; gx < glyphW; gx++)
            {
                if (glyph[gy][gx] != '1')
                    continue;

                DrawRect(tex, startX + gx * cell, startY + (glyphH - 1 - gy) * cell, cell, cell, color);
            }
        }
    }

    private static void DrawRect(Texture2D tex, int x, int y, int w, int h, Color32 c)
    {
        int xMax = Mathf.Min(Size, x + w);
        int yMax = Mathf.Min(Size, y + h);
        int xMin = Mathf.Max(0, x);
        int yMin = Mathf.Max(0, y);

        for (int yy = yMin; yy < yMax; yy++)
        {
            for (int xx = xMin; xx < xMax; xx++)
            {
                tex.SetPixel(xx, yy, c);
            }
        }
    }

    private static string[] GetGlyph(string letter)
    {
        switch ((letter ?? string.Empty).Trim().ToUpperInvariant())
        {
            case "H":
                return new[]
                {
                    "10001",
                    "10001",
                    "10001",
                    "11111",
                    "10001",
                    "10001",
                    "10001",
                };

            case "M":
                return new[]
                {
                    "10001",
                    "11011",
                    "10101",
                    "10001",
                    "10001",
                    "10001",
                    "10001",
                };

            case "S":
            default:
                return new[]
                {
                    "01111",
                    "10000",
                    "10000",
                    "01110",
                    "00001",
                    "00001",
                    "11110",
                };
        }
    }
}
