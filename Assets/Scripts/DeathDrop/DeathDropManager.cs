using System;
using System.Collections.Generic;
using Abyss.Equipment;
using Game.Systems;
using UnityEngine;

namespace Abyssbound.DeathDrop
{
    /*
     * QA CHECKLIST (DeathDrop v1)
     * 1) Give player items + town scroll, die -> keep scroll only, drop everything else
     * 2) Without scroll, die -> keep most valuable item, drop rest
     * 3) Die again before looting -> old pile destroyed (items lost)
     * 4) Loot pile -> items return to inventory, pile marked looted
     * 5) Respawn puts player at town spawn and restores health
     */

    [DisallowMultipleComponent]
    public sealed class DeathDropManager : MonoBehaviour
    {
        public static DeathDropManager Instance { get; private set; }

        public static float SuppressGameplayInputUntil;

        [Header("Config")]
        [SerializeField] private string townScrollId = "scroll_town";

        [Header("Pickup Prefab (optional)")]
        [Tooltip("If assigned, DeathPile will spawn these pickups. If missing, a minimal runtime pickup will be created.")]
        [SerializeField] private GameObject pickupPrefab;

        [Header("Debug")]
        [SerializeField] private bool logSummaryOnDeath = true;

        private DeathPile _activePile;
        private bool _handling;

        public string TownScrollId => townScrollId;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void HandlePlayerDeath(Transform playerTransform, PlayerHealth health, PlayerInventory inventory)
        {
            if (_handling)
                return;

            _handling = true;

            try
            {
                if (playerTransform == null)
                    return;

                var playerGo = playerTransform.gameObject;

                var inv = inventory;
                if (inv == null)
                {
                    try { inv = PlayerInventoryResolver.GetOrFind(); } catch { inv = null; }
                }

                PlayerEquipment equip = null;
                try { equip = PlayerEquipmentResolver.GetOrFindOrCreate(); } catch { equip = null; }

                // 1) If an existing death pile is still active (not looted), destroy it and lose items.
                if (_activePile != null && !_activePile.IsLooted)
                {
                    int lost = 0;
                    try { lost = _activePile.Items != null ? _activePile.Items.Count : 0; } catch { lost = 0; }

                    try { Destroy(_activePile.gameObject); } catch { }
                    _activePile = null;

                    Debug.LogWarning($"[DeathDrop] Previous death pile destroyed; items lost (entries={lost}).", this);
                }

                // Providers (inventory + equipment).
                var providers = new List<IDeathDropProvider>(2);
                if (inv != null) providers.Add(new InventoryDeathDropProvider(inv));
                if (equip != null) providers.Add(new EquipmentDeathDropProvider(equip));

                var totals = AggregateCounts(providers);
                string protectedId = DetermineProtectedItem(totals);

                var dropped = BuildDroppedList(totals, protectedId);

                // 4) Remove dropped items across providers (unequip gear, consume stacks).
                if (dropped.Count > 0)
                    RemoveDroppedItems(providers, dropped);

                // 3) Spawn a DeathPile at death position.
                if (dropped.Count > 0)
                    SpawnDeathPile(playerTransform.position, dropped);

                // 5) Respawn player at Town and restore health.
                Vector3 respawnPos;
                RespawnHelper.TryGetTownSpawn(out respawnPos);
                RespawnHelper.TeleportPlayerTo(playerTransform, respawnPos);
                RespawnHelper.RevivePlayer(health);
                RespawnHelper.ResetPlayerState(playerGo);

                if (logSummaryOnDeath)
                {
                    Debug.Log($"[DeathDrop] Death handled. Protected='{(string.IsNullOrWhiteSpace(protectedId) ? "(none)" : protectedId)}' DroppedEntries={dropped.Count}", this);
                }
            }
            finally
            {
                _handling = false;
            }
        }

        public DeathPilePickup SpawnPickupForItem(Transform pileTransform, DroppedItem it)
        {
            if (pileTransform == null)
                return null;

            GameObject go = null;
            if (pickupPrefab != null)
            {
                try { go = Instantiate(pickupPrefab); } catch { go = null; }
            }

            if (go == null)
                return SpawnFallbackPickup(pileTransform, it);

            return FinalizePickupGO(pileTransform, go, it);
        }

        public static DeathPilePickup SpawnFallbackPickup(Transform pileTransform, DroppedItem it)
        {
            if (pileTransform == null)
                return null;

            GameObject go;
            try { go = GameObject.CreatePrimitive(PrimitiveType.Sphere); }
            catch { return null; }

            if (go == null)
                return null;

            return FinalizePickupGO(pileTransform, go, it);
        }

        private static DeathPilePickup FinalizePickupGO(Transform pileTransform, GameObject go, DroppedItem it)
        {
            if (pileTransform == null || go == null)
                return null;

            try
            {
                go.transform.SetParent(pileTransform, worldPositionStays: true);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one * 0.5f;
                go.transform.localPosition = Vector3.up * 0.25f;
            }
            catch { }

            try
            {
                var col = go.GetComponent<Collider>();
                if (col == null) col = go.AddComponent<SphereCollider>();
                if (col != null) col.isTrigger = true;
            }
            catch { }

            try
            {
                var rb = go.GetComponent<Rigidbody>();
                if (rb == null) rb = go.AddComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.useGravity = false;
                    rb.isKinematic = true;
                }
            }
            catch { }

            DeathPilePickup pickup = null;
            try
            {
                pickup = go.GetComponent<DeathPilePickup>();
                if (pickup == null) pickup = go.AddComponent<DeathPilePickup>();
            }
            catch { pickup = null; }

            try { go.name = $"DeathPickup_{it.itemId}"; } catch { }

            if (pickup != null)
            {
                DeathPile pile = null;
                try { pile = pileTransform.GetComponent<DeathPile>(); } catch { pile = null; }
                pickup.Configure(pile, it.itemId, it.amount);
            }

            return pickup;
        }

        internal void NotifyPileLooted(DeathPile pile)
        {
            if (pile == null)
                return;

            if (_activePile == pile)
                _activePile = null;
        }

        private void SpawnDeathPile(Vector3 deathPos, List<DroppedItem> dropped)
        {
            var go = new GameObject("[DeathPile]");
            go.transform.position = deathPos;

            var pile = go.AddComponent<DeathPile>();
            pile.Initialize(this, dropped);

            _activePile = pile;
        }

        private string DetermineProtectedItem(IReadOnlyDictionary<string, int> totals)
        {
            if (totals == null || totals.Count == 0)
                return null;

            // 2a) If the player has a Town Scroll, it is protected (kept, not consumed).
            if (!string.IsNullOrWhiteSpace(townScrollId) && totals.TryGetValue(townScrollId, out var scrollCount) && scrollCount > 0)
                return townScrollId;

            // 2b) Otherwise, protect the most valuable item.
            string bestId = null;
            int bestScore = int.MinValue;

            foreach (var kvp in totals)
            {
                var id = kvp.Key;
                var count = kvp.Value;

                if (string.IsNullOrWhiteSpace(id))
                    continue;
                if (count <= 0)
                    continue;

                int score = 0;
                try { score = ItemValueEvaluator.Evaluate(id, townScrollId); } catch { score = 0; }

                if (bestId == null || score > bestScore)
                {
                    bestId = id;
                    bestScore = score;
                }
                else if (score == bestScore && bestId != null)
                {
                    // Deterministic tie-break.
                    if (string.Compare(id, bestId, StringComparison.OrdinalIgnoreCase) < 0)
                        bestId = id;
                }
            }

            return bestId;
        }

        private static List<IDeathDropProvider> RemoveNullProviders(List<IDeathDropProvider> providers)
        {
            if (providers == null) return new List<IDeathDropProvider>(0);

            for (int i = providers.Count - 1; i >= 0; i--)
            {
                if (providers[i] == null)
                    providers.RemoveAt(i);
            }

            return providers;
        }

        private static Dictionary<string, int> AggregateCounts(List<IDeathDropProvider> providers)
        {
            providers = RemoveNullProviders(providers);
            var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int p = 0; p < providers.Count; p++)
            {
                IEnumerable<DroppedItem> items = null;
                try { items = providers[p].GetAllItems(); } catch { items = null; }
                if (items == null) continue;

                foreach (var it in items)
                {
                    if (string.IsNullOrWhiteSpace(it.itemId) || it.amount <= 0)
                        continue;

                    if (!totals.TryGetValue(it.itemId, out var cur)) cur = 0;
                    totals[it.itemId] = cur + it.amount;
                }
            }

            return totals;
        }

        private List<DroppedItem> BuildDroppedList(IReadOnlyDictionary<string, int> totals, string protectedId)
        {
            var list = new List<DroppedItem>(32);
            if (totals == null || totals.Count == 0)
                return list;

            foreach (var kvp in totals)
            {
                var id = kvp.Key;
                var count = kvp.Value;

                if (string.IsNullOrWhiteSpace(id))
                    continue;
                if (count <= 0)
                    continue;

                int dropCount = count;

                // Protect ONE copy of the protected item (scroll or most valuable).
                if (!string.IsNullOrWhiteSpace(protectedId) && string.Equals(id, protectedId, StringComparison.OrdinalIgnoreCase))
                    dropCount = Mathf.Max(0, count - 1);

                if (dropCount > 0)
                    list.Add(new DroppedItem(id, dropCount));
            }

            return list;
        }

        private static void RemoveDroppedItems(List<IDeathDropProvider> providers, List<DroppedItem> dropped)
        {
            if (providers == null || providers.Count == 0)
                return;
            if (dropped == null || dropped.Count == 0)
                return;

            for (int i = 0; i < dropped.Count; i++)
            {
                var it = dropped[i];
                if (string.IsNullOrWhiteSpace(it.itemId))
                    continue;
                if (it.amount <= 0)
                    continue;

                RemoveAcrossProviders(providers, it.itemId, it.amount);
            }
        }

        private static void RemoveAcrossProviders(List<IDeathDropProvider> providers, string itemId, int amount)
        {
            if (providers == null || string.IsNullOrWhiteSpace(itemId) || amount <= 0)
                return;

            int remaining = amount;

            // Prefer equipment first so stats update immediately when dropping.
            for (int pass = 0; pass < 2 && remaining > 0; pass++)
            {
                for (int p = 0; p < providers.Count && remaining > 0; p++)
                {
                    var provider = providers[p];
                    if (provider == null) continue;

                    bool isEquipmentProvider = provider is EquipmentDeathDropProvider;
                    if (pass == 0 && !isEquipmentProvider) continue;
                    if (pass == 1 && isEquipmentProvider) continue;

                    int have = 0;
                    try { have = provider.GetCount(itemId); } catch { have = 0; }
                    if (have <= 0) continue;

                    int take = Mathf.Min(remaining, have);
                    bool ok = false;
                    try { ok = provider.TryRemove(itemId, take); } catch { ok = false; }

                    if (ok)
                        remaining -= take;
                }
            }
        }
    }
}
