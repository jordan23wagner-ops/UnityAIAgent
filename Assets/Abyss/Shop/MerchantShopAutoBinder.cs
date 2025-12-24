using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Town;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Abyss.Shop
{
    /// <summary>
    /// Keeps scanning briefly to attach MerchantShop to any spawned TownKey merchants.
    /// Works with manual town setup (late spawns).
    /// </summary>
    public sealed class MerchantShopAutoBinder : MonoBehaviour
    {
        private const float ScanInterval = 0.5f;
        private const float ScanForSeconds = 12f;

        private float _timeLeft;
        private float _timer;

        private static readonly HashSet<int> WarnedMissingInventory = new();

        private static MerchantShopAutoBinder _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            // Ensure one instance only
            var existing = FindAnyObjectByType<MerchantShopAutoBinder>();
            if (existing != null)
            {
                _instance = existing;
                _instance.BeginScanning();
                return;
            }

            var go = new GameObject("MerchantShopAutoBinder");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            _instance = go.AddComponent<MerchantShopAutoBinder>();
            _instance.BeginScanning();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            BeginScanning();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            BeginScanning();
        }

        private void BeginScanning()
        {
            _timeLeft = ScanForSeconds;
            _timer = 0f;
        }

        private void Update()
        {
            if (_timeLeft <= 0f) return;

            _timeLeft -= Time.unscaledDeltaTime;
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = ScanInterval;

            TryBindAll();
        }

        private static void TryBindAll()
        {
            try
            {
                int merchantsFound = 0;
                int shopsAdded = 0;
                int collidersAdded = 0;

#if UNITY_2022_2_OR_NEWER
                var tags = FindObjectsByType<TownKeyTag>(FindObjectsSortMode.None);
#else
                var tags = FindObjectsOfType<TownKeyTag>();
#endif
                foreach (var tag in tags)
                {
                    if (tag == null) continue;

                    var key = tag.Key;
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    if (!key.StartsWith("merchant_", StringComparison.OrdinalIgnoreCase)) continue;

                    merchantsFound++;
                    var go = tag.gameObject;
                    if (go == null) continue;

                    var shop = go.GetComponent<MerchantShop>();
                    if (shop == null)
                    {
                        shop = go.AddComponent<MerchantShop>();
                        shopsAdded++;
                    }

                    if (go.GetComponent<Collider>() == null)
                    {
                        var box = go.AddComponent<BoxCollider>();
                        box.isTrigger = false;
                        box.size = Vector3.one;
                        box.center = Vector3.zero;
                        collidersAdded++;
                    }

                    // Runtime fallback: do not assign inventories outside editor.
                    if (!Application.isEditor && shop != null && shop.shopInventory == null)
                    {
                        int id = go.GetInstanceID();
                        if (!WarnedMissingInventory.Contains(id))
                        {
                            WarnedMissingInventory.Add(id);
                            Debug.LogWarning($"[MerchantShopAutoBinder] Merchant '{go.name}' key='{key}' has no ShopInventory assigned. Using fallback stock.");
                        }
                    }
                }

                if (merchantsFound > 0 && (shopsAdded > 0 || collidersAdded > 0))
                    Debug.Log($"[MerchantShopAutoBinder] MerchantsFound={merchantsFound} ShopsAdded={shopsAdded} CollidersAdded={collidersAdded}");
            }
            catch
            {
                // Swallow errors from destroyed objects during spawn churn
            }
        }

#if UNITY_EDITOR
        [MenuItem("Tools/Abyss/Fix Town Merchants (Add MerchantShop + Assign Inventories)")]
        private static void FixTownMerchantsEditor()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Run this in Edit Mode (not Play Mode).");
                return;
            }

            int merchantsFound = 0;
            int shopsAdded = 0;
            int collidersAdded = 0;
            int inventoriesAssigned = 0;

            var invWeapons = LoadInventoryByName("ShopInventory_Weapons");
            var invConsumables = LoadInventoryByName("ShopInventory_Consumables");
            var invSkilling = LoadInventoryByName("ShopInventory_Skilling");
            var invWorkshop = LoadInventoryByName("ShopInventory_Workshop");

            var tags = UnityEngine.Object.FindObjectsByType<TownKeyTag>(FindObjectsSortMode.None);
            foreach (var tag in tags)
            {
                if (tag == null) continue;
                var key = tag.Key;
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (!key.StartsWith("merchant_", StringComparison.OrdinalIgnoreCase)) continue;

                merchantsFound++;
                var go = tag.gameObject;
                if (go == null) continue;

                bool addedShop = false;
                bool addedCollider = false;

                var shop = go.GetComponent<MerchantShop>();
                if (shop == null)
                {
                    shop = Undo.AddComponent<MerchantShop>(go);
                    addedShop = true;
                    shopsAdded++;
                }

                if (go.GetComponent<Collider>() == null)
                {
                    var box = Undo.AddComponent<BoxCollider>(go);
                    box.isTrigger = false;
                    box.size = Vector3.one;
                    box.center = Vector3.zero;
                    addedCollider = true;
                    collidersAdded++;
                }

                var chosen = ChooseInventoryForKey(key, invWeapons, invConsumables, invSkilling, invWorkshop);
                if (shop != null)
                {
                    if (chosen != null)
                    {
                        if (shop.shopInventory != chosen)
                        {
                            Undo.RecordObject(shop, "Assign ShopInventory");
                            shop.shopInventory = chosen;
                            if (shop.stock != null && shop.stock.Count > 0)
                                shop.stock.Clear();
                            inventoriesAssigned++;
                        }
                    }
                    else if (shop.shopInventory == null)
                    {
                        Debug.LogWarning($"[FixTownMerchants] No ShopInventory asset resolved for '{go.name}' key='{key}'. MerchantShop will use fallback stock.");
                    }

                    EditorUtility.SetDirty(shop);
                }

                if (addedShop || addedCollider || (shop != null && chosen != null))
                {
                    Debug.Log($"[FixTownMerchants] {go.name} key='{key}' shopAdded={addedShop} colliderAdded={addedCollider} inventory={(chosen != null ? chosen.name : (shop != null && shop.shopInventory != null ? shop.shopInventory.name : "<null>"))}");
                }
            }

            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            }
            AssetDatabase.SaveAssets();

            Debug.Log($"[FixTownMerchants] MerchantsFound={merchantsFound} ShopsAdded={shopsAdded} CollidersAdded={collidersAdded} InventoriesAssigned={inventoriesAssigned}");
        }

        private static ShopInventory LoadInventoryByName(string assetName)
        {
            if (string.IsNullOrWhiteSpace(assetName)) return null;

            var guids = AssetDatabase.FindAssets($"{assetName} t:ShopInventory");
            if (guids == null || guids.Length == 0)
                return null;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var inv = AssetDatabase.LoadAssetAtPath<ShopInventory>(path);
                if (inv != null && string.Equals(inv.name, assetName, StringComparison.OrdinalIgnoreCase))
                    return inv;
            }

            // Fallback: first match.
            return AssetDatabase.LoadAssetAtPath<ShopInventory>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
#endif

        private static ShopInventory ChooseInventoryForKey(string key, ShopInventory weapons, ShopInventory consumables, ShopInventory skilling, ShopInventory workshop)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            var k = key.ToLowerInvariant();
            if (k.Contains("weapons")) return weapons;
            if (k.Contains("consumables")) return consumables;
            if (k.Contains("skilling")) return skilling;
            if (k.Contains("workshop")) return workshop;
            return null;
        }
    }
}
