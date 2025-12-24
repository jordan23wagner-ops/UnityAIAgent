#if UNITY_EDITOR
using System.Collections.Generic;
using Abyss.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CleanMerchantShopStockEditor
{
    [MenuItem("Tools/Abyssbound/Dev/Shops/Clean MerchantShop Minimal Stock")]
    public static void CleanMinimalStock()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Run this in Edit Mode (not Play Mode).");
            return;
        }

        int cleaned = 0;
        var touchedScenes = new HashSet<Scene>();

        var shops = Object.FindObjectsByType<MerchantShop>(FindObjectsSortMode.None);
        foreach (var shop in shops)
        {
            if (shop == null) continue;
            if (shop.shopInventory == null) continue;
            if (shop.stock == null || shop.stock.Count == 0) continue;

            Undo.RecordObject(shop, "Clean MerchantShop Minimal Stock");
            shop.stock.Clear();
            EditorUtility.SetDirty(shop);
            cleaned++;

            touchedScenes.Add(shop.gameObject.scene);
        }

        foreach (var scene in touchedScenes)
        {
            if (!scene.IsValid() || !scene.isLoaded) continue;
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        AssetDatabase.SaveAssets();

        Debug.Log($"[CleanMerchantShopMinimalStock] Cleaned {cleaned} MerchantShop fallback lists.");
    }
}
#endif
