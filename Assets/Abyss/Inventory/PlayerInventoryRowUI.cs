using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Abyss.Items;

using AbyssItemRarity = Abyss.Items.ItemRarity;

namespace Abyss.Inventory
{
    public sealed class PlayerInventoryRowUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Fill (Raycast Target)")]
        [SerializeField] private Image background;

        [Header("Borders (Legacy / Disabled)")]
        [SerializeField] private Image hoverBorderImage;
        [SerializeField] private Image selectedBorderImage;

        [Header("Borders (4-Line, Grid Mode)")]
        [SerializeField] private RectTransform borderRoot;
        [SerializeField] private Image borderTop;
        [SerializeField] private Image borderBottom;
        [SerializeField] private Image borderLeft;
        [SerializeField] private Image borderRight;

        [Header("Optional Visuals")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image rarityStrip;

        [Header("Text")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text countText;

        [SerializeField] private Button button;

        private Action _onClick;
        private bool _colorsInitialized;
        private Color _listBaseColor;
        private Color _listHoverColor;

        private bool _isHovered;
        private bool _isSelected;
        private bool _isGridMode;

        private bool _hasItem;
        private int _boundCount;

        // [INV] Debug context (set by PlayerInventoryUI during RefreshList)
        private int _debugSlotIndex = -1;
        private bool _debugIsEmpty;

        private ItemTooltipTrigger _tooltipTrigger;

        public int SlotIndex { get; private set; } = -1;
        public bool IsEmpty => !_hasItem;

        public void SetSlotIndex(int index)
        {
            SlotIndex = index;
        }

        private Color _baseNameColor;

        // Grid visuals (explicit OSRS-style rules)
        private static readonly Color GridEmptyFill = new(0.18f, 0.18f, 0.18f, 1f);
        private static readonly Color GridOccupiedFill = new(0.24f, 0.24f, 0.24f, 1f);
        // User requirement: Selected > Hover > Normal with explicit alphas.
        private static readonly Color GridBorderNormalColor = new(1f, 1f, 1f, 0.18f);
        private static readonly Color GridHoverBorderColor = new(1f, 1f, 1f, 0.45f);
        private static readonly Color GridSelectedBorderColor = new(1f, 1f, 1f, 0.90f);
        private const float GridSelectedFillBrighten = 0.06f;

        // User requirement: consistent borders across all resolutions.
        // Force thickness to 2px (avoid 1px subpixel disappearance).
        private const float BorderThicknessNormal = 2f;
        private const float BorderThicknessSelected = 2f;

        private bool _gridBordersInitialized;

        private static Sprite s_WhiteSprite;

        // ItemDefinition lookup cache (covers cases where PlayerInventoryUI provides only a string id/name).
        // We keep this local to the row so icon binding can't silently fail.
        private static Dictionary<string, ItemDefinition> s_ItemDefByKey;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly HashSet<string> s_WarnedMissingIconImageByItemId = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> s_WarnedMissingIconByItemId = new(StringComparer.OrdinalIgnoreCase);

        private static void WarnOnce(HashSet<string> cache, string key, string message, UnityEngine.Object context)
        {
            try
            {
                if (cache == null) return;
                key ??= "(null)";
                if (cache.Contains(key)) return;
                cache.Add(key);
                Debug.LogWarning(message, context);
            }
            catch { }
        }
#endif

        private static ItemDefinition ResolveItemDefinitionFallback(string keyA, string keyB)
        {
            try
            {
                if (s_ItemDefByKey == null)
                {
                    s_ItemDefByKey = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);

                    var defs = Resources.FindObjectsOfTypeAll<ItemDefinition>();
                    if (defs != null)
                    {
                        foreach (var def in defs)
                        {
                            if (def == null) continue;

                            string id = null;
                            try { id = def.itemId; } catch { }

                            string displayName = null;
                            try { displayName = def.displayName; } catch { }

                            TryAddDefKey(id, def);
                            TryAddDefKey(displayName, def);
                            TryAddDefKey(def.name, def);
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(keyA) && s_ItemDefByKey.TryGetValue(keyA, out var a) && a != null)
                    return a;

                if (!string.IsNullOrWhiteSpace(keyB) && s_ItemDefByKey.TryGetValue(keyB, out var b) && b != null)
                    return b;
            }
            catch { }

            return null;
        }

        private static void TryAddDefKey(string key, ItemDefinition def)
        {
            if (string.IsNullOrWhiteSpace(key) || def == null)
                return;

            try
            {
                // Prefer a definition that has an icon.
                if (s_ItemDefByKey.TryGetValue(key, out var existing) && existing != null)
                {
                    bool existingHasIcon = false;
                    bool defHasIcon = false;
                    try { existingHasIcon = existing.icon != null; } catch { }
                    try { defHasIcon = def.icon != null; } catch { }

                    if (!existingHasIcon && defHasIcon)
                        s_ItemDefByKey[key] = def;

                    return;
                }

                s_ItemDefByKey[key] = def;
            }
            catch { }
        }

        private static Sprite GetOrCreateWhiteSprite()
        {
            if (s_WhiteSprite != null)
                return s_WhiteSprite;

            // Guaranteed runtime sprite (avoids built-in UI resources that may not exist).
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false, linear: false)
            {
                name = "RuntimeWhiteSpriteTex",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            s_WhiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            s_WhiteSprite.name = "RuntimeWhiteSprite";
            s_WhiteSprite.hideFlags = HideFlags.HideAndDontSave;
            return s_WhiteSprite;
        }

        public void SetDebugContext(int slotIndex, bool isEmpty)
        {
            _debugSlotIndex = slotIndex;
            _debugIsEmpty = isEmpty;
        }

        private void Awake()
        {
            EnsureDefaultColors();
            if (_isGridMode)
                EnsureGridBorderLines();
            RenderState();

            EnsureTooltipTrigger();

            if (nameText != null)
                _baseNameColor = nameText.color;
        }

        private void OnEnable()
        {
            // Snap to integer pixel grid (both immediately and after layout has positioned us).
            // GridLayoutGroup positions are applied after instantiation, so do both.
            try
            {
                var rt = transform as RectTransform;
                if (rt != null)
                {
                    SnapToPixelGrid(rt);
                    StartCoroutine(SnapAfterLayout(rt));
                }
            }
            catch { }
        }

        private IEnumerator SnapAfterLayout(RectTransform rt)
        {
            yield return null;

            try
            {
                if (rt != null)
                    SnapToPixelGrid(rt);

                if (_isGridMode)
                {
                    EnsureGridBorderLines();
                    RenderState();
                }
            }
            catch { }
        }

        private static void SnapToPixelGrid(RectTransform rt)
        {
            if (rt == null) return;
            var p = rt.anchoredPosition;
            p.x = Mathf.Round(p.x);
            p.y = Mathf.Round(p.y);
            rt.anchoredPosition = p;
        }

        public void Bind(ItemDefinition def, string fallbackItemId, int count, Action onClick)
        {
            ResolveBackgroundImage();
            EnsureDefaultColors();

            if (_isGridMode)
                EnsureGridElements();

            string display = def != null
                ? (string.IsNullOrWhiteSpace(def.displayName) ? ResolveFallbackName(def, fallbackItemId) : def.displayName)
                : (string.IsNullOrWhiteSpace(fallbackItemId) ? "(Unknown)" : fallbackItemId);

            // Rolled loot items: show base item display name if available.
            if (def == null && !string.IsNullOrWhiteSpace(fallbackItemId))
            {
                try
                {
                    var reg = Abyssbound.Loot.LootRegistryRuntime.GetOrCreate();
                    if (reg != null && reg.TryResolveDisplay(fallbackItemId, out var lootName, out var lootIcon))
                    {
                        if (!string.IsNullOrWhiteSpace(lootName))
                            display = lootName;
                    }
                }
                catch { }
            }

            // Grid mode requirement: no item name text inside the cell.
            if (!_isGridMode)
            {
                if (nameText != null) nameText.text = display;
            }
            else
            {
                if (nameText != null) nameText.text = string.Empty;
            }

            int safeCount = Mathf.Max(0, count);
            _boundCount = safeCount;
            _hasItem = def != null || !string.IsNullOrWhiteSpace(fallbackItemId);

            if (countText != null)
            {
                // UI requirement: show stack count only if > 1.
                countText.text = safeCount > 1 ? $"x{safeCount}" : string.Empty;
            }

            // Resolve ItemDefinition even if caller only supplies a string key (some inventories use display name keys).
            var resolvedDef = def != null ? def : ResolveItemDefinitionFallback(fallbackItemId, display);

            // Tooltip binding (hover): uses the resolved definition where possible.
            try
            {
                EnsureTooltipTrigger();
                if (_tooltipTrigger != null)
                    _tooltipTrigger.BindInventoryItem(resolvedDef, fallbackItemId, safeCount);
            }
            catch { }

            Sprite icon = null;
            AbyssItemRarity rarity = AbyssItemRarity.Common;
            try
            {
                if (resolvedDef != null)
                {
                    icon = resolvedDef.icon;
                    rarity = ItemRarityVisuals.Normalize(resolvedDef.rarity);
                }
            }
            catch { }

            if (icon == null && resolvedDef == null && !string.IsNullOrWhiteSpace(fallbackItemId))
            {
                try
                {
                    var reg = Abyssbound.Loot.LootRegistryRuntime.GetOrCreate();
                    if (reg != null && reg.TryResolveDisplay(fallbackItemId, out _, out var lootIcon) && lootIcon != null)
                        icon = lootIcon;
                }
                catch { }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_hasItem)
            {
                string id = null;
                try { id = resolvedDef != null && !string.IsNullOrWhiteSpace(resolvedDef.itemId) ? resolvedDef.itemId : fallbackItemId; } catch { id = fallbackItemId; }
                if (string.IsNullOrWhiteSpace(id)) id = display;

                if (iconImage == null)
                    WarnOnce(s_WarnedMissingIconImageByItemId, id, $"[INV][ICON] IconImage NULL for {id} row={gameObject.name}", this);

                if (icon == null)
                    WarnOnce(s_WarnedMissingIconByItemId, id, $"[INV][ICON] icon NULL for {id} row={gameObject.name}", this);
            }
#endif

            ApplyVisuals(icon, rarity);

            _onClick = onClick;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => _onClick?.Invoke());
            }

            _isHovered = false;
            RenderState();
        }

        public void BindEmpty()
        {
            BindEmpty(null);
        }

        public void BindEmpty(Action onClick)
        {
            ResolveBackgroundImage();
            EnsureDefaultColors();

            if (_isGridMode)
                EnsureGridElements();

            if (nameText != null) nameText.text = string.Empty;
            if (countText != null) countText.text = string.Empty;

            _boundCount = 0;
            _hasItem = false;

            // Clear tooltip binding for empty slots.
            try
            {
                EnsureTooltipTrigger();
                if (_tooltipTrigger != null)
                    _tooltipTrigger.BindInventoryItem(null, null, 0);
            }
            catch { }

            _isHovered = false;
            _isSelected = false;

            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
                if (iconImage.gameObject.activeSelf)
                    iconImage.gameObject.SetActive(false);
            }

            if (rarityStrip != null)
            {
                rarityStrip.enabled = false;
                var rc = rarityStrip.color;
                rarityStrip.color = new Color(rc.r, rc.g, rc.b, 0f);
            }

            _onClick = onClick;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                if (_onClick != null)
                    button.onClick.AddListener(() => _onClick?.Invoke());

                // In grid mode we want empty slots hoverable + clickable.
                button.interactable = _isGridMode;
            }

            RenderState();
        }

        private void EnsureTooltipTrigger()
        {
            if (_tooltipTrigger != null)
                return;

            try
            {
                _tooltipTrigger = GetComponent<ItemTooltipTrigger>();
                if (_tooltipTrigger == null)
                    _tooltipTrigger = gameObject.AddComponent<ItemTooltipTrigger>();
            }
            catch
            {
                _tooltipTrigger = null;
            }
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            RenderState();
        }

        public void SetHovered(bool hovered)
        {
            _isHovered = hovered;
            RenderState();
        }

        public void SetGridMode(bool enabled)
        {
            _isGridMode = enabled;

            if (_isGridMode)
            {
                ResolveBackgroundImage();
                EnsureGridElements();
                EnsureGridBorderLines();
                DisableLegacyBorders();
            }

            // In grid mode we prefer icon + count (name shown in details panel).
            if (nameText != null)
                nameText.gameObject.SetActive(!enabled);

            // Also hard-hide any other TMP labels in the slot (prevents cramped names if a template has extras).
            if (enabled)
            {
                try
                {
                    var tmps = GetComponentsInChildren<TMP_Text>(true);
                    if (tmps != null)
                    {
                        for (int i = 0; i < tmps.Length; i++)
                        {
                            var t = tmps[i];
                            if (t == null) continue;
                            if (t == countText) continue;
                            t.gameObject.SetActive(false);
                        }
                    }
                }
                catch { }
            }

            // Rarity strip is now driven by binding (enabled for items, disabled for empty).

            if (enabled)
            {
                try
                {
                    if (iconImage != null)
                    {
                        var rt = iconImage.rectTransform;
                        // ~70% size with padding so it doesn't touch borders.
                        rt.anchorMin = new Vector2(0.15f, 0.15f);
                        rt.anchorMax = new Vector2(0.85f, 0.85f);
                        rt.pivot = new Vector2(0.5f, 0.5f);
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                        iconImage.preserveAspect = true;
                        iconImage.raycastTarget = false;
                    }

                    if (countText != null)
                    {
                        var rt = countText.rectTransform;
                        rt.anchorMin = new Vector2(1f, 0f);
                        rt.anchorMax = new Vector2(1f, 0f);
                        rt.pivot = new Vector2(1f, 0f);
                        rt.anchoredPosition = new Vector2(-4f, 4f);

                        if (countText.fontSize > 16f)
                            countText.fontSize = 16f;

                        countText.textWrappingMode = TextWrappingModes.NoWrap;
                        countText.alignment = TextAlignmentOptions.BottomRight;
                        countText.raycastTarget = false;

                        var shadow = countText.GetComponent<Shadow>();
                        if (shadow == null) shadow = countText.gameObject.AddComponent<Shadow>();
                        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
                        shadow.effectDistance = new Vector2(1f, -1f);
                        shadow.useGraphicAlpha = true;
                    }
                }
                catch { }
            }
            else
            {
                if (button != null)
                    button.interactable = true;

                if (nameText != null)
                    nameText.gameObject.SetActive(true);
            }

            RenderState();
        }

        private void EnsureGridElements()
        {
            // Ensure we have an icon + count text for grid slots.
            // Keep this lightweight: only searches/creates when references are missing.

            try
            {
                if (iconImage == null)
                {
                    var t = transform.Find("Icon");
                    if (t != null) iconImage = t.GetComponent<Image>();
                }
            }
            catch { }

            try
            {
                if (countText == null)
                {
                    var t = transform.Find("Count");
                    if (t != null) countText = t.GetComponent<TMP_Text>();
                }
            }
            catch { }

            // If the template doesn't have these (older scenes), create them.
            if (iconImage == null)
            {
                try
                {
                    var go = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.layer = gameObject.layer;
                    go.transform.SetParent(transform, false);
                    iconImage = go.GetComponent<Image>();
                    iconImage.preserveAspect = true;
                }
                catch { }
            }

            if (countText == null)
            {
                try
                {
                    var go = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
                    go.layer = gameObject.layer;
                    go.transform.SetParent(transform, false);
                    var tmp = go.GetComponent<TextMeshProUGUI>();
                    tmp.text = string.Empty;
                    tmp.fontSize = 16f;
                    tmp.alignment = TextAlignmentOptions.BottomRight;
                    tmp.raycastTarget = false;
                    tmp.textWrappingMode = TextWrappingModes.NoWrap;
                    countText = tmp;
                }
                catch { }
            }

            try { if (iconImage != null) iconImage.raycastTarget = false; } catch { }
            try { if (countText != null) countText.raycastTarget = false; } catch { }
        }

        private void ApplyVisuals(Sprite icon, AbyssItemRarity rarity)
        {
            if (iconImage != null)
            {
                // User requirement: never use the runtime white sprite (borders) as an icon placeholder.
                // If icon is null, disable the icon image so we don't show gray squares.
                bool hasIcon = icon != null;

                iconImage.sprite = icon;
                iconImage.enabled = hasIcon;
                if (iconImage.gameObject.activeSelf != hasIcon)
                    iconImage.gameObject.SetActive(hasIcon);

                if (hasIcon)
                {
                    try
                    {
                        var iconCol = iconImage.color;
                        iconImage.color = new Color(iconCol.r, iconCol.g, iconCol.b, 1f);
                    }
                    catch { }
                    try
                    {
                        iconImage.type = Image.Type.Simple;
                        iconImage.preserveAspect = true;
                        iconImage.raycastTarget = false;
                    }
                    catch { }

                    try { iconImage.SetAllDirty(); } catch { }
                }
            }

            rarity = ItemRarityVisuals.Normalize(rarity);
            var rarityColor = InventoryRarityColors.GetColor(rarity);

            if (rarityStrip != null)
            {
                rarityStrip.enabled = _hasItem;
                rarityStrip.color = _hasItem
                    ? rarityColor
                    : new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0f);
                if (!rarityStrip.gameObject.activeSelf)
                    rarityStrip.gameObject.SetActive(true);
            }
            else if (nameText != null)
            {
                if (_baseNameColor.a <= 0f)
                    _baseNameColor = nameText.color;

                nameText.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, _baseNameColor.a);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            RenderState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            RenderState();
        }

        private void EnsureDefaultColors()
        {
            if (_colorsInitialized)
                return;

            var baseC = background != null ? background.color : default;
            if (baseC.a <= 0f)
                baseC = new Color(0.10f, 0.10f, 0.10f, 0.85f);

            _listBaseColor = baseC;
            _listHoverColor = AddRgb(baseC, 0.10f);

            _colorsInitialized = true;
        }

        private Image ResolveBackgroundImage()
        {
            if (background != null)
                return background;

            // Prefer a dedicated inner fill image.
            try
            {
                var t = transform.Find("InnerBackground");
                if (t != null)
                {
                    var img = t.GetComponent<Image>();
                    if (img != null)
                    {
                        background = img;
                        return background;
                    }
                }
            }
            catch { }

            background = GetComponent<Image>();
            if (background != null)
                return background;

            // Otherwise, find the first suitable Image in children.
            try
            {
                var images = GetComponentsInChildren<Image>(true);
                if (images != null)
                {
                    for (int i = 0; i < images.Length; i++)
                    {
                        var img = images[i];
                        if (img == null) continue;
                        if (img == iconImage) continue;
                        if (img == rarityStrip) continue;
                        if (img == hoverBorderImage) continue;
                        if (img == selectedBorderImage) continue;
                        if (string.Equals(img.name, "HoverBorderImage", StringComparison.OrdinalIgnoreCase)) continue;
                        if (string.Equals(img.name, "SelectedBorderImage", StringComparison.OrdinalIgnoreCase)) continue;
                        if (string.Equals(img.name, "InnerBackground", StringComparison.OrdinalIgnoreCase)) continue;

                        background = img;
                        return background;
                    }
                }
            }
            catch { }

            return null;
        }

        private Image EnsureChildImage(string name)
        {
            return EnsureChildImage(transform, name);
        }

        private Image EnsureChildImage(Transform parent, string name)
        {
            try
            {
                var t = parent != null ? parent.Find(name) : null;
                if (t != null)
                {
                    var existing = t.GetComponent<Image>();
                    if (existing != null)
                        return existing;
                }
            }
            catch { }

            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gameObject.layer;
            go.transform.SetParent(parent != null ? parent : transform, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return go.GetComponent<Image>();
        }

        private RectTransform EnsureBorderRoot()
        {
            if (borderRoot != null)
                return borderRoot;

            try
            {
                var t = transform.Find("BorderRoot");
                if (t != null)
                {
                    borderRoot = t as RectTransform;
                    if (borderRoot == null) borderRoot = t.GetComponent<RectTransform>();
                    return borderRoot;
                }
            }
            catch { }

            var go = new GameObject("BorderRoot", typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            borderRoot = go.GetComponent<RectTransform>();
            borderRoot.anchorMin = Vector2.zero;
            borderRoot.anchorMax = Vector2.one;
            borderRoot.pivot = new Vector2(0.5f, 0.5f);
            borderRoot.offsetMin = Vector2.zero;
            borderRoot.offsetMax = Vector2.zero;
            return borderRoot;
        }

        private void EnsureGridInnerBackground()
        {
            // Avoid offsetting the slot root RectTransform (GridLayoutGroup owns it).
            // Force every slot to use the SAME fill hierarchy: an InnerBackground child inset by 2px.
            // This prevents mixed hierarchies (root Image vs child Image) from causing border inconsistencies.

            var inner = EnsureChildImage("InnerBackground");
            inner.raycastTarget = true;
            inner.type = Image.Type.Simple;

            // If we already had a background reference that isn't InnerBackground, migrate its visuals.
            try
            {
                if (background != null && background != inner)
                {
                    inner.sprite = background.sprite;
                    inner.material = background.material;
                    inner.color = background.color;
                }
                else
                {
                    var rootImg = GetComponent<Image>();
                    if (rootImg != null && rootImg != inner)
                    {
                        inner.sprite = rootImg.sprite;
                        inner.material = rootImg.material;
                        inner.color = rootImg.color;
                    }
                }
            }
            catch { }

            // Hide any root Image so it cannot cover the border lines.
            try
            {
                var rootImg = GetComponent<Image>();
                if (rootImg != null)
                {
                    rootImg.color = new Color(1f, 1f, 1f, 0f);
                    rootImg.raycastTarget = false;
                }
            }
            catch { }

            try
            {
                var rt = inner.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(2f, 2f);
                rt.offsetMax = new Vector2(-2f, -2f);
            }
            catch { }

            // Keep fill underneath everything else.
            try { inner.transform.SetAsFirstSibling(); } catch { }

            background = inner;
        }

        private Image FindOrCreateUniqueBorderLine(string name, RectTransform desiredParent, ref Image cache)
        {
            if (cache != null)
            {
                try
                {
                    if (desiredParent != null && cache.transform.parent != desiredParent)
                        cache.transform.SetParent(desiredParent, false);
                    cache.gameObject.SetActive(true);
                }
                catch { }
                return cache;
            }

            Image found = null;
            try
            {
                var images = GetComponentsInChildren<Image>(true);
                if (images != null)
                {
                    for (int i = 0; i < images.Length; i++)
                    {
                        var img = images[i];
                        if (img == null) continue;
                        if (!string.Equals(img.name, name, StringComparison.Ordinal))
                            continue;

                        if (found == null)
                        {
                            found = img;
                        }
                        else
                        {
                            // Disable duplicates to ensure every slot uses ONE consistent border set.
                            try { img.gameObject.SetActive(false); } catch { }
                        }
                    }
                }
            }
            catch { }

            if (found == null)
            {
                found = EnsureChildImage(desiredParent != null ? desiredParent.transform : transform, name);
            }

            cache = found;
            try
            {
                if (desiredParent != null && cache.transform.parent != desiredParent)
                    cache.transform.SetParent(desiredParent, false);
                cache.gameObject.SetActive(true);
            }
            catch { }

            return cache;
        }


        private void DisableLegacyBorders()
        {
            // Disable any previously attempted border approaches to prevent conflicts.
            try
            {
                if (hoverBorderImage != null) hoverBorderImage.enabled = false;
                if (selectedBorderImage != null) selectedBorderImage.enabled = false;

                var legacyHover = transform.Find("HoverBorderImage");
                if (legacyHover != null)
                {
                    var img = legacyHover.GetComponent<Image>();
                    if (img != null) img.enabled = false;
                }

                var legacySelected = transform.Find("SelectedBorderImage");
                if (legacySelected != null)
                {
                    var img = legacySelected.GetComponent<Image>();
                    if (img != null) img.enabled = false;
                }

                var oldHover = transform.Find("HoverBorder");
                if (oldHover != null) oldHover.gameObject.SetActive(false);

                var oldSelected = transform.Find("SelectedBorder");
                if (oldSelected != null) oldSelected.gameObject.SetActive(false);

                // Hard-disable by name as well (prefab leftovers / older runtime systems).
                var legacyNames = new[] { "HoverBorderImage", "SelectedBorderImage", "HoverBorder", "SelectedBorder" };
                for (int i = 0; i < legacyNames.Length; i++)
                {
                    var t = transform.Find(legacyNames[i]);
                    if (t != null) t.gameObject.SetActive(false);
                }
            }
            catch { }

            // Ensure any Outline on the root/background is off.
            try
            {
                var o1 = GetComponent<Outline>();
                if (o1 != null) o1.enabled = false;
            }
            catch { }

            try
            {
                if (background != null)
                {
                    var o2 = background.GetComponent<Outline>();
                    if (o2 != null) o2.enabled = false;
                }
            }
            catch { }
        }

        private void EnsureGridBorderLines()
        {
            if (!_isGridMode)
                return;

            ResolveBackgroundImage();
            EnsureGridInnerBackground();

            if (!_gridBordersInitialized)
            {
                // Make sure no legacy systems remain active.
                DisableLegacyBorders();
                _gridBordersInitialized = true;
            }

            var br = EnsureBorderRoot();
            try { if (br != null) br.transform.SetAsLastSibling(); } catch { }

            // IMPORTANT: reuse ANY existing border lines (even if they were created under a different parent)
            // and disable duplicates. This guarantees a single border construction codepath for every slot.
            borderTop = FindOrCreateUniqueBorderLine("BorderTop", br, ref borderTop);
            borderBottom = FindOrCreateUniqueBorderLine("BorderBottom", br, ref borderBottom);
            borderLeft = FindOrCreateUniqueBorderLine("BorderLeft", br, ref borderLeft);
            borderRight = FindOrCreateUniqueBorderLine("BorderRight", br, ref borderRight);

            SetupLine(borderTop);
            SetupLine(borderBottom);
            SetupLine(borderLeft);
            SetupLine(borderRight);

            // Ensure line geometry (anchors/pivots) is correct.
            try
            {
                ConfigureTop(borderTop.rectTransform, BorderThicknessNormal);
                ConfigureBottom(borderBottom.rectTransform, BorderThicknessNormal);
                ConfigureLeft(borderLeft.rectTransform, BorderThicknessNormal);
                ConfigureRight(borderRight.rectTransform, BorderThicknessNormal);
            }
            catch { }

            // Draw borders above everything but never block clicks.
            try
            {
                if (borderTop != null) borderTop.transform.SetAsLastSibling();
                if (borderBottom != null) borderBottom.transform.SetAsLastSibling();
                if (borderLeft != null) borderLeft.transform.SetAsLastSibling();
                if (borderRight != null) borderRight.transform.SetAsLastSibling();
            }
            catch { }
        }

        private static void SetupLine(Image img)
        {
            if (img == null)
                return;

            img.enabled = true;
            img.raycastTarget = false;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;

            // Ensure a valid source sprite so the Image actually draws.
            if (img.sprite == null)
                img.sprite = GetOrCreateWhiteSprite();
        }

        private static void ConfigureTop(RectTransform rt, float thickness)
        {
            if (rt == null) return;
            thickness = Mathf.Max(1f, Mathf.Round(thickness));
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = new Vector2(0f, -thickness);
            rt.offsetMax = new Vector2(0f, 0f);
        }

        private static void ConfigureBottom(RectTransform rt, float thickness)
        {
            if (rt == null) return;
            thickness = Mathf.Max(1f, Mathf.Round(thickness));
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, thickness);
        }

        private static void ConfigureLeft(RectTransform rt, float thickness)
        {
            if (rt == null) return;
            thickness = Mathf.Max(1f, Mathf.Round(thickness));
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(thickness, 0f);
        }

        private static void ConfigureRight(RectTransform rt, float thickness)
        {
            if (rt == null) return;
            thickness = Mathf.Max(1f, Mathf.Round(thickness));
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = new Vector2(-thickness, 0f);
            rt.offsetMax = new Vector2(0f, 0f);
        }

        private void RenderState()
        {
            ResolveBackgroundImage();
            if (background == null)
                return;

            EnsureDefaultColors();

            bool isEmpty = !_hasItem;
            bool isHovered = _isHovered;
            bool isSelected = _isSelected;

            // Count visibility.
            if (countText != null)
            {
                if (isEmpty)
                {
                    if (!string.IsNullOrEmpty(countText.text))
                        countText.text = string.Empty;

                    if (_isGridMode)
                    {
                        try { if (countText.gameObject.activeSelf) countText.gameObject.SetActive(false); } catch { }
                    }
                }
                else
                {
                    if (_isGridMode)
                    {
                        if (_boundCount > 1)
                        {
                            countText.text = $"x{Mathf.Max(0, _boundCount)}";
                            try { if (!countText.gameObject.activeSelf) countText.gameObject.SetActive(true); } catch { }
                        }
                        else
                        {
                            countText.text = string.Empty;
                            try { if (countText.gameObject.activeSelf) countText.gameObject.SetActive(false); } catch { }
                        }
                    }
                    else
                    {
                        // UI requirement: show stack count only if > 1.
                        countText.text = _boundCount > 1 ? $"x{Mathf.Max(0, _boundCount)}" : string.Empty;
                    }
                }
            }

            // Icon visibility.
            if (_isGridMode && iconImage != null && isEmpty)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
                if (iconImage.gameObject.activeSelf)
                    iconImage.gameObject.SetActive(false);
            }

            // Ensure raycast target graphic.
            try
            {
                if (button == null)
                    button = GetComponent<Button>();

                if (_isGridMode)
                {
                    EnsureGridBorderLines();
                    DisableLegacyBorders();

                    // Force borders to top every frame.
                    try { if (borderRoot != null) borderRoot.transform.SetAsLastSibling(); } catch { }

                    background.raycastTarget = true;
                    if (button != null) button.targetGraphic = background;
                }
                else
                {
                    if (background != null)
                        background.raycastTarget = true;
                    if (button != null && button.targetGraphic == null)
                        button.targetGraphic = background;

                    // Hide grid borders in list mode.
                    if (borderTop != null) borderTop.enabled = false;
                    if (borderBottom != null) borderBottom.enabled = false;
                    if (borderLeft != null) borderLeft.enabled = false;
                    if (borderRight != null) borderRight.enabled = false;
                }
            }
            catch { }

            if (!_isGridMode)
            {
                background.color = isHovered ? _listHoverColor : _listBaseColor;
                return;
            }

            // Grid fill.
            var baseFill = isEmpty ? GridEmptyFill : GridOccupiedFill;
            background.color = isSelected ? AddRgb(baseFill, GridSelectedFillBrighten) : baseFill;

            // Borders: Selected > Hover > Normal. Geometry stays constant.
            try
            {
                float thickness = BorderThicknessNormal;

                if (borderTop != null)
                {
                    borderTop.enabled = true;
                    ConfigureTop(borderTop.rectTransform, thickness);
                }
                if (borderBottom != null)
                {
                    borderBottom.enabled = true;
                    ConfigureBottom(borderBottom.rectTransform, thickness);
                }
                if (borderLeft != null)
                {
                    borderLeft.enabled = true;
                    ConfigureLeft(borderLeft.rectTransform, thickness);
                }
                if (borderRight != null)
                {
                    borderRight.enabled = true;
                    ConfigureRight(borderRight.rectTransform, thickness);
                }

                Color borderColor = GridBorderNormalColor;
                if (isSelected) borderColor = GridSelectedBorderColor;
                else if (isHovered) borderColor = GridHoverBorderColor;

                if (borderTop != null) borderTop.color = borderColor;
                if (borderBottom != null) borderBottom.color = borderColor;
                if (borderLeft != null) borderLeft.color = borderColor;
                if (borderRight != null) borderRight.color = borderColor;
            }
            catch { }
        }

        private static Color AddRgb(Color c, float amount)
        {
            return new Color(
                Mathf.Clamp01(c.r + amount),
                Mathf.Clamp01(c.g + amount),
                Mathf.Clamp01(c.b + amount),
                c.a);
        }

        private static string ResolveFallbackName(ItemDefinition def, string fallbackItemId)
        {
            if (def == null) return string.IsNullOrWhiteSpace(fallbackItemId) ? "(Unknown)" : fallbackItemId;
            if (!string.IsNullOrWhiteSpace(def.itemId)) return def.itemId;
            if (!string.IsNullOrWhiteSpace(def.name)) return def.name;
            return string.IsNullOrWhiteSpace(fallbackItemId) ? "(Unknown)" : fallbackItemId;
        }

        public Button Button => button;
        public bool CanShowIcon => iconImage != null;
        public bool IsGridMode => _isGridMode;
    }
}
