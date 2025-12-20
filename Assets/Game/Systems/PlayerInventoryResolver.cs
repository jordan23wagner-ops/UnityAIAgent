using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Input;

namespace Game.Systems
{
    public static class PlayerInventoryResolver
    {
        private static bool _warnedMultiple;
        private static bool _warnedMissing;

        public static PlayerInventory GetOrFind()
        {
            // 1) Prefer inventory attached to the player input authority (single source of truth).
            try
            {
#if UNITY_2022_2_OR_NEWER
                var authority = UnityEngine.Object.FindFirstObjectByType<PlayerInputAuthority>(FindObjectsInactive.Exclude);
#else
                var authority = UnityEngine.Object.FindObjectOfType<PlayerInputAuthority>();
#endif
                if (authority != null)
                {
                    var inv = authority.GetComponentInParent<PlayerInventory>();
                    if (inv != null) return inv;

                    inv = authority.GetComponentInChildren<PlayerInventory>();
                    if (inv != null) return inv;
                }
            }
            catch { }

            // 2) Known player object name.
            try
            {
                var hero = GameObject.Find("Player_Hero");
                if (hero != null)
                {
                    var inv = hero.GetComponentInChildren<PlayerInventory>(true);
                    if (inv != null) return inv;
                }
            }
            catch { }

            // 3) Scan active inventories.
            List<PlayerInventory> active = null;
            try
            {
#if UNITY_2022_2_OR_NEWER
                var all = UnityEngine.Object.FindObjectsByType<PlayerInventory>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
                var all = UnityEngine.Object.FindObjectsOfType<PlayerInventory>();
#endif
                if (all != null && all.Length > 0)
                {
                    active = new List<PlayerInventory>(all.Length);
                    foreach (var inv in all)
                    {
                        if (inv == null) continue;
                        if (!inv.isActiveAndEnabled) continue;
                        active.Add(inv);
                    }
                }
            }
            catch { }

            if (active == null || active.Count == 0)
            {
                if (!_warnedMissing)
                {
                    _warnedMissing = true;
                    Debug.LogError("[PlayerInventoryResolver] No active PlayerInventory found.");
                }
                return null;
            }

            if (active.Count == 1)
                return active[0];

            // 4) Deterministic choice among multiple inventories.
            //    Prefer names containing "Player".
            PlayerInventory chosen = null;

            var playerNamed = new List<PlayerInventory>();
            foreach (var inv in active)
            {
                var name = SafeName(inv);
                if (name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0)
                    playerNamed.Add(inv);
            }

            if (playerNamed.Count > 0)
            {
                playerNamed.Sort(CompareDeterministic);
                chosen = playerNamed[0];
            }
            else
            {
                int bestStacks = -1;
                foreach (var inv in active)
                {
                    int stacks = 0;
                    try
                    {
                        var snap = inv.GetAllItemsSnapshot();
                        stacks = snap != null ? snap.Count : 0;
                    }
                    catch { stacks = 0; }

                    if (stacks > bestStacks)
                    {
                        bestStacks = stacks;
                        chosen = inv;
                    }
                    else if (stacks == bestStacks && chosen != null)
                    {
                        // Tie-break deterministically.
                        if (CompareDeterministic(inv, chosen) < 0)
                            chosen = inv;
                    }
                }
            }

            if (!_warnedMultiple)
            {
                _warnedMultiple = true;
                Debug.LogWarning($"[PlayerInventoryResolver] Multiple active PlayerInventory instances found: {active.Count}. Chose '{SafeName(chosen)}' (instanceId={(chosen != null ? chosen.GetInstanceID() : 0)})");
            }

            return chosen;
        }

        public static void AssertSingleInventoryOptional()
        {
            try
            {
#if UNITY_2022_2_OR_NEWER
                var all = UnityEngine.Object.FindObjectsByType<PlayerInventory>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
                var all = UnityEngine.Object.FindObjectsOfType<PlayerInventory>();
#endif
                int activeCount = 0;
                if (all != null)
                {
                    foreach (var inv in all)
                    {
                        if (inv != null && inv.isActiveAndEnabled)
                            activeCount++;
                    }
                }

                if (activeCount > 1)
                    Debug.LogWarning($"[PlayerInventoryResolver] AssertSingleInventoryOptional: {activeCount} active PlayerInventory instances exist.");
            }
            catch { }
        }

        public static string GetGameObjectPath(GameObject go)
        {
            if (go == null) return "(null)";

            try
            {
                var names = new List<string>(8);
                var t = go.transform;
                while (t != null)
                {
                    names.Add(t.name);
                    t = t.parent;
                }
                names.Reverse();
                return string.Join("/", names);
            }
            catch
            {
                return go.name;
            }
        }

        private static string SafeName(PlayerInventory inv)
        {
            try { return inv != null ? inv.gameObject.name : "(null)"; }
            catch { return "(null)"; }
        }

        private static int CompareDeterministic(PlayerInventory a, PlayerInventory b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;

            string an = SafeName(a);
            string bn = SafeName(b);
            int c = string.Compare(an, bn, StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;

            int aid = 0;
            int bid = 0;
            try { aid = a.GetInstanceID(); } catch { }
            try { bid = b.GetInstanceID(); } catch { }
            return aid.CompareTo(bid);
        }
    }
}
