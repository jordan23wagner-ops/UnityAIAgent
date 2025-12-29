#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Abyssbound.Threat.Editor
{
    /// <summary>
    /// Generates a deterministic monochrome skull icon as a Sprite for the Threat HUD.
    /// Re-runnable; overwrites the PNG.
    /// </summary>
    public static class ThreatSkullSpriteGenerator
    {
        public const string SkullPngPath = "Assets/Art/UI/Threat/Skull_16.png";

        public static Sprite EnsureSkullSpriteAsset(bool forceRegenerate = false)
        {
            EnsureFoldersExist();

            if (forceRegenerate || !File.Exists(SkullPngPath))
                GeneratePng();

            ConfigureImporter();

            try
            {
                AssetDatabase.ImportAsset(SkullPngPath, ImportAssetOptions.ForceUpdate);
                return AssetDatabase.LoadAssetAtPath<Sprite>(SkullPngPath);
            }
            catch
            {
                return null;
            }
        }

        private static void EnsureFoldersExist()
        {
            try
            {
                EnsureFolder("Assets", "Art");
                EnsureFolder("Assets/Art", "UI");
                EnsureFolder("Assets/Art/UI", "Threat");
            }
            catch { }
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent.EndsWith("/") ? parent + child : parent + "/" + child;
            if (AssetDatabase.IsValidFolder(path))
                return;
            if (!AssetDatabase.IsValidFolder(parent))
                return;

            AssetDatabase.CreateFolder(parent, child);
        }

        private static void GeneratePng()
        {
            const int size = 16;

            // Simple 16x16 pixel-art skull silhouette. 'X' = white pixel, '.' = transparent.
            string[] pattern =
            {
                "................",
                ".....XXXXXX.....",
                "....XXXXXXXX....",
                "...XX.XXXX.XX...",
                "...XX.XXXX.XX...",
                "...XXXXXXXXXX...",
                "...XXX.XX.XXX...",
                "...XXX.XX.XXX...",
                "....XX....XX....",
                "....XXXXXXXX....",
                "...XX..XX..XX...",
                "...XX..XX..XX...",
                "....XX....XX....",
                ".....XX..XX.....",
                "......XXXX......",
                "................",
            };

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
            tex.name = "Threat_Skull_16_Generated";

            var clear = new Color32(0, 0, 0, 0);
            var white = new Color32(255, 255, 255, 255);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, size - 1 - y, clear);
            }

            for (int y = 0; y < size && y < pattern.Length; y++)
            {
                var row = pattern[y];
                for (int x = 0; x < size && x < row.Length; x++)
                {
                    if (row[x] == 'X')
                        tex.SetPixel(x, size - 1 - y, white);
                }
            }

            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            byte[] png;
            try { png = ImageConversion.EncodeToPNG(tex); }
            catch { UnityEngine.Object.DestroyImmediate(tex); return; }

            UnityEngine.Object.DestroyImmediate(tex);

            try
            {
                File.WriteAllBytes(SkullPngPath, png);
            }
            catch { }
        }

        private static void ConfigureImporter()
        {
            try
            {
                var importer = AssetImporter.GetAtPath(SkullPngPath) as TextureImporter;
                if (importer == null)
                    return;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.sRGBTexture = true;
                importer.spritePixelsPerUnit = 16f;

                importer.SaveAndReimport();
            }
            catch { }
        }

        [MenuItem("Tools/Threat/Regenerate Skull Sprite Asset")]
        private static void RegenerateMenu()
        {
            var sprite = EnsureSkullSpriteAsset(forceRegenerate: true);
            Debug.Log($"[Threat] Skull sprite regenerated: {(sprite != null ? SkullPngPath : "FAILED")}");
        }
    }
}
#endif
