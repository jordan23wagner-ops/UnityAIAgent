using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Abyss.Items;
using Abyss.Equipment;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

using AbyssItemRarity = Abyss.Items.ItemRarity;
using AbyssItemType = Abyss.Items.ItemType;

namespace Abyss.Inventory
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class PlayerInventoryUI : MonoBehaviour
    {
        private enum InventoryTab
        {
            WeaponsGear = 0,
            Materials = 1,
            Consumables = 2,
            Skilling = 3,
        }

        private const bool INVENTORY_UI_DEBUG = false;
        private static bool InventoryUiDebugEnabled => INVENTORY_UI_DEBUG;

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

        [Header("Tabs (optional)")]
        [Tooltip("If not set, a simple tab bar will be created at runtime above the list.")]
        [SerializeField] private RectTransform tabsRoot;

        [Header("Details")]
        [SerializeField] private PlayerInventoryDetailsUI detailsUI;

        private Game.Input.PlayerInputAuthority _inputAuthority;
        private PlayerInventory _inventory;
        private Abyss.Shop.PlayerGoldWallet _wallet;
        private PlayerEquipment _equipment;

        private string _inventorySource;
        private int _lastInventoryInstanceId;
        private bool _loggedInventoryForThisOpen;
        private bool _loggedScrollWiringForThisOpen;
        private bool _loggedFirstRowVisibilityThisOpen;

        private readonly List<GameObject> _spawnedRows = new();
        private Dictionary<string, ItemDefinition> _itemDefById;

        private string _selectedItemId;
        private ItemDefinition _selectedDef;
        private int _selectedCount;

        private InventoryTab _activeTab = InventoryTab.WeaponsGear;

        private Button _tabWeapons;
        private Button _tabMaterials;
        private Button _tabConsumables;
        private Button _tabSkilling;

        private TMP_Text _tabWeaponsText;
        private TMP_Text _tabMaterialsText;
        private TMP_Text _tabConsumablesText;
        private TMP_Text _tabSkillingText;

        private Button _equipButton;
        private TMP_Text _equipButtonText;

        private bool _isOpen;

        private int _lastRefreshFrame = -1;
        private bool _refreshQueued;

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

            // Resolve inventory on-demand to avoid wrong instance bindings.
            _inventory = null;

            detailsUI?.Clear();
        }

        private void Update()
        {
            if (_refreshQueued)
            {
                _refreshQueued = false;
                RefreshAll();
            }

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
            if (root == null || _isOpen)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log($"[InventoryUI TRACE] Open frame={Time.frameCount}", this);
#endif

            _isOpen = true;
            root.SetActive(true);

            _loggedInventoryForThisOpen = false;
            _loggedScrollWiringForThisOpen = false;
            _loggedFirstRowVisibilityThisOpen = false;

            EnsureCanvasVisibility();
            EnsureScrollRectWiring();
            EnsureScrollViewLayoutHard(); // NEW: enforce known-good layout up front

            EnsureTabs();

            EnsureEquipButton();

            BringListToFront();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log($"[PlayerInventoryUI] BringListToFront parent='{scrollRect.transform.parent?.name}' siblingIndex={scrollRect.transform.GetSiblingIndex()} childCount={scrollRect.transform.parent?.childCount}", this);
#endif

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

            EnsureEquipment();

            try { _inputAuthority?.SetUiInputLocked(true); } catch { }

            _refreshQueued = true;
        }

        public void Close()
        {
            if (!_isOpen)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log($"[InventoryUI TRACE] Close frame={Time.frameCount}", this);
#endif

            _isOpen = false;

            try { _inputAuthority?.SetUiInputLocked(false); } catch { }

            if (_wallet != null)
                _wallet.GoldChanged -= OnGoldChanged;

            if (_inventory != null)
                _inventory.Changed -= OnInventoryChanged;

            detailsUI?.Clear();

            if (root != null)
                root.SetActive(false);
        }

        private void RefreshAll()
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log($"[InventoryUI TRACE] OnInventoryChanged frame={Time.frameCount}", this);
#endif
            // If inventory changes while open, refresh list/details.
            if (!_isOpen) return;
            RefreshList();
            RefreshDetails();
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log($"[InventoryUI TRACE] RefreshList ENTER frame={Time.frameCount}", this);
#endif

            // Prevent multiple rebuilds in the same frame
            if (_lastRefreshFrame == Time.frameCount)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (InventoryUiDebugEnabled) Debug.Log($"[InventoryUI TRACE] RefreshList SKIPPED (already ran this frame)", this);
#endif
                return;
            }

            _lastRefreshFrame = Time.frameCount;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log("[InventoryUI TRACE] RefreshList BEGIN frame=" + Time.frameCount, this);
#endif

            if (contentRoot == null || rowTemplate == null)
                return;

            BringListToFront();

            EnsureScrollRectWiring();
            EnsureScrollViewLayoutHard(); // NEW: enforce layout every refresh (safe, cheap)

            EnsureInventory();
            if (_inventory == null)
            {
                ClearRows();
                return;
            }

            _itemDefById ??= BuildItemDefinitionIndex();

            // Keep template under contentRoot and disabled.
            if (rowTemplate.transform != null && rowTemplate.transform.parent != contentRoot)
                rowTemplate.transform.SetParent(contentRoot, false);
            if (rowTemplate.gameObject.activeSelf)
                rowTemplate.gameObject.SetActive(false);

            ClearRows();

            var snap = _inventory.GetAllItemsSnapshot();
            if (snap == null)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log("[InventoryUI TRACE] RefreshList snapshotCount=" + snap.Count + " frame=" + Time.frameCount, this);
#endif

            // One-time diagnostics per open (no spam).
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
                        if (InventoryUiDebugEnabled) Debug.Log($"[PlayerInventoryUI] Inventory source={_inventorySource ?? "(unknown)"} instanceId={instanceId} stacks={stacks} totalItems={total}", this);
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
            int createdRowCount = 0;
            int rowIndex = 0;

            bool selectedRendered = false;

            foreach (var itemId in keys)
            {
                if (string.IsNullOrWhiteSpace(itemId))
                    continue;

                int count = snap.TryGetValue(itemId, out var c) ? c : 0;
                if (count <= 0)
                    continue;

                var def = ResolveItemDefinition(itemId);

                if (!PassesTabFilter(def, itemId))
                    continue;

                var go = Instantiate(rowTemplate.gameObject, contentRoot, false);
                createdRowCount++;
                go.name = $"Row_{itemId}";
                go.SetActive(true);

                // Ensure stable layout metadata.
                var le = go.GetComponent<LayoutElement>();
                if (le == null) le = go.AddComponent<LayoutElement>();

                float templateH = 60f;
                try
                {
                    var tmplRt = rowTemplate.GetComponent<RectTransform>();
                    if (tmplRt != null && tmplRt.rect.height > 1f) templateH = tmplRt.rect.height;
                }
                catch { }

                le.preferredHeight = templateH;
                le.minHeight = templateH;
                le.flexibleHeight = 0f;
                le.flexibleWidth = 1f;

                var rt = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);

                    // VerticalLayoutGroup controls Y positioning
                    rt.anchoredPosition = Vector2.zero;

                    // Full width, fixed height
                    rt.sizeDelta = new Vector2(0f, le.preferredHeight);

                    rt.localScale = Vector3.one;
                    rt.localRotation = Quaternion.identity;
                }

                // NEW: brute-force visibility so “clickable but invisible” can’t happen.
                ForceRowVisible(go);

                // Readability + selection/hover styling (runtime only; no prefab edits).
                ApplyRowVisualStyling(go, rowIndex, !string.IsNullOrWhiteSpace(_selectedItemId) && itemId == _selectedItemId);

                var row = go.GetComponent<PlayerInventoryRowUI>();
                if (row != null)
                {
                    string capturedId = itemId;
                    int capturedCount = count;

                    row.Bind(def, capturedId, capturedCount, () => Select(capturedId, capturedCount));

                    if (!string.IsNullOrWhiteSpace(_selectedItemId) && string.Equals(capturedId, _selectedItemId, StringComparison.OrdinalIgnoreCase))
                        selectedRendered = true;

                    if (first == null)
                    {
                        first = row;
                        firstId = capturedId;
                        firstCount = capturedCount;
                    }

                    renderedStacks++;
                }

                rowIndex++;

                _spawnedRows.Add(go);
            }

            // Keep selection valid.
            if (string.IsNullOrWhiteSpace(_selectedItemId))
            {
                if (first != null)
                    Select(firstId, firstCount);
                else
                    detailsUI?.Clear();
            }
            else
            {
                // If selected item no longer exists, fall back to first.
                int selCount = snap.TryGetValue(_selectedItemId, out var sc) ? sc : 0;
                if (selCount <= 0 && first != null)
                    Select(firstId, firstCount);
                else if (!selectedRendered)
                {
                    // Selection exists in inventory but is filtered out by current tab.
                    if (first != null)
                        Select(firstId, firstCount);
                    else
                    {
                        _selectedItemId = null;
                        _selectedDef = null;
                        _selectedCount = 0;
                        detailsUI?.Clear();
                    }
                }
            }

            // Force rebuild now that children exist.
            try
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
                if (scrollRect != null && scrollRect.viewport != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.viewport);
                Canvas.ForceUpdateCanvases();
            }
            catch { }

            BringListToFront();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                RectTransform firstRowRt = null;
                for (int i = 0; i < contentRoot.childCount; i++)
                {
                    var ch = contentRoot.GetChild(i);
                    if (ch == null) continue;
                    if (rowTemplate != null && ch == rowTemplate.transform) continue;
                    firstRowRt = ch as RectTransform;
                    if (firstRowRt != null) break;
                }

                var anchors = $"({contentRoot.anchorMin}->{contentRoot.anchorMax})";
                var pivot = contentRoot.pivot;

                string firstRowInfo = "(no rows)";
                if (firstRowRt != null)
                    firstRowInfo = $"firstRow='{firstRowRt.name}' localPos={firstRowRt.localPosition} anchoredPos={firstRowRt.anchoredPosition}";

                if (InventoryUiDebugEnabled) Debug.Log($"[InventoryUI TRACE] PostRebuild content anchors={anchors} pivot={pivot} {firstRowInfo}", this);
            }
            catch { }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                int rowsUnderContent = 0;
                for (int i = 0; i < contentRoot.childCount; i++)
                {
                    var ch = contentRoot.GetChild(i);
                    if (ch == null) continue;
                    if (rowTemplate != null && ch == rowTemplate.transform) continue;
                    rowsUnderContent++;
                }

                var vpSize = scrollRect != null && scrollRect.viewport != null ? scrollRect.viewport.rect.size : Vector2.zero;
                var cSize = contentRoot != null ? contentRoot.rect.size : Vector2.zero;

                if (InventoryUiDebugEnabled) Debug.Log($"[PlayerInventoryUI] Rendered={renderedStacks} rowsUnderContent={rowsUnderContent} viewportSize={vpSize} contentRectSize={cSize}", this);
            }
            catch { }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log("[InventoryUI TRACE] RefreshList END frame=" + Time.frameCount + " rowsCreated=" + createdRowCount, this);
#endif
        }

        private void RefreshDetails()
        {
            if (detailsUI == null)
                return;

            if (string.IsNullOrWhiteSpace(_selectedItemId))
            {
                detailsUI.Clear();
                RefreshEquipButtonState(null);
                return;
            }

            EnsureInventory();
            int count = _inventory != null ? _inventory.Count(_selectedItemId) : _selectedCount;
            var def = ResolveItemDefinition(_selectedItemId);

            detailsUI.Set(def, _selectedItemId, count);
            RefreshEquipButtonState(def);
        }

        private void Select(string itemId, int count)
        {
            _selectedItemId = itemId;
            _selectedCount = Mathf.Max(0, count);
            _selectedDef = ResolveItemDefinition(itemId);

            // Update highlight immediately without rebuilding the list.
            UpdateSelectionHighlightVisuals();

            RefreshDetails();
        }

        private void EnsureEquipment()
        {
            if (_equipment != null)
                return;

            try
            {
                _equipment = PlayerEquipmentResolver.GetOrFindOrCreate();
            }
            catch
            {
                _equipment = null;
            }
        }

        private void EnsureEquipButton()
        {
            if (_equipButton != null)
                return;

            if (detailsUI == null)
                return;

            var existing = detailsUI.transform.Find("EquipButton");
            if (existing != null)
            {
                _equipButton = existing.GetComponent<Button>();
                _equipButtonText = existing.GetComponentInChildren<TMP_Text>(true);
                if (_equipButton != null)
                {
                    _equipButton.onClick.RemoveAllListeners();
                    _equipButton.onClick.AddListener(OnEquipPressed);
                }
                return;
            }

            var go = new GameObject("EquipButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(detailsUI.transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.06f, 0.01f);
            rt.anchorMax = new Vector2(0.94f, 0.07f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = Color.white;

            _equipButton = go.GetComponent<Button>();
            _equipButton.onClick.RemoveAllListeners();
            _equipButton.onClick.AddListener(OnEquipPressed);

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(go.transform, false);
            var lrt = label.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            var tmp = label.GetComponent<TextMeshProUGUI>();
            tmp.text = "Equip";
            tmp.fontSize = 22;
            tmp.color = Color.black;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;

            _equipButtonText = tmp;
            RefreshEquipButtonState(_selectedDef);
        }

        private void RefreshEquipButtonState(ItemDefinition selectedDef)
        {
            if (_equipButton == null)
                return;

            bool canEquip = selectedDef != null && selectedDef.equipmentSlot != EquipmentSlot.None;

            _equipButton.interactable = canEquip && _inventory != null;
            if (_equipButtonText != null)
                _equipButtonText.text = canEquip ? "Equip" : "Not equippable";
        }

        private void OnEquipPressed()
        {
            EnsureInventory();
            EnsureEquipment();

            if (_inventory == null || _equipment == null)
                return;

            if (string.IsNullOrWhiteSpace(_selectedItemId))
                return;

            if (_equipment.TryEquipFromInventory(_inventory, ResolveItemDefinition, _selectedItemId, out var message))
            {
                // Inventory change event should refresh list/details.
                if (!string.IsNullOrWhiteSpace(message))
                    Debug.Log($"[Equipment] {message}");
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(message))
                    Debug.LogWarning($"[Equipment] {message}");
            }
        }

        private void ClearRows()
        {
            if (contentRoot == null)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                int diagChildCountBefore = contentRoot.childCount;
                int diagSnapCount = -1;
                try
                {
                    if (_inventory != null)
                    {
                        var s = _inventory.GetAllItemsSnapshot();
                        diagSnapCount = s != null ? s.Count : 0;
                    }
                    else
                    {
                        diagSnapCount = 0;
                    }
                }
                catch { }

                var st = new System.Diagnostics.StackTrace(true).ToString();
                if (InventoryUiDebugEnabled)
                {
                    Debug.Log(
                        "[InventoryUI TRACE] ClearRows() frame=" + Time.frameCount +
                        " contentChildCountBefore=" + diagChildCountBefore +
                        " snapshotCount=" + diagSnapCount +
                        "\n" + st,
                        this);
                }
            }
            catch { }
#endif

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

        private void EnsureInventory()
        {
            var inv = ResolveInventory(out var source);
            _inventorySource = source;

            if (_inventory == inv)
                return;

            // Swap subscription when inventory instance changes.
            if (_inventory != null)
                _inventory.Changed -= OnInventoryChanged;

            _inventory = inv;

            if (_inventory != null && _isOpen)
            {
                _inventory.Changed -= OnInventoryChanged;
                _inventory.Changed += OnInventoryChanged;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_isOpen)
            {
                try
                {
                    var id = _inventory != null ? _inventory.GetInstanceID() : 0;
                    var goName = _inventory != null ? _inventory.gameObject.name : "(null)";
                    if (InventoryUiDebugEnabled) Debug.Log($"[PlayerInventoryUI] Open resolved inventory instanceId={id} goPath='{goName}'", this);
                }
                catch { }
            }
#endif
        }

        // IMPORTANT: This resolver matches your current project approach; keep it.
        // If you later finalize a centralized resolver class, you can simplify this to call it.
        private PlayerInventory ResolveInventory(out string source)
        {
            source = null;

            // 1) Prefer player authority chain.
            try
            {
                if (_inputAuthority != null)
                {
                    var inv = _inputAuthority.GetComponentInParent<PlayerInventory>();
                    if (inv == null) inv = _inputAuthority.GetComponentInChildren<PlayerInventory>();
                    if (inv != null)
                    {
                        source = "PlayerInputAuthority(chain)";
                        return inv;
                    }
                }
            }
            catch { }

            // 2) Try best-by-items among active inventories.
            try
            {
#if UNITY_2022_2_OR_NEWER
                var all = FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None);
#else
                var all = FindObjectsOfType<PlayerInventory>();
#endif
                if (all != null && all.Length > 0)
                {
                    PlayerInventory best = null;
                    int bestTotalItems = -1;
                    int bestStacks = -1;

                    foreach (var inv in all)
                    {
                        if (inv == null || !inv.isActiveAndEnabled) continue;

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

            // 3) Last resort.
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

        private void EnsureScrollRectWiring()
        {
            if (scrollRect == null)
                return;

            // Try to infer contentRoot if missing.
            try
            {
                if (scrollRect.content == null && contentRoot != null)
                    scrollRect.content = contentRoot;

                if (scrollRect.content == null && contentRoot == null)
                    contentRoot = scrollRect.content;
            }
            catch { }

            // Infer viewport if missing.
            try
            {
                if (scrollRect.viewport == null)
                {
                    RectTransform candidate = null;

                    try
                    {
                        var p = contentRoot != null ? contentRoot.parent as RectTransform : null;
                        if (p != null && (p.name == "Viewport" || p.GetComponent<Mask>() != null || p.GetComponent<RectMask2D>() != null))
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
            }
            catch { }

            scrollRect.horizontal = false;
            scrollRect.vertical = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_isOpen && !_loggedScrollWiringForThisOpen)
            {
                _loggedScrollWiringForThisOpen = true;
                try
                {
                    var vp = scrollRect.viewport;
                    var vpName = vp != null ? vp.name : "(null)";
                    if (InventoryUiDebugEnabled) Debug.Log($"[PlayerInventoryUI] ScrollRect wiring viewport={vpName} content={(scrollRect.content != null ? scrollRect.content.name : "(null)")} scrollRect={(scrollRect != null ? scrollRect.name : "(null)")}", this);

                    if (vp != null)
                        if (InventoryUiDebugEnabled) Debug.Log($"[PlayerInventoryUI] Viewport Rect anchors=({vp.anchorMin}->{vp.anchorMax}) sizeDelta={vp.sizeDelta} offsetMin={vp.offsetMin} offsetMax={vp.offsetMax} pivot={vp.pivot}", this);

                    if (contentRoot != null)
                        if (InventoryUiDebugEnabled) Debug.Log($"[PlayerInventoryUI] Content Rect anchors=({contentRoot.anchorMin}->{contentRoot.anchorMax}) pos={contentRoot.anchoredPosition} sizeDelta={contentRoot.sizeDelta} offsetMin={contentRoot.offsetMin} offsetMax={contentRoot.offsetMax} pivot={contentRoot.pivot}", this);
                }
                catch { }
            }
#endif
        }

        private void BringListToFront()
        {
            try
            {
                if (scrollRect == null) return;
                var t = scrollRect.transform;
                // Bring the whole scroll view to the front of its parent (Unity UI draws later siblings on top)
                t.SetAsLastSibling();

                // Also bring viewport/content just in case they’re nested under a weird layout wrapper
                if (scrollRect.viewport != null) scrollRect.viewport.SetAsLastSibling();
                if (contentRoot != null) contentRoot.SetAsLastSibling();
            }
            catch { }
        }

        /// <summary>
        /// Hard-enforces a known-good ScrollView layout so rows can't exist-but-not-render.
        /// This intentionally overrides bad inspector values during dev.
        /// </summary>
        private void EnsureScrollViewLayoutHard()
        {
            if (scrollRect == null || contentRoot == null)
                return;

            // Viewport must stretch and must mask.
            if (scrollRect.viewport != null)
            {
                var vp = scrollRect.viewport;
                try
                {
                    vp.anchorMin = new Vector2(0f, 0f);
                    vp.anchorMax = new Vector2(1f, 1f);
                    vp.pivot = new Vector2(0.5f, 0.5f);
                    vp.offsetMin = Vector2.zero;
                    vp.offsetMax = Vector2.zero;
                }
                catch { }

                var vpGo = vp.gameObject;

                // Force Viewport masking to RectMask2D (remove Mask).
                try
                {
                    var mask = vpGo.GetComponent<Mask>();
                    if (mask != null)
                    {
                        if (Application.isPlaying) Destroy(mask);
                        else DestroyImmediate(mask);
                    }
                }
                catch { }

                try
                {
                    if (vpGo.GetComponent<RectMask2D>() == null)
                        vpGo.AddComponent<RectMask2D>();

                    // Ensure an Image exists (can be transparent); RectMask2D is the clipper.
                    var img = vpGo.GetComponent<Image>();
                    if (img == null) img = vpGo.AddComponent<Image>();
                    var c = img.color;
                    if (c.a > 0.001f)
                        img.color = new Color(c.r, c.g, c.b, 0f);
                }
                catch { }
            }

            // Content should be top-stretched.
            try
            {
                contentRoot.anchorMin = new Vector2(0f, 1f);
                contentRoot.anchorMax = new Vector2(1f, 1f);
                contentRoot.pivot = new Vector2(0.5f, 1f);
                contentRoot.anchoredPosition = Vector2.zero;

                contentRoot.sizeDelta = new Vector2(0f, 0f);
            }
            catch { }

            // Layout components on Content.
            try
            {
                var vlg = contentRoot.GetComponent<VerticalLayoutGroup>();
                if (vlg == null) vlg = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();

                vlg.childAlignment = TextAnchor.UpperLeft;
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.spacing = 6f;
                vlg.padding = new RectOffset(10, 10, 10, 10);

                var csf = contentRoot.GetComponent<ContentSizeFitter>();
                if (csf == null) csf = contentRoot.gameObject.AddComponent<ContentSizeFitter>();

                csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            catch { }

            // Ensure scrollRect.content is contentRoot.
            try
            {
                if (scrollRect.content != contentRoot)
                    scrollRect.content = contentRoot;
            }
            catch { }
        }

        /// <summary>
        /// This is the missing piece in your current setup:
        /// your rows exist and are clickable, but their graphics/text are invisible.
        /// We force them visible here to eliminate alpha/canvasgroup regressions.
        /// </summary>
        private void ForceRowVisible(GameObject rowGo)
        {
            if (rowGo == null) return;

            // CanvasGroups can hide everything.
            try
            {
                var cgs = rowGo.GetComponentsInChildren<CanvasGroup>(true);
                foreach (var cg in cgs)
                {
                    if (cg == null) continue;
                    cg.alpha = 1f;
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                    cg.ignoreParentGroups = false;
                }
            }
            catch { }

            // Force all UI Graphics visible.
            try
            {
                var graphics = rowGo.GetComponentsInChildren<Graphic>(true);
                foreach (var g in graphics)
                {
                    if (g == null) continue;
                    var c = g.color;
                    if (c.a < 0.99f)
                        g.color = new Color(c.r, c.g, c.b, 1f);
                    g.raycastTarget = true; // keep clicking working
                    g.enabled = true;
                    if (!g.gameObject.activeSelf) g.gameObject.SetActive(true);
                }
            }
            catch { }

            // Force all TMP text visible and non-tiny.
            try
            {
                var tmps = rowGo.GetComponentsInChildren<TMP_Text>(true);
                foreach (var t in tmps)
                {
                    if (t == null) continue;

                    // Ensure active/enabled.
                    if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                    t.enabled = true;

                    // Ensure alpha is 1.
                    t.alpha = 1f;

                    // Ensure color alpha is 1.
                    var c = t.color;
                    if (c.a < 0.99f)
                        t.color = new Color(c.r, c.g, c.b, 1f);

                    // If the text is effectively black-on-black, nudge to white (dev-safe).
                    // We do this only if it's very dark.
                    if (t.color.r < 0.15f && t.color.g < 0.15f && t.color.b < 0.15f)
                        t.color = new Color(1f, 1f, 1f, 1f);

                    // Font size floor.
                    if (t.fontSize < 18f)
                        t.fontSize = 22f;
                }
            }
            catch { }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // One-time “what was invisible?” report per open.
            if (_isOpen && !_loggedFirstRowVisibilityThisOpen)
            {
                _loggedFirstRowVisibilityThisOpen = true;
                try
                {
                    var anyTmp = rowGo.GetComponentInChildren<TMP_Text>(true);
                    var anyGraphic = rowGo.GetComponentInChildren<Graphic>(true);
                    var cg = rowGo.GetComponentInChildren<CanvasGroup>(true);

                    if (InventoryUiDebugEnabled)
                    {
                        Debug.Log(
                            $"[PlayerInventoryUI] FirstRowVisibilityReport row='{rowGo.name}' " +
                            $"hasTMP={(anyTmp != null)} tmpText='{(anyTmp != null ? anyTmp.text : "(null)")}' tmpColor={(anyTmp != null ? anyTmp.color.ToString() : "(n/a)")} tmpAlpha={(anyTmp != null ? anyTmp.alpha : -1f)} " +
                            $"hasGraphic={(anyGraphic != null)} graphicColor={(anyGraphic != null ? anyGraphic.color.ToString() : "(n/a)")} " +
                            $"hasCanvasGroup={(cg != null)} cgAlpha={(cg != null ? cg.alpha : -1f)}",
                            this);
                    }
                }
                catch { }
            }
#endif
        }

        private static bool EqualsIgnoreCase(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static TMP_Text FindTmpByNameHint(GameObject rootGo, params string[] nameHints)
        {
            if (rootGo == null || nameHints == null || nameHints.Length == 0)
                return null;

            TMP_Text best = null;

            try
            {
                var tmps = rootGo.GetComponentsInChildren<TMP_Text>(true);
                foreach (var t in tmps)
                {
                    if (t == null) continue;
                    var n = t.gameObject != null ? t.gameObject.name : null;
                    if (string.IsNullOrWhiteSpace(n)) continue;

                    for (int i = 0; i < nameHints.Length; i++)
                    {
                        var hint = nameHints[i];
                        if (string.IsNullOrWhiteSpace(hint)) continue;
                        if (EqualsIgnoreCase(n, hint) || n.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                            return t;
                    }

                    // fallback if nothing matches: take the largest font TMP as best guess
                    if (best == null || t.fontSize > best.fontSize)
                        best = t;
                }
            }
            catch { }

            return best;
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = Mathf.Clamp01(a);
            return c;
        }

        private Color GetRowBaseColor()
        {
            try
            {
                if (rowTemplate != null)
                {
                    var img = rowTemplate.GetComponent<Image>();
                    if (img != null)
                    {
                        var c = img.color;
                        // Avoid fully transparent base; default to white if it is.
                        if (c.a <= 0.001f)
                            return Color.white;
                        return new Color(c.r, c.g, c.b, 1f);
                    }
                }
            }
            catch { }

            return Color.white;
        }

        private Image EnsureRowBackgroundImage(GameObject rowGo)
        {
            if (rowGo == null) return null;

            Image img = null;
            try { img = rowGo.GetComponent<Image>(); } catch { }
            if (img == null)
            {
                try { img = rowGo.AddComponent<Image>(); } catch { }
            }

            if (img != null)
            {
                try
                {
                    img.raycastTarget = true;

                    // Ensure a sane baseline RGB, but leave alpha to styling.
                    var baseC = GetRowBaseColor();
                    img.color = new Color(baseC.r, baseC.g, baseC.b, img.color.a);
                }
                catch { }
            }

            return img;
        }

        private static void EnsureSelectedBar(GameObject rowGo, bool enabled, Color baseColor)
        {
            if (rowGo == null) return;

            Transform barTf = null;
            try { barTf = rowGo.transform.Find("SelectedBar"); } catch { }

            if (!enabled)
            {
                if (barTf != null)
                {
                    try { barTf.gameObject.SetActive(false); } catch { }
                }
                return;
            }

            GameObject barGo = null;
            if (barTf == null)
            {
                try
                {
                    barGo = new GameObject("SelectedBar", typeof(RectTransform), typeof(Image));
                    barGo.transform.SetParent(rowGo.transform, false);
                    barTf = barGo.transform;
                }
                catch { return; }
            }
            else
            {
                barGo = barTf.gameObject;
            }

            try { barGo.SetActive(true); } catch { }

            try
            {
                var rt = barTf as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = new Vector2(4f, 0f);
                }
            }
            catch { }

            try
            {
                var img = barGo.GetComponent<Image>();
                if (img != null)
                {
                    img.raycastTarget = false;
                    img.color = WithAlpha(baseColor, 0.90f);
                }
            }
            catch { }
        }

        private static void ConfigureButtonColors(Button btn, Color normal)
        {
            if (btn == null) return;

            try
            {
                var cb = btn.colors;

                // Subtle adjustments: slightly brighter on hover, slightly darker on press.
                var highlighted = normal;
                highlighted.r = Mathf.Clamp01(highlighted.r + 0.06f);
                highlighted.g = Mathf.Clamp01(highlighted.g + 0.06f);
                highlighted.b = Mathf.Clamp01(highlighted.b + 0.06f);
                highlighted.a = Mathf.Clamp01(highlighted.a + 0.06f);

                var pressed = normal;
                pressed.r = Mathf.Clamp01(pressed.r - 0.05f);
                pressed.g = Mathf.Clamp01(pressed.g - 0.05f);
                pressed.b = Mathf.Clamp01(pressed.b - 0.05f);
                pressed.a = Mathf.Clamp01(pressed.a - 0.06f);

                cb.normalColor = normal;
                cb.highlightedColor = highlighted;
                cb.pressedColor = pressed;
                cb.selectedColor = highlighted;
                cb.disabledColor = new Color(normal.r, normal.g, normal.b, Mathf.Clamp01(normal.a * 0.6f));

                btn.colors = cb;

                if (btn.transition == Selectable.Transition.None)
                    btn.transition = Selectable.Transition.ColorTint;
            }
            catch { }
        }

        private static void ApplyTextReadability(GameObject rowGo)
        {
            if (rowGo == null) return;

            try
            {
                var nameTmp = FindTmpByNameHint(rowGo, "Name", "ItemName", "Title", "Label");
                if (nameTmp != null)
                {
                    if (nameTmp.fontSize < 22f)
                        nameTmp.fontSize = 22f;

                    var c = nameTmp.color;
                    nameTmp.color = new Color(c.r, c.g, c.b, 1f);

                    var m = nameTmp.margin;
                    if (m.x < 12f) m.x = 12f;
                    nameTmp.margin = m;
                }

                var countTmp = FindTmpByNameHint(rowGo, "Count", "Qty", "Quantity", "Stack");
                if (countTmp != null)
                {
                    countTmp.fontSize = 20f;
                    var c2 = countTmp.color;
                    countTmp.color = new Color(c2.r, c2.g, c2.b, 0.95f);
                }
            }
            catch { }
        }

        private void ApplyRowVisualStyling(GameObject rowGo, int rowIndex, bool isSelected)
        {
            if (rowGo == null) return;

            // Normal shading
            const float evenAlpha = 0.18f;
            const float oddAlpha = 0.26f;
            const float selectedAlpha = 0.45f;

            float normalAlpha = (rowIndex % 2 == 0) ? evenAlpha : oddAlpha;
            float a = isSelected ? selectedAlpha : normalAlpha;

            var bg = EnsureRowBackgroundImage(rowGo);
            var baseColor = GetRowBaseColor();

            if (bg != null)
            {
                try
                {
                    bg.color = WithAlpha(new Color(baseColor.r, baseColor.g, baseColor.b, 1f), a);
                    bg.raycastTarget = true;
                }
                catch { }
            }

            // Optional accent bar for selected
            EnsureSelectedBar(rowGo, isSelected, baseColor);

            // Hover styling only if there is a Button
            try
            {
                var btn = rowGo.GetComponent<Button>();
                if (btn != null)
                {
                    if (bg != null && btn.targetGraphic == null)
                        btn.targetGraphic = bg;

                    ConfigureButtonColors(btn, bg != null ? bg.color : WithAlpha(baseColor, a));
                }
            }
            catch { }

            ApplyTextReadability(rowGo);
        }

        private void UpdateSelectionHighlightVisuals()
        {
            if (contentRoot == null)
                return;

            int rowIndex = 0;
            var templateTf = rowTemplate != null ? rowTemplate.transform : null;

            for (int i = 0; i < contentRoot.childCount; i++)
            {
                var ch = contentRoot.GetChild(i);
                if (ch == null) continue;
                if (templateTf != null && ch == templateTf) continue;

                var go = ch.gameObject;
                bool isSelected = !string.IsNullOrWhiteSpace(_selectedItemId) && go != null && go.name == $"Row_{_selectedItemId}";
                ApplyRowVisualStyling(go, rowIndex, isSelected);
                rowIndex++;
            }
        }

        private void EnsureCanvasVisibility()
        {
            // Make sure root is active and visible if someone zeroed a CanvasGroup higher up.
            try
            {
                if (root == null) return;

                var cgs = root.GetComponentsInChildren<CanvasGroup>(true);
                foreach (var cg in cgs)
                {
                    if (cg == null) continue;
                    if (cg.alpha < 1f) cg.alpha = 1f;
                    cg.blocksRaycasts = true;
                    cg.interactable = true;
                }
            }
            catch { }
        }

        private Dictionary<string, ItemDefinition> BuildItemDefinitionIndex()
        {
            var map = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);

            try
            {
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

            _itemDefById ??= BuildItemDefinitionIndex();
            if (_itemDefById != null && _itemDefById.TryGetValue(itemId, out var def))
                return def;

            return null;
        }

        private string ResolveItemId(ItemDefinition def)
        {
            if (def == null) return null;

            // Best-effort: match your existing item ID convention.
            // If your ItemDefinition has a canonical ID field, prefer that.
            try
            {
                // Common patterns: def.id, def.itemId, def.name
                var t = def.GetType();

                var f = t.GetField("id");
                if (f != null)
                {
                    var v = f.GetValue(def) as string;
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }

                var p = t.GetProperty("id");
                if (p != null && p.PropertyType == typeof(string))
                {
                    var v = p.GetValue(def) as string;
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }

                f = t.GetField("itemId");
                if (f != null)
                {
                    var v = f.GetValue(def) as string;
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }

                p = t.GetProperty("itemId");
                if (p != null && p.PropertyType == typeof(string))
                {
                    var v = p.GetValue(def) as string;
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }
            }
            catch { }

            // Fallback: sanitized name.
            return def.name != null ? def.name.Trim() : null;
        }

        private bool WasTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            try
            {
                return Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame;
            }
            catch { return false; }
#else
            return Input.GetKeyDown(KeyCode.I);
#endif
        }

        private void EnsureTabs()
        {
            if (scrollRect == null || root == null)
                return;

            if (tabsRoot == null)
            {
                // Try to find an existing tabs root.
                var found = root.transform.Find("Tabs");
                if (found != null)
                    tabsRoot = found as RectTransform;
            }

            if (tabsRoot == null)
            {
                // Create a minimal tab bar above the ScrollRect.
                var parent = scrollRect.transform.parent as RectTransform;
                if (parent == null)
                    parent = root.transform as RectTransform;
                if (parent == null)
                    return;

                var tabsGo = new GameObject("Tabs", typeof(RectTransform));
                tabsRoot = tabsGo.GetComponent<RectTransform>();
                tabsRoot.SetParent(parent, false);

                // Insert just above the scroll rect if possible.
                try
                {
                    int idx = scrollRect.transform.GetSiblingIndex();
                    tabsRoot.SetSiblingIndex(Mathf.Max(0, idx));
                }
                catch { }

                var hlg = tabsGo.AddComponent<HorizontalLayoutGroup>();
                hlg.childForceExpandHeight = false;
                hlg.childForceExpandWidth = true;
                hlg.childControlHeight = true;
                hlg.childControlWidth = true;
                hlg.spacing = 6f;
                hlg.padding = new RectOffset(8, 8, 6, 6);

                var fitter = tabsGo.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // Background tint to separate from list.
                var bg = tabsGo.AddComponent<Image>();
                bg.color = new Color(0.08f, 0.08f, 0.08f, 0.65f);
            }

            if (_tabWeapons != null && _tabMaterials != null && _tabConsumables != null && _tabSkilling != null)
            {
                RefreshTabVisuals();
                return;
            }

            CreateOrBindTabButtons();
            RefreshTabVisuals();
        }

        private void CreateOrBindTabButtons()
        {
            if (tabsRoot == null)
                return;

            // If children already exist, attempt to bind by name.
            if (tabsRoot.childCount > 0)
            {
                _tabWeapons = FindButtonUnder(tabsRoot, "Tab_WeaponsGear") ?? _tabWeapons;
                _tabMaterials = FindButtonUnder(tabsRoot, "Tab_Materials") ?? _tabMaterials;
                _tabConsumables = FindButtonUnder(tabsRoot, "Tab_Consumables") ?? _tabConsumables;
                _tabSkilling = FindButtonUnder(tabsRoot, "Tab_Skilling") ?? _tabSkilling;
            }

            _tabWeapons ??= CreateTabButton(tabsRoot, "Tab_WeaponsGear", "Weapons/Gear", out _tabWeaponsText);
            _tabMaterials ??= CreateTabButton(tabsRoot, "Tab_Materials", "Materials", out _tabMaterialsText);
            _tabConsumables ??= CreateTabButton(tabsRoot, "Tab_Consumables", "Consumables", out _tabConsumablesText);
            _tabSkilling ??= CreateTabButton(tabsRoot, "Tab_Skilling", "Skilling", out _tabSkillingText);

            WireTab(_tabWeapons, InventoryTab.WeaponsGear);
            WireTab(_tabMaterials, InventoryTab.Materials);
            WireTab(_tabConsumables, InventoryTab.Consumables);
            WireTab(_tabSkilling, InventoryTab.Skilling);
        }

        private void WireTab(Button button, InventoryTab tab)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (_activeTab == tab) return;
                _activeTab = tab;
                RefreshTabVisuals();
                RefreshList();
                RefreshDetails();
            });
        }

        private void RefreshTabVisuals()
        {
            ApplyTabVisual(_tabWeapons, _tabWeaponsText, _activeTab == InventoryTab.WeaponsGear);
            ApplyTabVisual(_tabMaterials, _tabMaterialsText, _activeTab == InventoryTab.Materials);
            ApplyTabVisual(_tabConsumables, _tabConsumablesText, _activeTab == InventoryTab.Consumables);
            ApplyTabVisual(_tabSkilling, _tabSkillingText, _activeTab == InventoryTab.Skilling);
        }

        private static void ApplyTabVisual(Button button, TMP_Text label, bool selected)
        {
            if (button != null)
            {
                var img = button.GetComponent<Image>();
                if (img != null)
                    img.color = selected
                        ? new Color(0.18f, 0.18f, 0.18f, 0.95f)
                        : new Color(0.12f, 0.12f, 0.12f, 0.80f);
            }

            if (label != null)
                label.color = selected ? new Color(0.95f, 0.95f, 0.95f, 1f) : new Color(0.75f, 0.75f, 0.75f, 1f);
        }

        private Button CreateTabButton(RectTransform parent, string name, string label, out TMP_Text labelText)
        {
            labelText = null;
            if (parent == null) return null;

            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.12f, 0.12f, 0.12f, 0.80f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 34f;
            le.minHeight = 34f;
            le.flexibleHeight = 0f;
            le.flexibleWidth = 1f;

            // Label
            var textGo = new GameObject("Label", typeof(RectTransform));
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.SetParent(rt, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.fontSize = 18f;
            tmp.color = new Color(0.75f, 0.75f, 0.75f, 1f);
            if (titleText != null && titleText.font != null)
                tmp.font = titleText.font;

            labelText = tmp;
            return btn;
        }

        private static Button FindButtonUnder(RectTransform parent, string childName)
        {
            if (parent == null) return null;
            var t = parent.Find(childName);
            if (t == null) return null;
            return t.GetComponent<Button>();
        }

        private bool PassesTabFilter(ItemDefinition def, string itemId)
        {
            // Unknown items: keep visible under Weapons/Gear so they don't disappear.
            if (def == null)
                return _activeTab == InventoryTab.WeaponsGear;

            return _activeTab switch
            {
                InventoryTab.WeaponsGear => def.itemType == AbyssItemType.Weapon || def.itemType == AbyssItemType.Misc,
                InventoryTab.Materials => def.itemType == AbyssItemType.Workshop,
                InventoryTab.Consumables => def.itemType == AbyssItemType.Consumable,
                InventoryTab.Skilling => def.itemType == AbyssItemType.Skilling,
                _ => true
            };
        }
    }
}
