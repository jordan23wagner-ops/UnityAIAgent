using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CreateBossHealthUIPrefab
{
    private const string PrefabPath = "Assets/Resources/UI/BossHealthUI.prefab";

    [MenuItem("Tools/Abyssbound/Create Boss Health UI Prefab")]
    public static void CreatePrefab()
    {
        EnsureFolders();

        var root = new GameObject("BossHealthUI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));

        try
        {
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rootRt = root.GetComponent<RectTransform>();
            rootRt.sizeDelta = new Vector2(200f, 20f);
            rootRt.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            var barRoot = new GameObject("BarRoot",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            barRoot.transform.SetParent(root.transform, false);

            var barRt = barRoot.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0.5f, 0.5f);
            barRt.anchorMax = new Vector2(0.5f, 0.5f);
            barRt.pivot = new Vector2(0.5f, 0.5f);
            barRt.sizeDelta = new Vector2(200f, 20f);

            var bg = barRoot.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);
            bg.raycastTarget = false;

            var fillGo = new GameObject("Fill",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            fillGo.transform.SetParent(barRoot.transform, false);

            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fillRt.pivot = new Vector2(0f, 0.5f);

            var fill = fillGo.GetComponent<Image>();
            fill.color = new Color(0.9f, 0.1f, 0.1f, 1f);
            fill.raycastTarget = false;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 1f;

            var ui = root.AddComponent<BossHealthUI>();
            SetSerializedRef(ui, "fillImage", fill);

            // Save prefab
            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (saved == null)
            {
                Debug.LogError($"[CreateBossHealthUIPrefab] Failed to save prefab at {PrefabPath}");
                return;
            }

            // Validator: load it back
            var loaded = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (loaded == null)
                Debug.LogError($"[CreateBossHealthUIPrefab] Saved prefab but failed to load it back: {PrefabPath}");
            else
                Debug.Log($"[CreateBossHealthUIPrefab] Created prefab: {PrefabPath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI"))
            AssetDatabase.CreateFolder("Assets/Resources", "UI");
    }

    private static void SetSerializedRef(Object target, string fieldName, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(fieldName);
        if (p != null)
        {
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
