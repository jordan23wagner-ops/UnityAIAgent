using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public static class BuildMerchantShopUIEditor
{
    [MenuItem("Tools/Abyssbound/Content/UI/Build Merchant Shop UI (Editor)")]
    public static void BuildMerchantShopUI()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Run this in Edit Mode (not Play Mode).");
            return;
        }

        EnsureEventSystemExists();

        // Best-effort upgrade: if a MerchantShopUI already exists in the active scene, wire/upgrade it in-place
        // (including its referenced row prefab) so projects don't require manual hookups.
        if (TryUpgradeExistingMerchantShopUIsInScene(out var upgradeSummary))
        {
            Debug.Log(upgradeSummary);
            return;
        }

        // Force rebuild: delete prior instances so styling/layout updates always apply.
        DestroySceneObjectsByName("MerchantShopUICanvas");
        DestroySceneObjectsByName("MerchantShopUIRoot");
        DestroySceneObjectsByName("MerchantShopBackdrop");
        DestroySceneObjectsByName("MerchantShopUI");

        // Create Canvas fresh every run
        var canvasGO = new GameObject("MerchantShopUICanvas", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create MerchantShopUICanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<GraphicRaycaster>();
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        StretchFullScreen(canvasGO.GetComponent<RectTransform>());

        // Root container (toggled active by MerchantShopUI)
        var root = new GameObject("MerchantShopUIRoot", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "Create MerchantShopUIRoot");
        root.transform.SetParent(canvasGO.transform, false);
        var rootRt = root.GetComponent<RectTransform>();
        StretchFullScreen(rootRt);

        // Full-screen backdrop (blocks clicks)
        var backdrop = new GameObject("MerchantShopBackdrop", typeof(RectTransform), typeof(Image), typeof(Button));
        Undo.RegisterCreatedObjectUndo(backdrop, "Create MerchantShopBackdrop");
        backdrop.transform.SetParent(root.transform, false);
        var backdropRt = backdrop.GetComponent<RectTransform>();
        StretchFullScreen(backdropRt);
        var backdropImg = backdrop.GetComponent<Image>();
        backdropImg.color = new Color(0f, 0f, 0f, 0.55f);
        backdropImg.raycastTarget = true;
        // Nice touch: click outside to close
        var backdropBtn = backdrop.GetComponent<Button>();
        backdropBtn.transition = Selectable.Transition.None;
        backdropBtn.onClick.RemoveAllListeners();
        backdropBtn.onClick.AddListener(Abyss.Shop.MerchantShopUI.CloseStatic);

        // Centered panel (visual root)
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(panel, "Create MerchantShop Panel");
        panel.transform.SetParent(root.transform, false);
        var panelRt = panel.GetComponent<RectTransform>();
        SetAnchors(panelRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta = new Vector2(1100f, 650f);
        var panelImg = panel.GetComponent<Image>();
        panelImg.color = new Color(0.066f, 0.066f, 0.066f, 0.95f); // ~#111 @ 0.95

        // Top: TitleText
        var titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(panel.transform, false);
        var title = titleGo.GetComponent<TextMeshProUGUI>();
        title.fontSize = 40;
        title.alignment = TextAlignmentOptions.Center;
        title.color = Color.white;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.fontStyle = FontStyles.Bold;
        var titleRt = titleGo.GetComponent<RectTransform>();
        SetAnchors(titleRt, new Vector2(0.2f, 0.92f), new Vector2(0.8f, 1f));
        SetOffsets(titleRt, 0f, 0f, 0f, 0f);

        // Top-left: GoldText
        var goldGo = new GameObject("GoldText", typeof(RectTransform), typeof(TextMeshProUGUI));
        goldGo.transform.SetParent(panel.transform, false);
        var gold = goldGo.GetComponent<TextMeshProUGUI>();
        gold.fontSize = 26;
        gold.color = Color.white;
        gold.alignment = TextAlignmentOptions.Left;
        gold.textWrappingMode = TextWrappingModes.NoWrap;
        var goldRt = goldGo.GetComponent<RectTransform>();
        SetAnchors(goldRt, new Vector2(0f, 0.92f), new Vector2(0.25f, 1f));
        // Padding left ~20
        SetOffsets(goldRt, 20f, 0f, 0f, 0f);

        // Exit button
        var exitGO = new GameObject("ExitButton", typeof(RectTransform), typeof(Image), typeof(Button));
        exitGO.transform.SetParent(panel.transform, false);
        var exitImg = exitGO.GetComponent<Image>();
        exitImg.color = Color.white; // Pure white button background
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
        var exitText = exitLabelGo.GetComponent<TextMeshProUGUI>();
        exitText.text = "Exit";
        exitText.fontSize = 28;
        exitText.color = Color.black;
        exitText.fontStyle = FontStyles.Bold;
        exitText.alignment = TextAlignmentOptions.Center;
        exitText.textWrappingMode = TextWrappingModes.NoWrap;
        exitText.raycastTarget = false;
        exitText.outlineWidth = 0.2f;
        exitText.outlineColor = new Color(1f, 1f, 1f, 0.9f);
        exitText.extraPadding = true;

        // Legacy Text fallback (in case something creates a UnityEngine.UI.Text under the Exit button)
        foreach (var legacyText in exitGO.GetComponentsInChildren<Text>(true))
        {
            legacyText.color = Color.black;
        }
        var exitRt = exitGO.GetComponent<RectTransform>();
        SetAnchors(exitRt, new Vector2(0.85f, 0.93f), new Vector2(1f, 1f));
        // Offsets create ~140x44 with small padding
        SetOffsets(exitRt, 0f, 10f, 0f, 5f);
        var exitLabelRt = exitLabelGo.GetComponent<RectTransform>();
        StretchFullScreen(exitLabelRt);

        // Scroll View
        var svGO = new GameObject("ItemsScrollView", typeof(RectTransform), typeof(ScrollRect));
        svGO.transform.SetParent(panel.transform, false);
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(svGO.transform, false);
        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);

        var svRt = svGO.GetComponent<RectTransform>();
        // Leave space at top for Buy/Sell mode tabs.
        SetAnchors(svRt, new Vector2(0.02f, 0.05f), new Vector2(0.62f, 0.86f));
        SetOffsets(svRt, 0f, 0f, 0f, 0f);

        var viewportRt = viewport.GetComponent<RectTransform>();
        StretchFullScreen(viewportRt);
        var viewportImg = viewport.GetComponent<Image>();
        viewportImg.color = new Color(0.14f, 0.14f, 0.14f, 0.35f);
        var mask = viewport.GetComponent<Mask>();
        mask.showMaskGraphic = true;

        var contentRt = content.GetComponent<RectTransform>();
        SetAnchors(contentRt, new Vector2(0f, 1f), new Vector2(1f, 1f));
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 0f);

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
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.childAlignment = TextAnchor.UpperLeft;
        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Details panel on right
        var details = new GameObject("DetailsPanel", typeof(RectTransform), typeof(Image));
        details.transform.SetParent(panel.transform, false);
        var detailsRt = details.GetComponent<RectTransform>();
        // Leave space at top for Buy/Sell mode tabs.
        SetAnchors(detailsRt, new Vector2(0.64f, 0.05f), new Vector2(0.98f, 0.86f));
        SetOffsets(detailsRt, 0f, 0f, 0f, 0f);
        var detailsImg = details.GetComponent<Image>();
        detailsImg.color = new Color(0.14f, 0.14f, 0.14f, 0.35f);

        // Mode Tabs (BUY / SELL)
        var buyTabGO = new GameObject("BuyTabButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buyTabGO.transform.SetParent(panel.transform, false);
        var buyTabRt = buyTabGO.GetComponent<RectTransform>();
        SetAnchors(buyTabRt, new Vector2(0.02f, 0.86f), new Vector2(0.32f, 0.90f));
        SetOffsets(buyTabRt, 0f, 0f, 0f, 0f);
        var buyTabImg = buyTabGO.GetComponent<Image>();
        buyTabImg.color = Color.white;
        var buyTabBtn = buyTabGO.GetComponent<Button>();
        var buyTabColors = buyTabBtn.colors;
        buyTabColors.normalColor = Color.white;
        buyTabColors.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        buyTabColors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        buyTabColors.selectedColor = Color.white;
        buyTabColors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
        buyTabBtn.colors = buyTabColors;
        var buyTabLabelGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        buyTabLabelGO.transform.SetParent(buyTabGO.transform, false);
        StretchFullScreen(buyTabLabelGO.GetComponent<RectTransform>());
        var buyTabLabel = buyTabLabelGO.GetComponent<TextMeshProUGUI>();
        buyTabLabel.text = "BUY";
        buyTabLabel.fontSize = 20;
        buyTabLabel.color = Color.black;
        buyTabLabel.fontStyle = FontStyles.Bold;
        buyTabLabel.alignment = TextAlignmentOptions.Center;
        buyTabLabel.textWrappingMode = TextWrappingModes.NoWrap;
        buyTabLabel.raycastTarget = false;
        buyTabLabel.extraPadding = true;

        var sellTabGO = new GameObject("SellTabButton", typeof(RectTransform), typeof(Image), typeof(Button));
        sellTabGO.transform.SetParent(panel.transform, false);
        var sellTabRt = sellTabGO.GetComponent<RectTransform>();
        SetAnchors(sellTabRt, new Vector2(0.32f, 0.86f), new Vector2(0.62f, 0.90f));
        SetOffsets(sellTabRt, 0f, 0f, 0f, 0f);
        var sellTabImg = sellTabGO.GetComponent<Image>();
        sellTabImg.color = Color.white;
        var sellTabBtn = sellTabGO.GetComponent<Button>();
        var sellTabColors = sellTabBtn.colors;
        sellTabColors.normalColor = Color.white;
        sellTabColors.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        sellTabColors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        sellTabColors.selectedColor = Color.white;
        sellTabColors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
        sellTabBtn.colors = sellTabColors;
        var sellTabLabelGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        sellTabLabelGO.transform.SetParent(sellTabGO.transform, false);
        StretchFullScreen(sellTabLabelGO.GetComponent<RectTransform>());
        var sellTabLabel = sellTabLabelGO.GetComponent<TextMeshProUGUI>();
        sellTabLabel.text = "SELL";
        sellTabLabel.fontSize = 20;
        sellTabLabel.color = Color.black;
        sellTabLabel.fontStyle = FontStyles.Bold;
        sellTabLabel.alignment = TextAlignmentOptions.Center;
        sellTabLabel.textWrappingMode = TextWrappingModes.NoWrap;
        sellTabLabel.raycastTarget = false;
        sellTabLabel.extraPadding = true;

        var nameText = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameText.transform.SetParent(details.transform, false);
        var nameTmp = nameText.GetComponent<TextMeshProUGUI>();
        nameTmp.fontSize = 32;
        nameTmp.color = Color.white;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.alignment = TextAlignmentOptions.Left;
        nameTmp.textWrappingMode = TextWrappingModes.NoWrap;
        var nameRt = nameText.GetComponent<RectTransform>();
        SetAnchors(nameRt, new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.95f));
        SetOffsets(nameRt, 0f, 0f, 0f, 0f);

        var priceText = new GameObject("PriceText", typeof(RectTransform), typeof(TextMeshProUGUI));
        priceText.transform.SetParent(details.transform, false);
        var priceTmp = priceText.GetComponent<TextMeshProUGUI>();
        priceTmp.fontSize = 26;
        priceTmp.color = Color.white;
        priceTmp.fontStyle = FontStyles.Bold;
        priceTmp.alignment = TextAlignmentOptions.Left;
        priceTmp.textWrappingMode = TextWrappingModes.NoWrap;
        var priceRt = priceText.GetComponent<RectTransform>();
        SetAnchors(priceRt, new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.78f));
        SetOffsets(priceRt, 0f, 0f, 0f, 0f);

        var descText = new GameObject("DescriptionText", typeof(RectTransform), typeof(TextMeshProUGUI));
        descText.transform.SetParent(details.transform, false);
        var descTmp = descText.GetComponent<TextMeshProUGUI>();
        descTmp.fontSize = 22;
        descTmp.color = Color.white;
        descTmp.alignment = TextAlignmentOptions.TopLeft;
        descTmp.textWrappingMode = TextWrappingModes.Normal;
        var descRt = descText.GetComponent<RectTransform>();
        // Leave room at the bottom for qty/buy/message controls.
        SetAnchors(descRt, new Vector2(0.05f, 0.24f), new Vector2(0.95f, 0.66f));
        SetOffsets(descRt, 0f, 0f, 0f, 0f);

        // Optional details visuals: icon + rarity label
        var detailIconGO = new GameObject("DetailIcon", typeof(RectTransform), typeof(Image));
        detailIconGO.transform.SetParent(details.transform, false);
        var detailIconRt = detailIconGO.GetComponent<RectTransform>();
        SetAnchors(detailIconRt, new Vector2(0.05f, 0.66f), new Vector2(0.18f, 0.78f));
        SetOffsets(detailIconRt, 0f, 0f, 0f, 0f);
        var detailIconImg = detailIconGO.GetComponent<Image>();
        detailIconImg.color = Color.white;
        detailIconImg.preserveAspect = true;
        detailIconImg.raycastTarget = false;
        detailIconGO.SetActive(false);

        var rarityText = new GameObject("DetailRarityText", typeof(RectTransform), typeof(TextMeshProUGUI));
        rarityText.transform.SetParent(details.transform, false);
        var rarityTmp = rarityText.GetComponent<TextMeshProUGUI>();
        rarityTmp.fontSize = 20;
        rarityTmp.color = Color.white;
        rarityTmp.alignment = TextAlignmentOptions.Left;
        rarityTmp.textWrappingMode = TextWrappingModes.NoWrap;
        var rarityRt = rarityText.GetComponent<RectTransform>();
        SetAnchors(rarityRt, new Vector2(0.20f, 0.66f), new Vector2(0.95f, 0.74f));
        SetOffsets(rarityRt, 0f, 0f, 0f, 0f);

        // Bottom controls: quantity +/- and buy button
        var qtyMinusGO = new GameObject("QtyMinusButton", typeof(RectTransform), typeof(Image), typeof(Button));
        qtyMinusGO.transform.SetParent(details.transform, false);
        var qtyMinusRt = qtyMinusGO.GetComponent<RectTransform>();
        SetAnchors(qtyMinusRt, new Vector2(0.05f, 0.14f), new Vector2(0.17f, 0.23f));
        SetOffsets(qtyMinusRt, 0f, 0f, 0f, 0f);
        var qtyMinusImg = qtyMinusGO.GetComponent<Image>();
        qtyMinusImg.color = Color.white;
        var qtyMinusBtn = qtyMinusGO.GetComponent<Button>();
        var qtyMinusColors = qtyMinusBtn.colors;
        qtyMinusColors.normalColor = Color.white;
        qtyMinusColors.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        qtyMinusColors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        qtyMinusColors.selectedColor = Color.white;
        qtyMinusColors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
        qtyMinusBtn.colors = qtyMinusColors;
        var qtyMinusLabelGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        qtyMinusLabelGO.transform.SetParent(qtyMinusGO.transform, false);
        StretchFullScreen(qtyMinusLabelGO.GetComponent<RectTransform>());
        var qtyMinusLabel = qtyMinusLabelGO.GetComponent<TextMeshProUGUI>();
        qtyMinusLabel.text = "-";
        qtyMinusLabel.fontSize = 28;
        qtyMinusLabel.color = Color.black;
        qtyMinusLabel.fontStyle = FontStyles.Bold;
        qtyMinusLabel.alignment = TextAlignmentOptions.Center;
        qtyMinusLabel.textWrappingMode = TextWrappingModes.NoWrap;
        qtyMinusLabel.raycastTarget = false;
        qtyMinusLabel.extraPadding = true;

        var qtyTextGO = new GameObject("QtyText", typeof(RectTransform), typeof(TextMeshProUGUI));
        qtyTextGO.transform.SetParent(details.transform, false);
        var qtyTextRt = qtyTextGO.GetComponent<RectTransform>();
        SetAnchors(qtyTextRt, new Vector2(0.18f, 0.14f), new Vector2(0.32f, 0.23f));
        SetOffsets(qtyTextRt, 0f, 0f, 0f, 0f);
        var qtyTextTmp = qtyTextGO.GetComponent<TextMeshProUGUI>();
        qtyTextTmp.text = "1";
        qtyTextTmp.fontSize = 22;
        qtyTextTmp.color = Color.white;
        qtyTextTmp.fontStyle = FontStyles.Bold;
        qtyTextTmp.alignment = TextAlignmentOptions.Center;
        qtyTextTmp.textWrappingMode = TextWrappingModes.NoWrap;

        var qtyPlusGO = new GameObject("QtyPlusButton", typeof(RectTransform), typeof(Image), typeof(Button));
        qtyPlusGO.transform.SetParent(details.transform, false);
        var qtyPlusRt = qtyPlusGO.GetComponent<RectTransform>();
        SetAnchors(qtyPlusRt, new Vector2(0.33f, 0.14f), new Vector2(0.45f, 0.23f));
        SetOffsets(qtyPlusRt, 0f, 0f, 0f, 0f);
        var qtyPlusImg = qtyPlusGO.GetComponent<Image>();
        qtyPlusImg.color = Color.white;
        var qtyPlusBtn = qtyPlusGO.GetComponent<Button>();
        var qtyPlusColors = qtyPlusBtn.colors;
        qtyPlusColors.normalColor = Color.white;
        qtyPlusColors.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        qtyPlusColors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        qtyPlusColors.selectedColor = Color.white;
        qtyPlusColors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
        qtyPlusBtn.colors = qtyPlusColors;
        var qtyPlusLabelGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        qtyPlusLabelGO.transform.SetParent(qtyPlusGO.transform, false);
        StretchFullScreen(qtyPlusLabelGO.GetComponent<RectTransform>());
        var qtyPlusLabel = qtyPlusLabelGO.GetComponent<TextMeshProUGUI>();
        qtyPlusLabel.text = "+";
        qtyPlusLabel.fontSize = 28;
        qtyPlusLabel.color = Color.black;
        qtyPlusLabel.fontStyle = FontStyles.Bold;
        qtyPlusLabel.alignment = TextAlignmentOptions.Center;
        qtyPlusLabel.textWrappingMode = TextWrappingModes.NoWrap;
        qtyPlusLabel.raycastTarget = false;
        qtyPlusLabel.extraPadding = true;

        var buyGO = new GameObject("BuyButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buyGO.transform.SetParent(details.transform, false);
        var buyRt = buyGO.GetComponent<RectTransform>();
        SetAnchors(buyRt, new Vector2(0.62f, 0.14f), new Vector2(0.95f, 0.23f));
        SetOffsets(buyRt, 0f, 0f, 0f, 0f);
        var buyImg = buyGO.GetComponent<Image>();
        buyImg.color = Color.white;
        var buyBtn = buyGO.GetComponent<Button>();
        var buyColors = buyBtn.colors;
        buyColors.normalColor = Color.white;
        buyColors.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        buyColors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        buyColors.selectedColor = Color.white;
        buyColors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
        buyBtn.colors = buyColors;
        var buyLabelGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        buyLabelGO.transform.SetParent(buyGO.transform, false);
        StretchFullScreen(buyLabelGO.GetComponent<RectTransform>());
        var buyLabel = buyLabelGO.GetComponent<TextMeshProUGUI>();
        buyLabel.text = "Buy";
        buyLabel.fontSize = 24;
        buyLabel.color = Color.black;
        buyLabel.fontStyle = FontStyles.Bold;
        buyLabel.alignment = TextAlignmentOptions.Center;
        buyLabel.textWrappingMode = TextWrappingModes.NoWrap;
        buyLabel.raycastTarget = false;
        buyLabel.extraPadding = true;

        // Sell button (hidden by default; toggled by MerchantShopUI)
        var sellGO = new GameObject("SellButton", typeof(RectTransform), typeof(Image), typeof(Button));
        sellGO.transform.SetParent(details.transform, false);
        var sellRt = sellGO.GetComponent<RectTransform>();
        SetAnchors(sellRt, new Vector2(0.62f, 0.14f), new Vector2(0.95f, 0.23f));
        SetOffsets(sellRt, 0f, 0f, 0f, 0f);
        var sellImg = sellGO.GetComponent<Image>();
        sellImg.color = Color.white;
        var sellBtn = sellGO.GetComponent<Button>();
        var sellColors = sellBtn.colors;
        sellColors.normalColor = Color.white;
        sellColors.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        sellColors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        sellColors.selectedColor = Color.white;
        sellColors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
        sellBtn.colors = sellColors;
        var sellLabelGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        sellLabelGO.transform.SetParent(sellGO.transform, false);
        StretchFullScreen(sellLabelGO.GetComponent<RectTransform>());
        var sellLabel = sellLabelGO.GetComponent<TextMeshProUGUI>();
        sellLabel.text = "Sell";
        sellLabel.fontSize = 24;
        sellLabel.color = Color.black;
        sellLabel.fontStyle = FontStyles.Bold;
        sellLabel.alignment = TextAlignmentOptions.Center;
        sellLabel.textWrappingMode = TextWrappingModes.NoWrap;
        sellLabel.raycastTarget = false;
        sellLabel.extraPadding = true;
        sellGO.SetActive(false);

        // Status message line
        var messageGO = new GameObject("MessageText", typeof(RectTransform), typeof(TextMeshProUGUI));
        messageGO.transform.SetParent(details.transform, false);
        var messageRt = messageGO.GetComponent<RectTransform>();
        SetAnchors(messageRt, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.13f));
        SetOffsets(messageRt, 0f, 0f, 0f, 0f);
        var messageTmp = messageGO.GetComponent<TextMeshProUGUI>();
        messageTmp.text = string.Empty;
        messageTmp.fontSize = 18;
        messageTmp.color = new Color(1f, 0.95f, 0.7f, 1f);
        messageTmp.alignment = TextAlignmentOptions.Left;
        messageTmp.textWrappingMode = TextWrappingModes.NoWrap;

        // Create Row prefab under project
        string prefabFolder = "Assets/Prefabs/UI";
        if (!AssetDatabase.IsValidFolder(prefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
        }
        var rowGO = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(Button));
        var rowRt = rowGO.GetComponent<RectTransform>();
        SetAnchors(rowRt, new Vector2(0f, 1f), new Vector2(1f, 1f));
        rowRt.pivot = new Vector2(0.5f, 0.5f);
        rowRt.sizeDelta = new Vector2(0f, 52f);
        var rowLayout = rowGO.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 52f;

        var rowImg = rowGO.GetComponent<Image>();
        // Default unselected color (selection logic will adjust at runtime).
        rowImg.color = new Color(0.10f, 0.10f, 0.10f, 0.85f);

        // Optional visuals: rarity strip + icon
        var rarityStripGO = new GameObject("RarityStrip", typeof(RectTransform), typeof(Image));
        rarityStripGO.transform.SetParent(rowGO.transform, false);
        var rarityStripRt = rarityStripGO.GetComponent<RectTransform>();
        SetAnchors(rarityStripRt, new Vector2(0f, 0f), new Vector2(0f, 1f));
        rarityStripRt.pivot = new Vector2(0f, 0.5f);
        rarityStripRt.anchoredPosition = Vector2.zero;
        rarityStripRt.sizeDelta = new Vector2(6f, 0f);
        var rarityStripImg = rarityStripGO.GetComponent<Image>();
        rarityStripImg.color = new Color(0.92f, 0.92f, 0.92f, 1f);
        rarityStripImg.raycastTarget = false;

        var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(rowGO.transform, false);
        var iconRt = iconGO.GetComponent<RectTransform>();
        SetAnchors(iconRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(12f, 0f);
        iconRt.sizeDelta = new Vector2(34f, 34f);
        var iconImg = iconGO.GetComponent<Image>();
        iconImg.color = Color.white;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        iconGO.SetActive(false);

        var nameChild = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameChild.transform.SetParent(rowGO.transform, false);
        var nameChildRt = nameChild.GetComponent<RectTransform>();
        // Leave room on the left for rarity strip + icon.
        SetAnchors(nameChildRt, new Vector2(0.09f, 0f), new Vector2(0.70f, 1f));
        SetOffsets(nameChildRt, 0f, 0f, 0f, 0f);
        var nameRowTmp = nameChild.GetComponent<TextMeshProUGUI>();
        nameRowTmp.fontSize = 26;
        nameRowTmp.color = Color.white;
        nameRowTmp.fontStyle = FontStyles.Bold;
        nameRowTmp.alignment = TextAlignmentOptions.Left;
        nameRowTmp.textWrappingMode = TextWrappingModes.NoWrap;
        var priceChild = new GameObject("PriceText", typeof(RectTransform), typeof(TextMeshProUGUI));
        priceChild.transform.SetParent(rowGO.transform, false);
        var priceChildRt = priceChild.GetComponent<RectTransform>();
        SetAnchors(priceChildRt, new Vector2(0.70f, 0f), new Vector2(0.97f, 1f));
        SetOffsets(priceChildRt, 0f, 0f, 0f, 0f);
        var priceRowTmp = priceChild.GetComponent<TextMeshProUGUI>();
        priceRowTmp.fontSize = 26;
        priceRowTmp.color = Color.white;
        priceRowTmp.fontStyle = FontStyles.Bold;
        priceRowTmp.alignment = TextAlignmentOptions.Right;
        priceRowTmp.textWrappingMode = TextWrappingModes.NoWrap;
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
        soRow.FindProperty("background").objectReferenceValue = rowImg;
        soRow.FindProperty("iconImage").objectReferenceValue = iconImg;
        soRow.FindProperty("rarityStrip").objectReferenceValue = rarityStripImg;
        soRow.FindProperty("nameText").objectReferenceValue = nameChild.GetComponent<TextMeshProUGUI>();
        soRow.FindProperty("priceText").objectReferenceValue = priceChild.GetComponent<TextMeshProUGUI>();
        soRow.FindProperty("button").objectReferenceValue = rowGO.GetComponent<Button>();
        soRow.ApplyModifiedProperties();

        string prefabPath = prefabFolder + "/MerchantShopRowUI_Row.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(rowGO, prefabPath);
        Object.DestroyImmediate(rowGO);

        // Create MerchantShopUI GameObject and assign serialized fields
        var uiGO = new GameObject("MerchantShopUI", typeof(RectTransform));
        uiGO.transform.SetParent(canvasGO.transform, false);
        StretchFullScreen(uiGO.GetComponent<RectTransform>());
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
        so.FindProperty("detailIconImage").objectReferenceValue = detailIconImg;
        so.FindProperty("detailRarityText").objectReferenceValue = rarityTmp;
        so.FindProperty("buyTabButton").objectReferenceValue = buyTabBtn;
        so.FindProperty("sellTabButton").objectReferenceValue = sellTabBtn;
        so.FindProperty("buyButton").objectReferenceValue = buyBtn;
        so.FindProperty("sellButton").objectReferenceValue = sellBtn;
        so.FindProperty("qtyMinusButton").objectReferenceValue = qtyMinusBtn;
        so.FindProperty("qtyPlusButton").objectReferenceValue = qtyPlusBtn;
        so.FindProperty("qtyText").objectReferenceValue = qtyTextTmp;
        so.FindProperty("messageText").objectReferenceValue = messageTmp;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("goldText").objectReferenceValue = gold;
        so.ApplyModifiedProperties();

        // Default the root to inactive
        root.SetActive(false);

        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.Refresh();

        Debug.Log("Built MerchantShop UI hierarchy and prefab.");
    }

    private static bool TryUpgradeExistingMerchantShopUIsInScene(out string summary)
    {
        summary = string.Empty;

        // Includes inactive objects.
        var all = Resources.FindObjectsOfTypeAll<Abyss.Shop.MerchantShopUI>();
        if (all == null || all.Length == 0)
            return false;

        // Only scene instances (not prefab assets).
        var sceneUis = all
            .Where(ui => ui != null && ui.gameObject != null)
            .Where(ui => ui.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(ui.gameObject))
            .ToArray();

        if (sceneUis.Length == 0)
            return false;

        int upgradedUiCount = 0;
        int upgradedPrefabCount = 0;
        int createdSceneObjects = 0;

        foreach (var ui in sceneUis)
        {
            if (ui == null) continue;

            bool wiredThisUi = false;

            // Wire/ensure details visuals.
            if (TryWireDetailsVisuals(ui, ref createdSceneObjects))
                wiredThisUi = true;

            // Wire/ensure mode tabs and sell button.
            if (TryWireModeTabsAndSellButton(ui, ref createdSceneObjects))
                wiredThisUi = true;

            if (wiredThisUi)
                upgradedUiCount++;

            // Upgrade referenced row prefab (if any).
            var rowPrefab = GetSerializedObjectField<Abyss.Shop.MerchantShopRowUI>(ui, "rowPrefab");
            if (rowPrefab != null)
            {
                var prefabPath = AssetDatabase.GetAssetPath(rowPrefab);
                if (string.IsNullOrWhiteSpace(prefabPath))
                    prefabPath = AssetDatabase.GetAssetPath(rowPrefab.gameObject);
                if (string.IsNullOrWhiteSpace(prefabPath))
                    prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(rowPrefab.gameObject);

                if (!string.IsNullOrWhiteSpace(prefabPath) && UpgradeRowPrefabAsset(prefabPath))
                    upgradedPrefabCount++;
            }
        }

        summary = $"[BuildMerchantShopUIEditor] Upgraded existing MerchantShopUI(s) in scene: uiWired={upgradedUiCount}/{sceneUis.Length}, rowPrefabUpgraded={upgradedPrefabCount}, sceneObjectsCreated={createdSceneObjects}.";
        return true;
    }

    private static bool TryWireModeTabsAndSellButton(Abyss.Shop.MerchantShopUI ui, ref int createdSceneObjects)
    {
        // Determine the panel via scroll rect, which is already wired in existing scenes.
        var scrollRect = GetSerializedObjectField<ScrollRect>(ui, "scrollRect");
        if (scrollRect == null) return false;

        var panel = scrollRect.transform != null ? scrollRect.transform.parent : null;
        // scrollRect is on ItemsScrollView; parent should be the panel.
        if (panel == null) return false;

        // Ensure layout space for tabs.
        var svRt = scrollRect.GetComponent<RectTransform>();
        if (svRt != null && Mathf.Abs(svRt.anchorMax.y - 0.90f) < 0.001f)
            svRt.anchorMax = new Vector2(svRt.anchorMax.x, 0.86f);

        var detailName = GetSerializedObjectField<TMP_Text>(ui, "detailNameText");
        var detailsPanel = detailName != null && detailName.transform != null ? detailName.transform.parent : null;
        if (detailsPanel != null)
        {
            var detailsRt = detailsPanel.GetComponent<RectTransform>();
            if (detailsRt != null && Mathf.Abs(detailsRt.anchorMax.y - 0.90f) < 0.001f)
                detailsRt.anchorMax = new Vector2(detailsRt.anchorMax.x, 0.86f);
        }

        // Create tab buttons on the panel.
        var buyTab = FindOrCreateChild(panel, "BuyTabButton", ref createdSceneObjects);
        var buyTabImg = EnsureComponent<Image>(buyTab, ref createdSceneObjects);
        var buyTabBtn = EnsureComponent<Button>(buyTab, ref createdSceneObjects);
        ConfigureSimpleTabButton(buyTabBtn, buyTabImg, "BUY");
        var buyTabRt = buyTab.GetComponent<RectTransform>();
        if (buyTabRt != null)
        {
            SetAnchors(buyTabRt, new Vector2(0.02f, 0.86f), new Vector2(0.32f, 0.90f));
            SetOffsets(buyTabRt, 0f, 0f, 0f, 0f);
        }

        var sellTab = FindOrCreateChild(panel, "SellTabButton", ref createdSceneObjects);
        var sellTabImg = EnsureComponent<Image>(sellTab, ref createdSceneObjects);
        var sellTabBtn = EnsureComponent<Button>(sellTab, ref createdSceneObjects);
        ConfigureSimpleTabButton(sellTabBtn, sellTabImg, "SELL");
        var sellTabRt = sellTab.GetComponent<RectTransform>();
        if (sellTabRt != null)
        {
            SetAnchors(sellTabRt, new Vector2(0.32f, 0.86f), new Vector2(0.62f, 0.90f));
            SetOffsets(sellTabRt, 0f, 0f, 0f, 0f);
        }

        // Create sell button inside details panel alongside buy button.
        var buyButton = GetSerializedObjectField<Button>(ui, "buyButton");
        if (buyButton == null) return false;
        var details = buyButton.transform != null ? buyButton.transform.parent : null;
        if (details == null) return false;

        var sellGo = FindOrCreateChild(details, "SellButton", ref createdSceneObjects);
        var sellImg = EnsureComponent<Image>(sellGo, ref createdSceneObjects);
        var sellBtn = EnsureComponent<Button>(sellGo, ref createdSceneObjects);
        ConfigureSimpleActionButton(sellBtn, sellImg, "Sell");
        var sellRt = sellGo.GetComponent<RectTransform>();
        if (sellRt != null)
        {
            SetAnchors(sellRt, new Vector2(0.62f, 0.14f), new Vector2(0.95f, 0.23f));
            SetOffsets(sellRt, 0f, 0f, 0f, 0f);
        }
        if (sellGo.activeSelf) sellGo.SetActive(false);

        var so = new SerializedObject(ui);
        so.FindProperty("buyTabButton").objectReferenceValue = buyTabBtn;
        so.FindProperty("sellTabButton").objectReferenceValue = sellTabBtn;
        so.FindProperty("sellButton").objectReferenceValue = sellBtn;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(ui);
        return true;
    }

    private static void ConfigureSimpleTabButton(Button btn, Image img, string label)
    {
        if (img != null)
            img.color = Color.white;

        if (btn != null)
        {
            var c = btn.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            c.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            c.selectedColor = Color.white;
            c.disabledColor = new Color(1f, 1f, 1f, 0.5f);
            btn.colors = c;
        }

        EnsureButtonLabel(btn != null ? btn.gameObject : null, label, 20);
    }

    private static void ConfigureSimpleActionButton(Button btn, Image img, string label)
    {
        if (img != null)
            img.color = Color.white;

        if (btn != null)
        {
            var c = btn.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            c.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            c.selectedColor = Color.white;
            c.disabledColor = new Color(1f, 1f, 1f, 0.5f);
            btn.colors = c;
        }

        EnsureButtonLabel(btn != null ? btn.gameObject : null, label, 24);
    }

    private static void EnsureButtonLabel(GameObject buttonGo, string text, int fontSize)
    {
        if (buttonGo == null) return;
        var existing = buttonGo.transform.Find("Text");
        GameObject labelGo = existing != null ? existing.gameObject : null;
        if (labelGo == null)
        {
            labelGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(buttonGo.transform, false);
            StretchFullScreen(labelGo.GetComponent<RectTransform>());
        }

        var tmp = labelGo.GetComponent<TextMeshProUGUI>();
        if (tmp == null) tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.black;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        tmp.extraPadding = true;
    }

    private static bool TryWireDetailsVisuals(Abyss.Shop.MerchantShopUI ui, ref int createdSceneObjects)
    {
        // We locate the details panel via existing assigned NameText (if present).
        var detailName = GetSerializedObjectField<TMP_Text>(ui, "detailNameText");
        if (detailName == null)
            return false;

        var detailsPanel = detailName.transform != null ? detailName.transform.parent : null;
        if (detailsPanel == null)
            return false;

        var detailIcon = FindOrCreateChild(detailsPanel, "DetailIcon", ref createdSceneObjects);
        var detailIconImage = EnsureComponent<Image>(detailIcon, ref createdSceneObjects);
        ConfigureDetailsIconRect(detailIconImage);

        var rarityTextGo = FindOrCreateChild(detailsPanel, "DetailRarityText", ref createdSceneObjects);
        var rarityTmp = EnsureComponent<TextMeshProUGUI>(rarityTextGo, ref createdSceneObjects);
        ConfigureDetailsRarityRect(rarityTmp);

        var so = new SerializedObject(ui);
        so.FindProperty("detailIconImage").objectReferenceValue = detailIconImage;
        so.FindProperty("detailRarityText").objectReferenceValue = rarityTmp;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(ui);
        return true;
    }

    private static T GetSerializedObjectField<T>(UnityEngine.Object obj, string fieldName) where T : UnityEngine.Object
    {
        if (obj == null) return null;
        var so = new SerializedObject(obj);
        var prop = so.FindProperty(fieldName);
        return prop != null ? prop.objectReferenceValue as T : null;
    }

    private static bool UpgradeRowPrefabAsset(string prefabPath)
    {
        if (string.IsNullOrWhiteSpace(prefabPath))
            return false;

        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null) return false;

            var row = root.GetComponent<Abyss.Shop.MerchantShopRowUI>();
            if (row == null) return false;

            int created = 0;
            var rarityStripGo = FindOrCreateChild(root.transform, "RarityStrip", ref created);
            var rarityStripImg = EnsureComponent<Image>(rarityStripGo, ref created);
            ConfigureRowRarityStripRect(rarityStripImg);

            var iconGo = FindOrCreateChild(root.transform, "Icon", ref created);
            var iconImg = EnsureComponent<Image>(iconGo, ref created);
            ConfigureRowIconRect(iconImg);
            if (iconGo.activeSelf) iconGo.SetActive(false);

            // Adjust the NameText anchors slightly to make room (only if it still looks like the old layout).
            var nameText = GetSerializedObjectField<TextMeshProUGUI>(row, "nameText");
            if (nameText != null)
            {
                var rt = nameText.rectTransform;
                if (rt != null && Mathf.Abs(rt.anchorMin.x - 0.03f) < 0.001f)
                {
                    rt.anchorMin = new Vector2(0.09f, rt.anchorMin.y);
                }
            }

            var soRow = new SerializedObject(row);
            soRow.FindProperty("iconImage").objectReferenceValue = iconImg;
            soRow.FindProperty("rarityStrip").objectReferenceValue = rarityStripImg;
            soRow.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            return true;
        }
        finally
        {
            if (root != null)
                PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject FindOrCreateChild(Transform parent, string name, ref int createdCount)
    {
        if (parent == null) return null;

        var t = parent.Find(name);
        if (t != null) return t.gameObject;

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        createdCount++;
        return go;
    }

    private static T EnsureComponent<T>(GameObject go, ref int createdCount) where T : Component
    {
        if (go == null) return null;
        var c = go.GetComponent<T>();
        if (c != null) return c;
        createdCount++;
        return go.AddComponent<T>();
    }

    private static void ConfigureRowRarityStripRect(Image strip)
    {
        if (strip == null) return;
        strip.raycastTarget = false;
        strip.color = new Color(0.92f, 0.92f, 0.92f, 1f);

        var rt = strip.rectTransform;
        if (rt == null) return;
        SetAnchors(rt, new Vector2(0f, 0f), new Vector2(0f, 1f));
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(6f, 0f);
    }

    private static void ConfigureRowIconRect(Image icon)
    {
        if (icon == null) return;
        icon.raycastTarget = false;
        icon.color = Color.white;
        icon.preserveAspect = true;

        var rt = icon.rectTransform;
        if (rt == null) return;
        SetAnchors(rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(12f, 0f);
        rt.sizeDelta = new Vector2(34f, 34f);
    }

    private static void ConfigureDetailsIconRect(Image icon)
    {
        if (icon == null) return;
        icon.raycastTarget = false;
        icon.color = Color.white;
        icon.preserveAspect = true;

        var rt = icon.rectTransform;
        if (rt == null) return;
        // Reasonable default; caller parent is the details panel.
        SetAnchors(rt, new Vector2(0.05f, 0.66f), new Vector2(0.18f, 0.78f));
        SetOffsets(rt, 0f, 0f, 0f, 0f);
    }

    private static void ConfigureDetailsRarityRect(TextMeshProUGUI tmp)
    {
        if (tmp == null) return;
        tmp.fontSize = 20;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;

        var rt = tmp.rectTransform;
        if (rt == null) return;
        SetAnchors(rt, new Vector2(0.20f, 0.66f), new Vector2(0.95f, 0.74f));
        SetOffsets(rt, 0f, 0f, 0f, 0f);
    }

    private static void EnsureEventSystemExists()
    {
        // Includes inactive objects.
        var existing = Object.FindAnyObjectByType<EventSystem>();
        if (existing != null)
        {
            return;
        }

        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
    }

    private static void DestroySceneObjectsByName(string name)
    {
        var gos = FindSceneObjectsByName(name);
        if (gos == null || gos.Length == 0)
        {
            return;
        }

        // Destroy immediately as requested for editor rebuild tools.
        foreach (var go in gos)
        {
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }
    }

    private static GameObject[] FindSceneObjectsByName(string name)
    {
        // Resources.FindObjectsOfTypeAll includes inactive and objects not returned by FindObjectsOfType.
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(go => go != null && go.name == name)
            .Where(go => go.scene.IsValid() && !EditorUtility.IsPersistent(go))
            .OrderByDescending(go => go.activeInHierarchy)
            .ToArray();
    }

    private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
    }

    // For stretched rects, left/right/top/bottom padding.
    private static void SetOffsets(RectTransform rt, float left, float right, float bottom, float top)
    {
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    private static void StretchFullScreen(RectTransform rt)
    {
        SetAnchors(rt, Vector2.zero, Vector2.one);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        SetOffsets(rt, 0f, 0f, 0f, 0f);
    }
}
