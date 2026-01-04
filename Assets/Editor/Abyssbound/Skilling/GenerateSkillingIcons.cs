using System;
using System.IO;
using Abyss.Items;
using UnityEditor;
using UnityEngine;

namespace Abyssbound.EditorTools.Skilling
{
    public static class GenerateSkillingIcons
    {
        private const int Size = 64;
        private const string OutputFolder = "Assets/UI/Generated/Icons/Skilling";

        private const string CopperOreAssetPath = "Assets/Resources/ItemDefinitions/CopperOre.asset";
        private const string CopperBarAssetPath = "Assets/Resources/ItemDefinitions/CopperBar.asset";

        private const string CopperOrePngPath = "Assets/UI/Generated/Icons/Skilling/CopperOre.png";
        private const string CopperBarPngPath = "Assets/UI/Generated/Icons/Skilling/CopperBar.png";

        public static void GenerateCopperIconsAndAssign()
        {
            EnsureFolder(OutputFolder);

            WriteIcon(CopperOrePngPath, DrawCopperOre);
            WriteIcon(CopperBarPngPath, DrawCopperBar);

            var oreSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CopperOrePngPath);
            var barSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CopperBarPngPath);

            AssignIcon(CopperOreAssetPath, oreSprite);
            AssignIcon(CopperBarAssetPath, barSprite);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Skilling] Generated Copper icons and assigned to ItemDefinitions.");
        }

        private static void AssignIcon(string itemDefAssetPath, Sprite icon)
        {
            var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemDefAssetPath);
            if (def == null)
            {
                Debug.LogWarning($"[Skilling] Missing ItemDefinition at '{itemDefAssetPath}'. Run Setup Copper Items first.");
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

                // Keep readable for EncodeToPNG.
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

        private static void DrawCopperOre(Texture2D tex)
        {
            // Imperfect stone circle with speckles.
            var rng = new System.Random(1337);
            int cx = Size / 2;
            int cy = Size / 2;

            float baseRadius = 22f;
            float[] radial = new float[360];
            for (int a = 0; a < radial.Length; a++)
            {
                // Low-frequency jitter so edge isn't perfectly round.
                radial[a] = baseRadius + (float)(rng.NextDouble() * 4.0 - 2.0);
            }

            var fill = new Color32(186, 115, 68, 255);
            var shadow = new Color32(120, 70, 40, 255);
            var border = new Color32(80, 45, 25, 255);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = x - cx + 0.5f;
                    float dy = y - cy + 0.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (ang < 0) ang += 360f;
                    int ia = Mathf.Clamp((int)ang, 0, 359);
                    float r = radial[ia];

                    if (dist <= r)
                    {
                        // Simple shading: darker on lower-right.
                        float shadeT = Mathf.Clamp01((dx + dy) / (baseRadius * 2f) + 0.5f);
                        var c = Color32.Lerp(fill, shadow, shadeT * 0.6f);

                        // Border band.
                        if (dist >= r - 1.2f)
                            c = border;

                        SetPixel(tex, x, y, c);
                    }
                }
            }

            // Add a few darker speckles.
            var speck = new Color32(90, 55, 35, 255);
            for (int i = 0; i < 26; i++)
            {
                int x = rng.Next(0, Size);
                int y = rng.Next(0, Size);
                var p = tex.GetPixel(x, y);
                if (p.a < 0.1f) { i--; continue; }

                SetPixel(tex, x, y, speck);
                if (rng.NextDouble() < 0.5) SetPixel(tex, x + 1, y, speck);
                if (rng.NextDouble() < 0.5) SetPixel(tex, x, y + 1, speck);
            }
        }

        private static void DrawCopperBar(Texture2D tex)
        {
            // Angled ingot with top + side faces.
            var top = new Color32(214, 130, 72, 255);
            var side = new Color32(150, 80, 45, 255);
            var edge = new Color32(255, 200, 150, 255);

            // Define two quads in pixel space.
            // Top face (parallelogram)
            Vector2 a = new Vector2(16, 38);
            Vector2 b = new Vector2(44, 46);
            Vector2 c = new Vector2(54, 32);
            Vector2 d = new Vector2(26, 24);

            // Side face (right/lower)
            Vector2 e = new Vector2(44, 46);
            Vector2 f = new Vector2(54, 32);
            Vector2 g = new Vector2(54, 22);
            Vector2 h = new Vector2(44, 36);

            FillConvexQuad(tex, a, b, c, d, top);
            FillConvexQuad(tex, e, f, g, h, side);

            // Highlight along the top edge.
            DrawLine(tex, a, b, edge);
        }

        private static void FillConvexQuad(Texture2D tex, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color32 col)
        {
            int minX = Mathf.FloorToInt(Mathf.Min(p0.x, p1.x, p2.x, p3.x));
            int maxX = Mathf.CeilToInt(Mathf.Max(p0.x, p1.x, p2.x, p3.x));
            int minY = Mathf.FloorToInt(Mathf.Min(p0.y, p1.y, p2.y, p3.y));
            int maxY = Mathf.CeilToInt(Mathf.Max(p0.y, p1.y, p2.y, p3.y));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    if (PointInConvexQuad(p, p0, p1, p2, p3))
                        SetPixel(tex, x, y, col);
                }
            }
        }

        private static bool PointInConvexQuad(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            return SameSide(p, a, b, c) &&
                   SameSide(p, b, c, d) &&
                   SameSide(p, c, d, a) &&
                   SameSide(p, d, a, b);
        }

        private static bool SameSide(Vector2 p, Vector2 a, Vector2 b, Vector2 refPoint)
        {
            float cp1 = Cross(b - a, p - a);
            float cp2 = Cross(b - a, refPoint - a);
            return cp1 * cp2 >= 0f;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static void DrawLine(Texture2D tex, Vector2 from, Vector2 to, Color32 col)
        {
            int x0 = Mathf.RoundToInt(from.x);
            int y0 = Mathf.RoundToInt(from.y);
            int x1 = Mathf.RoundToInt(to.x);
            int y1 = Mathf.RoundToInt(to.y);

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                SetPixel(tex, x0, y0, col);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
        }
    }
}
