using System;
using System.Reflection;
using UnityEngine;
using Game.Systems;

namespace Abyssbound.DebugTools
{
    public static class DevCheatActions
    {
        public static void SpawnBossSigil()
        {
            // Grant the boss sigil item directly to the player's inventory.
            // Item id in this repo: "AbyssalSigil".
            const string itemId = "AbyssalSigil";

            if (TryGrantInventoryItem(itemId, 1, out var details))
            {
                Debug.Log("[DevCheats] Granted AbyssalSigil x1");
                return;
            }

            Debug.LogWarning($"[DevCheats] Could not grant AbyssalSigil. Tried: {details}");
        }

        public static void AddTestSword()
        {
            const string itemId = "Test_Rare_Sword";

            if (TryGrantInventoryItem(itemId, 1, out var details))
            {
                Debug.Log("[DevCheats] Granted Test_Rare_Sword x1");
                return;
            }

            Debug.LogWarning($"[DevCheats] Could not grant Test_Rare_Sword. Tried: {details}");
        }

        private static PlayerInventory FindPlayerInventory()
        {
            return PlayerInventoryResolver.GetOrFind();
        }

        private static bool TryGrantInventoryItem(string itemId, int amount, out string tried)
        {
            tried = "";
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            {
                tried = "invalid itemId/amount";
                return false;
            }

            // Prefer the strongly-typed inventory if it exists.
            var playerInv = FindPlayerInventory();
            if (playerInv != null)
            {
                tried = "PlayerInventory." + GetTriedSignatures();
                return TryInvokeGrant(playerInv, itemId, amount);
            }

            // Fallback: look for a generic Inventory component (by name) if the project swaps implementations.
            var inventoryType = FindTypeByName("Inventory");
            if (inventoryType != null)
            {
                var invObj = FindFirstObjectByType(inventoryType);
                if (invObj != null)
                {
                    tried = "Inventory." + GetTriedSignatures();
                    return TryInvokeGrant(invObj, itemId, amount);
                }
            }

            tried = "no inventory instance found";
            return false;
        }

        private static string GetTriedSignatures()
        {
            // Required order (Add first, then AddItem).
            return "Add(string,int), Add(string), AddItem(string,int), AddItem(string)";
        }

        private static bool TryInvokeGrant(object inventoryInstance, string itemId, int amount)
        {
            if (inventoryInstance == null) return false;

            var type = inventoryInstance.GetType();

            // Add(string,int)
            if (TryInvoke(type, inventoryInstance, "Add", new[] { typeof(string), typeof(int) }, new object[] { itemId, amount })) return true;
            // Add(string)
            if (TryInvoke(type, inventoryInstance, "Add", new[] { typeof(string) }, new object[] { itemId })) return true;
            // AddItem(string,int)
            if (TryInvoke(type, inventoryInstance, "AddItem", new[] { typeof(string), typeof(int) }, new object[] { itemId, amount })) return true;
            // AddItem(string)
            if (TryInvoke(type, inventoryInstance, "AddItem", new[] { typeof(string) }, new object[] { itemId })) return true;

            return false;
        }

        private static bool TryInvoke(Type type, object instance, string methodName, Type[] parameterTypes, object[] args)
        {
            MethodInfo mi = null;
            try
            {
                mi = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, null, parameterTypes, null);
            }
            catch
            {
                mi = null;
            }

            if (mi == null) return false;

            try
            {
                mi.Invoke(instance, args);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static UnityEngine.Object FindFirstObjectByType(Type type)
        {
            if (type == null) return null;
            var all = UnityEngine.Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (all == null || all.Length == 0) return null;
            return all[0];
        }

        private static Type FindTypeByName(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = asm.GetType(typeName); } catch { }
                if (t != null) return t;

                // Also try namespace-agnostic match:
                Type[] types = null;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var cand in types)
                {
                    if (cand.Name == typeName) return cand;
                }
            }
            return null;
        }
    }
}
