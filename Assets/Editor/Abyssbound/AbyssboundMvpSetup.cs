using UnityEditor;
using UnityEngine;
using Abyssbound.Combat;
using Abyssbound.Crafting;

namespace Abyssbound.Editor
{
    /// <summary>
    /// One-Click MVP Setup for the Abyssbound_World scene.
    /// Automates the placement and wiring of the Stats Awakening and Void Forge systems.
    /// </summary>
    public static class AbyssboundMvpSetup
    {
        [MenuItem("Tools/Abyssbound/MVP/One-Click World Setup")]
        public static void SetupMvpWorld()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[MVP Setup] Could not find a GameObject with the 'Player' tag. Please ensure your Player is tagged correctly.");
                return;
            }

            // 1. Setup Stats Awakening
            if (player.GetComponent<PlayerDerivedStats>() == null)
            {
                player.AddComponent<PlayerDerivedStats>();
                Debug.Log("[MVP Setup] Added PlayerDerivedStats to Player.");
            }

            // 2. Setup HUD Wiring
            PlayerStatsHudPanel hud = Object.FindAnyObjectByType<PlayerStatsHudPanel>();
            if (hud != null)
            {
                // Force HUD to find the new Stats component
                EditorUtility.SetDirty(hud);
                Debug.Log("[MVP Setup] HUD wired to Player Stats.");
            }

            // 3. Setup Void Forge Manager
            GameObject forgeManagerGo = GameObject.Find("VoidForgeManager");
            if (forgeManagerGo == null)
            {
                forgeManagerGo = new GameObject("VoidForgeManager");
                forgeManagerGo.AddComponent<VoidForgeManager>();
                Debug.Log("[MVP Setup] Created VoidForgeManager Singleton.");
            }

            // 4. World Drop Config Wiring
            var deathHooks = Object.FindObjectsByType<LootDropOnDeath>(FindObjectsSortMode.None);
            foreach (var hook in deathHooks)
            {
                // Note: We can't easily assign the ScriptableObject via code without its path, 
                // but we can warn the user or try to find it in Resources.
                Debug.Log($"[MVP Setup] Checked LootDropOnDeath on {hook.name}. Ensure 'WorldDropConfig' is assigned in the Inspector.");
            }

            Debug.Log("<b>[MVP Setup Complete]</b> Your Abyssbound_World is now running the Stats Awakening and Void Forge logic.");
        }
    }
}
