using System;
using System.Collections;
using Abyssbound.BagUpgrades;
using Abyssbound.Stats;
using Game.Systems;
using UnityEngine;

namespace Abyssbound.Skills.Gathering
{
    [DisallowMultipleComponent]
    public sealed class GatheringSkillController : MonoBehaviour
    {
        public bool IsBusy => _busy;

        private bool _busy;
        private float _busyUntil;

        private PlayerInventory _inventory;
        private PlayerStatsRuntime _stats;

        private Fishing.FishingSpot _activeFishingSpot;
        private Coroutine _fishingRoutine;

        public bool TryStartTimedAction(
            float seconds,
            Func<bool> preflight,
            Action onComplete,
            out string reason)
        {
            reason = string.Empty;

            if (_busy && Time.time < _busyUntil)
            {
                reason = "Busy.";
                return false;
            }

            if (seconds <= 0f)
                seconds = 0.1f;

            if (preflight != null)
            {
                bool ok = false;
                try { ok = preflight(); }
                catch { ok = false; }

                if (!ok)
                {
                    reason = string.IsNullOrWhiteSpace(reason) ? "Cannot start." : reason;
                    return false;
                }
            }

            _busy = true;
            _busyUntil = Time.time + seconds;
            StartCoroutine(CoComplete(seconds, onComplete));

            return true;
        }

        private IEnumerator CoComplete(float seconds, Action onComplete)
        {
            yield return new WaitForSeconds(Mathf.Max(0.01f, seconds));
            _busy = false;
            _busyUntil = 0f;
            try { onComplete?.Invoke(); }
            catch { }
        }

        public PlayerInventory GetOrFindInventory()
        {
            if (_inventory != null) return _inventory;
            try { _inventory = PlayerInventoryResolver.GetOrFind(); }
            catch { _inventory = null; }
            if (_inventory == null)
            {
                try { _inventory = GetComponentInChildren<PlayerInventory>(true); }
                catch { _inventory = null; }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                // Log the chosen inventory instance for diagnosing duplicates.
                // Note: verbose logging is gated in PlayerInventoryResolver.
                Game.Systems.PlayerInventoryResolver.LogAllInventoriesOnStart("GatheringSkillController.GetOrFindInventory");

                if (_inventory != null)
                {
                    var id = _inventory.GetInstanceID();
                    var path = Game.Systems.PlayerInventoryResolver.GetGameObjectPath(_inventory.gameObject);
                    // Keep this behind the resolver's verbose flag by using its method as the gate.
                    // If verbose is off, LogAllInventoriesOnStart does nothing and this log stays quiet.
                    // (We avoid introducing another global debug flag.)
                    // Intentionally no log here unless the global verbose is enabled.
                }
            }
            catch { }
#endif
            return _inventory;
        }

        public PlayerStatsRuntime GetOrFindStats()
        {
            if (_stats != null) return _stats;
            try { _stats = GetComponentInChildren<PlayerStatsRuntime>(true); }
            catch { _stats = null; }
            if (_stats == null)
            {
                try
                {
#if UNITY_2022_2_OR_NEWER
                    _stats = UnityEngine.Object.FindFirstObjectByType<PlayerStatsRuntime>(FindObjectsInactive.Exclude);
#else
                    _stats = UnityEngine.Object.FindObjectOfType<PlayerStatsRuntime>();
#endif
                }
                catch { _stats = null; }
            }
            return _stats;
        }

        public void StartGathering(Fishing.FishingSpot spot)
        {
            if (spot == null)
                return;

            if (_activeFishingSpot == spot && _fishingRoutine != null)
                return;

            StopGathering();
            _activeFishingSpot = spot;

            if (_fishingRoutine != null)
                StopCoroutine(_fishingRoutine);

            _fishingRoutine = StartCoroutine(CoFishingLoop());

            Debug.Log("[Fishing] Started.", this);
        }

        public void StopGathering(Fishing.FishingSpot spot = null)
        {
            if (spot != null && spot != _activeFishingSpot)
                return;

            if (_fishingRoutine != null)
            {
                StopCoroutine(_fishingRoutine);
                _fishingRoutine = null;
            }

            if (_activeFishingSpot != null)
                Debug.Log("[Fishing] Stopped.", this);

            _activeFishingSpot = null;
        }

        private IEnumerator CoFishingLoop()
        {
            while (_activeFishingSpot != null)
            {
                var stats = GetOrFindStats();
                if (stats == null)
                {
                    yield return null;
                    continue;
                }

                if (!_activeFishingSpot.TryGetFishingAction(out var seconds, out var actionXp, out var yieldItemId, out var yieldAmount))
                {
                    StopGathering(_activeFishingSpot);
                    yield break;
                }

                yield return new WaitForSeconds(Mathf.Max(0.1f, seconds));

                if (_activeFishingSpot == null)
                    yield break;

                if (actionXp > 0)
                {
                    try { stats.AddXp(Abyssbound.Loot.StatType.Fishing, actionXp); }
                    catch { }
                    Debug.Log($"[Fishing] +{actionXp} XP", this);
                }

                if (!string.IsNullOrWhiteSpace(yieldItemId) && yieldAmount > 0)
                {
                    // 1) Yield rolled log
                    Debug.Log($"[Fishing] Yield rolled: {yieldItemId} x{yieldAmount}", this);

                    var inv = GetOrFindInventory();
                    if (inv == null)
                    {
                        Debug.LogWarning($"[Fishing] Inventory add FAILED for {yieldItemId} (no inventory)", this);
                        continue;
                    }

                    // 2) Inventory usage log
                    int used = 0;
                    int max = 0;
                    try { used = inv.GetStackCount(); } catch { used = 0; }
                    try { max = inv.GetMaxInventorySlots(); } catch { max = 0; }
                    Debug.Log($"[Fishing] Inventory slots: {used}/{max}", this);

                    // 5) Safety: if inventory is full and this would consume a new slot, do not add.
                    bool wouldConsumeNewSlot = false;
                    try { wouldConsumeNewSlot = inv.EstimateAdditionalStacksForAdd(yieldItemId, yieldAmount) > 0; }
                    catch { wouldConsumeNewSlot = true; }

                    if (max > 0 && used >= max && wouldConsumeNewSlot)
                    {
                        Debug.LogWarning($"[Fishing] Inventory add FAILED for {yieldItemId} (inventory full)", this);
                        continue;
                    }

                    // 3-4) Attempt add + explicit success/failure
                    int beforeCount = 0;
                    int beforeStacks = used;
                    try { beforeCount = inv.Count(yieldItemId); } catch { beforeCount = 0; }
                    try { beforeStacks = inv.GetStackCount(); } catch { beforeStacks = used; }

                    bool addThrew = false;
                    try { inv.Add(yieldItemId, yieldAmount); }
                    catch { addThrew = true; }

                    int afterCount = beforeCount;
                    int afterStacks = beforeStacks;
                    try { afterCount = inv.Count(yieldItemId); } catch { afterCount = beforeCount; }
                    try { afterStacks = inv.GetStackCount(); } catch { afterStacks = beforeStacks; }

                    if (addThrew)
                    {
                        Debug.LogWarning($"[Fishing] Inventory add FAILED for {yieldItemId} (exception)", this);
                    }
                    else if (afterCount > beforeCount || afterStacks > beforeStacks)
                    {
                        Debug.Log($"[Fishing] Inventory add SUCCESS for {yieldItemId}", this);

                        // Rare bag upgrade drop from fishing (never T5).
                        try
                        {
                            string bagId = BagUpgradeDropRuntime.TryRollFishingDropBaseId();
                            if (!string.IsNullOrWhiteSpace(bagId))
                            {
                                Debug.Log($"[Fishing] Bag upgrade rolled: {bagId} x1", this);

                                int used2 = 0;
                                int max2 = 0;
                                try { used2 = inv.GetStackCount(); } catch { used2 = 0; }
                                try { max2 = inv.GetMaxInventorySlots(); } catch { max2 = 0; }

                                bool wouldConsumeNewSlot2 = false;
                                try { wouldConsumeNewSlot2 = inv.EstimateAdditionalStacksForAdd(bagId, 1) > 0; }
                                catch { wouldConsumeNewSlot2 = true; }

                                if (max2 > 0 && used2 >= max2 && wouldConsumeNewSlot2)
                                {
                                    Debug.LogWarning($"[Fishing] Bag upgrade add FAILED for {bagId} (inventory full)", this);
                                }
                                else
                                {
                                    int beforeBag = 0;
                                    int beforeBagStacks = used2;
                                    try { beforeBag = inv.Count(bagId); } catch { beforeBag = 0; }
                                    try { beforeBagStacks = inv.GetStackCount(); } catch { beforeBagStacks = used2; }

                                    bool addBagThrew = false;
                                    try { inv.Add(bagId, 1); } catch { addBagThrew = true; }

                                    int afterBag = beforeBag;
                                    int afterBagStacks = beforeBagStacks;
                                    try { afterBag = inv.Count(bagId); } catch { afterBag = beforeBag; }
                                    try { afterBagStacks = inv.GetStackCount(); } catch { afterBagStacks = beforeBagStacks; }

                                    if (addBagThrew)
                                        Debug.LogWarning($"[Fishing] Bag upgrade add FAILED for {bagId} (exception)", this);
                                    else if (afterBag > beforeBag || afterBagStacks > beforeBagStacks)
                                        Debug.Log($"[Fishing] Bag upgrade add SUCCESS for {bagId}", this);
                                    else
                                        Debug.LogWarning($"[Fishing] Bag upgrade add FAILED for {bagId}", this);
                                }
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        Debug.LogWarning($"[Fishing] Inventory add FAILED for {yieldItemId}", this);
                    }
                }
            }
        }

        public static GatheringSkillController GetOrAttachToPlayer()
        {
            // Prefer PlayerInputAuthority root.
            try
            {
#if UNITY_2022_2_OR_NEWER
                var authority = UnityEngine.Object.FindFirstObjectByType<Game.Input.PlayerInputAuthority>(FindObjectsInactive.Exclude);
#else
                var authority = UnityEngine.Object.FindObjectOfType<Game.Input.PlayerInputAuthority>();
#endif
                if (authority != null)
                {
                    var ctrl = authority.GetComponentInParent<GatheringSkillController>();
                    if (ctrl != null) return ctrl;
                    ctrl = authority.gameObject.AddComponent<GatheringSkillController>();
                    return ctrl;
                }
            }
            catch { }

            // Fallback: Player_Hero.
            try
            {
                var hero = GameObject.Find("Player_Hero");
                if (hero != null)
                {
                    var ctrl = hero.GetComponent<GatheringSkillController>();
                    if (ctrl != null) return ctrl;
                    ctrl = hero.AddComponent<GatheringSkillController>();
                    return ctrl;
                }
            }
            catch { }

            // Last resort: any existing controller.
            try
            {
#if UNITY_2022_2_OR_NEWER
                var any = UnityEngine.Object.FindFirstObjectByType<GatheringSkillController>(FindObjectsInactive.Exclude);
#else
                var any = UnityEngine.Object.FindObjectOfType<GatheringSkillController>();
#endif
                return any;
            }
            catch
            {
                return null;
            }
        }
    }
}
