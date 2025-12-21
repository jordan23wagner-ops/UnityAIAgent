using System.IO;
using UnityEditor;
using UnityEngine;

public static class GenerateUiBorderSprite
{
    private const int Size = 16;
    private const string AssetPath = "Assets/UI/Generated/UIBorder_16.png";
    private const string ResourcesPath = "Assets/Resources/UI/Generated/UIBorder_16.png";

    [MenuItem("Tools/UI/Generate Border Sprite")]
    public static void Generate()
    {
        GenerateAtPath(AssetPath);
        GenerateAtPath(ResourcesPath);

        AssetDatabase.Refresh();
        ConfigureImporter(AssetPath);
        ConfigureImporter(ResourcesPath);

        Debug.Log($"[UI] Generated border sprite at '{AssetPath}' (and Resources copy).", AssetDatabase.LoadAssetAtPath<Object>(AssetPath));
    }

    [InitializeOnLoadMethod]
    private static void AutoGenerateIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(AssetPath) || !File.Exists(ResourcesPath))
                Generate();
        };
    }

    private static void GenerateAtPath(string path)
    {
        // If you want to skip regeneration when the file already exists, flip this to false.
        const bool overwriteIfExists = true;

        if (!overwriteIfExists && File.Exists(path))
            return;

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: false, linear: false)
        {
            name = Path.GetFileNameWithoutExtension(path)
        };

        var clear = new Color(0f, 0f, 0f, 0f);
        var white = new Color(1f, 1f, 1f, 1f);

        var pixels = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                bool border = x == 0 || y == 0 || x == (Size - 1) || y == (Size - 1);
                pixels[y * Size + x] = border ? white : clear;
            }
        }

        tex.SetPixels(pixels);
        // IMPORTANT:
        // - Keep the in-memory texture readable until AFTER EncodeToPNG.
        // - Do NOT call EncodeToPNG on the imported asset texture.
        tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        byte[] png = ImageConversion.EncodeToPNG(tex);
        Object.DestroyImmediate(tex);

        if (png == null || png.Length == 0)
            throw new IOException($"Failed to encode PNG for '{path}'.");

        File.WriteAllBytes(path, png);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    private static void ConfigureImporter(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.isReadable = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = new Vector4(1f, 1f, 1f, 1f);

        importer.SaveAndReimport();
    }
}
