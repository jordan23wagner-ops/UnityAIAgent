using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Abyss.Equipment.EditorTools
{
    public static class BuildPlayerEquipmentUIEditor
    {
        private const string SilhouetteIconFolder = "Assets/Abyss/Equipment/Icons/";

        [MenuItem("Tools/Build Player Equipment UI (Editor)")]
        public static void Build()
        {
            DestroySceneObjectsByName("PlayerEquipmentUICanvas");
            DestroySceneObjectsByName("PlayerEquipmentUIRoot");
            DestroySceneObjectsByName("PlayerEquipmentUI");

            var canvasGO = new GameObject("PlayerEquipmentUICanvas", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create PlayerEquipmentUICanvas");

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 101;
            canvasGO.AddComponent<GraphicRaycaster>();

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            StretchFullScreen(canvasGO.GetComponent<RectTransform>());

            var root = new GameObject("PlayerEquipmentUIRoot", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Create PlayerEquipmentUIRoot");
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
            // Taller panel so vertical grid has more real pixels (prevents lower slots touching).
            panelRt.sizeDelta = new Vector2(700, 760);
            // Warmer/darker backdrop like the reference.
            panel.GetComponent<Image>().color = new Color(0.14f, 0.11f, 0.07f, 0.96f);
            {
                var outline = panel.AddComponent<Outline>();
                outline.effectColor = new Color(0.05f, 0.04f, 0.02f, 1f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            // UI controller host
            var uiGO = new GameObject("PlayerEquipmentUI", typeof(RectTransform), typeof(Abyss.Equipment.PlayerEquipmentUI));
            uiGO.transform.SetParent(canvasGO.transform, false);
            StretchFullScreen(uiGO.GetComponent<RectTransform>());

            // Header title
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(panel.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            SetAnchors(titleRt, new Vector2(0.04f, 0.90f), new Vector2(0.70f, 0.98f));
            SetOffsets(titleRt, 0, 0, 0, 0);
            var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "Equipment";
            titleTmp.fontSize = 36;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = Color.white;
            titleTmp.alignment = TextAlignmentOptions.Left;

            // Close button
            var closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(panel.transform, false);
            var closeRt = closeGo.GetComponent<RectTransform>();
            SetAnchors(closeRt, new Vector2(0.86f, 0.92f), new Vector2(0.98f, 0.98f));
            SetOffsets(closeRt, 0, 0, 0, 0);
            closeGo.GetComponent<Image>().color = Color.white;
            var closeBtn = closeGo.GetComponent<Button>();
            EnsureButtonLabel(closeGo, "X", 28);

            // Frame background ("box")
            var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(panel.transform, false);
            var frameRt = frame.GetComponent<RectTransform>();
            SetAnchors(frameRt, new Vector2(0.04f, 0.06f), new Vector2(0.98f, 0.88f));
            SetOffsets(frameRt, 0, 0, 0, 0);
            frame.GetComponent<Image>().color = new Color(0.22f, 0.18f, 0.11f, 0.96f);
            {
                var outline = frame.AddComponent<Outline>();
                outline.effectColor = new Color(0.07f, 0.05f, 0.03f, 1f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            // Central silhouette placeholder
            var silhouette = new GameObject("Silhouette", typeof(RectTransform), typeof(Image));
            silhouette.transform.SetParent(frame.transform, false);
            var silRt = silhouette.GetComponent<RectTransform>();
            SetAnchors(silRt, new Vector2(0.33f, 0.16f), new Vector2(0.67f, 0.84f));
            SetOffsets(silRt, 0, 0, 0, 0);
            var silImg = silhouette.GetComponent<Image>();
            silImg.color = new Color(1f, 1f, 1f, 0.06f);

            // Slot boxes (uniform spacing grid)
            // Slightly smaller + more vertical spacing so center column doesn't touch.
            const float slotSize = 68f;
            const float xLeft = 0.34f;
            const float xCenter = 0.50f;
            const float xRight = 0.66f;
            // Right-side jewelry column (separate cluster).
            const float xJewelry = 0.84f;

            // Evenly spaced center column like the reference.
            // With amulet moved to the jewelry column, keep the top row one grid-step above chest.
            const float yHelm = 0.70f;
            const float yChest = 0.56f;
            const float yBelt = 0.42f;
            const float yLegs = 0.28f;

            // Jewelry column vertical positions (separate from main gear)
            const float yJewelryAmulet = 0.78f;
            const float yJewelryRing1 = 0.60f;
            const float yJewelryRing2 = 0.42f;
            const float yJewelryArtifact = 0.24f;

            var slotHelm = BuildSlotBox(frame.transform, "Slot_Helm", new Vector2(xCenter, yHelm), slotSize);
            var slotCape = BuildSlotBox(frame.transform, "Slot_Cape", new Vector2(xLeft, yHelm), slotSize);
            var slotAmmo = BuildSlotBox(frame.transform, "Slot_Ammo", new Vector2(xRight, yHelm), slotSize);

            // Jewelry cluster (separate from the rest of the gear)
            var slotAmulet = BuildSlotBox(frame.transform, "Slot_Amulet", new Vector2(xJewelry, yJewelryAmulet), slotSize);
            var slotRing1 = BuildSlotBox(frame.transform, "Slot_Ring1", new Vector2(xJewelry, yJewelryRing1), slotSize);
            var slotRing2 = BuildSlotBox(frame.transform, "Slot_Ring2", new Vector2(xJewelry, yJewelryRing2), slotSize);
            var slotArtifact = BuildSlotBox(frame.transform, "Slot_Artifact", new Vector2(xJewelry, yJewelryArtifact), slotSize);

            // OSRS-style: weapon on the left, shield/offhand on the right.
            var slotRightHand = BuildSlotBox(frame.transform, "Slot_RightHand", new Vector2(xLeft, yChest), slotSize);
            var slotChest = BuildSlotBox(frame.transform, "Slot_Chest", new Vector2(xCenter, yChest), slotSize);
            var slotLeftHand = BuildSlotBox(frame.transform, "Slot_LeftHand", new Vector2(xRight, yChest), slotSize);

            // Per request: Gloves below Left, next to Belt.
            var slotGloves = BuildSlotBox(frame.transform, "Slot_Gloves", new Vector2(xLeft, yBelt), slotSize);
            var slotBelt = BuildSlotBox(frame.transform, "Slot_Belt", new Vector2(xCenter, yBelt), slotSize);
            var slotLegs = BuildSlotBox(frame.transform, "Slot_Legs", new Vector2(xCenter, yLegs), slotSize);

            // Connector lines (simple orthogonal connectors like the reference)
            var connectors = new GameObject("Connectors", typeof(RectTransform));
            connectors.transform.SetParent(frame.transform, false);
            var conRt = connectors.GetComponent<RectTransform>();
            StretchFullScreen(conRt);

            const float t = 5f;
            // Inset lines so they only occupy the gaps between boxes.
            const float inset = 0.058f;

            // Top row
            BuildHLineInset(connectors.transform, "Line_Cape_Helm", xLeft, xCenter, yHelm, t, inset);
            BuildHLineInset(connectors.transform, "Line_Helm_Ammo", xCenter, xRight, yHelm, t, inset);

            // Vertical spine (inset so it doesn't run through boxes)
            BuildVLineInset(connectors.transform, "Line_Helm_Chest", xCenter, yChest, yHelm, t, inset);
            BuildVLineInset(connectors.transform, "Line_Chest_Belt", xCenter, yBelt, yChest, t, inset);
            BuildVLineInset(connectors.transform, "Line_Belt_Legs", xCenter, yLegs, yBelt, t, inset);

            // Arms
            BuildHLineInset(connectors.transform, "Line_Left_Chest", xLeft, xCenter, yChest, t, inset);
            BuildHLineInset(connectors.transform, "Line_Chest_Right", xCenter, xRight, yChest, t, inset);

            // Left column down to gloves and across to belt
            BuildVLineInset(connectors.transform, "Line_Left_Gloves", xLeft, yBelt, yChest, t, inset);
            BuildHLineInset(connectors.transform, "Line_Gloves_Belt", xLeft, xCenter, yBelt, t, inset);

            // Jewelry connectors: ONLY vertical between these 4 items.
            BuildVLineInset(connectors.transform, "Line_Jewelry_Amulet_Ring1", xJewelry, yJewelryRing1, yJewelryAmulet, t, inset);
            BuildVLineInset(connectors.transform, "Line_Jewelry_Ring1_Ring2", xJewelry, yJewelryRing2, yJewelryRing1, t, inset);
            BuildVLineInset(connectors.transform, "Line_Jewelry_Ring2_Artifact", xJewelry, yJewelryArtifact, yJewelryRing2, t, inset);

            // Wire references
            var ui = uiGO.GetComponent<Abyss.Equipment.PlayerEquipmentUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("root").objectReferenceValue = root;
            so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            so.FindProperty("titleText").objectReferenceValue = titleTmp;

            so.FindProperty("paperDollSilhouette").objectReferenceValue = silImg;

            var slotsProp = so.FindProperty("slots");
            slotsProp.arraySize = 0;

            AddSlotWidget(slotsProp, 0, Abyss.Items.EquipmentSlot.Helm, slotHelm.button, slotHelm.icon, slotHelm.label);
            AddSlotWidget(slotsProp, 1, Abyss.Items.EquipmentSlot.Cape, slotCape.button, slotCape.icon, slotCape.label);
            AddSlotWidget(slotsProp, 2, Abyss.Items.EquipmentSlot.Amulet, slotAmulet.button, slotAmulet.icon, slotAmulet.label);
            AddSlotWidget(slotsProp, 3, Abyss.Items.EquipmentSlot.LeftHand, slotLeftHand.button, slotLeftHand.icon, slotLeftHand.label);
            AddSlotWidget(slotsProp, 4, Abyss.Items.EquipmentSlot.Chest, slotChest.button, slotChest.icon, slotChest.label);
            AddSlotWidget(slotsProp, 5, Abyss.Items.EquipmentSlot.RightHand, slotRightHand.button, slotRightHand.icon, slotRightHand.label);
            AddSlotWidget(slotsProp, 6, Abyss.Items.EquipmentSlot.Gloves, slotGloves.button, slotGloves.icon, slotGloves.label);
            AddSlotWidget(slotsProp, 7, Abyss.Items.EquipmentSlot.Belt, slotBelt.button, slotBelt.icon, slotBelt.label);
            AddSlotWidget(slotsProp, 8, Abyss.Items.EquipmentSlot.Legs, slotLegs.button, slotLegs.icon, slotLegs.label);
            AddSlotWidget(slotsProp, 9, Abyss.Items.EquipmentSlot.Ammo, slotAmmo.button, slotAmmo.icon, slotAmmo.label);
            AddSlotWidget(slotsProp, 10, Abyss.Items.EquipmentSlot.Ring1, slotRing1.button, slotRing1.icon, slotRing1.label);
            AddSlotWidget(slotsProp, 11, Abyss.Items.EquipmentSlot.Ring2, slotRing2.button, slotRing2.icon, slotRing2.label);
            AddSlotWidget(slotsProp, 12, Abyss.Items.EquipmentSlot.Artifact, slotArtifact.button, slotArtifact.icon, slotArtifact.label);

            so.ApplyModifiedProperties();

            // Default inactive
            root.SetActive(false);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[BuildPlayerEquipmentUIEditor] Built Player Equipment UI. connectorInset={inset:0.000} slotSize={slotSize:0.#}");
        }

        private struct BuiltSlot
        {
            public Button button;
            public Image icon;
            public TextMeshProUGUI label;
        }

        private static BuiltSlot BuildSlotBox(Transform parent, string name, Vector2 anchor01, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor01;
            rt.anchorMax = anchor01;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(size, size);

            var bg = go.GetComponent<Image>();
            // Stone-ish slot tiles.
            bg.color = new Color(0.34f, 0.32f, 0.29f, 1f);

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.10f, 0.08f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);

            var btn = go.GetComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = bg.color;
            cb.highlightedColor = new Color(0.40f, 0.38f, 0.34f, 1f);
            cb.pressedColor = new Color(0.28f, 0.26f, 0.23f, 1f);
            cb.selectedColor = cb.highlightedColor;
            cb.disabledColor = new Color(0.28f, 0.26f, 0.23f, 0.75f);
            cb.colorMultiplier = 1f;
            btn.colors = cb;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.12f, 0.12f);
            iconRt.anchorMax = new Vector2(0.88f, 0.88f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var icon = iconGo.GetComponent<Image>();
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.enabled = false;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            // Bottom caption area (keeps icon readable).
            labelRt.anchorMin = new Vector2(0.06f, 0.02f);
            labelRt.anchorMax = new Vector2(0.94f, 0.32f);
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = name;
            label.fontSize = 14;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 1f, 1f, 0.70f);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.gameObject.SetActive(true);

            return new BuiltSlot
            {
                button = go.GetComponent<Button>(),
                icon = icon,
                label = label,
            };
        }

        private static void AddSlotWidget(SerializedProperty arrayProp, int index, Abyss.Items.EquipmentSlot slot, Button button, Image icon, TextMeshProUGUI label)
        {
            arrayProp.arraySize = index + 1;
            var el = arrayProp.GetArrayElementAtIndex(index);
            // IMPORTANT: use intValue (underlying enum), not enumValueIndex (name index).
            el.FindPropertyRelative("slot").intValue = (int)slot;
            el.FindPropertyRelative("button").objectReferenceValue = button;
            el.FindPropertyRelative("iconImage").objectReferenceValue = icon;
            el.FindPropertyRelative("labelText").objectReferenceValue = label;
            el.FindPropertyRelative("emptyIcon").objectReferenceValue = GetEmptySilhouetteForSlot(slot);
        }

        private static Sprite GetEmptySilhouetteForSlot(Abyss.Items.EquipmentSlot slot)
        {
            string iconName = slot switch
            {
                Abyss.Items.EquipmentSlot.RightHand => "sil_sword",
                Abyss.Items.EquipmentSlot.LeftHand => "sil_shield",

                Abyss.Items.EquipmentSlot.Helm => "sil_helm",
                Abyss.Items.EquipmentSlot.Cape => "sil_cape",
                Abyss.Items.EquipmentSlot.Ammo => "sil_arrows",

                Abyss.Items.EquipmentSlot.Ring1 => "sil_ring",
                Abyss.Items.EquipmentSlot.Ring2 => "sil_ring",
                Abyss.Items.EquipmentSlot.Amulet => "sil_amulet",
                Abyss.Items.EquipmentSlot.Artifact => "sil_orb",

                Abyss.Items.EquipmentSlot.Chest => "sil_chest",
                Abyss.Items.EquipmentSlot.Belt => "sil_belt",
                Abyss.Items.EquipmentSlot.Gloves => "sil_gloves",
                Abyss.Items.EquipmentSlot.Legs => "sil_boots",

                _ => null,
            };

            if (string.IsNullOrEmpty(iconName))
                return null;

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SilhouetteIconFolder + iconName + ".png");
            if (sprite == null)
            {
                Debug.LogWarning($"[BuildPlayerEquipmentUIEditor] Missing silhouette sprite for {slot}: expected '{SilhouetteIconFolder}{iconName}.png'. Run Tools/Equipment/Generate Silhouette Icons.");
            }
            return sprite;
        }

        private static void BuildVLine(Transform parent, string name, float x01, float yMin01, float yMax01, float thickness)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(x01, Mathf.Min(yMin01, yMax01));
            rt.anchorMax = new Vector2(x01, Mathf.Max(yMin01, yMax01));
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(thickness, 0f);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.10f, 0.09f, 0.07f, 0.95f);
        }

        private static void BuildHLine(Transform parent, string name, float xMin01, float xMax01, float y01, float thickness)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(Mathf.Min(xMin01, xMax01), y01);
            rt.anchorMax = new Vector2(Mathf.Max(xMin01, xMax01), y01);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, thickness);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.10f, 0.09f, 0.07f, 0.95f);
        }

        private static void BuildVLineInset(Transform parent, string name, float x01, float yMin01, float yMax01, float thickness, float inset01)
        {
            var y0 = Mathf.Min(yMin01, yMax01) + inset01;
            var y1 = Mathf.Max(yMin01, yMax01) - inset01;
            if (y1 <= y0) return;
            BuildVLine(parent, name, x01, y0, y1, thickness);
        }

        private static void BuildHLineInset(Transform parent, string name, float xMin01, float xMax01, float y01, float thickness, float inset01)
        {
            var x0 = Mathf.Min(xMin01, xMax01) + inset01;
            var x1 = Mathf.Max(xMin01, xMax01) - inset01;
            if (x1 <= x0) return;
            BuildHLine(parent, name, x0, x1, y01, thickness);
        }

        private static void EnsureButtonLabel(GameObject btnGo, string text, int fontSize)
        {
            var existing = btnGo.transform.Find("Label");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(btnGo.transform, false);
            var rt = label.GetComponent<RectTransform>();
            StretchFullScreen(rt);

            var tmp = label.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.black;
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
