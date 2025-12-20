using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Abyss.Equipment.EditorTools
{
    public static class EquipmentSilhouetteIconGenerator
    {
        private const int Size = 96;
        private const int Ppu = 100;
        private const string OutputFolder = "Assets/Abyss/Equipment/Icons";

        [MenuItem("Tools/Equipment/Generate Silhouette Icons")]
        public static void Generate()
        {
            EnsureFolder(OutputFolder);

            WriteIcon("sil_sword", DrawSword);
            WriteIcon("sil_shield", DrawShield);
            WriteIcon("sil_helm", DrawHelm);
            WriteIcon("sil_cape", DrawCape);
            WriteIcon("sil_ring", DrawRing);
            WriteIcon("sil_amulet", DrawAmulet);
            WriteIcon("sil_arrows", DrawArrows);
            WriteIcon("sil_orb", DrawOrb);

            WriteIcon("sil_chest", DrawChest);
            WriteIcon("sil_belt", DrawBelt);
            WriteIcon("sil_boots", DrawBoots);
            WriteIcon("sil_gloves", DrawGloves);

            AssetDatabase.Refresh();
            Debug.Log("[EquipmentSilhouetteIconGenerator] Generated silhouette icons to " + OutputFolder);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            // Create nested folders.
            var parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void WriteIcon(string name, Action<Texture2D> draw)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Clear(tex);
            draw(tex);
            tex.Apply();

            var bytes = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            var path = Path.Combine(OutputFolder, name + ".png").Replace('\\', '/');
            File.WriteAllBytes(path, bytes);

            // Ensure Unity imports (or reimports) the file so it can be loaded as a Sprite immediately.
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = Ppu;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.sRGBTexture = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static void Clear(Texture2D t)
        {
            var c = new Color(0, 0, 0, 0);
            var pixels = new Color[Size * Size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
            t.SetPixels(pixels);
        }

        private static void Put(Texture2D t, int x, int y, Color c)
        {
            if ((uint)x >= Size || (uint)y >= Size) return;
            t.SetPixel(x, y, c);
        }

        private static void FillRect(Texture2D t, int x0, int y0, int x1, int y1, Color c)
        {
            if (x0 > x1) (x0, x1) = (x1, x0);
            if (y0 > y1) (y0, y1) = (y1, y0);
            x0 = Mathf.Clamp(x0, 0, Size - 1);
            x1 = Mathf.Clamp(x1, 0, Size - 1);
            y0 = Mathf.Clamp(y0, 0, Size - 1);
            y1 = Mathf.Clamp(y1, 0, Size - 1);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    Put(t, x, y, c);
        }

        private static void FillCircle(Texture2D t, int cx, int cy, int r, Color c)
        {
            int r2 = r * r;
            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    int dx = x - cx;
                    int dy = y - cy;
                    if (dx * dx + dy * dy <= r2)
                        Put(t, x, y, c);
                }
            }
        }

        private static void DrawLine(Texture2D t, int x0, int y0, int x1, int y1, int thickness, Color c)
        {
            int dx = Mathf.Abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0);
            int sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;

            while (true)
            {
                for (int oy = -thickness; oy <= thickness; oy++)
                    for (int ox = -thickness; ox <= thickness; ox++)
                        Put(t, x0 + ox, y0 + oy, c);

                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private static readonly Color White = new Color(1f, 1f, 1f, 1f);

        private static void DrawSword(Texture2D t)
        {
            DrawLine(t, 26, 18, 70, 62, 2, White);
            FillRect(t, 40, 22, 54, 30, White); // guard
            FillRect(t, 46, 12, 50, 22, White); // grip
            FillRect(t, 45, 8, 51, 12, White);  // pommel
        }

        private static void DrawShield(Texture2D t)
        {
            // Simple shield: rounded-ish rectangle + point
            FillRect(t, 30, 18, 66, 72, White);
            // carve corners
            FillCircle(t, 30, 72, 10, new Color(0, 0, 0, 0));
            FillCircle(t, 66, 72, 10, new Color(0, 0, 0, 0));
            // point
            for (int y = 18; y < 30; y++)
            {
                int inset = (30 - y);
                FillRect(t, 30 + inset, y, 66 - inset, y, White);
            }
        }

        private static void DrawHelm(Texture2D t)
        {
            // A more "full helm" silhouette: bucket + face opening + nose guard.
            // Crown
            FillCircle(t, 48, 60, 24, White);
            // Body
            FillRect(t, 26, 30, 70, 64, White);
            // Bottom trim flare
            FillRect(t, 22, 28, 74, 34, White);

            // Face opening
            FillRect(t, 32, 42, 64, 56, new Color(0, 0, 0, 0));
            // Nose guard
            FillRect(t, 46, 34, 50, 56, White);
            // Eye slit (adds character)
            FillRect(t, 34, 52, 62, 54, new Color(0, 0, 0, 0));
        }

        private static void DrawCape(Texture2D t)
        {
            // A slightly more dynamic cape: narrower at shoulders, wavy bottom edge.
            for (int y = 20; y <= 80; y++)
            {
                float k = (y - 20) / 60f;
                int half = Mathf.RoundToInt(Mathf.Lerp(8, 30, k));
                // Add a subtle wave near the bottom so it doesn't look like a trapezoid.
                float wave = Mathf.Sin((y - 20) * 0.25f) * 1.5f;
                int cx = 48 + Mathf.RoundToInt(wave);
                FillRect(t, cx - half, y, cx + half, y, White);
            }

            // Shoulder clasp hint
            FillCircle(t, 36, 72, 4, White);
            FillCircle(t, 60, 72, 4, White);

            // Neck opening cut
            FillRect(t, 40, 74, 56, 82, new Color(0, 0, 0, 0));
        }

        private static void DrawRing(Texture2D t)
        {
            FillCircle(t, 48, 48, 22, White);
            FillCircle(t, 48, 48, 14, new Color(0, 0, 0, 0));
        }

        private static void DrawAmulet(Texture2D t)
        {
            // chain arc
            for (int x = 22; x <= 74; x++)
            {
                float dx = x - 48;
                float y = 70 - (dx * dx) / 80f;
                FillRect(t, x, Mathf.RoundToInt(y), x, Mathf.RoundToInt(y) + 2, White);
            }
            // pendant
            FillCircle(t, 48, 36, 10, White);
            FillCircle(t, 48, 36, 5, new Color(0, 0, 0, 0));
        }

        private static void DrawArrows(Texture2D t)
        {
            for (int i = 0; i < 3; i++)
            {
                int x = 34 + i * 10;
                DrawLine(t, x, 20, x, 72, 1, White);
                // head
                DrawLine(t, x, 72, x - 6, 64, 1, White);
                DrawLine(t, x, 72, x + 6, 64, 1, White);
                // fletch
                DrawLine(t, x, 20, x - 6, 26, 1, White);
                DrawLine(t, x, 20, x + 6, 26, 1, White);
            }
        }

        private static void DrawOrb(Texture2D t)
        {
            FillCircle(t, 48, 48, 22, White);
            // highlight cut
            FillCircle(t, 40, 58, 10, new Color(0, 0, 0, 0));
        }

        private static void DrawChest(Texture2D t)
        {
            // simple torso plate
            FillRect(t, 28, 22, 68, 74, White);
            FillRect(t, 20, 58, 28, 74, White); // left shoulder
            FillRect(t, 68, 58, 76, 74, White); // right shoulder
            FillRect(t, 38, 22, 58, 32, new Color(0, 0, 0, 0)); // neck opening
        }

        private static void DrawBelt(Texture2D t)
        {
            FillRect(t, 20, 42, 76, 58, White);
            FillRect(t, 46, 44, 58, 56, new Color(0, 0, 0, 0)); // buckle hole
            FillRect(t, 44, 44, 60, 56, White); // buckle outline
        }

        private static void DrawBoots(Texture2D t)
        {
            // two boots
            FillRect(t, 26, 24, 44, 62, White);
            FillRect(t, 22, 24, 46, 32, White); // sole

            FillRect(t, 52, 24, 70, 62, White);
            FillRect(t, 48, 24, 74, 32, White);
        }

        private static void DrawGloves(Texture2D t)
        {
            // Two distinct gloves with fingers (less mitten-like).
            // Left glove
            FillRect(t, 18, 26, 42, 58, White); // palm
            FillRect(t, 14, 22, 46, 28, White); // cuff
            // fingers
            FillRect(t, 18, 58, 22, 72, White);
            FillRect(t, 24, 58, 28, 74, White);
            FillRect(t, 30, 58, 34, 72, White);
            FillRect(t, 36, 58, 40, 70, White);
            // thumb
            FillRect(t, 42, 40, 50, 54, White);

            // Right glove
            FillRect(t, 54, 26, 78, 58, White); // palm
            FillRect(t, 50, 22, 82, 28, White); // cuff
            // fingers
            FillRect(t, 54, 58, 58, 70, White);
            FillRect(t, 60, 58, 64, 72, White);
            FillRect(t, 66, 58, 70, 74, White);
            FillRect(t, 72, 58, 76, 72, White);
            // thumb
            FillRect(t, 46, 40, 54, 54, White);
        }
    }
}
