using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Abyss.Inventory.EditorTools
{
    public static class BuildPlayerInventoryUIEditor
    {
        [MenuItem("Tools/Build Player Inventory UI (Editor)")]
        public static void Build()
        {
            if (TryUpgradeExisting(out var upgradeSummary))
            {
                Debug.Log(upgradeSummary);
                return;
            }

            BuildFresh();
        }

        private static void BuildFresh()
        {
            DestroySceneObjectsByName("PlayerInventoryUICanvas");
            DestroySceneObjectsByName("PlayerInventoryUIRoot");
            DestroySceneObjectsByName("PlayerInventoryUI");

            var canvasGO = new GameObject("PlayerInventoryUICanvas", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create PlayerInventoryUICanvas");

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // keep above most gameplay UI
            canvasGO.AddComponent<GraphicRaycaster>();

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            StretchFullScreen(canvasGO.GetComponent<RectTransform>());

            var root = new GameObject("PlayerInventoryUIRoot", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Create PlayerInventoryUIRoot");
            root.transform.SetParent(canvasGO.transform, false);
            StretchFullScreen(root.GetComponent<RectTransform>());

            // Backdrop
            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(root.transform, false);
            StretchFullScreen(backdrop.GetComponent<RectTransform>());
            backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            // Panel
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var panelRt = panel.GetComponent<RectTransform>();
            SetAnchors(panelRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            panelRt.sizeDelta = new Vector2(1100, 650);
            panel.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

            // UI controller host
            var uiGO = new GameObject("PlayerInventoryUI", typeof(RectTransform), typeof(Abyss.Inventory.PlayerInventoryUI));
            uiGO.transform.SetParent(canvasGO.transform, false);
            StretchFullScreen(uiGO.GetComponent<RectTransform>());

            // Header title
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(panel.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            SetAnchors(titleRt, new Vector2(0.04f, 0.90f), new Vector2(0.50f, 0.98f));
            SetOffsets(titleRt, 0, 0, 0, 0);
            var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "Inventory";
            titleTmp.fontSize = 36;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = Color.white;
            titleTmp.alignment = TextAlignmentOptions.Left;

            // Close button
            var closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(panel.transform, false);
            var closeRt = closeGo.GetComponent<RectTransform>();
            SetAnchors(closeRt, new Vector2(0.90f, 0.92f), new Vector2(0.98f, 0.98f));
            SetOffsets(closeRt, 0, 0, 0, 0);
            closeGo.GetComponent<Image>().color = Color.white;
            var closeBtn = closeGo.GetComponent<Button>();
            EnsureButtonLabel(closeGo, "X", 28);

            // Gold text
            var goldGo = new GameObject("GoldText", typeof(RectTransform), typeof(TextMeshProUGUI));
            goldGo.transform.SetParent(panel.transform, false);
            var goldRt = goldGo.GetComponent<RectTransform>();
            SetAnchors(goldRt, new Vector2(0.04f, 0.84f), new Vector2(0.50f, 0.90f));
            SetOffsets(goldRt, 0, 0, 0, 0);
            var goldTmp = goldGo.GetComponent<TextMeshProUGUI>();
            goldTmp.text = "Gold: 0";
            goldTmp.fontSize = 24;
            goldTmp.color = Color.white;
            goldTmp.alignment = TextAlignmentOptions.Left;

            // ScrollView (left)
            var scrollView = new GameObject("ItemsScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollView.transform.SetParent(panel.transform, false);
            var svRt = scrollView.GetComponent<RectTransform>();
            ConfigureSplitRegionRect(svRt, new Vector2(0.04f, 0.06f), new Vector2(0.60f, 0.84f));
            scrollView.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollView.transform, false);
            var viewportRt = viewport.GetComponent<RectTransform>();
            StretchFullScreen(viewportRt);
            viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            ConfigureContentRect(contentRt);

            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 6f;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childAlignment = TextAnchor.UpperLeft;

            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var scrollRect = scrollView.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportRt;
            scrollRect.content = contentRt;
            scrollRect.horizontal = false;

            // Prevent scroll container from being shrunk by layout components.
            RemoveIfPresent<ContentSizeFitter>(scrollView);
            RemoveIfPresent<LayoutElement>(scrollView);
            RemoveIfPresent<HorizontalLayoutGroup>(scrollView);
            RemoveIfPresent<VerticalLayoutGroup>(scrollView);

            // Details (right)
            var details = new GameObject("DetailsPanel", typeof(RectTransform), typeof(Image), typeof(Abyss.Inventory.PlayerInventoryDetailsUI));
            details.transform.SetParent(panel.transform, false);
            var detailsRt = details.GetComponent<RectTransform>();
            ConfigureSplitRegionRect(detailsRt, new Vector2(0.62f, 0.06f), new Vector2(0.98f, 0.84f));
            details.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

            // Details icon
            var dIcon = new GameObject("DetailIcon", typeof(RectTransform), typeof(Image));
            dIcon.transform.SetParent(details.transform, false);
            var dIconRt = dIcon.GetComponent<RectTransform>();
            SetAnchors(dIconRt, new Vector2(0.06f, 0.80f), new Vector2(0.22f, 0.94f));
            SetOffsets(dIconRt, 0, 0, 0, 0);
            var dIconImg = dIcon.GetComponent<Image>();
            dIconImg.color = Color.white;
            dIconImg.preserveAspect = true;
            dIcon.SetActive(false);

            var dName = CreateDetailsText(details.transform, "DetailName", new Vector2(0.26f, 0.87f), new Vector2(0.94f, 0.96f), 28, FontStyles.Bold);
            var dRarity = CreateDetailsText(details.transform, "DetailRarity", new Vector2(0.26f, 0.80f), new Vector2(0.94f, 0.86f), 20, FontStyles.Normal);
            var dCount = CreateDetailsText(details.transform, "DetailCount", new Vector2(0.06f, 0.74f), new Vector2(0.94f, 0.80f), 20, FontStyles.Normal);

            var dDesc = CreateDetailsText(details.transform, "DetailDescription", new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.72f), 18, FontStyles.Normal);
            dDesc.textWrappingMode = TextWrappingModes.Normal;
            dDesc.alignment = TextAlignmentOptions.TopLeft;

            // Row template (disabled)
            var rowTemplate = BuildRowTemplate(content.transform);
            rowTemplate.SetActive(false);

            // Wire references
            var ui = uiGO.GetComponent<Abyss.Inventory.PlayerInventoryUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("root").objectReferenceValue = root;
            so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            so.FindProperty("titleText").objectReferenceValue = titleTmp;
            so.FindProperty("goldText").objectReferenceValue = goldTmp;
            so.FindProperty("scrollRect").objectReferenceValue = scrollRect;
            so.FindProperty("contentRoot").objectReferenceValue = contentRt;
            so.FindProperty("rowTemplate").objectReferenceValue = rowTemplate.GetComponent<Abyss.Inventory.PlayerInventoryRowUI>();
            so.FindProperty("detailsUI").objectReferenceValue = details.GetComponent<Abyss.Inventory.PlayerInventoryDetailsUI>();
            so.ApplyModifiedProperties();

            var detailsSo = new SerializedObject(details.GetComponent<Abyss.Inventory.PlayerInventoryDetailsUI>());
            detailsSo.FindProperty("iconImage").objectReferenceValue = dIconImg;
            detailsSo.FindProperty("nameText").objectReferenceValue = dName;
            detailsSo.FindProperty("rarityText").objectReferenceValue = dRarity;
            detailsSo.FindProperty("countText").objectReferenceValue = dCount;
            detailsSo.FindProperty("descriptionText").objectReferenceValue = dDesc;
            detailsSo.ApplyModifiedProperties();

            // Default inactive
            root.SetActive(false);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[BuildPlayerInventoryUIEditor] Built Player Inventory UI.");
        }

        private static bool TryUpgradeExisting(out string summary)
        {
            summary = string.Empty;

            var all = Resources.FindObjectsOfTypeAll<Abyss.Inventory.PlayerInventoryUI>();
            if (all == null || all.Length == 0)
                return false;

            var sceneUis = all
                .Where(ui => ui != null && ui.gameObject != null)
                .Where(ui => ui.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(ui.gameObject))
                .ToArray();

            if (sceneUis.Length == 0)
                return false;

            int wired = 0;
            int created = 0;

            foreach (var ui in sceneUis)
            {
                if (ui == null) continue;
                if (TryWireByName(ui, ref created))
                    wired++;
            }

            summary = $"[BuildPlayerInventoryUIEditor] Upgraded existing PlayerInventoryUI(s): uiWired={wired}/{sceneUis.Length}, sceneObjectsCreated={created}.";
            return true;
        }

        private static bool TryWireByName(Abyss.Inventory.PlayerInventoryUI ui, ref int created)
        {
            // Find the canvas/root/panel by convention.
            var canvas = GameObject.Find("PlayerInventoryUICanvas");
            var root = GameObject.Find("PlayerInventoryUIRoot");
            if (canvas == null || root == null)
                return false;

            var panel = FindDeepChild(root.transform, "Panel");
            if (panel == null)
                return false;

            var closeBtn = FindDeepChild(panel, "CloseButton")?.GetComponent<Button>();
            var title = FindDeepChild(panel, "Title")?.GetComponent<TextMeshProUGUI>();
            var gold = FindDeepChild(panel, "GoldText")?.GetComponent<TextMeshProUGUI>();

            var scrollRect = FindDeepChild(panel, "ItemsScrollView")?.GetComponent<ScrollRect>();
            var content = FindDeepChild(panel, "Content")?.GetComponent<RectTransform>();

            if (scrollRect != null)
                UpgradeScrollHierarchy(panel.gameObject, scrollRect, ref created);

            if (content != null)
            {
                EnsureListContentLayoutComponents(content, ref created);
                ConfigureContentRect(content);
            }

            if (content != null)
                EnsureListContentLayoutComponents(content, ref created);

            var detailsPanel = FindDeepChild(panel, "DetailsPanel");
            var details = detailsPanel != null ? detailsPanel.GetComponent<Abyss.Inventory.PlayerInventoryDetailsUI>() : null;

            // Ensure row template exists.
            GameObject rowTemplate = FindDeepChild(panel, "RowTemplate") != null ? FindDeepChild(panel, "RowTemplate").gameObject : null;
            if (rowTemplate == null && content != null)
            {
                rowTemplate = BuildRowTemplate(content);
                rowTemplate.name = "RowTemplate";
                rowTemplate.SetActive(false);
                created++;
            }
            else if (rowTemplate != null)
            {
                var rt = rowTemplate.GetComponent<RectTransform>();
                ConfigureRowTemplateRect(rt);

                var le = rowTemplate.GetComponent<LayoutElement>();
                if (le == null) le = rowTemplate.AddComponent<LayoutElement>();
                le.minHeight = 56f;
                le.preferredHeight = 56f;
                le.flexibleHeight = 0f;

                if (rowTemplate.activeSelf)
                    rowTemplate.SetActive(false);
            }

            if (detailsPanel != null && details == null)
                details = detailsPanel.gameObject.AddComponent<Abyss.Inventory.PlayerInventoryDetailsUI>();

            // Ensure detail children exist.
            if (detailsPanel != null)
                EnsureDetailsChildren(detailsPanel.gameObject, ref created);

            var rootGo = root;

            var so = new SerializedObject(ui);
            if (so.FindProperty("root") != null) so.FindProperty("root").objectReferenceValue = rootGo;
            if (so.FindProperty("closeButton") != null) so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            if (so.FindProperty("titleText") != null) so.FindProperty("titleText").objectReferenceValue = title;
            if (so.FindProperty("goldText") != null) so.FindProperty("goldText").objectReferenceValue = gold;
            if (so.FindProperty("scrollRect") != null) so.FindProperty("scrollRect").objectReferenceValue = scrollRect;
            if (so.FindProperty("contentRoot") != null) so.FindProperty("contentRoot").objectReferenceValue = content;
            if (so.FindProperty("rowTemplate") != null) so.FindProperty("rowTemplate").objectReferenceValue = rowTemplate != null ? rowTemplate.GetComponent<Abyss.Inventory.PlayerInventoryRowUI>() : null;
            if (so.FindProperty("detailsUI") != null) so.FindProperty("detailsUI").objectReferenceValue = details;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (details != null)
            {
                var icon = FindDeepChild(detailsPanel, "DetailIcon")?.GetComponent<Image>();
                var dName = FindDeepChild(detailsPanel, "DetailName")?.GetComponent<TextMeshProUGUI>();
                var dRarity = FindDeepChild(detailsPanel, "DetailRarity")?.GetComponent<TextMeshProUGUI>();
                var dCount = FindDeepChild(detailsPanel, "DetailCount")?.GetComponent<TextMeshProUGUI>();
                var dDesc = FindDeepChild(detailsPanel, "DetailDescription")?.GetComponent<TextMeshProUGUI>();

                var dso = new SerializedObject(details);
                dso.FindProperty("iconImage").objectReferenceValue = icon;
                dso.FindProperty("nameText").objectReferenceValue = dName;
                dso.FindProperty("rarityText").objectReferenceValue = dRarity;
                dso.FindProperty("countText").objectReferenceValue = dCount;
                dso.FindProperty("descriptionText").objectReferenceValue = dDesc;
                dso.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(details);
            }

            EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return true;
        }

        private static void EnsureDetailsChildren(GameObject detailsPanel, ref int created)
        {
            if (detailsPanel == null) return;

            if (FindDeepChild(detailsPanel, "DetailIcon") == null)
            {
                var dIcon = new GameObject("DetailIcon", typeof(RectTransform), typeof(Image));
                dIcon.transform.SetParent(detailsPanel.transform, false);
                var rt = dIcon.GetComponent<RectTransform>();
                SetAnchors(rt, new Vector2(0.06f, 0.80f), new Vector2(0.22f, 0.94f));
                SetOffsets(rt, 0, 0, 0, 0);
                var img = dIcon.GetComponent<Image>();
                img.color = Color.white;
                img.preserveAspect = true;
                dIcon.SetActive(false);
                created++;
            }

            EnsureText(detailsPanel.transform, "DetailName", new Vector2(0.26f, 0.87f), new Vector2(0.94f, 0.96f), 28, FontStyles.Bold, ref created);
            EnsureText(detailsPanel.transform, "DetailRarity", new Vector2(0.26f, 0.80f), new Vector2(0.94f, 0.86f), 20, FontStyles.Normal, ref created);
            EnsureText(detailsPanel.transform, "DetailCount", new Vector2(0.06f, 0.74f), new Vector2(0.94f, 0.80f), 20, FontStyles.Normal, ref created);

            var desc = EnsureText(detailsPanel.transform, "DetailDescription", new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.72f), 18, FontStyles.Normal, ref created);
            desc.textWrappingMode = TextWrappingModes.Normal;
            desc.alignment = TextAlignmentOptions.TopLeft;
        }

        private static TextMeshProUGUI EnsureText(Transform parent, string name, Vector2 min, Vector2 max, int size, FontStyles style, ref int created)
        {
            var existing = FindDeepChild(parent, name);
            if (existing == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(parent, false);
                var rt = go.GetComponent<RectTransform>();
                SetAnchors(rt, min, max);
                SetOffsets(rt, 0, 0, 0, 0);
                var tmp = go.GetComponent<TextMeshProUGUI>();
                tmp.text = string.Empty;
                tmp.fontSize = size;
                tmp.fontStyle = style;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Left;
                created++;
                return tmp;
            }

            return existing.GetComponent<TextMeshProUGUI>();
        }

        private static TextMeshProUGUI CreateDetailsText(Transform parent, string name, Vector2 min, Vector2 max, int size, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            SetAnchors(rt, min, max);
            SetOffsets(rt, 0, 0, 0, 0);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = string.Empty;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;
            return tmp;
        }

        private static GameObject BuildRowTemplate(Transform contentParent)
        {
            var row = new GameObject("RowTemplate", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Abyss.Inventory.PlayerInventoryRowUI));
            row.transform.SetParent(contentParent, false);

            var rt = row.GetComponent<RectTransform>();
            ConfigureRowTemplateRect(rt);

            var layout = row.GetComponent<LayoutElement>();
            layout.minHeight = 56f;
            layout.preferredHeight = 56f;
            layout.flexibleHeight = 0f;

            var bg = row.GetComponent<Image>();
            bg.color = new Color(0.10f, 0.10f, 0.10f, 0.85f);

            // Rarity strip
            var strip = new GameObject("RarityStrip", typeof(RectTransform), typeof(Image));
            strip.transform.SetParent(row.transform, false);
            var stripRt = strip.GetComponent<RectTransform>();
            SetAnchors(stripRt, new Vector2(0f, 0f), new Vector2(0f, 1f));
            stripRt.sizeDelta = new Vector2(6, 0);
            stripRt.anchoredPosition = new Vector2(3, 0);
            strip.GetComponent<Image>().color = Color.white;

            // Icon
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(row.transform, false);
            var iconRt = icon.GetComponent<RectTransform>();
            SetAnchors(iconRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            iconRt.sizeDelta = new Vector2(40, 40);
            iconRt.anchoredPosition = new Vector2(32, 0);
            var iconImg = icon.GetComponent<Image>();
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            icon.SetActive(false);

            // Name
            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(row.transform, false);
            var nameRt = nameGo.GetComponent<RectTransform>();
            SetAnchors(nameRt, new Vector2(0.10f, 0f), new Vector2(0.78f, 1f));
            SetOffsets(nameRt, 0, 0, 0, 0);
            var nameTmp = nameGo.GetComponent<TextMeshProUGUI>();
            nameTmp.text = "Item";
            nameTmp.fontSize = 22;
            nameTmp.color = Color.white;
            nameTmp.alignment = TextAlignmentOptions.Left;

            // Count
            var countGo = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
            countGo.transform.SetParent(row.transform, false);
            var countRt = countGo.GetComponent<RectTransform>();
            SetAnchors(countRt, new Vector2(0.78f, 0f), new Vector2(0.97f, 1f));
            SetOffsets(countRt, 0, 0, 0, 0);
            var countTmp = countGo.GetComponent<TextMeshProUGUI>();
            countTmp.text = "x1";
            countTmp.fontSize = 22;
            countTmp.color = Color.white;
            countTmp.alignment = TextAlignmentOptions.Right;

            // Wire row UI
            var rowUi = row.GetComponent<Abyss.Inventory.PlayerInventoryRowUI>();
            var so = new SerializedObject(rowUi);
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("iconImage").objectReferenceValue = iconImg;
            so.FindProperty("rarityStrip").objectReferenceValue = strip.GetComponent<Image>();
            so.FindProperty("nameText").objectReferenceValue = nameTmp;
            so.FindProperty("countText").objectReferenceValue = countTmp;
            so.FindProperty("button").objectReferenceValue = row.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();

            return row;
        }

        private static void ConfigureRowTemplateRect(RectTransform rt)
        {
            if (rt == null) return;
            // Stretch horizontally for VerticalLayoutGroup.
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 56f);
        }

        private static void EnsureListContentLayoutComponents(RectTransform content, ref int created)
        {
            if (content == null) return;

            var vlg = content.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
            {
                vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
                created++;
            }

            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 6f;
            vlg.padding = vlg.padding ?? new RectOffset(8, 8, 8, 8);
            vlg.childAlignment = TextAnchor.UpperLeft;

            var fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                created++;
            }
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        private static void UpgradeScrollHierarchy(GameObject panel, ScrollRect scrollRect, ref int created)
        {
            if (panel == null || scrollRect == null) return;

            var scrollGo = scrollRect.gameObject;
            var svRt = scrollGo.GetComponent<RectTransform>();
            ConfigureSplitRegionRect(svRt, new Vector2(0.04f, 0.06f), new Vector2(0.60f, 0.84f));

            // Remove layout components that can collapse size.
            RemoveIfPresent<ContentSizeFitter>(scrollGo);
            RemoveIfPresent<LayoutElement>(scrollGo);
            RemoveIfPresent<HorizontalLayoutGroup>(scrollGo);
            RemoveIfPresent<VerticalLayoutGroup>(scrollGo);

            // Ensure viewport exists and stretches.
            GameObject viewportGo = FindDeepChild(scrollGo, "Viewport");
            if (viewportGo == null)
            {
                viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
                viewportGo.transform.SetParent(scrollGo.transform, false);
                created++;
            }

            var viewportRt = viewportGo.GetComponent<RectTransform>();
            StretchFullScreen(viewportRt);
            var vpImg = viewportGo.GetComponent<Image>();
            if (vpImg == null) vpImg = viewportGo.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0);
            var mask = viewportGo.GetComponent<Mask>();
            if (mask == null) mask = viewportGo.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            // Ensure content is child of viewport.
            var contentGo = FindDeepChild(scrollGo, "Content");
            if (contentGo == null)
            {
                contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                contentGo.transform.SetParent(viewportGo.transform, false);
                created++;
            }
            if (contentGo != null && contentGo.transform.parent != viewportGo.transform)
                contentGo.transform.SetParent(viewportGo.transform, false);

            scrollRect.viewport = viewportRt;
            var contentRt = contentGo != null ? contentGo.GetComponent<RectTransform>() : null;
            if (contentRt != null)
            {
                ConfigureContentRect(contentRt);
                int localCreated = 0;
                EnsureListContentLayoutComponents(contentRt, ref localCreated);
                if (localCreated > 0) created += localCreated;

                scrollRect.content = contentRt;
            }

            // Ensure scroll directions are correct.
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
        }

        private static void ConfigureSplitRegionRect(RectTransform rt, Vector2 min, Vector2 max)
        {
            if (rt == null) return;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            SetOffsets(rt, 0, 0, 0, 0);
        }

        private static void ConfigureContentRect(RectTransform contentRt)
        {
            if (contentRt == null) return;

            // Stretch to fill the viewport; vertical position is driven by layout.
            contentRt.anchorMin = new Vector2(0f, 0f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;
        }

        private static void RemoveIfPresent<T>(GameObject go) where T : Component
        {
            if (go == null) return;
            var c = go.GetComponent<T>();
            if (c == null) return;
            UnityEngine.Object.DestroyImmediate(c);
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
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.black;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            tmp.extraPadding = true;
        }

        private static GameObject FindDeepChild(GameObject root, string name)
        {
            if (root == null) return null;
            return FindDeepChild(root.transform, name)?.gameObject;
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeepChild(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        private static void DestroySceneObjectsByName(string name)
        {
            var go = GameObject.Find(name);
            if (go == null) return;
            Undo.DestroyObjectImmediate(go);
        }

        private static void StretchFullScreen(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            if (rt == null) return;
            rt.anchorMin = min;
            rt.anchorMax = max;
        }

        private static void SetOffsets(RectTransform rt, float left, float right, float top, float bottom)
        {
            if (rt == null) return;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }
    }
}
