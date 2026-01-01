#if UNITY_EDITOR
using System;
using Abyss.Items;
using Abyss.Shop;
using Game.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyssbound.EditorTools.Dev
{
    public static class FishingRodToolsEditor
    {
        private const string FishingRodItemId = "tool_fishing_rod";
        private const int DefaultPrice = 65;

        private const string RodItemAssetPath = "Assets/Abyss/Items/Definitions/Item_FishingRod.asset";
        private const string SkillingInventoryAssetPath = "Assets/Abyss/Shops/Inventories/ShopInventory_Skilling.asset";

        [MenuItem("Tools/Abyssbound/Dev/Items/Grant Fishing Rod (Play Mode)")]
        public static void GrantFishingRodPlayMode()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Dev][FishingRod] Enter Play Mode to grant items to the player inventory.");
                return;
            }

            var inv = PlayerInventoryResolver.GetOrFind();
            if (inv == null)
            {
                Debug.LogError("[Dev][FishingRod] No PlayerInventory found.");
                return;
            }

            inv.Add(FishingRodItemId, 1);
            Debug.Log("[Dev][FishingRod] Granted 1x tool_fishing_rod");
        }

        [MenuItem("Tools/Abyssbound/Dev/Shops/Ensure Skilling Shop Sells Fishing Rod")]
        public static void EnsureSkillingShopSellsRod()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[Dev][FishingRod] Run this in Edit Mode (not Play Mode). It edits assets.");
                return;
            }

            EnsureFolder("Assets/Abyss/Items");
            EnsureFolder("Assets/Abyss/Items/Definitions");
            EnsureFolder("Assets/Abyss/Shops");
            EnsureFolder("Assets/Abyss/Shops/Inventories");

            var rodDef = FindItemDefinitionById(FishingRodItemId);
            if (rodDef == null)
            {
                rodDef = ScriptableObject.CreateInstance<ItemDefinition>();
                rodDef.itemId = FishingRodItemId;
                rodDef.displayName = "Fishing Rod";
                rodDef.description = "Used for fishing.";
                rodDef.itemType = Abyss.Items.ItemType.Skilling;
                rodDef.baseValue = DefaultPrice;
                rodDef.rarity = Abyss.Items.ItemRarity.Common;

                AssetDatabase.CreateAsset(rodDef, RodItemAssetPath);
                EditorUtility.SetDirty(rodDef);
                Debug.Log("[Dev][FishingRod] Created ItemDefinition tool_fishing_rod");
            }

            var inv = AssetDatabase.LoadAssetAtPath<ShopInventory>(SkillingInventoryAssetPath);
            if (inv == null)
            {
                inv = ScriptableObject.CreateInstance<ShopInventory>();
                AssetDatabase.CreateAsset(inv, SkillingInventoryAssetPath);
                EditorUtility.SetDirty(inv);
                Debug.Log("[Dev][FishingRod] Created ShopInventory_Skilling");
            }

            bool added = EnsureInventoryEntry(inv, rodDef, DefaultPrice);

            // Best-effort assign this inventory to any skilling merchant shops in the active scene.
            var scene = SceneManager.GetActiveScene();
            int assigned = 0;
            try
            {
#if UNITY_2022_2_OR_NEWER
                var shops = UnityEngine.Object.FindObjectsByType<MerchantShop>(FindObjectsSortMode.None);
#else
                var shops = UnityEngine.Object.FindObjectsOfType<MerchantShop>();
#endif
                foreach (var shop in shops)
                {
                    if (shop == null) continue;

                    string key = (shop.MerchantName + " " + shop.gameObject.name).ToLowerInvariant();
                    if (!key.Contains("skilling"))
                        continue;

                    shop.shopInventory = inv;
                    if (shop.stock != null && shop.stock.Count > 0)
                        shop.stock.Clear();

                    EditorUtility.SetDirty(shop);
                    assigned++;
                }
            }
            catch { }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[Dev][FishingRod] Skilling shop updated. addedEntry={added} assignedMerchants={assigned}");
        }

        private static bool EnsureInventoryEntry(ShopInventory inv, ItemDefinition item, int price)
        {
            if (inv == null || item == null)
                return false;

            if (inv.entries == null)
                inv.entries = new System.Collections.Generic.List<ShopInventory.Entry>();

            for (int i = 0; i < inv.entries.Count; i++)
            {
                var e = inv.entries[i];
                if (e == null) continue;

                if (ReferenceEquals(e.item, item))
                {
                    if (e.price <= 0)
                        e.price = Mathf.Max(1, price);

                    EditorUtility.SetDirty(inv);
                    return false;
                }

                try
                {
                    if (e.item != null && !string.IsNullOrWhiteSpace(e.item.itemId) &&
                        string.Equals(e.item.itemId, item.itemId, StringComparison.OrdinalIgnoreCase))
                    {
                        if (e.price <= 0)
                            e.price = Mathf.Max(1, price);

                        e.item = item;
                        EditorUtility.SetDirty(inv);
                        return false;
                    }
                }
                catch { }
            }

            inv.entries.Add(new ShopInventory.Entry { item = item, price = Mathf.Max(1, price) });
            EditorUtility.SetDirty(inv);
            return true;
        }

        private static ItemDefinition FindItemDefinitionById(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            try
            {
                var guids = AssetDatabase.FindAssets("t:ItemDefinition");
                foreach (var g in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(g);
                    var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                    if (def == null) continue;

                    if (!string.IsNullOrWhiteSpace(def.itemId) &&
                        string.Equals(def.itemId, itemId, StringComparison.OrdinalIgnoreCase))
                        return def;
                }
            }
            catch { }

            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
#endif
