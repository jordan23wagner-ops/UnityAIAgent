using System;
using System.IO;
using Abyss.Items;
using UnityEditor;
using UnityEngine;

namespace Abyssbound.EditorTools.Skilling
{
    public static class GenerateToolIcons
    {
        private const int Size = 64;
        private const string OutputFolder = "Assets/UI/Generated/Icons/Tools";

        private const string BasicPickaxeAssetPath = "Assets/Resources/ItemDefinitions/BasicPickaxe.asset";
        private const string BasicPickaxePngPath = "Assets/UI/Generated/Icons/Tools/BasicPickaxe.png";

        [MenuItem("Tools/Abyssbound/Skilling/Generate Pickaxe Icon")]
        public static void GeneratePickaxeIconMenu()
        {
            GeneratePickaxeIconAndAssign();
        }

        public static void GeneratePickaxeIconAndAssign()
        {
            EnsureFolder(OutputFolder);

            WriteIcon(BasicPickaxePngPath, DrawBasicPickaxe);

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BasicPickaxePngPath);
            AssignIcon(BasicPickaxeAssetPath, sprite);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Skilling] Generated Basic Pickaxe icon and assigned to ItemDefinition.");
        }

        private static void AssignIcon(string itemDefAssetPath, Sprite icon)
        {
            var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemDefAssetPath);
            if (def == null)
            {
                Debug.LogWarning($"[Skilling] Missing ItemDefinition at '{itemDefAssetPath}'. Run Setup Basic Pickaxe first.");
                return;
            }

            def.icon = icon;
            EditorUtility.SetDirty(def);
        }

        private static void WriteIcon(string assetPath, Action<Texture2D> draw)
        {
            assetPath = assetPath.Replace('\\', '/');

            Texture2D tex = null;
            try
            {
                tex = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: false, linear: false)
                {
                    name = Path.GetFileNameWithoutExtension(assetPath),
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };

                Clear(tex);
                draw(tex);
                tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);

                var png = ImageConversion.EncodeToPNG(tex);
                File.WriteAllBytes(assetPath, png);
            }
            finally
            {
                if (tex != null)
                    UnityEngine.Object.DestroyImmediate(tex);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureImportedSprite(assetPath);
        }

        private static void ConfigureImportedSprite(string assetPath)
        {
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

        private static void EnsureFolder(string assetFolder)
        {
            assetFolder = assetFolder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(assetFolder))
                return;

            var parent = Path.GetDirectoryName(assetFolder)?.Replace('\\', '/');
            var name = Path.GetFileName(assetFolder);

            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                return;

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }

        private static void Clear(Texture2D tex)
        {
            var pixels = new Color32[Size * Size];
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;
            tex.SetPixels32(pixels);
        }

        private static void SetPixel(Texture2D tex, int x, int y, Color32 c)
        {
            if (x < 0 || x >= Size || y < 0 || y >= Size)
                return;
            tex.SetPixel(x, y, c);
        }

        private static void DrawRect(Texture2D tex, int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    SetPixel(tex, x, y, c);
        }

        private static void DrawBasicPickaxe(Texture2D tex)
        {
            // Simple pickaxe: handle + angled head.
            var handle = new Color32(140, 90, 45, 255);
            var handleDark = new Color32(105, 65, 32, 255);
            var metal = new Color32(170, 180, 195, 255);
            var metalDark = new Color32(105, 115, 130, 255);
            var highlight = new Color32(240, 245, 255, 255);

            // Handle (vertical, slightly tapered)
            DrawRect(tex, 30, 10, 33, 44, handle);
            DrawRect(tex, 30, 10, 30, 44, handleDark);

            // Grip band
            DrawRect(tex, 29, 18, 34, 20, handleDark);

            // Head (angled)
            for (int i = 0; i < 18; i++)
            {
                int x = 18 + i;
                int y = 44 + (i / 3);
                DrawRect(tex, x, y, x + 1, y + 3, metal);
                SetPixel(tex, x, y, metalDark);
            }

            for (int i = 0; i < 18; i++)
            {
                int x = 33 + i;
                int y = 44 + ((18 - i) / 3);
                DrawRect(tex, x, y, x + 1, y + 3, metal);
                SetPixel(tex, x + 1, y, metalDark);
            }

            // Highlight on top edge
            for (int x = 22; x <= 42; x++)
                SetPixel(tex, x, 50, highlight);
        }
    }
}
