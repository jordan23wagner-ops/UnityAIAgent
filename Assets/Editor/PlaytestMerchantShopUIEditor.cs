using UnityEditor;
using UnityEngine;
using UnityEditor.Callbacks;

// Legacy QA tools are gated behind a compile define.
// Enable via: Project Settings > Player > Scripting Define Symbols: ABYSS_LEGACY_QA_TOOLS

public static class PlaytestMerchantShopUIEditor
{
    #if ABYSS_LEGACY_QA_TOOLS
    // Old menu: Tools/Abyssbound/QA/Playtest Merchant Shop UI (Selected)
    [MenuItem("Tools/Legacy QA/Abyssbound/QA/Playtest Merchant Shop UI (Selected)")]
    public static void PlaytestSelected()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("Playtest Merchant Shop", "Select a GameObject with a MerchantShop component first.", "OK");
            return;
        }

        var shop = go.GetComponent<Abyss.Shop.MerchantShop>();
        if (shop == null)
        {
            EditorUtility.DisplayDialog("Playtest Merchant Shop", "Selected GameObject does not have a MerchantShop component.", "OK");
            return;
        }

        if (EditorApplication.isPlaying)
        {
            TryOpenInPlayMode(shop);
            return;
        }

        // Start play mode and open when entered
        void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.delayCall += () => TryOpenInPlayMode(shop);
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            }
        }

        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.isPlaying = true;
    }
    #endif

    private static void TryOpenInPlayMode(Abyss.Shop.MerchantShop shop)
    {
        try
        {
            Abyss.Shop.MerchantShopUI ui = null;
#if UNITY_2022_2_OR_NEWER
            ui = Object.FindFirstObjectByType<Abyss.Shop.MerchantShopUI>();
#else
            ui = Object.FindObjectOfType<Abyss.Shop.MerchantShopUI>();
#endif
            if (ui == null)
            {
                Debug.LogWarning("No MerchantShopUI instance found in scene. Run Tools/Abyssbound/Content/UI/Build Merchant Shop UI (Editor) first.");
                return;
            }
            ui.Open(shop, shop.MerchantName, 0);
            Debug.Log("Opened MerchantShopUI for playtest.");
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}
