using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Abyss.Items;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

using AbyssItemRarity = Abyss.Items.ItemRarity;

namespace Abyss.Inventory
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class PlayerInventoryUI : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject root;
        [SerializeField] private Button closeButton;

        [Header("Top")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text goldText;

        [Header("List")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private PlayerInventoryRowUI rowTemplate;

        [Header("Details")]
        [SerializeField] private PlayerInventoryDetailsUI detailsUI;

        private Game.Input.PlayerInputAuthority _inputAuthority;
        private PlayerInventory _inventory;
        private Abyss.Shop.PlayerGoldWallet _wallet;

        private string _inventorySource;
        private int _lastInventoryInstanceId;
        private bool _loggedInventoryForThisOpen;
        private bool _loggedScrollWiringForThisOpen;

        private readonly List<GameObject> _spawnedRows = new();
        private Dictionary<string, ItemDefinition> _itemDefById;

        private string _selectedItemId;
        private ItemDefinition _selectedDef;
        private int _selectedCount;

        private bool _isOpen;

        private void Awake()
        {
#if UNITY_2022_2_OR_NEWER
            _inputAuthority = FindFirstObjectByType<Game.Input.PlayerInputAuthority>();
#else
            _inputAuthority = FindObjectOfType<Game.Input.PlayerInputAuthority>();
#endif

            if (root != null)
                root.SetActive(false);

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }

            if (titleText != null && string.IsNullOrWhiteSpace(titleText.text))
                titleText.text = "Inventory";

            _wallet = Abyss.Shop.PlayerGoldWallet.Instance;

            // Inventory is resolved on-demand (Open/Refresh) to avoid binding to the wrong instance
            // in scenes where more than one PlayerInventory exists.
            _inventory = null;

            // Keep details safe.
            detailsUI?.Clear();
        }

        private void Update()
        {
            if (!WasTogglePressed())
                return;

            // Avoid fighting with merchant UI.
            if (Abyss.Shop.MerchantShopUI.IsOpen)
                return;

            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (root == null)
                return;

            if (_isOpen)
                return;

            _isOpen = true;
            root.SetActive(true);

            _loggedInventoryForThisOpen = false;
            _loggedScrollWiringForThisOpen = false;

            EnsureCanvasVisibility();
            EnsureScrollRectWiring();

            _wallet = Abyss.Shop.PlayerGoldWallet.Instance;
            if (_wallet != null)
            {
                _wallet.GoldChanged -= OnGoldChanged;
                _wallet.GoldChanged += OnGoldChanged;
            }

            EnsureInventory();
            if (_inventory != null)
            {
                _inventory.Changed -= OnInventoryChanged;
                _inventory.Changed += OnInventoryChanged;
            }

            try { _inputAuthority?.SetUiInputLocked(true); } catch { }

            RefreshAll();

            try
            {
                if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
                if (contentRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
            }
            catch { }
        }

        public void Close()
        {
            if (!_isOpen)
                return;

            _isOpen = false;

            if (_wallet != null)
                _wallet.GoldChanged -= OnGoldChanged;

            if (_inventory != null)
                _inventory.Changed -= OnInventoryChanged;

            try { _inputAuthority?.SetUiInputLocked(false); } catch { }

            if (root != null)
                root.SetActive(false);
        }

        public void RefreshAll()
        {
            RefreshGold();
            RefreshList();
            RefreshDetails();
        }

        private void OnGoldChanged(int newGold)
        {
            RefreshGold();
        }

        private void OnInventoryChanged()
        {
            if (!_isOpen)
                return;

            // If the canonical inventory instance changes (e.g., player respawn), re-resolve.
            EnsureInventory();

            RefreshList();
            RefreshDetails();
        }

        private void EnsureCanvasVisibility()
        {
            try
            {
                var cgRoot = root != null ? root.GetComponent<CanvasGroup>() : null;
                if (cgRoot != null && cgRoot.alpha <= 0.01f)
                    cgRoot.alpha = 1f;

                var cgContent = contentRoot != null ? contentRoot.GetComponent<CanvasGroup>() : null;
                if (cgContent != null && cgContent.alpha <= 0.01f)
                    cgContent.alpha = 1f;
            }
            catch { }
        }

        private void EnsureListContentLayout()
        {
            if (contentRoot == null) return;

            var vlg = contentRoot.GetComponent<VerticalLayoutGroup>();
            if (vlg == null) vlg = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 6f;
            vlg.padding ??= new RectOffset(8, 8, 8, 8);
            vlg.childAlignment = TextAnchor.UpperLeft;

            var fitter = contentRoot.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void EnsureInventory()
        {
            var resolved = ResolveInventory(out var source);
            if (resolved == null)
                return;

            if (!ReferenceEquals(_inventory, resolved))
            {
                if (_inventory != null)
                    _inventory.Changed -= OnInventoryChanged;

                _inventory = resolved;
                _inventorySource = source;

                if (_isOpen)
                {
                    _inventory.Changed -= OnInventoryChanged;
                    _inventory.Changed += OnInventoryChanged;
                }
            }
        }

        private PlayerInventory ResolveInventory(out string source)
        {
            source = null;

            // 1) Prefer inventory attached to the player (same root as PlayerInputAuthority).
            try
            {
                if (_inputAuthority != null)
                {
                    var inv = _inputAuthority.GetComponentInParent<PlayerInventory>();
                    if (inv != null)
                    {
                        source = "PlayerInputAuthority.GetComponentInParent";
                        return inv;
                    }

                    inv = _inputAuthority.GetComponentInChildren<PlayerInventory>();
                    if (inv != null)
                    {
                        source = "PlayerInputAuthority.GetComponentInChildren";
                        return inv;
                    }
                }
            }
            catch { }

            // 2) If multiple inventories exist, choose the one that actually has items.
            try
            {
                PlayerInventory[] all;
#if UNITY_2022_2_OR_NEWER
                all = FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None);
#else
                all = FindObjectsOfType<PlayerInventory>();
#endif
                if (all != null && all.Length > 0)
                {
                    PlayerInventory best = null;
                    int bestTotalItems = -1;
                    int bestStacks = -1;

                    foreach (var inv in all)
                    {
                        if (inv == null || !inv.isActiveAndEnabled)
                            continue;

                        int stacks = 0;
                        int totalItems = 0;
                        try
                        {
                            var snap = inv.GetAllItemsSnapshot();
                            if (snap != null)
                            {
                                stacks = snap.Count;
                                foreach (var kv in snap)
                                    totalItems += Mathf.Max(0, kv.Value);
                            }
                        }
                        catch { }

                        if (totalItems > bestTotalItems || (totalItems == bestTotalItems && stacks > bestStacks))
                        {
                            best = inv;
                            bestTotalItems = totalItems;
                            bestStacks = stacks;
                        }
                    }

                    if (best != null)
                    {
                        source = all.Length == 1 ? "FindObjects(single)" : "FindObjects(best-by-items)";
                        return best;
                    }
                }
            }
            catch { }

            // 3) Last-resort fallback (mirrors older patterns).
            try
            {
#if UNITY_2022_2_OR_NEWER
                var inv = FindFirstObjectByType<PlayerInventory>();
#else
                var inv = FindObjectOfType<PlayerInventory>();
#endif
                if (inv != null)
                {
                    source = "FindFirstObjectByType";
                    return inv;
                }
            }
            catch { }

            return null;
        }

        private void RefreshGold()
        {
            if (goldText == null)
                return;

            _wallet = Abyss.Shop.PlayerGoldWallet.Instance;
            int g = _wallet != null ? _wallet.Gold : 0;
            goldText.text = $"Gold: {g}";
        }

        private void RefreshList()
        {
            if (contentRoot == null || rowTemplate == null)
                return;

            EnsureScrollRectWiring();

            EnsureInventory();
            if (_inventory == null)
            {
                ClearRows();
                return;
            }

            _itemDefById ??= BuildItemDefinitionIndex();

            EnsureListContentLayout();

            // Runtime safety: ensure Content is anchored to fill the ScrollRect viewport.
            try
            {
                contentRoot.anchorMin = new Vector2(0f, 0f);
                contentRoot.anchorMax = new Vector2(1f, 1f);
                contentRoot.pivot = new Vector2(0.5f, 1f);
                contentRoot.anchoredPosition = Vector2.zero;
                contentRoot.offsetMin = Vector2.zero;
                contentRoot.offsetMax = Vector2.zero;

                if (scrollRect != null && scrollRect.viewport != null)
                {
                    scrollRect.viewport.offsetMin = Vector2.zero;
                    scrollRect.viewport.offsetMax = Vector2.zero;
                }
            }
            catch { }

            // Keep the template under contentRoot and disabled.
            if (rowTemplate.transform != null && rowTemplate.transform.parent != contentRoot)
                rowTemplate.transform.SetParent(contentRoot, false);
            if (rowTemplate.gameObject.activeSelf)
                rowTemplate.gameObject.SetActive(false);

            ClearRows();

            var snap = _inventory.GetAllItemsSnapshot();
            if (snap == null)
                return;

            // One-time diagnostics on open/refresh (no per-frame spam).
            if (_isOpen)
            {
                try
                {
                    int stacks = snap.Count;
                    int total = 0;
                    foreach (var kv in snap)
                        total += Mathf.Max(0, kv.Value);

                    int instanceId = 0;
                    try { instanceId = _inventory != null ? _inventory.GetInstanceID() : 0; } catch { }

                    if (!_loggedInventoryForThisOpen || instanceId != _lastInventoryInstanceId)
                    {
                        _loggedInventoryForThisOpen = true;
                        _lastInventoryInstanceId = instanceId;
                        Debug.Log($"[PlayerInventoryUI] Inventory source={_inventorySource ?? "(unknown)"} instanceId={instanceId} hash={(_inventory != null ? _inventory.GetHashCode() : 0)} stacks={stacks} totalItems={total}", this);
                    }
                }
                catch { }
            }

            // Deterministic iteration to avoid flicker.
            var keys = new List<string>(snap.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);

            PlayerInventoryRowUI first = null;
            string firstId = null;
            int firstCount = 0;
            int renderedStacks = 0;
            bool loggedFirstRowRectThisRefresh = false;

            foreach (var itemId in keys)
            {
                if (string.IsNullOrWhiteSpace(itemId))
                    continue;

                int count = snap.TryGetValue(itemId, out var c) ? c : 0;
                if (count <= 0)
                    continue;

                var def = ResolveItemDefinition(itemId);

                var go = Instantiate(rowTemplate.gameObject, contentRoot, false);
                go.name = $"Row_{itemId}";
                go.SetActive(true);

                // Runtime safety: prevent zero-height rows if layout metadata is missing.
                var le = go.GetComponent<LayoutElement>();
                if (le == null) le = go.AddComponent<LayoutElement>();
                if (le.preferredHeight <= 0.01f) le.preferredHeight = 56f;
                if (le.minHeight <= 0.01f) le.minHeight = 56f;
                le.flexibleHeight = 0f;

                var rt = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    // Runtime enforcement: ensure row is full-width under Content.
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
                    rt.sizeDelta = new Vector2(0f, rt.sizeDelta.y);

                    rt.localScale = Vector3.one;
                    rt.localRotation = Quaternion.identity;
                    rt.anchoredPosition3D = new Vector3(rt.anchoredPosition.x, rt.anchoredPosition.y, 0f);

                    // Some layout setups can still leave sizeDelta.y at 0 even with LayoutElement.
                    // Nudge it to the preferred height to avoid edge-case collapse.
                    var sd = rt.sizeDelta;
                    if (sd.y <= 0.01f)
                        rt.sizeDelta = new Vector2(sd.x, le.preferredHeight);

                    if (!loggedFirstRowRectThisRefresh)
                    {
                        loggedFirstRowRectThisRefresh = true;
                        Debug.Log(
                            $"[PlayerInventoryUI] FirstRow RectTransform anchors=({rt.anchorMin}->{rt.anchorMax}) pos={rt.anchoredPosition} sizeDelta={rt.sizeDelta} pivot={rt.pivot}",
                            this);
                    }
                }

                var row = go.GetComponent<PlayerInventoryRowUI>();
                if (row != null)
                {
                    string capturedId = itemId;
                    int capturedCount = count;
                    row.Bind(def, capturedId, capturedCount, () => Select(capturedId, capturedCount));

                    if (first == null)
                    {
                        first = row;
                        firstId = capturedId;
                        firstCount = capturedCount;
                    }

                    renderedStacks++;
                }
            }

            // Keep selection valid.
            if (!string.IsNullOrWhiteSpace(_selectedItemId))
            {
                int owned = _inventory.Count(_selectedItemId);
                if (owned <= 0)
                {
                    _selectedItemId = null;
                    _selectedDef = null;
                    _selectedCount = 0;
                }
                else
                {
                    _selectedCount = owned;
                    _selectedDef = ResolveItemDefinition(_selectedItemId);
                }
            }

            // Auto-select first if none.
            if (string.IsNullOrWhiteSpace(_selectedItemId) && firstId != null)
            {
                Select(firstId, firstCount);
                first?.Button?.Select();
            }

            // Layout refresh so rows appear immediately (no polling).
            if (_isOpen)
            {
                try
                {
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
                }
                catch { }

                Debug.Log($"[PlayerInventoryUI] Rendered {renderedStacks} rows under {contentRoot.name}", this);
            }
        }

        private void EnsureScrollRectWiring()
        {
            if (scrollRect == null || contentRoot == null)
                return;

            try
            {
                if (scrollRect.content != contentRoot)
                    scrollRect.content = contentRoot;

                // Prefer explicit viewport, but try to recover if missing.
                if (scrollRect.viewport == null)
                {
                    RectTransform candidate = null;
                    try
                    {
                        var p = contentRoot.parent as RectTransform;
                        if (p != null && (p.name == "Viewport" || p.GetComponent<Mask>() != null))
                            candidate = p;
                    }
                    catch { }

                    if (candidate == null)
                    {
                        try
                        {
                            var t = scrollRect.transform.Find("Viewport") as RectTransform;
                            if (t != null) candidate = t;
                        }
                        catch { }
                    }

                    if (candidate != null)
                        scrollRect.viewport = candidate;
                }

                // Ensure viewport has a mask (otherwise list can appear "missing" depending on hierarchy).
                if (scrollRect.viewport != null)
                {
                    var vpGo = scrollRect.viewport.gameObject;
                    if (vpGo.GetComponent<Image>() == null)
                    {
                        var img = vpGo.AddComponent<Image>();
                        img.color = new Color(0, 0, 0, 0);
                    }
                    if (vpGo.GetComponent<Mask>() == null)
                    {
                        var mask = vpGo.AddComponent<Mask>();
                        mask.showMaskGraphic = false;
                    }

                    // Ensure stretched viewport has zero offsets.
                    try
                    {
                        scrollRect.viewport.offsetMin = Vector2.zero;
                        scrollRect.viewport.offsetMax = Vector2.zero;
                    }
                    catch { }
                }

                scrollRect.horizontal = false;
                scrollRect.vertical = true;
            }
            catch { }

            if (_isOpen && !_loggedScrollWiringForThisOpen)
            {
                _loggedScrollWiringForThisOpen = true;
                try
                {
                    var vp = scrollRect.viewport;
                    var vpName = vp != null ? vp.name : "(null)";
                    Debug.Log(
                        $"[PlayerInventoryUI] ScrollRect wiring viewport={vpName} content={(scrollRect.content != null ? scrollRect.content.name : "(null)")} scrollRect={(scrollRect != null ? scrollRect.name : "(null)")}",
                        this);

                    if (vp != null)
                    {
                        Debug.Log(
                            $"[PlayerInventoryUI] Viewport Rect anchors=({vp.anchorMin}->{vp.anchorMax}) sizeDelta={vp.sizeDelta} offsetMin={vp.offsetMin} offsetMax={vp.offsetMax} pivot={vp.pivot}",
                            this);
                    }

                    if (contentRoot != null)
                    {
                        Debug.Log(
                            $"[PlayerInventoryUI] Content Rect anchors=({contentRoot.anchorMin}->{contentRoot.anchorMax}) pos={contentRoot.anchoredPosition} sizeDelta={contentRoot.sizeDelta} offsetMin={contentRoot.offsetMin} offsetMax={contentRoot.offsetMax} pivot={contentRoot.pivot}",
                            this);
                    }
                }
                catch { }
            }
        }

        private void RefreshDetails()
        {
            if (detailsUI == null)
                return;

            if (string.IsNullOrWhiteSpace(_selectedItemId))
            {
                detailsUI.Clear();
                return;
            }

            EnsureInventory();
            int count = _inventory != null ? _inventory.Count(_selectedItemId) : _selectedCount;
            var def = ResolveItemDefinition(_selectedItemId);

            detailsUI.Set(def, _selectedItemId, count);
        }

        private void Select(string itemId, int count)
        {
            _selectedItemId = itemId;
            _selectedCount = Mathf.Max(0, count);
            _selectedDef = ResolveItemDefinition(itemId);

            RefreshDetails();
        }

        private void ClearRows()
        {
            if (contentRoot == null)
                return;

            var templateTf = rowTemplate != null ? rowTemplate.transform : null;

            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                var child = contentRoot.GetChild(i);
                if (child == null) continue;
                if (templateTf != null && child == templateTf) continue;
                Destroy(child.gameObject);
            }

            _spawnedRows.Clear();
        }

        private Dictionary<string, ItemDefinition> BuildItemDefinitionIndex()
        {
            var map = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // Mirror the merchant UI approach: gather from all merchants in the scene.
#if UNITY_2022_2_OR_NEWER
                var shops = FindObjectsByType<Abyss.Shop.MerchantShop>(FindObjectsSortMode.None);
#else
                var shops = FindObjectsOfType<Abyss.Shop.MerchantShop>();
#endif
                if (shops != null)
                {
                    foreach (var s in shops)
                    {
                        if (s == null || s.shopInventory == null || s.shopInventory.entries == null) continue;
                        foreach (var e in s.shopInventory.entries)
                        {
                            if (e == null || e.item == null) continue;
                            var def = e.item;
                            var id = ResolveItemId(def);
                            if (!string.IsNullOrWhiteSpace(id) && !map.ContainsKey(id))
                                map[id] = def;
                        }
                    }
                }

                // Also include any loaded ItemDefinition assets.
                var loaded = Resources.FindObjectsOfTypeAll<ItemDefinition>();
                if (loaded != null)
                {
                    foreach (var def in loaded)
                    {
                        if (def == null) continue;
                        var id = ResolveItemId(def);
                        if (!string.IsNullOrWhiteSpace(id) && !map.ContainsKey(id))
                            map[id] = def;
                    }
                }
            }
            catch { }

            return map;
        }

        private ItemDefinition ResolveItemDefinition(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            if (_itemDefById == null)
                _itemDefById = BuildItemDefinitionIndex();

            if (_itemDefById != null && _itemDefById.TryGetValue(itemId, out var def) && def != null)
                return def;

            return null;
        }

        private static string ResolveItemId(ItemDefinition def)
        {
            if (def == null) return null;
            string itemId = string.IsNullOrWhiteSpace(def.itemId) ? def.displayName : def.itemId;
            if (string.IsNullOrWhiteSpace(itemId)) itemId = def.name;
            return itemId;
        }

        private static bool WasTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.I);
#endif
        }

        public bool IsOpen => _isOpen;
    }
}
