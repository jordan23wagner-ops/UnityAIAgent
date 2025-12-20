using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Game.Town;
using Abyss.Items;
using Game.Systems;

using AbyssItemRarity = Abyss.Items.ItemRarity;

namespace Abyss.Shop
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public class MerchantShopUI : MonoBehaviour
    {
        private enum ShopMode
        {
            Buy = 0,
            Sell = 1,
        }

        [Header("Root")]
        [SerializeField] private GameObject root;
        [SerializeField] private Button exitButton;

        [Header("Mode Tabs")]
        [SerializeField] private Button buyTabButton;
        [SerializeField] private Button sellTabButton;

        [Header("Left List")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private MerchantShopRowUI rowPrefab;

        [Header("Right Details")]
        [SerializeField] private TMP_Text detailNameText;
        [SerializeField] private TMP_Text detailPriceText;
        [SerializeField] private TMP_Text detailDescText;
        [SerializeField] private Image detailIconImage;
        [SerializeField] private TMP_Text detailRarityText;

        [Header("Buy")]
        [SerializeField] private Button buyButton;
        [SerializeField] private Button sellButton;
        [SerializeField] private Button qtyMinusButton;
        [SerializeField] private Button qtyPlusButton;
        [SerializeField] private TMP_Text qtyText;
        [SerializeField] private TMP_Text messageText;

        [Header("Top")]
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text titleText;

        private MerchantShop _currentShop;
        private bool _isOpen;
        private Game.Input.PlayerInputAuthority _inputAuthority;

        private PlayerInventory _inventory;
        private PlayerGoldWallet _wallet;
        private MerchantShopRowUI _selectedRow;
        private string _selectedItemId;
        private string _selectedDisplayName;
        private string _selectedDescription;
        private int _selectedPrice;
        private int _qty = 1;

        private Sprite _selectedIcon;
        private AbyssItemRarity _selectedRarity = AbyssItemRarity.Common;
        private bool _warnedMissingRowIconOnce;
        private bool _warnedMissingDetailVisualsOnce;

        private ShopMode _mode = ShopMode.Buy;
        private int _selectedOwnedCount;
        private Abyss.Items.ItemDefinition _selectedItemDef;
        private Dictionary<string, Abyss.Items.ItemDefinition> _itemDefById;

        public static bool IsOpen { get; private set; }
        public static event Action<bool> OnOpenChanged;

        private void Awake()
        {
#if UNITY_2022_2_OR_NEWER
            _inputAuthority = FindFirstObjectByType<Game.Input.PlayerInputAuthority>();
#else
            _inputAuthority = FindObjectOfType<Game.Input.PlayerInputAuthority>();
#endif
            if (_inputAuthority == null)
                Debug.LogWarning("[MerchantShopUI] PlayerInputAuthority not found; input won't be locked automatically.");

            if (root != null)
                root.SetActive(false);

            if (exitButton != null)
            {
                exitButton.onClick.RemoveAllListeners();
                exitButton.onClick.AddListener(Close);
            }

            if (buyTabButton != null)
            {
                buyTabButton.onClick.RemoveAllListeners();
                buyTabButton.onClick.AddListener(() => SetMode(ShopMode.Buy));
            }

            if (sellTabButton != null)
            {
                sellTabButton.onClick.RemoveAllListeners();
                sellTabButton.onClick.AddListener(() => SetMode(ShopMode.Sell));
            }

            if (buyButton != null)
            {
                buyButton.onClick.RemoveAllListeners();
                buyButton.onClick.AddListener(TryBuy);
            }

            if (sellButton != null)
            {
                sellButton.onClick.RemoveAllListeners();
                sellButton.onClick.AddListener(TrySell);
                // Default to BUY mode visibility on boot.
                sellButton.gameObject.SetActive(false);
            }

            if (qtyMinusButton != null)
            {
                qtyMinusButton.onClick.RemoveAllListeners();
                qtyMinusButton.onClick.AddListener(() => AdjustQty(-1));
            }

            if (qtyPlusButton != null)
            {
                qtyPlusButton.onClick.RemoveAllListeners();
                qtyPlusButton.onClick.AddListener(() => AdjustQty(+1));
            }

            _wallet = PlayerGoldWallet.Instance;

            _inventory = PlayerInventoryResolver.GetOrFind();

            SetQty(1);
            SetMessage(string.Empty);

            RefreshAffordabilityUI();
        }

        public void Open(MerchantShop shop, string displayName, int playerGold)
        {
            if (shop == null || root == null || contentRoot == null || rowPrefab == null)
            {
                Debug.LogWarning("[MerchantShopUI] Cannot open: missing references or shop.");
                return;
            }

            _warnedMissingRowIconOnce = false;
            _warnedMissingDetailVisualsOnce = false;

            _currentShop = shop;
            _isOpen = true;
            IsOpen = true;
            OnOpenChanged?.Invoke(true);
            root.SetActive(true);

            _wallet = PlayerGoldWallet.Instance;
            if (_wallet != null)
            {
                _wallet.GoldChanged -= OnGoldChanged;
                _wallet.GoldChanged += OnGoldChanged;
            }

            try { _inputAuthority?.SetUiInputLocked(true); } catch { }

            if (titleText != null) titleText.text = string.IsNullOrWhiteSpace(displayName) ? shop.MerchantName : displayName;
            RefreshGold();
            SetMessage(string.Empty);

            _itemDefById = BuildItemDefinitionIndex();
            SetMode(ShopMode.Buy, force: true);

            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                var c = contentRoot.GetChild(i);
                Destroy(c.gameObject);
            }

            // Populate list for current mode.
            RefreshListAndSelection();

            try
            {
                if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
            }
            catch { }

            int buyCount = 0;
            try { buyCount = shop != null ? (shop.GetResolvedStock()?.Count ?? 0) : 0; } catch { }
            Debug.Log($"[MerchantShopUI] Opened shop={shop.gameObject.name} buyItems={buyCount}");
        }

        private void AdjustQty(int delta)
        {
            if (_mode == ShopMode.Buy)
            {
                SetQty(_qty + delta);
            }
            else
            {
                SetSellQty(_qty + delta);
                RefreshSellUI();
            }
        }

        private void SetMode(ShopMode mode, bool force = false)
        {
            if (!force && _mode == mode) return;
            _mode = mode;

            // Button visibility.
            if (buyButton != null) buyButton.gameObject.SetActive(_mode == ShopMode.Buy);
            if (sellButton != null) sellButton.gameObject.SetActive(_mode == ShopMode.Sell);

            // Qty defaults.
            if (_mode == ShopMode.Buy)
                SetQty(1);
            else
                SetSellQty(0);

            RefreshListAndSelection();
            RefreshModeSpecificUI();
        }

        private void RefreshListAndSelection()
        {
            if (contentRoot == null || rowPrefab == null) return;

            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                var c = contentRoot.GetChild(i);
                Destroy(c.gameObject);
            }

            MerchantShopRowUI firstRow = null;
            MerchantShop.ResolvedStock firstResolved = default;
            bool hasFirstResolved = false;

            if (_mode == ShopMode.Buy)
            {
                var items = _currentShop != null ? _currentShop.GetResolvedStock() : null;
                if (items != null)
                {
                    foreach (var it in items)
                    {
                        if (string.IsNullOrWhiteSpace(it.itemId) || it.price <= 0)
                            continue;

                        var captured = it;
                        var go = Instantiate(rowPrefab.gameObject, contentRoot, false);
                        var row = go.GetComponent<MerchantShopRowUI>();
                        if (row != null)
                        {
                            row.Bind(captured.displayName, captured.price, captured.itemId, captured.icon, captured.rarity, () => SelectResolvedRow(row, captured));

                            if (!_warnedMissingRowIconOnce && captured.icon != null && !row.CanShowIcon)
                            {
                                _warnedMissingRowIconOnce = true;
                                Debug.LogWarning("[MerchantShopUI] Row prefab has no Icon Image reference; item icons will be hidden. Rebuild UI via Tools->Build Merchant Shop UI (Editor) or wire iconImage on MerchantShopRowUI.");
                            }

                            if (firstRow == null)
                            {
                                firstRow = row;
                                firstResolved = captured;
                                hasFirstResolved = true;
                            }
                        }
                    }
                }
            }
            else
            {
                EnsureInventory();
                var owned = GetAllOwned();
                foreach (var (def, count) in owned)
                {
                    if (def == null || count <= 0) continue;
                    string itemId = ResolveItemId(def);
                    int unit = _currentShop != null ? _currentShop.GetSellUnitPrice(def) : Mathf.Max(1, Mathf.RoundToInt(def.baseValue * 0.5f));
                    string display = string.IsNullOrWhiteSpace(def.displayName) ? itemId : def.displayName;
                    if (count > 1) display = $"{display} x{count}";

                    var capturedDef = def;
                    var capturedCount = count;
                    var go = Instantiate(rowPrefab.gameObject, contentRoot, false);
                    var row = go.GetComponent<MerchantShopRowUI>();
                    if (row != null)
                    {
                        row.Bind(display, unit, itemId, def.icon, def.rarity, () => SelectOwnedRow(row, capturedDef, capturedCount));
                        if (firstRow == null)
                        {
                            firstRow = row;
                            // we'll select via SelectOwnedRow below
                        }
                    }
                }
            }

            if (firstRow != null)
            {
                if (_mode == ShopMode.Buy)
                {
                    if (hasFirstResolved)
                        SelectResolvedRow(firstRow, firstResolved);
                }
                else
                {
                    // Trigger selection by simulating a click.
                    firstRow.Button?.onClick?.Invoke();
                }

                firstRow.ButtonSelect();
            }
            else
            {
                ClearSelection();
            }

            RefreshModeSpecificUI();
        }

        private void RefreshModeSpecificUI()
        {
            if (_mode == ShopMode.Buy)
            {
                RefreshAffordabilityUI();
            }
            else
            {
                RefreshSellUI();
            }
        }

        private void EnsureInventory()
        {
            if (_inventory != null) return;
            _inventory = PlayerInventoryResolver.GetOrFind();
        }

        private void SelectOwnedRow(MerchantShopRowUI row, ItemDefinition def, int ownedCount)
        {
            if (_selectedRow != null) _selectedRow.SetSelected(false);

            _selectedRow = row;
            _selectedRow?.SetSelected(true);

            _selectedItemDef = def;
            _selectedOwnedCount = Mathf.Max(0, ownedCount);

            _selectedItemId = def != null ? ResolveItemId(def) : null;
            _selectedDisplayName = def != null ? (string.IsNullOrWhiteSpace(def.displayName) ? _selectedItemId : def.displayName) : _selectedItemId;
            _selectedDescription = def != null ? (string.IsNullOrWhiteSpace(def.description) ? "No description." : def.description) : "No description.";

            _selectedIcon = def != null ? def.icon : null;
            _selectedRarity = def != null ? ItemRarityVisuals.Normalize(def.rarity) : AbyssItemRarity.Common;

            _selectedPrice = (_currentShop != null && def != null) ? _currentShop.GetSellUnitPrice(def) : 1;

            if (detailNameText != null) detailNameText.text = _selectedDisplayName ?? string.Empty;
            if (detailPriceText != null) detailPriceText.text = _selectedPrice.ToString();
            if (detailDescText != null) detailDescText.text = _selectedDescription ?? string.Empty;
            ApplyDetailsVisuals(_selectedIcon, _selectedRarity);

            SetMessage(string.Empty);

            // Default to selling 1 if possible.
            SetSellQty(_selectedOwnedCount > 0 ? 1 : 0);
            RefreshSellUI();
        }

        private void OnRowClicked(string name, int price)
        {
            if (detailNameText != null) detailNameText.text = name;
            if (detailPriceText != null) detailPriceText.text = price.ToString();
            if (detailDescText != null) detailDescText.text = "No description.";
        }

        private void SelectRow(MerchantShopRowUI row, string itemName, int price)
        {
            if (_selectedRow != null) _selectedRow.SetSelected(false);

            _selectedRow = row;
            _selectedRow?.SetSelected(true);

            _selectedItemId = itemName;
            _selectedDisplayName = itemName;
            _selectedDescription = "No description.";
            _selectedPrice = price;

            if (detailNameText != null) detailNameText.text = itemName;
            if (detailPriceText != null) detailPriceText.text = price.ToString();
            if (detailDescText != null) detailDescText.text = "No description.";

            SetMessage(string.Empty);

            RefreshAffordabilityUI();
        }

        private void SelectResolvedRow(MerchantShopRowUI row, MerchantShop.ResolvedStock resolved)
        {
            if (_selectedRow != null) _selectedRow.SetSelected(false);

            _selectedRow = row;
            _selectedRow?.SetSelected(true);

            _selectedItemId = resolved.itemId;
            _selectedDisplayName = string.IsNullOrWhiteSpace(resolved.displayName) ? resolved.itemId : resolved.displayName;
            _selectedDescription = string.IsNullOrWhiteSpace(resolved.description) ? "No description." : resolved.description;
            _selectedPrice = resolved.price;
            _selectedIcon = resolved.icon;
            _selectedRarity = ItemRarityVisuals.Normalize(resolved.rarity);

            if (detailNameText != null) detailNameText.text = _selectedDisplayName;
            if (detailPriceText != null) detailPriceText.text = resolved.price.ToString();
            if (detailDescText != null) detailDescText.text = _selectedDescription;

            ApplyDetailsVisuals(_selectedIcon, _selectedRarity);

            SetMessage(string.Empty);
            RefreshAffordabilityUI();
        }

        private void ClearSelection()
        {
            if (_selectedRow != null) _selectedRow.SetSelected(false);
            _selectedRow = null;
            _selectedItemId = null;
            _selectedDisplayName = null;
            _selectedDescription = null;
            _selectedPrice = 0;
            _selectedIcon = null;
            _selectedRarity = AbyssItemRarity.Common;
            _selectedOwnedCount = 0;
            _selectedItemDef = null;

            if (detailNameText != null) detailNameText.text = string.Empty;
            if (detailPriceText != null) detailPriceText.text = string.Empty;
            if (detailDescText != null) detailDescText.text = string.Empty;

            ApplyDetailsVisuals(null, AbyssItemRarity.Common, clearText: true);

            RefreshAffordabilityUI();
        }

        private void ApplyDetailsVisuals(Sprite icon, AbyssItemRarity rarity, bool clearText = false)
        {
            rarity = ItemRarityVisuals.Normalize(rarity);

            if (detailIconImage != null)
            {
                bool hasIcon = icon != null;
                detailIconImage.sprite = icon;
                detailIconImage.enabled = hasIcon;
                if (detailIconImage.gameObject.activeSelf != hasIcon)
                    detailIconImage.gameObject.SetActive(hasIcon);
            }
            else if (!_warnedMissingDetailVisualsOnce && icon != null)
            {
                _warnedMissingDetailVisualsOnce = true;
                Debug.LogWarning("[MerchantShopUI] Details panel has no Icon Image reference; item icon will be hidden. Rebuild UI via Tools->Build Merchant Shop UI (Editor) or wire detailIconImage.");
            }

            if (detailRarityText != null)
            {
                detailRarityText.text = clearText ? string.Empty : $"Rarity: {ItemRarityVisuals.ToDisplayString(rarity)}";
            }
            else if (!_warnedMissingDetailVisualsOnce)
            {
                _warnedMissingDetailVisualsOnce = true;
                Debug.LogWarning("[MerchantShopUI] Details panel has no Rarity Text reference; rarity label will be hidden. Rebuild UI via Tools->Build Merchant Shop UI (Editor) or wire detailRarityText.");
            }
        }

        private void SetQty(int newQty)
        {
            _qty = Mathf.Clamp(newQty, 1, 99);
            if (qtyText != null) qtyText.text = _qty.ToString();

            if (_mode == ShopMode.Buy)
                RefreshAffordabilityUI();
        }

        private void SetSellQty(int newQty)
        {
            _qty = Mathf.Clamp(newQty, 0, Mathf.Max(0, _selectedOwnedCount));
            if (qtyText != null) qtyText.text = _qty.ToString();
        }

        private void TryBuy()
        {
            if (_mode != ShopMode.Buy) return;
            if (string.IsNullOrWhiteSpace(_selectedItemId) || _selectedPrice <= 0)
            {
                SetMessage("Select an item first.");
                return;
            }

            // If UI says we can't afford, don't proceed.
            if (buyButton != null && !buyButton.interactable)
            {
                SetMessage("Not enough gold");
                return;
            }

            _wallet ??= PlayerGoldWallet.Instance;
            if (_wallet == null)
            {
                // Shouldn't happen after PlayerGoldWallet Boot(), but keep a guard.
                SetMessage("No wallet found.");
                return;
            }

            if (_inventory == null)
                _inventory = PlayerInventoryResolver.GetOrFind();
            if (_inventory == null)
            {
                SetMessage("No inventory found.");
                return;
            }

            int before = GetWalletGold();
            int totalCost = _selectedPrice * _qty;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ShopBuy] WalletID={(PlayerGoldWallet.Instance != null ? PlayerGoldWallet.Instance.GetInstanceID() : -1)} GoldBefore={before} Item={_selectedItemId} Qty={_qty} UnitPrice={_selectedPrice} Total={totalCost}");
#endif
            if (!_wallet.TrySpend(totalCost))
            {
                SetMessage("Not enough gold");
                RefreshGold();
                RefreshAffordabilityUI();
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                Debug.Log($"[ShopBuy] Adding to PlayerInventory instanceId={_inventory.GetInstanceID()} go='{_inventory.gameObject.name}' item='{_selectedItemId}' qty={_qty}", _inventory);
            }
            catch { }
#endif
            _inventory.Add(_selectedItemId, _qty);
            string purchasedName = string.IsNullOrWhiteSpace(_selectedDisplayName) ? _selectedItemId : _selectedDisplayName;
            SetMessage($"Purchased x{_qty} {purchasedName}");
            RefreshGold();
            int after = GetWalletGold();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ShopBuy] GoldAfter={after}", this);
#endif
            RefreshAffordabilityUI();
        }

        private void TrySell()
        {
            if (_mode != ShopMode.Sell) return;

            if (_selectedItemDef == null || string.IsNullOrWhiteSpace(_selectedItemId))
            {
                SetMessage("Select an item first.");
                return;
            }

            if (_qty <= 0)
            {
                SetMessage("Set quantity to sell.");
                RefreshSellUI();
                return;
            }

            EnsureInventory();
            if (_inventory == null)
            {
                SetMessage("No inventory found.");
                return;
            }

            int owned = _inventory.Count(_selectedItemId);
            if (owned < _qty)
            {
                SetMessage("Not enough items.");
                _selectedOwnedCount = owned;
                SetSellQty(Mathf.Clamp(_qty, 0, owned));
                RefreshSellUI();
                return;
            }

            _wallet ??= PlayerGoldWallet.Instance;
            if (_wallet == null)
            {
                SetMessage("No wallet found.");
                return;
            }

            int unit = _currentShop != null ? _currentShop.GetSellUnitPrice(_selectedItemDef) : 1;
            unit = Mathf.Max(1, unit);
            int total = unit * _qty;

            if (!_inventory.TryRemove(_selectedItemId, _qty))
            {
                SetMessage("Could not remove items.");
                RefreshSellUI();
                return;
            }

            _wallet.AddGold(total);
            SetMessage($"Sold x{_qty} {_selectedDisplayName} (+{total}g)");

            RefreshGold();
            _selectedOwnedCount = _inventory.Count(_selectedItemId);
            if (_selectedOwnedCount <= 0)
            {
                RefreshListAndSelection();
                return;
            }

            SetSellQty(Mathf.Clamp(_qty, 0, _selectedOwnedCount));
            RefreshSellUI();
            RefreshListAndSelection();
        }

        private void OnGoldChanged(int newGold)
        {
            RefreshGold();
            RefreshModeSpecificUI();
        }

        private void RefreshGold()
        {
            if (goldText != null)
                goldText.text = $"Gold: {GetWalletGold()}";
        }

        private int GetWalletGold()
        {
            _wallet = PlayerGoldWallet.Instance;
            return _wallet != null ? _wallet.Gold : 0;
        }

        private int GetTotalCost() => (_selectedPrice > 0 ? _selectedPrice : 0) * _qty;

        private int GetMaxAffordableQty()
        {
            if (_selectedPrice <= 0)
                return 1;

            int gold = GetWalletGold();
            int max = gold / _selectedPrice;
            return Mathf.Clamp(max, 1, 99);
        }

        private void RefreshAffordabilityUI()
        {
            if (_mode != ShopMode.Buy)
                return;

            // No wallet yet? Keep UI safe.
            _wallet ??= PlayerGoldWallet.Instance;

            int gold = GetWalletGold();
            int maxAffordable = GetMaxAffordableQty();

            // Clamp qty to what is affordable (but always keep >= 1).
            if (_qty > maxAffordable)
            {
                _qty = maxAffordable;
                if (qtyText != null) qtyText.text = _qty.ToString();
            }

            bool hasSelection = !string.IsNullOrWhiteSpace(_selectedItemId) && _selectedPrice > 0;
            bool canAfford = hasSelection && gold >= GetTotalCost();

            if (qtyMinusButton != null)
                qtyMinusButton.interactable = _qty > 1;

            if (qtyPlusButton != null)
                qtyPlusButton.interactable = hasSelection && (_qty < maxAffordable);

            if (buyButton != null)
                buyButton.interactable = canAfford;
        }

        private void RefreshSellUI()
        {
            EnsureInventory();

            int owned = 0;
            if (_inventory != null && !string.IsNullOrWhiteSpace(_selectedItemId))
                owned = _inventory.Count(_selectedItemId);

            _selectedOwnedCount = owned;
            if (_qty > owned)
                SetSellQty(owned);

            if (qtyMinusButton != null)
                qtyMinusButton.interactable = _qty > 0;

            if (qtyPlusButton != null)
                qtyPlusButton.interactable = owned > 0 && _qty < owned;

            if (sellButton != null)
                sellButton.interactable = owned > 0 && _qty > 0;
        }

        private IEnumerable<(ItemDefinition item, int count)> GetAllOwned()
        {
            EnsureInventory();
            if (_inventory == null)
                yield break;

            var snap = _inventory.GetAllItemsSnapshot();
            if (snap == null) yield break;

            foreach (var kv in snap)
            {
                if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value <= 0) continue;
                yield return (ResolveItemDefinition(kv.Key), kv.Value);
            }
        }

        private ItemDefinition ResolveItemDefinition(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return null;

            _itemDefById ??= BuildItemDefinitionIndex();
            if (_itemDefById != null && _itemDefById.TryGetValue(itemId, out var def) && def != null)
                return def;

            return null;
        }

        private Dictionary<string, ItemDefinition> BuildItemDefinitionIndex()
        {
            var map = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // Collect from all merchants in the scene (covers typical item set).
#if UNITY_2022_2_OR_NEWER
                var shops = FindObjectsByType<MerchantShop>(FindObjectsSortMode.None);
#else
                var shops = FindObjectsOfType<MerchantShop>();
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

                // Also include any ItemDefinition assets already loaded.
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

        private static string ResolveItemId(ItemDefinition def)
        {
            if (def == null) return null;
            string itemId = string.IsNullOrWhiteSpace(def.itemId) ? def.displayName : def.itemId;
            if (string.IsNullOrWhiteSpace(itemId)) itemId = def.name;
            return itemId;
        }

        private void SetMessage(string msg)
        {
            if (messageText != null)
                messageText.text = msg ?? string.Empty;
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            IsOpen = false;
            OnOpenChanged?.Invoke(false);

            if (_wallet != null)
                _wallet.GoldChanged -= OnGoldChanged;

            try { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); } catch { }

            if (root != null) root.SetActive(false);

            ClearSelection();
            SetMessage(string.Empty);
            SetQty(1);

            try { _inputAuthority?.SetUiInputLocked(false); } catch { }

            Debug.Log("[MerchantShopUI] Closed UI");
        }

        // Backwards-compatible static helpers
        public static void Open(MerchantShop shop)
        {
            #if UNITY_2022_2_OR_NEWER
            var inst = FindFirstObjectByType<MerchantShopUI>();
            #else
            var inst = FindObjectOfType<MerchantShopUI>();
            #endif
            int gold = PlayerGoldWallet.Instance != null ? PlayerGoldWallet.Instance.Gold : 0;
            if (inst != null) inst.Open(shop, shop?.MerchantName ?? "Merchant", gold);
            else Debug.LogWarning("[MerchantShopUI] No MerchantShopUI instance found in scene.");
        }

        public static void CloseStatic()
        {
#if UNITY_2022_2_OR_NEWER
            var inst = FindFirstObjectByType<MerchantShopUI>();
#else
            var inst = FindObjectOfType<MerchantShopUI>();
#endif
            if (inst != null) inst.Close();
        }
    }

    // Minimal TMP proxy so we can create a fallback NoItems label without pulling TextMeshPro specifics here.
    internal class TMP_TextProxy : MonoBehaviour
    {
        private TMP_Text _tmp;
        private void Awake()
        {
            _tmp = gameObject.AddComponent<TextMeshProUGUI>();
            _tmp.fontSize = 24;
            _tmp.alignment = TextAlignmentOptions.Center;
            var rt = GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(0f, 40f);
        }
        public void SetText(string t)
        {
            if (_tmp == null) _tmp = GetComponent<TextMeshProUGUI>();
            if (_tmp != null) _tmp.text = t;
        }
    }
}

