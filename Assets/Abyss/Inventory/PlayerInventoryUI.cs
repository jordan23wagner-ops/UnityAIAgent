using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
            // QA (Dec 2025):
            // - Enter Play Mode
            // - Add fish_raw_shrimp via fishing
            // - Open inventory: shrimp should appear under All tab by default
            // - Switch to Weapons/Gear: shrimp should disappear there if classified as Skilling (expected)
            // - Switch back to All: shrimp appears again
            // - No exceptions, no new compile errors
            All = 4,
            WeaponsGear = 0,
            Materials = 1,
            Consumables = 2,
            Skilling = 3,
        }

        private const bool INVENTORY_UI_DEBUG = false;
        private static bool InventoryUiDebugEnabled => INVENTORY_UI_DEBUG;

        private const bool INV_DEBUG = false;

    #if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const bool INV_DIAGNOSTICS = true;
    #else
        private const bool INV_DIAGNOSTICS = false;
    #endif

        // OSRS-style inventory grid.
        private const int InventoryGridColumns = 4;
        private const int InventoryGridRows = 7;
        private const int InventoryGridSlots = InventoryGridColumns * InventoryGridRows;

        [Header("Root")]
        [SerializeField] private GameObject root;
        [SerializeField] private Button closeButton;

        [Header("Character Tabs (optional)")]
        [SerializeField] private Button characterInventoryTabButton;
        [SerializeField] private Button characterEquipmentTabButton;

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
        private Abyss.Equipment.PlayerEquipmentUI _equipmentUi;

        private string _inventorySource;
        private int _lastInventoryInstanceId;
        private bool _loggedInventoryForThisOpen;
        private bool _loggedScrollWiringForThisOpen;
        private bool _loggedFirstRowVisibilityThisOpen;

        private readonly List<GameObject> _spawnedRows = new();
        private readonly List<PlayerInventoryRowUI> _spawnedSlotViews = new();
        private Dictionary<string, ItemDefinition> _itemDefById;

        private string _selectedItemId;
        private ItemDefinition _selectedDef;
        private int _selectedCount;

        // UI-only selection index for visuals (grid slot index 0..27, or -1 none)
        private int _selectedSlotIndex = -1;

        // Default to All so dev testing never hides items behind filters.
        private InventoryTab _activeTab = InventoryTab.All;

        private Button _tabAll;
        private Button _tabWeapons;
        private Button _tabMaterials;
        private Button _tabConsumables;
        private Button _tabSkilling;

        private TMP_Text _tabAllText;
        private TMP_Text _tabWeaponsText;
        private TMP_Text _tabMaterialsText;
        private TMP_Text _tabConsumablesText;
        private TMP_Text _tabSkillingText;

        private Button _equipButton;
        private TMP_Text _equipButtonText;

        private bool _isOpen;

        public bool IsOpen => _isOpen;

        private readonly Dictionary<Image, Color> _forcedOpaqueImages = new();

        private Image _backdropImage;
        private Color _backdropOriginalColor;
        private bool _backdropOriginalCaptured;

        private int _lastRefreshFrame = -1;
        private bool _refreshQueued;

        private bool _warnedContentLayoutConflict;

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

            WireCharacterTabs();
        }

        private void Update()
        {
            // Some scenes/scripts may toggle the inventory root active without calling Open()/Close().
            // Keep _isOpen in sync so hotkeys/buttons still work.
            SyncOpenStateFromRoot();

            if (_refreshQueued)
            {
                _refreshQueued = false;
                RefreshAll();
            }

            // TASK 1: fallback input: E equips selected item while inventory is open.
            if (_isOpen && WasEquipPressed() && !Abyss.Shop.MerchantShopUI.IsOpen)
                TryEquipSelected();

            if (!WasTogglePressed())
                return;

            // Avoid fighting with merchant UI.
            if (Abyss.Shop.MerchantShopUI.IsOpen)
                return;

            if (_isOpen) Close();
            else Open();
        }

        private bool WasEquipPressed()
        {
#if ENABLE_INPUT_SYSTEM
            try
            {
                return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
            }
            catch { return false; }
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }

        private void SyncOpenStateFromRoot()
        {
            if (root == null)
                return;

            // If the root is visible, treat as open.
            if (root.activeSelf)
            {
                if (!_isOpen)
                {
                    _isOpen = true;
                    EnsureEquipButton();
                    EnsureInventory();
                    EnsureEquipment();
                    RefreshDetails();
                }
            }
            else
            {
                if (_isOpen)
                    _isOpen = false;
            }
        }

        private static EquipmentSlot GuessEquipSlot(ItemDefinition def, string itemId)
        {
            if (def != null)
            {
                try
                {
                    if (def.equipmentSlot != EquipmentSlot.None)
                        return def.equipmentSlot;
                }
                catch { }

                return EquipmentSlot.None;
            }

            if (string.IsNullOrWhiteSpace(itemId))
                return EquipmentSlot.None;

            // Rolled loot instance support
            try
            {
                var reg = Abyssbound.Loot.LootRegistryRuntime.GetOrCreate();
                if (reg != null && reg.TryGetRolledInstance(itemId, out var inst) && inst != null)
                {
                    if (reg.TryGetItem(inst.baseItemId, out var baseItem) && baseItem != null)
                        return baseItem.slot;
                }
            }
            catch { }

            return EquipmentSlot.None;
        }

        private bool CanEquipSelected(ItemDefinition def, string itemId)
        {
            if (def != null)
            {
                try
                {
                    if (def.equipmentSlot != EquipmentSlot.None)
                        return true;
                }
                catch { }

                return false;
            }

            if (string.IsNullOrWhiteSpace(itemId))
                return false;

            // Rolled loot instance support
            try
            {
                var reg = Abyssbound.Loot.LootRegistryRuntime.GetOrCreate();
                if (reg != null && reg.TryGetRolledInstance(itemId, out var inst) && inst != null)
                {
                    if (reg.TryGetItem(inst.baseItemId, out var baseItem) && baseItem != null)
                        return baseItem.slot != EquipmentSlot.None;
                }
            }
            catch { }

            return false;
        }

        private static string SanitizeReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return "";

            reason = reason.Replace("\r", " ").Replace("\n", " ");
            while (reason.Contains("  "))
                reason = reason.Replace("  ", " ");
            return reason.Trim();
        }

        private static void LogEquipAttempt(string itemId, EquipmentSlot slot, bool success, string reason)
        {
            itemId ??= "";
            reason = SanitizeReason(reason);
            var ok = success.ToString().ToLowerInvariant();
            Debug.Log($"[EQUIP] itemId={itemId} slot={slot} success={ok} reason={reason}");
        }

        private void TryEquipSelected()
        {
            EnsureEquipment();
            EnsureInventory();

            var def = _selectedDef;
            var itemId = _selectedItemId;
            var slot = GuessEquipSlot(def, itemId);

            // TASK 1: single log line per attempt, exactly matching requested format.
            if (_equipment == null)
            {
                LogEquipAttempt(itemId, slot, success: false, reason: "No PlayerEquipment");
                return;
            }

            if (string.IsNullOrWhiteSpace(itemId))
            {
                LogEquipAttempt(itemId, slot, success: false, reason: "No item selected");
                return;
            }

            // Spec: equippable only when equipmentSlot != None.
            if (slot == EquipmentSlot.None)
            {
                LogEquipAttempt(itemId, slot, success: false, reason: "Not equippable (equipmentSlot=None)");
                return;
            }

            if (_inventory == null)
            {
                LogEquipAttempt(itemId, slot, success: false, reason: "No PlayerInventory");
                return;
            }

            // Inventory-authoritative: consume 1 from inventory, equip, and return conflicts to inventory.
            bool ok = _equipment.TryEquipFromInventory(_inventory, ResolveItemDefinition, itemId, out var message);
            string reason = string.IsNullOrWhiteSpace(message) ? (ok ? "OK" : "Failed") : message;
            LogEquipAttempt(itemId, slot, ok, reason);

            if (ok)
                RefreshAll();
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

            // Ensure a single tooltip instance exists under this UI root.
            try { ItemTooltipUI.GetOrCreateUnder(root.transform); } catch { }

            HideStrayLegacyCategoryTexts();
            EnsureBackdropIsTransparent();
            ForceOpaqueBackground(true);

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

            if (_equipmentUi == null)
            {
                try
                {
#if UNITY_2022_2_OR_NEWER
                    _equipmentUi = FindFirstObjectByType<Abyss.Equipment.PlayerEquipmentUI>();
#else
                    _equipmentUi = FindObjectOfType<Abyss.Equipment.PlayerEquipmentUI>();
#endif
                }
                catch { }
            }

            try { _inputAuthority?.SetUiInputLocked(true); } catch { }

            // Build the grid immediately so we don't show a blank/flashy intermediate frame.
            _refreshQueued = false;
            RefreshAll();
        }

        private void WireCharacterTabs()
        {
            // Inventory tab is "selected" while this window is open.
            if (characterInventoryTabButton != null)
                characterInventoryTabButton.interactable = false;

            if (characterEquipmentTabButton != null)
            {
                characterEquipmentTabButton.onClick.RemoveAllListeners();
                characterEquipmentTabButton.onClick.AddListener(() =>
                {
                    Close();
                    try { _equipmentUi?.Open(); } catch { }
                });
            }
        }

        private void EnsureBackdropIsTransparent()
        {
            if (root == null)
                return;

            try
            {
                if (_backdropImage == null)
                {
                    var t = FindDeepChild(root.transform, "Backdrop");
                    if (t != null)
                        _backdropImage = t.GetComponent<Image>();
                }

                if (_backdropImage == null)
                    return;

                if (!_backdropOriginalCaptured)
                {
                    _backdropOriginalCaptured = true;
                    _backdropOriginalColor = _backdropImage.color;
                }

                // User request: see the game behind the inventory. Keep the backdrop for raycast-blocking,
                // but make it visually transparent.
                var c = _backdropImage.color;
                if (c.a > 0.001f)
                    _backdropImage.color = new Color(c.r, c.g, c.b, 0f);
            }
            catch
            {
                // Best-effort only.
            }
        }

        private void ForceOpaqueBackground(bool enabled)
        {
            if (root == null)
                return;

            if (!enabled)
            {
                if (_forcedOpaqueImages.Count == 0)
                    return;

                foreach (var kv in _forcedOpaqueImages)
                {
                    try
                    {
                        if (kv.Key != null)
                            kv.Key.color = kv.Value;
                    }
                    catch { }
                }

                _forcedOpaqueImages.Clear();
                return;
            }

            if (_forcedOpaqueImages.Count > 0)
                return;

            // Preferred: known names created by BuildPlayerInventoryUIEditor.
            TryForceOpaqueByName("Panel");
            TryForceOpaqueByName("ItemsScrollView");
            TryForceOpaqueByName("DetailsPanel");

            // Fallback: if we couldn't find any of the conventional panels, force opaque on large semi-transparent
            // images under the inventory root (excluding item tiles/tabs/details).
            if (_forcedOpaqueImages.Count == 0)
                TryForceOpaqueHeuristic();
        }

        private void TryForceOpaqueByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || root == null)
                return;

            var t = FindDeepChild(root.transform, name);
            if (t == null)
                return;

            var img = t.GetComponent<Image>();
            if (img == null)
                return;

            ForceOpaque(img);
        }

        private void TryForceOpaqueHeuristic()
        {
            try
            {
                var images = root.GetComponentsInChildren<Image>(true);
                if (images == null || images.Length == 0)
                    return;

                for (int i = 0; i < images.Length; i++)
                {
                    var img = images[i];
                    if (img == null)
                        continue;

                    // Never force the full-screen backdrop opaque; we want the world visible.
                    if (string.Equals(img.gameObject.name, "Backdrop", StringComparison.Ordinal))
                        continue;

                    if (contentRoot != null && img.transform.IsChildOf(contentRoot))
                        continue;
                    if (tabsRoot != null && img.transform.IsChildOf(tabsRoot))
                        continue;
                    if (detailsUI != null && img.transform.IsChildOf(detailsUI.transform))
                        continue;
                    if (rowTemplate != null && img.transform.IsChildOf(rowTemplate.transform))
                        continue;

                    var rt = img.rectTransform;
                    if (rt == null)
                        continue;

                    float area = Mathf.Abs(rt.rect.width * rt.rect.height);
                    if (area < 20000f)
                        continue;

                    ForceOpaque(img);
                }
            }
            catch
            {
                // Best-effort only.
            }
        }

        private void ForceOpaque(Image img)
        {
            if (img == null)
                return;

            try
            {
                var c = img.color;
                if (c.a >= 0.999f)
                    return;

                if (!_forcedOpaqueImages.ContainsKey(img))
                    _forcedOpaqueImages.Add(img, c);

                c.a = 1f;
                img.color = c;
            }
            catch
            {
                // Best-effort only.
            }
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent == null || string.IsNullOrEmpty(name))
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child == null) continue;

                if (string.Equals(child.name, name, StringComparison.Ordinal))
                    return child;

                var found = FindDeepChild(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private void HideStrayLegacyCategoryTexts()
        {
            if (root == null)
                return;

            try
            {
                var texts = root.GetComponentsInChildren<TMP_Text>(true);
                if (texts == null || texts.Length == 0)
                    return;

                for (int i = 0; i < texts.Length; i++)
                {
                    var t = texts[i];
                    if (t == null) continue;

                    // Keep known UI text elements.
                    if (t == titleText || t == goldText)
                        continue;

                    if (detailsUI != null && t.transform.IsChildOf(detailsUI.transform))
                        continue;

                    // Don't hide tab button labels.
                    if (tabsRoot != null && t.transform.IsChildOf(tabsRoot))
                        continue;

                    // Don't hide inventory slot row text.
                    if (rowTemplate != null && t.transform.IsChildOf(rowTemplate.transform))
                        continue;
                    if (contentRoot != null && t.transform.IsChildOf(contentRoot))
                        continue;

                    var s = t.text;
                    if (string.IsNullOrWhiteSpace(s))
                        continue;

                    s = s.Trim();
                    var lower = s.ToLowerInvariant();

                    // Legacy category label(s) that shouldn't float over the grid.
                    if (lower.Contains("weapon") && (lower.Contains("util") || lower.Contains("utility") || lower.Contains("utilities")))
                    {
                        t.gameObject.SetActive(false);
                        continue;
                    }
                }
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }

        public void Close()
        {
            if (!_isOpen)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log($"[InventoryUI TRACE] Close frame={Time.frameCount}", this);
#endif

            _isOpen = false;

            ForceOpaqueBackground(false);

            // Restore original backdrop tint (if any).
            try
            {
                if (_backdropImage != null && _backdropOriginalCaptured)
                    _backdropImage.color = _backdropOriginalColor;
            }
            catch { }

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // TEMP (Dec 2025): tab/filter diagnostics to ensure dev testing doesn't hide items.
            // Acceptance / QA steps are documented at the InventoryTab enum.
            try
            {
                EnsureInventory();
                var snap = _inventory != null ? _inventory.GetAllItemsSnapshot() : null;

                int considered = 0;
                int passed = 0;
                bool shrimpPresent = false;
                string shrimpType = "(unresolved)";
                bool shrimpHasIcon = false;

                if (snap != null)
                {
                    foreach (var kv in snap)
                    {
                        var itemId = kv.Key;
                        int count = kv.Value;
                        if (string.IsNullOrWhiteSpace(itemId) || count <= 0)
                            continue;

                        considered++;
                        var def = ResolveItemDefinition(itemId);
                        if (PassesTabFilter(def, itemId))
                            passed++;
                    }

                    if (snap.TryGetValue("fish_raw_shrimp", out var shrimpCount) && shrimpCount > 0)
                    {
                        shrimpPresent = true;
                        var def = ResolveItemDefinition("fish_raw_shrimp");
                        if (def != null)
                        {
                            shrimpType = def.itemType.ToString();
                            shrimpHasIcon = def.icon != null;
                        }
                    }
                }

                Debug.Log(
                    $"[INV][REFRESHALL] tab={_activeTab} considered={considered} pass={passed} " +
                    (shrimpPresent
                        ? $"fish_raw_shrimp: itemType={shrimpType} hasIcon={shrimpHasIcon}"
                        : "fish_raw_shrimp: absent"),
                    this);
            }
            catch { }
#endif

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
#pragma warning disable CS0162
            if (INV_DEBUG)
            {
                int nonEmptyStacks = 0;
                try
                {
                    foreach (var kv in snap)
                    {
                        if (!string.IsNullOrWhiteSpace(kv.Key) && kv.Value > 0)
                            nonEmptyStacks++;
                    }
                }
                catch { }

                Debug.Log($"[INVDBG][UI REFRESH] snapshotKeys={snap.Count} nonEmptyStacks={nonEmptyStacks} gridSlots={InventoryGridSlots}", this);
            }
#pragma warning restore CS0162
#endif

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

            // Build a filtered list of stacks, then place them into a fixed 4x7 grid.
            var visibleStacks = new List<(string itemId, int count, ItemDefinition def)>(keys.Count);
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

                visibleStacks.Add((itemId, count, def));
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
#pragma warning disable CS0162
            if (INV_DEBUG)
            {
                int nonEmptySlots = Mathf.Min(visibleStacks.Count, InventoryGridSlots);
                Debug.Log($"[INVDBG][UI REFRESH] visibleStacks={visibleStacks.Count} nonEmptySlotsShown={nonEmptySlots} rowsToCreate={InventoryGridSlots}", this);
            }
#pragma warning restore CS0162
#endif

            // Sync selected slot index based on currently selected item id (UI-only).
            _selectedSlotIndex = FindSelectedSlotIndexInVisibleStacks(visibleStacks, _selectedItemId);

            if (visibleStacks.Count > InventoryGridSlots)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[PlayerInventoryUI] Inventory has {visibleStacks.Count} stacks but UI is fixed to {InventoryGridSlots} slots; truncating display.", this);
#endif
            }

            // Prefer square cells sized to fit the viewport.
            Vector2 cellSize = new Vector2(64f, 64f);
            try
            {
                if (contentRoot != null)
                {
                    var grid = contentRoot.GetComponent<GridLayoutGroup>();
                    if (grid != null)
                        cellSize = grid.cellSize;
                }
            }
            catch { }

            for (int slotIndex = 0; slotIndex < InventoryGridSlots; slotIndex++)
            {
                bool hasItem = slotIndex < visibleStacks.Count;
                string itemId = hasItem ? visibleStacks[slotIndex].itemId : null;
                int count = hasItem ? visibleStacks[slotIndex].count : 0;
                var def = hasItem ? visibleStacks[slotIndex].def : null;

                int capturedSlotIndex = slotIndex;

                var go = Instantiate(rowTemplate.gameObject, contentRoot, false);
                createdRowCount++;
                go.name = hasItem ? $"Row_{itemId}" : $"EmptySlot_{slotIndex}";
                go.SetActive(true);

                var capturedGo = go;

                // IMPORTANT: put the row into grid mode immediately so it never renders a list-mode
                // (often-white) background for a frame.
                var row = go.GetComponent<PlayerInventoryRowUI>();
                if (row != null)
                    row.SetGridMode(true);

                // Requirement: stable slot index stored on each row.
                if (row != null)
                {
                    try { row.SetSlotIndex(slotIndex); } catch { }
                }

                if (row != null)
                {
                    if (_spawnedSlotViews.Count <= capturedSlotIndex)
                    {
                        while (_spawnedSlotViews.Count <= capturedSlotIndex)
                            _spawnedSlotViews.Add(null);
                    }
                    _spawnedSlotViews[capturedSlotIndex] = row;
                }

                // Ensure stable layout metadata.
                var le = go.GetComponent<LayoutElement>();
                if (le == null) le = go.AddComponent<LayoutElement>();
                le.preferredWidth = cellSize.x;
                le.preferredHeight = cellSize.y;
                le.minWidth = cellSize.x;
                le.minHeight = cellSize.y;
                le.flexibleWidth = 0f;
                le.flexibleHeight = 0f;

                var rt = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    // GridLayoutGroup controls positioning and size.
                    rt.localScale = Vector3.one;
                    rt.localRotation = Quaternion.identity;

                    // Normalize anchors so layout calculations are consistent.
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                }

                // Brute-force visibility so “clickable but invisible” can’t happen.
                ForceRowVisible(go);

                bool isSelected = hasItem && _selectedSlotIndex == capturedSlotIndex;
                ApplyRowVisualStyling(go, rowIndex, isSelected);

                // --- [INV] Debug: build-time diagnostics (one log per slot) ---
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (INV_DIAGNOSTICS)
                {
                    try
                    {
                        var btn = go.GetComponent<Button>();
                        var img = go.GetComponent<Image>();
                        Debug.Log($"[INV][BUILD] Slot {slotIndex} | empty={!hasItem} | hasButton={(btn != null)} | hasImage={(img != null)} | raycast={(img != null && img.raycastTarget)}", this);
                    }
                    catch { }
                }
#endif

                if (row != null)
                {
                    // Debug context for hover logs.
                    try { row.SetDebugContext(slotIndex, !hasItem); } catch { }

                    if (hasItem)
                    {
                        string capturedId = itemId;
                        int capturedCount = count;
                        row.Bind(def, capturedId, capturedCount, () =>
                        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.Log($"[INV][CLICK] slotIndex={capturedSlotIndex} empty=false", this);
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            if (INV_DIAGNOSTICS)
                            {
                                try
                                {
                                    Debug.Log($"[INV][CLICK] Slot {slotIndex} empty=false", this);
                                    Debug.Log($"[INV][CLICK ITEM] Selecting itemId={capturedId}", this);
                                    Debug.Log($"[INV][RAYCAST] currentSelected={((EventSystem.current != null) ? EventSystem.current.currentSelectedGameObject?.name : "(no EventSystem)")}", this);

                                    var btn = go.GetComponent<Button>();
                                    var img = go.GetComponent<Image>();
                                    Debug.Log($"[INV][RAYCAST TARGETS] hasButton={(btn != null)} targetGraphic={(btn != null && btn.targetGraphic != null ? btn.targetGraphic.name : "(null)")} hasImage={(img != null)} imgRaycast={(img != null && img.raycastTarget)}", this);
                                }
                                catch { }
                            }
#endif
                            _selectedSlotIndex = capturedSlotIndex;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.Log($"[INV][SEL SET] _selectedSlotIndex={_selectedSlotIndex}", this);
#endif
                            Select(capturedId, capturedCount);
                            UpdateSelectionVisuals();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.Log($"[INV][SELECT] slot={_selectedSlotIndex}", this);
#endif
                        });

                        if (first == null)
                        {
                            first = row;
                            firstId = capturedId;
                            firstCount = capturedCount;
                        }

                        renderedStacks++;
                    }
                    else
                    {
                        row.BindEmpty(() =>
                        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.Log($"[INV][CLICK] slotIndex={capturedSlotIndex} empty=true", this);
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            if (INV_DIAGNOSTICS)
                            {
                                try
                                {
                                    Debug.Log($"[INV][CLICK] Slot {slotIndex} empty=true", this);
                                    Debug.Log("[INV][CLICK EMPTY] Clearing selection", this);
                                    Debug.Log($"[INV][RAYCAST] currentSelected={((EventSystem.current != null) ? EventSystem.current.currentSelectedGameObject?.name : "(no EventSystem)")}", this);

                                    var btn = go.GetComponent<Button>();
                                    var img = go.GetComponent<Image>();
                                    Debug.Log($"[INV][RAYCAST TARGETS] hasButton={(btn != null)} targetGraphic={(btn != null && btn.targetGraphic != null ? btn.targetGraphic.name : "(null)")} hasImage={(img != null)} imgRaycast={(img != null && img.raycastTarget)}", this);
                                }
                                catch { }
                            }
#endif
                            _selectedSlotIndex = -1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.Log($"[INV][SEL SET] _selectedSlotIndex={_selectedSlotIndex}", this);
#endif
                            ClearSelection();
                            UpdateSelectionVisuals();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.Log($"[INV][SELECT] slot={_selectedSlotIndex}", this);
#endif
                        });
                    }
                }

                rowIndex++;
                _spawnedRows.Add(go);
            }

            // Keep selection valid.
            // If nothing is selected, keep it empty (do not auto-select first).
            if (!string.IsNullOrWhiteSpace(_selectedItemId))
            {
                // If selected item no longer exists, fall back to first.
                int selCount = snap.TryGetValue(_selectedItemId, out var sc) ? sc : 0;
                if (selCount <= 0 && first != null)
                {
                    Select(firstId, firstCount);
                    _selectedSlotIndex = 0;
                }
                else if (_selectedSlotIndex < 0)
                {
                    // Selection exists in inventory but is filtered out by current tab.
                    if (first != null)
                    {
                        Select(firstId, firstCount);
                        _selectedSlotIndex = 0;
                    }
                    else
                    {
                        _selectedItemId = null;
                        _selectedDef = null;
                        _selectedCount = 0;
                        detailsUI?.Clear();
                        _selectedSlotIndex = -1;
                    }
                }
            }
            else
            {
                _selectedSlotIndex = -1;
            }

            // Ensure selection visuals survive RefreshList rebuild.
            UpdateSelectionVisuals();

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
            string detailsId = _selectedItemId;
            var def = ResolveItemDefinition(_selectedItemId);

            // Rolled loot instances (ri_...) use Loot V2 details (includes iLvl + set display).
            if (!string.IsNullOrWhiteSpace(_selectedItemId) && _selectedItemId.StartsWith("ri_", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var reg = Abyssbound.Loot.LootRegistryRuntime.GetOrCreate();
                    if (reg != null && reg.TryGetRolledInstance(_selectedItemId, out var inst) && inst != null)
                    {
                        detailsUI.SetLootInstance(inst, reg, count);
                        RefreshEquipButtonState(null);
                        return;
                    }
                }
                catch { }
            }

            detailsUI.Set(def, detailsId, count);
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

        private void ClearSelection()
        {
            _selectedItemId = null;
            _selectedDef = null;
            _selectedCount = 0;

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

            bool canEquip = CanEquipSelected(selectedDef, _selectedItemId);

            // UX: only show the button when it can actually do something.
            bool show = canEquip;
            try { _equipButton.gameObject.SetActive(show); } catch { }

            if (!show)
                return;

            _equipButton.interactable = true;
            if (_equipButtonText != null)
                _equipButtonText.text = "Equip";
        }

        private void OnEquipPressed()
        {
            EnsureEquipment();

            // Same equip attempt as hotkey.
            TryEquipSelected();
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
            _spawnedSlotViews.Clear();
        }

        private int FindSelectedSlotIndexInVisibleStacks(List<(string itemId, int count, ItemDefinition def)> visibleStacks, string selectedItemId)
        {
            if (string.IsNullOrWhiteSpace(selectedItemId) || visibleStacks == null)
                return -1;

            for (int i = 0; i < visibleStacks.Count && i < InventoryGridSlots; i++)
            {
                if (string.Equals(visibleStacks[i].itemId, selectedItemId, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private void UpdateSelectionVisualsBySlotIndex(int selectedIndexOrMinus1)
        {
            // Iterate instantiated slot views and set selection state.
            for (int i = 0; i < _spawnedSlotViews.Count; i++)
            {
                var row = _spawnedSlotViews[i];
                if (row == null) continue;
                row.SetSelected(i == selectedIndexOrMinus1);
            }
        }

        private void UpdateSelectionVisuals()
        {
            if (contentRoot == null)
                return;

            // Requirement: iterate all PlayerInventoryRowUI instances under contentRoot.
            try
            {
                var rows = contentRoot.GetComponentsInChildren<PlayerInventoryRowUI>(includeInactive: false);
                if (rows == null)
                    return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[INV][SEL APPLY] applying selectedSlot={_selectedSlotIndex} to rows={rows.Length}", this);
#endif

                for (int i = 0; i < rows.Length; i++)
                {
                    var row = rows[i];
                    if (row == null) continue;
                    row.SetSelected(row.SlotIndex == _selectedSlotIndex);
                }
            }
            catch
            {
                // Best-effort only.
            }
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

            // Centralized resolver: keep UI and gameplay (Fishing/Gathering/Loot) on the same PlayerInventory instance.
            try
            {
                var inv = Game.Systems.PlayerInventoryResolver.GetOrFindWithDiagnostics(out source);
                if (inv != null)
                    return inv;
            }
            catch
            {
                // Fall back to legacy resolver below.
                source = null;
            }

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

                if (scrollRect.viewport != null)
                    MakeViewportTransparent(scrollRect.viewport);
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

                    // RectMask2D does not require an Image. If one exists, force it fully transparent
                    // to avoid a one-frame white flash on open.
                    var img = vpGo.GetComponent<Image>();
                    if (img != null)
                    {
                        var c = img.color;
                        if (c.a > 0.001f)
                            img.color = new Color(c.r, c.g, c.b, 0f);
                    }
                }
                catch { }
            }

            // Content should stretch (GridLayoutGroup will align items).
            try
            {
                contentRoot.anchorMin = new Vector2(0f, 0f);
                contentRoot.anchorMax = new Vector2(1f, 1f);
                contentRoot.pivot = new Vector2(0.5f, 0.5f);
                contentRoot.offsetMin = Vector2.zero;
                contentRoot.offsetMax = Vector2.zero;
            }
            catch { }

            // Layout components on Content: fixed 4x7 grid.
            try
            {
                // Remove list layout components if present.
                var vlg = contentRoot.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    // IMPORTANT: in play mode we must remove immediately; Destroy() is deferred and will
                    // prevent adding GridLayoutGroup in the same frame.
                    try { DestroyImmediate(vlg); } catch { if (Application.isPlaying) Destroy(vlg); else DestroyImmediate(vlg); }
                }

                var csf = contentRoot.GetComponent<ContentSizeFitter>();
                if (csf != null)
                {
                    // IMPORTANT: same reasoning as above.
                    try { DestroyImmediate(csf); } catch { if (Application.isPlaying) Destroy(csf); else DestroyImmediate(csf); }
                }

                // If something still blocks, bail out (and warn once) rather than spamming the console.
                if (contentRoot.GetComponent<VerticalLayoutGroup>() != null || contentRoot.GetComponent<ContentSizeFitter>() != null)
                {
                    if (!_warnedContentLayoutConflict)
                    {
                        _warnedContentLayoutConflict = true;
                        Debug.LogWarning("[PlayerInventoryUI] ContentRoot still has list-layout components; cannot ensure GridLayoutGroup this frame.", this);
                    }
                    return;
                }

                var grid = contentRoot.GetComponent<GridLayoutGroup>();
                if (grid == null) grid = contentRoot.gameObject.AddComponent<GridLayoutGroup>();

                // Border around the grid pane (not per-slot lines).
                EnsureGridPaneBorder(scrollRect.viewport);

                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = InventoryGridColumns;
                grid.startAxis = GridLayoutGroup.Axis.Horizontal;
                grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                grid.childAlignment = TextAnchor.UpperLeft;
                grid.spacing = new Vector2(4f, 4f);
                grid.padding = new RectOffset(8, 8, 8, 8);

                // Compute a TRUE square cell size that fits 4x7 in the viewport.
                float cell = 64f;
                try
                {
                    var vp = scrollRect.viewport;
                    if (vp != null)
                    {
                        float padX = grid.padding.left + grid.padding.right;
                        float padY = grid.padding.top + grid.padding.bottom;
                        float availW = Mathf.Max(0f, vp.rect.width - padX - grid.spacing.x * (InventoryGridColumns - 1));
                        float availH = Mathf.Max(0f, vp.rect.height - padY - grid.spacing.y * (InventoryGridRows - 1));

                        float cw = Mathf.Floor(availW / InventoryGridColumns);
                        float ch = Mathf.Floor(availH / InventoryGridRows);
                        cell = Mathf.Clamp(Mathf.Floor(Mathf.Min(cw, ch)), 48f, 220f);
                    }
                }
                catch { }

                // Pixel-perfect (prefer even) to keep 1px/2px outlines consistent.
                int size = Mathf.FloorToInt(cell);
                size = Mathf.Clamp(size, 48, 220);
                if ((size % 2) == 1) size -= 1;
                if (size < 48) size = 48;
                cell = size;

                grid.cellSize = new Vector2(cell, cell);

                // No scrolling for a fixed 4x7 inventory.
                scrollRect.horizontal = false;
                scrollRect.vertical = false;
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

        private static void EnsureGridPaneBorder(RectTransform viewport)
        {
            if (viewport == null)
                return;

            try
            {
                // Create (or reuse) a simple border overlay in the viewport.
                Transform borderTf = null;
                try { borderTf = viewport.Find("GridPaneBorder"); } catch { }

                GameObject borderGo;
                if (borderTf == null)
                {
                    borderGo = new GameObject("GridPaneBorder", typeof(RectTransform));
                    borderGo.transform.SetParent(viewport, false);
                }
                else
                {
                    borderGo = borderTf.gameObject;
                }

                var brt = borderGo.GetComponent<RectTransform>();
                brt.anchorMin = Vector2.zero;
                brt.anchorMax = Vector2.one;
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;
                brt.pivot = new Vector2(0.5f, 0.5f);

                // Keep border above content but not blocking interaction.
                borderGo.transform.SetAsLastSibling();

                const float thickness = 1f;
                var lineColor = new Color(1f, 1f, 1f, 0.65f);

                EnsureBorderLine(borderGo.transform, "TopLine", lineColor,
                    anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
                    pivot: new Vector2(0.5f, 1f), sizeDelta: new Vector2(0f, thickness), anchoredPos: Vector2.zero);

                EnsureBorderLine(borderGo.transform, "BottomLine", lineColor,
                    anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0f),
                    pivot: new Vector2(0.5f, 0f), sizeDelta: new Vector2(0f, thickness), anchoredPos: Vector2.zero);

                EnsureBorderLine(borderGo.transform, "LeftLine", lineColor,
                    anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0f, 1f),
                    pivot: new Vector2(0f, 0.5f), sizeDelta: new Vector2(thickness, 0f), anchoredPos: Vector2.zero);

                EnsureBorderLine(borderGo.transform, "RightLine", lineColor,
                    anchorMin: new Vector2(1f, 0f), anchorMax: new Vector2(1f, 1f),
                    pivot: new Vector2(1f, 0.5f), sizeDelta: new Vector2(thickness, 0f), anchoredPos: Vector2.zero);
            }
            catch
            {
                // Best-effort only.
            }
        }

        private static void EnsureBorderLine(
            Transform parent,
            string name,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 sizeDelta,
            Vector2 anchoredPos)
        {
            if (parent == null || string.IsNullOrWhiteSpace(name))
                return;

            Transform tf = null;
            try { tf = parent.Find(name); } catch { }

            GameObject go;
            if (tf == null)
            {
                go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
            }
            else
            {
                go = tf.gameObject;
                if (go.GetComponent<Image>() == null)
                    go.AddComponent<Image>();
            }

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPos;

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
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

            // Grid slots are icon+count only; don't modify TMP sizing/colors here.
            try
            {
                var rowUi = rowGo.GetComponent<PlayerInventoryRowUI>();
                if (rowUi != null && rowUi.IsGridMode)
                    return;
            }
            catch { }

            try
            {
                var nameTmp = FindTmpByNameHint(rowGo, "Name", "ItemName", "Title", "Label");
                if (nameTmp != null)
                {
                    // Keep names readable but don't blow up grid tiles.
                    if (nameTmp.fontSize < 16f)
                        nameTmp.fontSize = 16f;

                    var c = nameTmp.color;
                    nameTmp.color = new Color(c.r, c.g, c.b, 1f);

                    var m = nameTmp.margin;
                    if (m.x < 12f) m.x = 12f;
                    nameTmp.margin = m;
                }

                var countTmp = FindTmpByNameHint(rowGo, "Count", "Qty", "Quantity", "Stack");
                if (countTmp != null)
                {
                    if (countTmp.fontSize < 14f)
                        countTmp.fontSize = 14f;
                    var c2 = countTmp.color;
                    countTmp.color = new Color(c2.r, c2.g, c2.b, 0.95f);
                }
            }
            catch { }
        }

        private void ApplyRowVisualStyling(GameObject rowGo, int rowIndex, bool isSelected)
        {
            if (rowGo == null) return;

            bool isGrid = false;
            try
            {
                var rowUi = rowGo.GetComponent<PlayerInventoryRowUI>();
                isGrid = rowUi != null && rowUi.IsGridMode;
            }
            catch { }

            // Normal shading
            const float evenAlpha = 0.18f;
            const float oddAlpha = 0.26f;
            const float selectedAlpha = 0.45f;

            float normalAlpha = (rowIndex % 2 == 0) ? evenAlpha : oddAlpha;
            float a = isSelected ? selectedAlpha : normalAlpha;

            var bg = EnsureRowBackgroundImage(rowGo);
            var baseColor = GetRowBaseColor();

            // Raycast fix: tiles must always have a raycastable graphic.
            try
            {
                if (bg != null)
                    bg.raycastTarget = true;
            }
            catch { }

            // For grid tiles, background/hover is handled by PlayerInventoryRowUI.
            // Driving bg.color here can cause one-frame flashes on open.
            if (!isGrid && bg != null)
            {
                try
                {
                    bg.color = WithAlpha(new Color(baseColor.r, baseColor.g, baseColor.b, 1f), a);
                }
                catch { }
            }

            // Optional accent bar for selected
            if (!isGrid)
                EnsureSelectedBar(rowGo, isSelected, baseColor);
            else
                EnsureSelectedBar(rowGo, false, baseColor);

            // Grid slot border (subtle) + stronger when selected.
            if (isGrid)
            {
                // Border/hover/selection visuals are handled by PlayerInventoryRowUI.
            }

            // Hover styling only if there is a Button
            try
            {
                var btn = rowGo.GetComponent<Button>();
                if (btn != null)
                {
                    if (bg != null && btn.targetGraphic == null)
                        btn.targetGraphic = bg;

                    if (isGrid)
                    {
                        // Prevent Unity's Selectable tinting from flashing tiles.
                        btn.transition = Selectable.Transition.None;
                    }
                    else
                    {
                        ConfigureButtonColors(btn, bg != null ? bg.color : WithAlpha(baseColor, a));
                    }
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

                try
                {
                    var rowUi = go != null ? go.GetComponent<PlayerInventoryRowUI>() : null;
                    if (rowUi != null)
                    {
                        // Grid selection is slot-index based (stable), not name-based.
                        if (rowUi.IsGridMode)
                            rowUi.SetSelected(rowUi.SlotIndex == _selectedSlotIndex);
                        else
                            rowUi.SetSelected(isSelected);
                    }
                }
                catch { }

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

        private static void MakeViewportTransparent(RectTransform viewport)
        {
            if (viewport == null)
                return;

            try
            {
                var img = viewport.GetComponent<Image>();
                if (img == null)
                    return;

                var c = img.color;
                if (c.a > 0.001f)
                    img.color = new Color(c.r, c.g, c.b, 0f);
            }
            catch { }
        }

        private Dictionary<string, ItemDefinition> BuildItemDefinitionIndex()
        {
            var map = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);

            static bool HasIcon(ItemDefinition d)
            {
                try { return d != null && d.icon != null; } catch { return false; }
            }

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
                            if (string.IsNullOrWhiteSpace(id))
                                continue;

                            if (!map.TryGetValue(id, out var existing) || existing == null)
                            {
                                map[id] = def;
                            }
                            else
                            {
                                // Prefer the definition that actually has an icon assigned.
                                if (!HasIcon(existing) && HasIcon(def))
                                    map[id] = def;
                            }
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
                        if (string.IsNullOrWhiteSpace(id))
                            continue;

                        if (!map.TryGetValue(id, out var existing) || existing == null)
                        {
                            map[id] = def;
                        }
                        else
                        {
                            if (!HasIcon(existing) && HasIcon(def))
                                map[id] = def;
                        }
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
            {
                // If we found a definition but it doesn't have an icon, it may be a stale/duplicate instance.
                // Rebuild the index once and retry.
                try
                {
                    if (def != null && def.icon == null)
                    {
                        _itemDefById = BuildItemDefinitionIndex();
                        if (_itemDefById != null && _itemDefById.TryGetValue(itemId, out var refreshed))
                            return refreshed;
                    }
                }
                catch { }

                return def;
            }

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

            if (_tabAll != null && _tabWeapons != null && _tabMaterials != null && _tabConsumables != null && _tabSkilling != null)
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
                _tabAll = FindButtonUnder(tabsRoot, "Tab_All") ?? _tabAll;
                _tabWeapons = FindButtonUnder(tabsRoot, "Tab_WeaponsGear") ?? _tabWeapons;
                _tabMaterials = FindButtonUnder(tabsRoot, "Tab_Materials") ?? _tabMaterials;
                _tabConsumables = FindButtonUnder(tabsRoot, "Tab_Consumables") ?? _tabConsumables;
                _tabSkilling = FindButtonUnder(tabsRoot, "Tab_Skilling") ?? _tabSkilling;
            }

            _tabAll ??= CreateTabButton(tabsRoot, "Tab_All", "All", out _tabAllText);
            _tabWeapons ??= CreateTabButton(tabsRoot, "Tab_WeaponsGear", "Weapons/Gear", out _tabWeaponsText);
            _tabMaterials ??= CreateTabButton(tabsRoot, "Tab_Materials", "Materials", out _tabMaterialsText);
            _tabConsumables ??= CreateTabButton(tabsRoot, "Tab_Consumables", "Consumables", out _tabConsumablesText);
            _tabSkilling ??= CreateTabButton(tabsRoot, "Tab_Skilling", "Skilling", out _tabSkillingText);

            // Keep the All tab first when we create it dynamically.
            try { if (_tabAll != null) _tabAll.transform.SetSiblingIndex(0); } catch { }

            WireTab(_tabAll, InventoryTab.All);
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
            ApplyTabVisual(_tabAll, _tabAllText, _activeTab == InventoryTab.All);
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
            // All tab: no filtering (dev testing default).
            if (_activeTab == InventoryTab.All)
                return true;

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
