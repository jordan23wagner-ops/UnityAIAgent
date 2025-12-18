using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.IO;

public static class BuildMerchantShopUIEditor
{
    [MenuItem("Tools/Build Merchant Shop UI (Editor)")]
    public static void BuildMerchantShopUI()
    {
        // Create or find Canvas
        var canvasGO = GameObject.Find("MerchantShopUICanvas");
        if (canvasGO == null)
        {
            canvasGO = new GameObject("MerchantShopUICanvas");
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create MerchantShopUICanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<GraphicRaycaster>();
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        // Root panel
        var root = new GameObject("MerchantShopUIRoot", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(root, "Create MerchantShopUIRoot");
        root.transform.SetParent(canvasGO.transform, false);
        var rootImg = root.GetComponent<Image>();
        rootImg.color = Color.black; // Solid black, not transparent

        // Top: TitleText
        var titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(root.transform, false);
        var title = titleGo.GetComponent<TextMeshProUGUI>();
        title.fontSize = 36;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;
        title.enableWordWrapping = false;
        title.fontStyle = FontStyles.Bold;

        // Top-left: GoldText
        var goldGo = new GameObject("GoldText", typeof(RectTransform), typeof(TextMeshProUGUI));
        goldGo.transform.SetParent(root.transform, false);
        var gold = goldGo.GetComponent<TextMeshProUGUI>();
        gold.fontSize = 24;
        gold.color = Color.white;
        gold.alignment = TextAlignmentOptions.Left;
        gold.enableWordWrapping = false;

        // Exit button
        var exitGO = new GameObject("ExitButton", typeof(RectTransform), typeof(Image), typeof(Button));
        exitGO.transform.SetParent(root.transform, false);
        var exitImg = exitGO.GetComponent<Image>();
        exitImg.color = Color.white; // White button
        var exitBtn = exitGO.GetComponent<Button>();
        // Button color transitions
        var colors = exitBtn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f); // Light gray on hover
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
        exitBtn.colors = colors;
        var exitLabelGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        exitLabelGo.transform.SetParent(exitGO.transform, false);
        var exitLabel = exitLabelGo.GetComponent<TextMeshProUGUI>();
        exitLabel.text = "Exit";
        exitLabel.fontSize = 28;
        exitLabel.color = Color.black;
        exitLabel.alignment = TextAlignmentOptions.Center;
        exitLabel.enableWordWrapping = false;
        exitLabel.fontStyle = FontStyles.Bold;

        // Scroll View
        var svGO = new GameObject("ItemsScrollView", typeof(RectTransform), typeof(ScrollRect));
        svGO.transform.SetParent(root.transform, false);
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(svGO.transform, false);
        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);

        // Configure ScrollRect
        var scrollRect = svGO.GetComponent<ScrollRect>();
        scrollRect.content = content.GetComponent<RectTransform>();
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.vertical = true;
        scrollRect.horizontal = false;

        // Add layout components to Content
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childForceExpandHeight = false;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.spacing = 6f;
        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Details panel on right
        var details = new GameObject("DetailsPanel", typeof(RectTransform), typeof(Image));
        details.transform.SetParent(root.transform, false);
        var nameText = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameText.transform.SetParent(details.transform, false);
        var nameTmp = nameText.GetComponent<TextMeshProUGUI>();
        nameTmp.fontSize = 28;
        nameTmp.color = Color.white;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.alignment = TextAlignmentOptions.Left;
        nameTmp.enableWordWrapping = false;
        var priceText = new GameObject("PriceText", typeof(RectTransform), typeof(TextMeshProUGUI));
        priceText.transform.SetParent(details.transform, false);
        var priceTmp = priceText.GetComponent<TextMeshProUGUI>();
        priceTmp.fontSize = 24;
        priceTmp.color = Color.white;
        priceTmp.fontStyle = FontStyles.Bold;
        priceTmp.alignment = TextAlignmentOptions.Left;
        priceTmp.enableWordWrapping = false;
        var descText = new GameObject("DescriptionText", typeof(RectTransform), typeof(TextMeshProUGUI));
        descText.transform.SetParent(details.transform, false);
        var descTmp = descText.GetComponent<TextMeshProUGUI>();
        descTmp.fontSize = 20;
        descTmp.color = Color.white;
        descTmp.alignment = TextAlignmentOptions.TopLeft;
        descTmp.enableWordWrapping = true;

        // Create Row prefab under project
        string prefabFolder = "Assets/Prefabs/UI";
        if (!AssetDatabase.IsValidFolder(prefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
        }
        var rowGO = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(Button));
        var rowImg = rowGO.GetComponent<Image>();
        rowImg.color = new Color(0.12f, 0.12f, 0.12f, 1f); // dark solid
        var nameChild = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameChild.transform.SetParent(rowGO.transform, false);
        var nameRowTmp = nameChild.GetComponent<TextMeshProUGUI>();
        nameRowTmp.fontSize = 26;
        nameRowTmp.color = Color.white;
        nameRowTmp.fontStyle = FontStyles.Bold;
        nameRowTmp.alignment = TextAlignmentOptions.Left;
        nameRowTmp.enableWordWrapping = false;
        var priceChild = new GameObject("PriceText", typeof(RectTransform), typeof(TextMeshProUGUI));
        priceChild.transform.SetParent(rowGO.transform, false);
        var priceRowTmp = priceChild.GetComponent<TextMeshProUGUI>();
        priceRowTmp.fontSize = 26;
        priceRowTmp.color = Color.white;
        priceRowTmp.fontStyle = FontStyles.Bold;
        priceRowTmp.alignment = TextAlignmentOptions.Right;
        priceRowTmp.enableWordWrapping = false;
        var rowComp = rowGO.AddComponent<Abyss.Shop.MerchantShopRowUI>();
        // Button highlight for row
        var rowBtn = rowGO.GetComponent<Button>();
        var rowColors = rowBtn.colors;
        rowColors.normalColor = rowImg.color;
        rowColors.highlightedColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        rowColors.pressedColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        rowColors.selectedColor = rowImg.color;
        rowColors.disabledColor = new Color(0.12f, 0.12f, 0.12f, 0.5f);
        rowBtn.colors = rowColors;

        // Assign Row component references via SerializedObject
        var soRow = new SerializedObject(rowComp);
        soRow.FindProperty("nameText").objectReferenceValue = nameChild.GetComponent<TextMeshProUGUI>();
        soRow.FindProperty("priceText").objectReferenceValue = priceChild.GetComponent<TextMeshProUGUI>();
        soRow.FindProperty("button").objectReferenceValue = rowGO.GetComponent<Button>();
        soRow.ApplyModifiedProperties();

        string prefabPath = Path.Combine(prefabFolder, "MerchantShopRowUI_Row.prefab");
        var prefab = PrefabUtility.SaveAsPrefabAsset(rowGO, prefabPath);
        Object.DestroyImmediate(rowGO);

        // Create MerchantShopUI GameObject and assign serialized fields
        var uiGO = new GameObject("MerchantShopUI", typeof(RectTransform));
        uiGO.transform.SetParent(canvasGO.transform, false);
        var uiComp = uiGO.AddComponent<Abyss.Shop.MerchantShopUI>();

        var so = new SerializedObject(uiComp);
        so.FindProperty("root").objectReferenceValue = root;
        so.FindProperty("exitButton").objectReferenceValue = exitBtn;
        so.FindProperty("scrollRect").objectReferenceValue = scrollRect;
        so.FindProperty("contentRoot").objectReferenceValue = content.GetComponent<RectTransform>();
        so.FindProperty("rowPrefab").objectReferenceValue = prefab.GetComponent<Abyss.Shop.MerchantShopRowUI>();
        so.FindProperty("detailNameText").objectReferenceValue = nameText.GetComponent<TextMeshProUGUI>();
        so.FindProperty("detailPriceText").objectReferenceValue = priceText.GetComponent<TextMeshProUGUI>();
        so.FindProperty("detailDescText").objectReferenceValue = descText.GetComponent<TextMeshProUGUI>();
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("goldText").objectReferenceValue = gold;
        so.ApplyModifiedProperties();

        // Default the root to inactive
        root.SetActive(false);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.Refresh();

        Debug.Log("Built MerchantShop UI hierarchy and prefab. Assign visuals/sizes in Inspector as needed.");
    }
}
