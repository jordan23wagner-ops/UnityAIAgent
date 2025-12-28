# INV ICON + STACK DEBUG BUNDLE

Project: Abyssbound (Unity)

## 1) Symptom Summary
- Fishing yields log as successful (e.g. fish_raw_shrimp), but inventory UI appears empty and/or shows no icon and no stack count.
- Prior console warning observed: [INV][ICON] icon NULL for fish_raw_shrimp.

## 2) Primary Hypotheses (based on current code)
1. Wrong inventory instance: gameplay adds to one PlayerInventory, UI binds a different one.
2. UI row template hides icon: the row template Icon GameObject starts inactive (see UI builder), so if binding only sets Image.enabled it will still not render.
3. Icon resolution failure: string item ids that exist only as LootV2 base items require fallback resolution.

## 3) Involved Scripts (roles)
- Assets/Game/Skills/Gathering/PlayerGatheringController.cs
  - Fishing loop yield + inv.Add(yieldItemId, yieldAmount) + explicit success/fail logs.
- Assets/Game/Systems/PlayerInventoryResolver.cs
  - Authoritative inventory selection (prefers Player_Hero).
- Assets/Scripts/Player/PlayerInventory.cs
  - Inventory model: _items dictionary, Add, Changed event, slot capacity rules.
- Assets/Abyss/Inventory/PlayerInventoryUI.cs
  - UI controller: resolves inventory, rebuilds 4x7 grid, calls row.Bind(...).
- Assets/Abyss/Inventory/PlayerInventoryRowUI.cs
  - Per-slot renderer: resolves icon + count; has default-off debug logs.
- Assets/Editor/BuildPlayerInventoryUIEditor.cs
  - Editor builder that constructs the inventory UI hierarchy and the RowTemplate.

## 4) Binding / Event Trace (expected)
1. GatheringSkillController.CoFishingLoop()
   - Logs: [Fishing] Yield rolled: <id> x<amount>
   - Calls: inv.Add(yieldItemId, yieldAmount)
2. PlayerInventory.Add()
   - Mutates _items and invokes Changed.
3. PlayerInventoryUI.OnInventoryChanged()
   - Calls RefreshList() and RefreshDetails() when open.
4. PlayerInventoryUI.RefreshList()
   - Gets snapshot via _inventory.GetAllItemsSnapshot()
   - Filters by tab, then instantiates 28 slots
   - Calls PlayerInventoryRowUI.Bind(def, itemId, count, ...) or BindEmpty()
5. PlayerInventoryRowUI.Bind(...)
   - Resolves display name + icon (legacy + LootV2 fallbacks)
   - Writes icon sprite + count text visibility

## 5) Prefab / Hierarchy Details (Row Template)
This project appears to generate the inventory UI via Tools/Abyssbound/Content/UI/Build Player Inventory UI (Editor).

Key detail from BuildPlayerInventoryUIEditor.BuildRowTemplate():
- Creates a RowTemplate GameObject with children:
  - RarityStrip (Image)
  - Icon (Image) starts SetActive(false)
  - Name (TMP)
  - Count (TMP)

Implication:
- If runtime binding only sets iconImage.enabled = true but never re-activates the Icon GameObject, the icon will remain invisible even with a valid sprite.

## 6) Temporary Debug Helpers (default off)
- Assets/Abyss/Inventory/PlayerInventoryUI.cs: logs snapshot sizes + visible stacks when INV_DEBUG is enabled.
- Assets/Abyss/Inventory/PlayerInventoryRowUI.cs: logs per-bind slot/id/count/icon state when INV_DEBUG is enabled.

To enable temporarily:
- Set INV_DEBUG = true in both scripts above.
- (Optional) set VERBOSE_INVENTORY_DEBUG = true in PlayerInventoryResolver to log duplicates.

---
## 7) Full Source: PlayerGatheringController.cs
Path: c:\Users\Jordon\UnityAIAgent\Assets\Game\Skills\Gathering\PlayerGatheringController.cs
```csharp
using System;
using System.Collections;
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

```

## 8) Full Source: PlayerInventoryResolver.cs
Path: c:\Users\Jordon\UnityAIAgent\Assets\Game\Systems\PlayerInventoryResolver.cs
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Input;

namespace Game.Systems
{
    public static class PlayerInventoryResolver
    {
        // Keep console clean by default; enable temporarily for diagnosing duplicate inventories.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool VERBOSE_INVENTORY_DEBUG = false;
#else
        private static bool VERBOSE_INVENTORY_DEBUG = false;
#endif

        private static bool _warnedMultiple;
        private static bool _warnedMissing;

        public static PlayerInventory GetOrFind()
        {
            // Preferred rule (project): Player_Hero inventory is authoritative.
            // NOTE: Some systems may attach PlayerInputAuthority on a different object; do not let that override Player_Hero.
            try
            {
                var hero = GameObject.Find("Player_Hero");
                if (hero != null)
                {
                    var inv = hero.GetComponentInChildren<PlayerInventory>(true);
                    if (inv != null)
                        return inv;
                }
            }
            catch { }

            // Fallback: inventory attached to the player input authority chain.
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

            // Last resort: scan active inventories.
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

        public static PlayerInventory GetOrFindWithDiagnostics(out string source)
        {
            source = null;

            PlayerInventory inv = null;

            try
            {
                var hero = GameObject.Find("Player_Hero");
                if (hero != null)
                {
                    inv = hero.GetComponentInChildren<PlayerInventory>(true);
                    if (inv != null)
                    {
                        source = "Player_Hero";
                        return inv;
                    }
                }
            }
            catch { }

            try
            {
#if UNITY_2022_2_OR_NEWER
                var authority = UnityEngine.Object.FindFirstObjectByType<PlayerInputAuthority>(FindObjectsInactive.Exclude);
#else
                var authority = UnityEngine.Object.FindObjectOfType<PlayerInputAuthority>();
#endif
                if (authority != null)
                {
                    inv = authority.GetComponentInParent<PlayerInventory>();
                    if (inv != null)
                    {
                        source = "PlayerInputAuthority(parent)";
                        return inv;
                    }

                    inv = authority.GetComponentInChildren<PlayerInventory>();
                    if (inv != null)
                    {
                        source = "PlayerInputAuthority(child)";
                        return inv;
                    }
                }
            }
            catch { }

            inv = GetOrFind();
            source = inv != null ? "ScanFallback" : "(null)";
            return inv;
        }

        public static void LogAllInventoriesOnStart(string context)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!VERBOSE_INVENTORY_DEBUG)
                return;

            try
            {
                List<PlayerInventory> allList = null;

#if UNITY_2022_2_OR_NEWER
                var all = UnityEngine.Object.FindObjectsByType<PlayerInventory>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
                var all = UnityEngine.Object.FindObjectsOfType<PlayerInventory>();
#endif

                if (all != null)
                    allList = new List<PlayerInventory>(all);

                Debug.Log($"[INV][DIAG] {context}: PlayerInventory instances found={(allList != null ? allList.Count : 0)}");

                if (allList == null) return;
                for (int i = 0; i < allList.Count; i++)
                {
                    var inv = allList[i];
                    if (inv == null) continue;
                    var go = inv.gameObject;
                    var path = GetGameObjectPath(go);
                    int id = 0;
                    try { id = inv.GetInstanceID(); } catch { }
                    bool active = false;
                    bool enabled = false;
                    try { active = go != null && go.activeInHierarchy; } catch { }
                    try { enabled = inv.enabled; } catch { }
                    Debug.Log($"[INV][DIAG] - invId={id} enabled={enabled} active={active} go='{path}'", inv);
                }
            }
            catch { }
#endif
        }

        public static void EnforceSingleAuthoritativeInventoryOptional(bool destroyDuplicateComponents)
        {
            var authoritative = GetOrFind();
            if (authoritative == null)
                return;

            try
            {
#if UNITY_2022_2_OR_NEWER
                var all = UnityEngine.Object.FindObjectsByType<PlayerInventory>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
                var all = UnityEngine.Object.FindObjectsOfType<PlayerInventory>();
#endif
                if (all == null) return;

                int activeCount = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    var inv = all[i];
                    if (inv != null && inv.isActiveAndEnabled)
                        activeCount++;
                }

                if (activeCount <= 1)
                    return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (VERBOSE_INVENTORY_DEBUG)
                {
                    int authId = 0;
                    try { authId = authoritative.GetInstanceID(); } catch { }
                    Debug.LogWarning($"[INV][DIAG] Multiple active PlayerInventory instances ({activeCount}). Authoritative invId={authId} go='{GetGameObjectPath(authoritative.gameObject)}'.");
                }
#endif

                if (!destroyDuplicateComponents)
                    return;

                for (int i = 0; i < all.Length; i++)
                {
                    var inv = all[i];
                    if (inv == null) continue;
                    if (!inv.isActiveAndEnabled) continue;
                    if (ReferenceEquals(inv, authoritative)) continue;

                    // Destroy only the duplicate inventory COMPONENT (not the whole GameObject) to reduce blast radius.
                    UnityEngine.Object.Destroy(inv);
                }
            }
            catch { }
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

```

## 9) Full Source: PlayerInventory.cs
Path: c:\Users\Jordon\UnityAIAgent\Assets\Scripts\Player\PlayerInventory.cs
```csharp
using System;
using System.Collections.Generic;
using Abyss.Items;
using Abyssbound.Loot;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    private readonly Dictionary<string, int> _items = new();

    // Authoritative inventory slot capacity.
    // NOTE: Inventory is stack-based; "slots" means number of distinct keys in _items.
    private const int BaseInventorySlots = 10;

    // Bag upgrades (T1â€“T5) increase capacity up to 24 total.
    // These IDs are intentionally plain strings so content can be authored as legacy ItemDefinition assets.
    // If your project uses different IDs, update these constants.
    private static readonly string[] s_BagTier1Ids = { "bag_t1", "bag_tier_1", "bag1", "Bag T1", "T1 Bag" };
    private static readonly string[] s_BagTier2Ids = { "bag_t2", "bag_tier_2", "bag2", "Bag T2", "T2 Bag" };
    private static readonly string[] s_BagTier3Ids = { "bag_t3", "bag_tier_3", "bag3", "Bag T3", "T3 Bag" };
    private static readonly string[] s_BagTier4Ids = { "bag_t4", "bag_tier_4", "bag4", "Bag T4", "T4 Bag" };
    private static readonly string[] s_BagTier5Ids = { "bag_t5", "bag_tier_5", "bag5", "Bag T5", "T5 Bag" };

    // Legacy item definition lookup cache.
    private static Dictionary<string, ItemDefinition> s_LegacyDefById;

    public event System.Action Changed;

    // Number of distinct stacks/entries (rolled instances count as their own stack).
    public int GetStackCount()
    {
        return _items != null ? _items.Count : 0;
    }

    public int GetMaxInventorySlots()
    {
        int max = BaseInventorySlots;

        // Highest tier wins.
        if (HasAny(s_BagTier5Ids)) return 24;
        if (HasAny(s_BagTier4Ids)) return 22;
        if (HasAny(s_BagTier3Ids)) return 20;
        if (HasAny(s_BagTier2Ids)) return 18;
        if (HasAny(s_BagTier1Ids)) return 16;

        return max;
    }

    public int GetFreeInventorySlots()
    {
        return Mathf.Max(0, GetMaxInventorySlots() - GetStackCount());
    }

    public bool WouldExceedMaxSlotsWithAdditionalStacks(int additionalStacks)
    {
        additionalStacks = Mathf.Max(0, additionalStacks);
        return (GetStackCount() + additionalStacks) > GetMaxInventorySlots();
    }

    // Conservative estimate of whether adding an item would consume a new slot.
    // This intentionally mirrors the inventory's non-stackable policy for rolled instances.
    public int EstimateAdditionalStacksForAdd(string itemId, int amount)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            return 0;

        // Rolled instances never stack; each copy is a new key.
        if (itemId.StartsWith("ri_", StringComparison.OrdinalIgnoreCase))
            return Mathf.Max(1, amount);

        // Stackable by default: only consumes a new slot if it doesn't already exist.
        return _items != null && _items.ContainsKey(itemId) ? 0 : 1;
    }

    public bool HasRoomForAdd(string itemId, int amount)
    {
        int addStacks = EstimateAdditionalStacksForAdd(itemId, amount);
        return !WouldExceedMaxSlotsWithAdditionalStacks(addStacks);
    }

    private bool HasAny(IReadOnlyList<string> ids)
    {
        if (ids == null || ids.Count == 0)
            return false;

        for (int i = 0; i < ids.Count; i++)
        {
            var id = ids[i];
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (Has(id, 1)) return true;
        }
        return false;
    }

    public void Add(string itemId, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return;
        if (amount <= 0) return;

        // Non-stackable policy:
        // - Rolled instances (ri_...) should never stack; if amount > 1, clone into multiple rolled IDs.
        // - Legacy equippable items should never stack; convert into rolled instances so the UI/equip/tooltip pipelines can treat each copy as unique.
        if (TryAddAsNonStackable(itemId, amount))
            return;

        int next = _items.TryGetValue(itemId, out var cur) ? (cur + amount) : amount;
        _items[itemId] = next;

        if (LootQaSettings.DebugLogsEnabled)
            Debug.Log($"[Inventory] Added {amount}x {itemId}. Now: {next}", this);

        try { Changed?.Invoke(); } catch { }
    }

    private bool TryAddAsNonStackable(string itemId, int amount)
    {
        // 1) Rolled loot instance IDs should never stack.
        if (itemId.StartsWith("ri_", StringComparison.OrdinalIgnoreCase))
        {
            // Most callsites add rolled IDs with amount=1; handle >1 defensively.
            if (amount == 1)
            {
                _items[itemId] = 1;
                if (LootQaSettings.DebugLogsEnabled)
                    Debug.Log($"[Inventory] Added 1x {itemId} (non-stackable)", this);
                try { Changed?.Invoke(); } catch { }
                return true;
            }

            var reg = LootRegistryRuntime.GetOrCreate();
            if (reg != null && reg.TryGetRolledInstance(itemId, out var inst) && inst != null)
            {
                // Add the original once.
                _items[itemId] = 1;

                // Clone remaining copies into new rolled IDs.
                for (int i = 1; i < amount; i++)
                {
                    var clone = CloneInstance(inst);
                    var cloneId = reg.RegisterRolledInstance(clone);
                    if (!string.IsNullOrWhiteSpace(cloneId))
                        _items[cloneId] = 1;
                }

                if (LootQaSettings.DebugLogsEnabled)
                    Debug.Log($"[Inventory] Added {amount}x {itemId} as unique rolled instances (non-stackable)", this);
                try { Changed?.Invoke(); } catch { }
                return true;
            }

            // If we can't resolve the instance, fall back to stacking by key (best-effort).
            return false;
        }

        // 2) Legacy equippable items should never stack.
        var def = ResolveLegacyItemDefinition(itemId);
        if (def == null)
            return false;

        bool equippable = false;
        try { equippable = def.equipmentSlot != EquipmentSlot.None; } catch { equippable = false; }

        if (!equippable)
            return false;

        var reg2 = LootRegistryRuntime.GetOrCreate();
        if (reg2 == null)
            return false;

        EnsureLootV2BaseItemRegistered(reg2, def);

        for (int i = 0; i < amount; i++)
        {
            var inst = CreateInstanceFromLegacy(def);
            var rolledId = reg2.RegisterRolledInstance(inst);
            if (string.IsNullOrWhiteSpace(rolledId))
                continue;

            _items[rolledId] = 1;
        }

        if (LootQaSettings.DebugLogsEnabled)
            Debug.Log($"[Inventory] Added {amount}x {itemId} as unique equipment entries (non-stackable)", this);
        try { Changed?.Invoke(); } catch { }
        return true;
    }

    private static ItemDefinition ResolveLegacyItemDefinition(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        s_LegacyDefById ??= BuildLegacyIndex();
        if (s_LegacyDefById != null && s_LegacyDefById.TryGetValue(itemId, out var def) && def != null)
            return def;

        // Rebuild once (covers domain reload / asset load order).
        s_LegacyDefById = BuildLegacyIndex();
        if (s_LegacyDefById != null && s_LegacyDefById.TryGetValue(itemId, out var refreshed))
            return refreshed;

        return null;
    }

    private static Dictionary<string, ItemDefinition> BuildLegacyIndex()
    {
        var map = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var defs = Resources.FindObjectsOfTypeAll<ItemDefinition>();
            if (defs == null) return map;

            for (int i = 0; i < defs.Length; i++)
            {
                var def = defs[i];
                if (def == null) continue;

                string id = null;
                try { id = def.itemId; } catch { id = null; }
                if (string.IsNullOrWhiteSpace(id)) continue;

                if (!map.ContainsKey(id))
                    map[id] = def;
            }
        }
        catch { }

        return map;
    }

    private static void EnsureLootV2BaseItemRegistered(LootRegistryRuntime registry, ItemDefinition legacy)
    {
        if (registry == null || legacy == null) return;

        string id = null;
        try { id = legacy.itemId; } catch { id = null; }
        if (string.IsNullOrWhiteSpace(id)) return;

        if (registry.TryGetItem(id, out var existing) && existing != null)
            return;

        var baseItem = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        baseItem.id = id;

        try { baseItem.displayName = string.IsNullOrWhiteSpace(legacy.displayName) ? id : legacy.displayName; }
        catch { baseItem.displayName = id; }

        try { baseItem.icon = legacy.icon; } catch { baseItem.icon = null; }

        try { baseItem.slot = legacy.equipmentSlot; }
        catch { baseItem.slot = EquipmentSlot.None; }

        // Occupies slots for two-handed / offhand conventions.
        baseItem.occupiesSlots = new List<EquipmentSlot>(2);
        try
        {
            if (legacy.weaponHandedness == WeaponHandedness.TwoHanded)
            {
                baseItem.occupiesSlots.Add(EquipmentSlot.LeftHand);
                baseItem.occupiesSlots.Add(EquipmentSlot.RightHand);
            }
            else if (legacy.weaponHandedness == WeaponHandedness.Offhand)
            {
                baseItem.occupiesSlots.Add(EquipmentSlot.LeftHand);
            }
        }
        catch { }

        // Convert legacy stats into Loot V2 base stats so tooltips and combat/HP can read them.
        baseItem.baseStats = new List<StatMod>(4);

        try
        {
            if (legacy.DamageBonus != 0)
            {
                baseItem.baseStats.Add(new StatMod
                {
                    stat = GuessDamageStatType(id),
                    value = legacy.DamageBonus,
                    percent = false
                });
            }
        }
        catch { }

        try
        {
            if (legacy.MaxHealthBonus != 0)
            {
                baseItem.baseStats.Add(new StatMod
                {
                    stat = StatType.MaxHealth,
                    value = legacy.MaxHealthBonus,
                    percent = false
                });
            }
        }
        catch { }

        try
        {
            if (legacy.DamageReductionFlat != 0)
            {
                baseItem.baseStats.Add(new StatMod
                {
                    stat = StatType.Defense,
                    value = legacy.DamageReductionFlat,
                    percent = false
                });
            }
        }
        catch { }

        registry.RegisterOrUpdateItem(baseItem);
    }

    private static StatType GuessDamageStatType(string legacyItemId)
    {
        if (string.IsNullOrWhiteSpace(legacyItemId))
            return StatType.MeleeDamage;

        var id = legacyItemId.ToLowerInvariant();
        if (id.Contains("bow") || id.Contains("ranged")) return StatType.RangedDamage;
        if (id.Contains("staff") || id.Contains("wand") || id.Contains("magic")) return StatType.MagicDamage;
        return StatType.MeleeDamage;
    }

    private static ItemInstance CreateInstanceFromLegacy(ItemDefinition legacy)
    {
        string baseId = null;
        try { baseId = legacy.itemId; } catch { baseId = null; }

        string rarityId = "Common";
        try { rarityId = legacy.rarity.ToString(); } catch { rarityId = "Common"; }

        int lvl = 1;
        string lvlSource = "Default";
        try
        {
            if (LootQaSettings.TryGetItemLevelOverride(out var overrideLvl, out var src))
            {
                lvl = overrideLvl;
                lvlSource = src;
            }
        }
        catch { lvl = 1; lvlSource = "Default"; }

        if (LootQaSettings.DebugLogsEnabled)
        {
            string name = baseId;
            try { name = !string.IsNullOrWhiteSpace(legacy.displayName) ? legacy.displayName : baseId; } catch { name = baseId; }
            Debug.Log($"[Loot] Created {name} ilvl={Mathf.Max(1, lvl)} source={lvlSource}");
        }

        return new ItemInstance
        {
            baseItemId = baseId,
            rarityId = rarityId,
            itemLevel = Mathf.Max(1, lvl),
            baseScalar = 1f,
            affixes = new List<AffixRoll>()
        };
    }

    private static ItemInstance CloneInstance(ItemInstance src)
    {
        if (src == null) return null;
        var dst = new ItemInstance
        {
            baseItemId = src.baseItemId,
            rarityId = src.rarityId,
            itemLevel = src.itemLevel,
            baseScalar = src.baseScalar,
            affixes = new List<AffixRoll>(src.affixes != null ? src.affixes.Count : 0)
        };

        if (src.affixes != null)
        {
            for (int i = 0; i < src.affixes.Count; i++)
                dst.affixes.Add(src.affixes[i]);
        }

        return dst;
    }

    public bool Has(string itemId, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return false;
        return _items.TryGetValue(itemId, out var count) && count >= amount;
    }

    public int Count(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return 0;
        return _items.TryGetValue(itemId, out var count) ? count : 0;
    }

    public bool TryConsume(string itemId, int amount = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return false;
        if (!_items.TryGetValue(itemId, out var count)) return false;
        if (count < amount) return false;

        int newCount = count - amount;
        if (newCount <= 0) _items.Remove(itemId);
        else _items[itemId] = newCount;

        Debug.Log($"[Inventory] Consumed {amount}x {itemId}. Now: {Count(itemId)}");

        try { Changed?.Invoke(); } catch { }
        return true;
    }

    public bool TryRemove(string itemId, int amount = 1)
    {
        // Alias for clarity in systems like merchant selling.
        return TryConsume(itemId, amount);
    }

    public IReadOnlyDictionary<string, int> GetAllItemsSnapshot()
    {
        // Snapshot to prevent callers from mutating internal state.
        return new Dictionary<string, int>(_items);
    }
}

```

## 10) Full Source: PlayerInventoryUI.cs
Path: c:\Users\Jordon\UnityAIAgent\Assets\Abyss\Inventory\PlayerInventoryUI.cs
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Abyss.Items;
using Abyss.Equipment;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

using AbyssItemRarity = Abyss.Items.ItemRarity;
using AbyssItemType = Abyss.Items.ItemType;

namespace Abyss.Inventory
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class PlayerInventoryUI : MonoBehaviour
    {
        private enum InventoryTab
        {
            WeaponsGear = 0,
            Materials = 1,
            Consumables = 2,
            Skilling = 3,
        }

        private const bool INVENTORY_UI_DEBUG = false;
        private static bool InventoryUiDebugEnabled => INVENTORY_UI_DEBUG;

        private const bool INV_DEBUG = false;

    #if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const bool INV_DIAGNOSTICS = true;
    #else
        private const bool INV_DIAGNOSTICS = false;
    #endif

        // OSRS-style inventory grid.
        private const int InventoryGridColumns = 4;
        private const int InventoryGridRows = 7;
        private const int InventoryGridSlots = InventoryGridColumns * InventoryGridRows;

        [Header("Root")]
        [SerializeField] private GameObject root;
        [SerializeField] private Button closeButton;

        [Header("Character Tabs (optional)")]
        [SerializeField] private Button characterInventoryTabButton;
        [SerializeField] private Button characterEquipmentTabButton;

        [Header("Top")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text goldText;

        [Header("List")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private PlayerInventoryRowUI rowTemplate;

        [Header("Tabs (optional)")]
        [Tooltip("If not set, a simple tab bar will be created at runtime above the list.")]
        [SerializeField] private RectTransform tabsRoot;

        [Header("Details")]
        [SerializeField] private PlayerInventoryDetailsUI detailsUI;

        private Game.Input.PlayerInputAuthority _inputAuthority;
        private PlayerInventory _inventory;
        private Abyss.Shop.PlayerGoldWallet _wallet;
        private PlayerEquipment _equipment;
        private Abyss.Equipment.PlayerEquipmentUI _equipmentUi;

        private string _inventorySource;
        private int _lastInventoryInstanceId;
        private bool _loggedInventoryForThisOpen;
        private bool _loggedScrollWiringForThisOpen;
        private bool _loggedFirstRowVisibilityThisOpen;

        private readonly List<GameObject> _spawnedRows = new();
        private readonly List<PlayerInventoryRowUI> _spawnedSlotViews = new();
        private Dictionary<string, ItemDefinition> _itemDefById;

        private string _selectedItemId;
        private ItemDefinition _selectedDef;
        private int _selectedCount;

        // UI-only selection index for visuals (grid slot index 0..27, or -1 none)
        private int _selectedSlotIndex = -1;

        private InventoryTab _activeTab = InventoryTab.WeaponsGear;

        private Button _tabWeapons;
        private Button _tabMaterials;
        private Button _tabConsumables;
        private Button _tabSkilling;

        private TMP_Text _tabWeaponsText;
        private TMP_Text _tabMaterialsText;
        private TMP_Text _tabConsumablesText;
        private TMP_Text _tabSkillingText;

        private Button _equipButton;
        private TMP_Text _equipButtonText;

        private bool _isOpen;

        public bool IsOpen => _isOpen;

        private readonly Dictionary<Image, Color> _forcedOpaqueImages = new();

        private Image _backdropImage;
        private Color _backdropOriginalColor;
        private bool _backdropOriginalCaptured;

        private int _lastRefreshFrame = -1;
        private bool _refreshQueued;

        private bool _warnedContentLayoutConflict;

        private void Awake()
        {
#if UNITY_2022_2_OR_NEWER
            _inputAuthority = FindFirstObjectByType<Game.Input.PlayerInputAuthority>();
#else
            _inputAuthority = FindObjectOfType<Game.Input.PlayerInputAuthority>();
#endif

            if (root != null)
                root.SetActive(false);

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }

            if (titleText != null && string.IsNullOrWhiteSpace(titleText.text))
                titleText.text = "Inventory";

            _wallet = Abyss.Shop.PlayerGoldWallet.Instance;

            // Resolve inventory on-demand to avoid wrong instance bindings.
            _inventory = null;

            detailsUI?.Clear();

            WireCharacterTabs();
        }

        private void Update()
        {
            // Some scenes/scripts may toggle the inventory root active without calling Open()/Close().
            // Keep _isOpen in sync so hotkeys/buttons still work.
            SyncOpenStateFromRoot();

            if (_refreshQueued)
            {
                _refreshQueued = false;
                RefreshAll();
            }

            // TASK 1: fallback input: E equips selected item while inventory is open.
            if (_isOpen && WasEquipPressed() && !Abyss.Shop.MerchantShopUI.IsOpen)
                TryEquipSelected();

            if (!WasTogglePressed())
                return;

            // Avoid fighting with merchant UI.
            if (Abyss.Shop.MerchantShopUI.IsOpen)
                return;

            if (_isOpen) Close();
            else Open();
        }

        private bool WasEquipPressed()
        {
#if ENABLE_INPUT_SYSTEM
            try
            {
                return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
            }
            catch { return false; }
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }

        private void SyncOpenStateFromRoot()
        {
            if (root == null)
                return;

            // If the root is visible, treat as open.
            if (root.activeSelf)
            {
                if (!_isOpen)
                {
                    _isOpen = true;
                    EnsureEquipButton();
                    EnsureInventory();
                    EnsureEquipment();
                    RefreshDetails();
                }
            }
            else
            {
                if (_isOpen)
                    _isOpen = false;
            }
        }

        private static EquipmentSlot GuessEquipSlot(ItemDefinition def, string itemId)
        {
            if (def != null)
            {
                try
                {
                    if (def.equipmentSlot != EquipmentSlot.None)
                        return def.equipmentSlot;
                }
                catch { }

                return EquipmentSlot.None;
            }

            if (string.IsNullOrWhiteSpace(itemId))
                return EquipmentSlot.None;

            // Rolled loot instance support
            try
            {
                var reg = Abyssbound.Loot.LootRegistryRuntime.GetOrCreate();
                if (reg != null && reg.TryGetRolledInstance(itemId, out var inst) && inst != null)
                {
                    if (reg.TryGetItem(inst.baseItemId, out var baseItem) && baseItem != null)
                        return baseItem.slot;
                }
            }
            catch { }

            return EquipmentSlot.None;
        }

        private bool CanEquipSelected(ItemDefinition def, string itemId)
        {
            if (def != null)
            {
                try
                {
                    if (def.equipmentSlot != EquipmentSlot.None)
                        return true;
                }
                catch { }

                return false;
            }

            if (string.IsNullOrWhiteSpace(itemId))
                return false;

            // Rolled loot instance support
            try
            {
                var reg = Abyssbound.Loot.LootRegistryRuntime.GetOrCreate();
                if (reg != null && reg.TryGetRolledInstance(itemId, out var inst) && inst != null)
                {
                    if (reg.TryGetItem(inst.baseItemId, out var baseItem) && baseItem != null)
                        return baseItem.slot != EquipmentSlot.None;
                }
            }
            catch { }

            return false;
        }

        private static string SanitizeReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return "";

            reason = reason.Replace("\r", " ").Replace("\n", " ");
            while (reason.Contains("  "))
                reason = reason.Replace("  ", " ");
            return reason.Trim();
        }

        private static void LogEquipAttempt(string itemId, EquipmentSlot slot, bool success, string reason)
        {
            itemId ??= "";
            reason = SanitizeReason(reason);
            var ok = success.ToString().ToLowerInvariant();
            Debug.Log($"[EQUIP] itemId={itemId} slot={slot} success={ok} reason={reason}");
        }

        private void TryEquipSelected()
        {
            EnsureEquipment();
            EnsureInventory();

            var def = _selectedDef;
            var itemId = _selectedItemId;
            var slot = GuessEquipSlot(def, itemId);

            // TASK 1: single log line per attempt, exactly matching requested format.
            if (_equipment == null)
            {
                LogEquipAttempt(itemId, slot, success: false, reason: "No PlayerEquipment");
                return;
            }

            if (string.IsNullOrWhiteSpace(itemId))
            {
                LogEquipAttempt(itemId, slot, success: false, reason: "No item selected");
                return;
            }

            // Spec: equippable only when equipmentSlot != None.
            if (slot == EquipmentSlot.None)
            {
                LogEquipAttempt(itemId, slot, success: false, reason: "Not equippable (equipmentSlot=None)");
                return;
            }

            if (_inventory == null)
            {
                LogEquipAttempt(itemId, slot, success: false, reason: "No PlayerInventory");
                return;
            }

            // Inventory-authoritative: consume 1 from inventory, equip, and return conflicts to inventory.
            bool ok = _equipment.TryEquipFromInventory(_inventory, ResolveItemDefinition, itemId, out var message);
            string reason = string.IsNullOrWhiteSpace(message) ? (ok ? "OK" : "Failed") : message;
            LogEquipAttempt(itemId, slot, ok, reason);

            if (ok)
                RefreshAll();
        }

        public void Open()
        {
            if (root == null || _isOpen)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log($"[InventoryUI TRACE] Open frame={Time.frameCount}", this);
#endif

            _isOpen = true;
            root.SetActive(true);

            // Ensure a single tooltip instance exists under this UI root.
            try { ItemTooltipUI.GetOrCreateUnder(root.transform); } catch { }

            HideStrayLegacyCategoryTexts();
            EnsureBackdropIsTransparent();
            ForceOpaqueBackground(true);

            _loggedInventoryForThisOpen = false;
            _loggedScrollWiringForThisOpen = false;
            _loggedFirstRowVisibilityThisOpen = false;

            EnsureCanvasVisibility();
            EnsureScrollRectWiring();
            EnsureScrollViewLayoutHard(); // NEW: enforce known-good layout up front

            EnsureTabs();

            EnsureEquipButton();

            BringListToFront();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log($"[PlayerInventoryUI] BringListToFront parent='{scrollRect.transform.parent?.name}' siblingIndex={scrollRect.transform.GetSiblingIndex()} childCount={scrollRect.transform.parent?.childCount}", this);
#endif

            _wallet = Abyss.Shop.PlayerGoldWallet.Instance;
            if (_wallet != null)
            {
                _wallet.GoldChanged -= OnGoldChanged;
                _wallet.GoldChanged += OnGoldChanged;
            }

            EnsureInventory();
            if (_inventory != null)
            {
                _inventory.Changed -= OnInventoryChanged;
                _inventory.Changed += OnInventoryChanged;
            }

            EnsureEquipment();

            if (_equipmentUi == null)
            {
                try
                {
#if UNITY_2022_2_OR_NEWER
                    _equipmentUi = FindFirstObjectByType<Abyss.Equipment.PlayerEquipmentUI>();
#else
                    _equipmentUi = FindObjectOfType<Abyss.Equipment.PlayerEquipmentUI>();
#endif
                }
                catch { }
            }

            try { _inputAuthority?.SetUiInputLocked(true); } catch { }

            // Build the grid immediately so we don't show a blank/flashy intermediate frame.
            _refreshQueued = false;
            RefreshAll();
        }

        private void WireCharacterTabs()
        {
            // Inventory tab is "selected" while this window is open.
            if (characterInventoryTabButton != null)
                characterInventoryTabButton.interactable = false;

            if (characterEquipmentTabButton != null)
            {
                characterEquipmentTabButton.onClick.RemoveAllListeners();
                characterEquipmentTabButton.onClick.AddListener(() =>
                {
                    Close();
                    try { _equipmentUi?.Open(); } catch { }
                });
            }
        }

        private void EnsureBackdropIsTransparent()
        {
            if (root == null)
                return;

            try
            {
                if (_backdropImage == null)
                {
                    var t = FindDeepChild(root.transform, "Backdrop");
                    if (t != null)
                        _backdropImage = t.GetComponent<Image>();
                }

                if (_backdropImage == null)
                    return;

                if (!_backdropOriginalCaptured)
                {
                    _backdropOriginalCaptured = true;
                    _backdropOriginalColor = _backdropImage.color;
                }

                // User request: see the game behind the inventory. Keep the backdrop for raycast-blocking,
                // but make it visually transparent.
                var c = _backdropImage.color;
                if (c.a > 0.001f)
                    _backdropImage.color = new Color(c.r, c.g, c.b, 0f);
            }
            catch
            {
                // Best-effort only.
            }
        }

        private void ForceOpaqueBackground(bool enabled)
        {
            if (root == null)
                return;

            if (!enabled)
            {
                if (_forcedOpaqueImages.Count == 0)
                    return;

                foreach (var kv in _forcedOpaqueImages)
                {
                    try
                    {
                        if (kv.Key != null)
                            kv.Key.color = kv.Value;
                    }
                    catch { }
                }

                _forcedOpaqueImages.Clear();
                return;
            }

            if (_forcedOpaqueImages.Count > 0)
                return;

            // Preferred: known names created by BuildPlayerInventoryUIEditor.
            TryForceOpaqueByName("Panel");
            TryForceOpaqueByName("ItemsScrollView");
            TryForceOpaqueByName("DetailsPanel");

            // Fallback: if we couldn't find any of the conventional panels, force opaque on large semi-transparent
            // images under the inventory root (excluding item tiles/tabs/details).
            if (_forcedOpaqueImages.Count == 0)
                TryForceOpaqueHeuristic();
        }

        private void TryForceOpaqueByName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || root == null)
                return;

            var t = FindDeepChild(root.transform, name);
            if (t == null)
                return;

            var img = t.GetComponent<Image>();
            if (img == null)
                return;

            ForceOpaque(img);
        }

        private void TryForceOpaqueHeuristic()
        {
            try
            {
                var images = root.GetComponentsInChildren<Image>(true);
                if (images == null || images.Length == 0)
                    return;

                for (int i = 0; i < images.Length; i++)
                {
                    var img = images[i];
                    if (img == null)
                        continue;

                    // Never force the full-screen backdrop opaque; we want the world visible.
                    if (string.Equals(img.gameObject.name, "Backdrop", StringComparison.Ordinal))
                        continue;

                    if (contentRoot != null && img.transform.IsChildOf(contentRoot))
                        continue;
                    if (tabsRoot != null && img.transform.IsChildOf(tabsRoot))
                        continue;
                    if (detailsUI != null && img.transform.IsChildOf(detailsUI.transform))
                        continue;
                    if (rowTemplate != null && img.transform.IsChildOf(rowTemplate.transform))
                        continue;

                    var rt = img.rectTransform;
                    if (rt == null)
                        continue;

                    float area = Mathf.Abs(rt.rect.width * rt.rect.height);
                    if (area < 20000f)
                        continue;

                    ForceOpaque(img);
                }
            }
            catch
            {
                // Best-effort only.
            }
        }

        private void ForceOpaque(Image img)
        {
            if (img == null)
                return;

            try
            {
                var c = img.color;
                if (c.a >= 0.999f)
                    return;

                if (!_forcedOpaqueImages.ContainsKey(img))
                    _forcedOpaqueImages.Add(img, c);

                c.a = 1f;
                img.color = c;
            }
            catch
            {
                // Best-effort only.
            }
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            if (parent == null || string.IsNullOrEmpty(name))
                return null;

            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child == null) continue;

                if (string.Equals(child.name, name, StringComparison.Ordinal))
                    return child;

                var found = FindDeepChild(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }

        private void HideStrayLegacyCategoryTexts()
        {
            if (root == null)
                return;

            try
            {
                var texts = root.GetComponentsInChildren<TMP_Text>(true);
                if (texts == null || texts.Length == 0)
                    return;

                for (int i = 0; i < texts.Length; i++)
                {
                    var t = texts[i];
                    if (t == null) continue;

                    // Keep known UI text elements.
                    if (t == titleText || t == goldText)
                        continue;

                    if (detailsUI != null && t.transform.IsChildOf(detailsUI.transform))
                        continue;

                    // Don't hide tab button labels.
                    if (tabsRoot != null && t.transform.IsChildOf(tabsRoot))
                        continue;

                    // Don't hide inventory slot row text.
                    if (rowTemplate != null && t.transform.IsChildOf(rowTemplate.transform))
                        continue;
                    if (contentRoot != null && t.transform.IsChildOf(contentRoot))
                        continue;

                    var s = t.text;
                    if (string.IsNullOrWhiteSpace(s))
                        continue;

                    s = s.Trim();
                    var lower = s.ToLowerInvariant();

                    // Legacy category label(s) that shouldn't float over the grid.
                    if (lower.Contains("weapon") && (lower.Contains("util") || lower.Contains("utility") || lower.Contains("utilities")))
                    {
                        t.gameObject.SetActive(false);
                        continue;
                    }
                }
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }

        public void Close()
        {
            if (!_isOpen)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log($"[InventoryUI TRACE] Close frame={Time.frameCount}", this);
#endif

            _isOpen = false;

            ForceOpaqueBackground(false);

            // Restore original backdrop tint (if any).
            try
            {
                if (_backdropImage != null && _backdropOriginalCaptured)
                    _backdropImage.color = _backdropOriginalColor;
            }
            catch { }

            try { _inputAuthority?.SetUiInputLocked(false); } catch { }

            if (_wallet != null)
                _wallet.GoldChanged -= OnGoldChanged;

            if (_inventory != null)
                _inventory.Changed -= OnInventoryChanged;

            detailsUI?.Clear();

            if (root != null)
                root.SetActive(false);
        }

        private void RefreshAll()
        {
            RefreshGold();
            RefreshList();
            RefreshDetails();
        }

        private void OnGoldChanged(int newGold)
        {
            RefreshGold();
        }

        private void OnInventoryChanged()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log($"[InventoryUI TRACE] OnInventoryChanged frame={Time.frameCount}", this);
#endif
            // If inventory changes while open, refresh list/details.
            if (!_isOpen) return;
            RefreshList();
            RefreshDetails();
        }

        private void RefreshGold()
        {
            if (goldText == null)
                return;

            _wallet = Abyss.Shop.PlayerGoldWallet.Instance;
            int g = _wallet != null ? _wallet.Gold : 0;
            goldText.text = $"Gold: {g}";
        }

        private void RefreshList()
        {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log($"[InventoryUI TRACE] RefreshList ENTER frame={Time.frameCount}", this);
#endif

            // Prevent multiple rebuilds in the same frame
            if (_lastRefreshFrame == Time.frameCount)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (InventoryUiDebugEnabled) Debug.Log($"[InventoryUI TRACE] RefreshList SKIPPED (already ran this frame)", this);
#endif
                return;
            }

            _lastRefreshFrame = Time.frameCount;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log("[InventoryUI TRACE] RefreshList BEGIN frame=" + Time.frameCount, this);
#endif

            if (contentRoot == null || rowTemplate == null)
                return;

            BringListToFront();

            EnsureScrollRectWiring();
            EnsureScrollViewLayoutHard(); // NEW: enforce layout every refresh (safe, cheap)

            EnsureInventory();
            if (_inventory == null)
            {
                ClearRows();
                return;
            }

            _itemDefById ??= BuildItemDefinitionIndex();

            // Keep template under contentRoot and disabled.
            if (rowTemplate.transform != null && rowTemplate.transform.parent != contentRoot)
                rowTemplate.transform.SetParent(contentRoot, false);
            if (rowTemplate.gameObject.activeSelf)
                rowTemplate.gameObject.SetActive(false);

            ClearRows();

            var snap = _inventory.GetAllItemsSnapshot();
            if (snap == null)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
#pragma warning disable CS0162
            if (INV_DEBUG)
            {
                int nonEmptyStacks = 0;
                try
                {
                    foreach (var kv in snap)
                    {
                        if (!string.IsNullOrWhiteSpace(kv.Key) && kv.Value > 0)
                            nonEmptyStacks++;
                    }
                }
                catch { }

                Debug.Log($"[INVDBG][UI REFRESH] snapshotKeys={snap.Count} nonEmptyStacks={nonEmptyStacks} gridSlots={InventoryGridSlots}", this);
            }
#pragma warning restore CS0162
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log("[InventoryUI TRACE] RefreshList snapshotCount=" + snap.Count + " frame=" + Time.frameCount, this);
#endif

            // One-time diagnostics per open (no spam).
            if (_isOpen)
            {
                try
                {
                    int stacks = snap.Count;
                    int total = 0;
                    foreach (var kv in snap)
                        total += Mathf.Max(0, kv.Value);

                    int instanceId = 0;
                    try { instanceId = _inventory != null ? _inventory.GetInstanceID() : 0; } catch { }

                    if (!_loggedInventoryForThisOpen || instanceId != _lastInventoryInstanceId)
                    {
                        _loggedInventoryForThisOpen = true;
                        _lastInventoryInstanceId = instanceId;
                        if (InventoryUiDebugEnabled) Debug.Log($"[PlayerInventoryUI] Inventory source={_inventorySource ?? "(unknown)"} instanceId={instanceId} stacks={stacks} totalItems={total}", this);
                    }
                }
                catch { }
            }

            // Deterministic iteration to avoid flicker.
            var keys = new List<string>(snap.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);

            PlayerInventoryRowUI first = null;
            string firstId = null;
            int firstCount = 0;
            int renderedStacks = 0;
            int createdRowCount = 0;
            int rowIndex = 0;

            // Build a filtered list of stacks, then place them into a fixed 4x7 grid.
            var visibleStacks = new List<(string itemId, int count, ItemDefinition def)>(keys.Count);
            foreach (var itemId in keys)
            {
                if (string.IsNullOrWhiteSpace(itemId))
                    continue;

                int count = snap.TryGetValue(itemId, out var c) ? c : 0;
                if (count <= 0)
                    continue;

                var def = ResolveItemDefinition(itemId);
                if (!PassesTabFilter(def, itemId))
                    continue;

                visibleStacks.Add((itemId, count, def));
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
#pragma warning disable CS0162
            if (INV_DEBUG)
            {
                int nonEmptySlots = Mathf.Min(visibleStacks.Count, InventoryGridSlots);
                Debug.Log($"[INVDBG][UI REFRESH] visibleStacks={visibleStacks.Count} nonEmptySlotsShown={nonEmptySlots} rowsToCreate={InventoryGridSlots}", this);
            }
#pragma warning restore CS0162
#endif

            // Sync selected slot index based on currently selected item id (UI-only).
            _selectedSlotIndex = FindSelectedSlotIndexInVisibleStacks(visibleStacks, _selectedItemId);

            if (visibleStacks.Count > InventoryGridSlots)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[PlayerInventoryUI] Inventory has {visibleStacks.Count} stacks but UI is fixed to {InventoryGridSlots} slots; truncating display.", this);
#endif
            }

            // Prefer square cells sized to fit the viewport.
            Vector2 cellSize = new Vector2(64f, 64f);
            try
            {
                if (contentRoot != null)
                {
                    var grid = contentRoot.GetComponent<GridLayoutGroup>();
                    if (grid != null)
                        cellSize = grid.cellSize;
                }
            }
            catch { }

            for (int slotIndex = 0; slotIndex < InventoryGridSlots; slotIndex++)
            {
                bool hasItem = slotIndex < visibleStacks.Count;
                string itemId = hasItem ? visibleStacks[slotIndex].itemId : null;
                int count = hasItem ? visibleStacks[slotIndex].count : 0;
                var def = hasItem ? visibleStacks[slotIndex].def : null;

                int capturedSlotIndex = slotIndex;

                var go = Instantiate(rowTemplate.gameObject, contentRoot, false);
                createdRowCount++;
                go.name = hasItem ? $"Row_{itemId}" : $"EmptySlot_{slotIndex}";
                go.SetActive(true);

                var capturedGo = go;

                // IMPORTANT: put the row into grid mode immediately so it never renders a list-mode
                // (often-white) background for a frame.
                var row = go.GetComponent<PlayerInventoryRowUI>();
                if (row != null)
                    row.SetGridMode(true);

                // Requirement: stable slot index stored on each row.
                if (row != null)
                {
                    try { row.SetSlotIndex(slotIndex); } catch { }
                }

                if (row != null)
                {
                    if (_spawnedSlotViews.Count <= capturedSlotIndex)
                    {
                        while (_spawnedSlotViews.Count <= capturedSlotIndex)
                            _spawnedSlotViews.Add(null);
                    }
                    _spawnedSlotViews[capturedSlotIndex] = row;
                }

                // Ensure stable layout metadata.
                var le = go.GetComponent<LayoutElement>();
                if (le == null) le = go.AddComponent<LayoutElement>();
                le.preferredWidth = cellSize.x;
                le.preferredHeight = cellSize.y;
                le.minWidth = cellSize.x;
                le.minHeight = cellSize.y;
                le.flexibleWidth = 0f;
                le.flexibleHeight = 0f;

                var rt = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    // GridLayoutGroup controls positioning and size.
                    rt.localScale = Vector3.one;
                    rt.localRotation = Quaternion.identity;

                    // Normalize anchors so layout calculations are consistent.
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                }

                // Brute-force visibility so â€œclickable but invisibleâ€ canâ€™t happen.
                ForceRowVisible(go);

                bool isSelected = hasItem && _selectedSlotIndex == capturedSlotIndex;
                ApplyRowVisualStyling(go, rowIndex, isSelected);

                // --- [INV] Debug: build-time diagnostics (one log per slot) ---
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (INV_DIAGNOSTICS)
                {
                    try
                    {
                        var btn = go.GetComponent<Button>();
                        var img = go.GetComponent<Image>();
                        Debug.Log($"[INV][BUILD] Slot {slotIndex} | empty={!hasItem} | hasButton={(btn != null)} | hasImage={(img != null)} | raycast={(img != null && img.raycastTarget)}", this);
                    }
                    catch { }
                }
#endif

                if (row != null)
                {
                    // Debug context for hover logs.
                    try { row.SetDebugContext(slotIndex, !hasItem); } catch { }

                    if (hasItem)
                    {
                        string capturedId = itemId;
                        int capturedCount = count;
                        row.Bind(def, capturedId, capturedCount, () =>
                        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.Log($"[INV][CLICK] slotIndex={capturedSlotIndex} empty=false", this);
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            if (INV_DIAGNOSTICS)
                            {
                                try
                                {
                                    Debug.Log($"[INV][CLICK] Slot {slotIndex} empty=false", this);
                                    Debug.Log($"[INV][CLICK ITEM] Selecting itemId={capturedId}", this);
                                    Debug.Log($"[INV][RAYCAST] currentSelected={((EventSystem.current != null) ? EventSystem.current.currentSelectedGameObject?.name : "(no EventSystem)")}", this);

                                    var btn = go.GetComponent<Button>();
                                    var img = go.GetComponent<Image>();
                                    Debug.Log($"[INV][RAYCAST TARGETS] hasButton={(btn != null)} targetGraphic={(btn != null && btn.targetGraphic != null ? btn.targetGraphic.name : "(null)")} hasImage={(img != null)} imgRaycast={(img != null && img.raycastTarget)}", this);
                                }
                                catch { }
                            }
#endif
                            _selectedSlotIndex = capturedSlotIndex;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.Log($"[INV][SEL SET] _selectedSlotIndex={_selectedSlotIndex}", this);
#endif
                            Select(capturedId, capturedCount);
                            UpdateSelectionVisuals();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.Log($"[INV][SELECT] slot={_selectedSlotIndex}", this);
#endif
                        });

                        if (first == null)
                        {
                            first = row;
                            firstId = capturedId;
                            firstCount = capturedCount;
                        }

                        renderedStacks++;
                    }
                    else
                    {
                        row.BindEmpty(() =>
                        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.Log($"[INV][CLICK] slotIndex={capturedSlotIndex} empty=true", this);
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            if (INV_DIAGNOSTICS)
                            {
                                try
                                {
                                    Debug.Log($"[INV][CLICK] Slot {slotIndex} empty=true", this);
                                    Debug.Log("[INV][CLICK EMPTY] Clearing selection", this);
                                    Debug.Log($"[INV][RAYCAST] currentSelected={((EventSystem.current != null) ? EventSystem.current.currentSelectedGameObject?.name : "(no EventSystem)")}", this);

                                    var btn = go.GetComponent<Button>();
                                    var img = go.GetComponent<Image>();
                                    Debug.Log($"[INV][RAYCAST TARGETS] hasButton={(btn != null)} targetGraphic={(btn != null && btn.targetGraphic != null ? btn.targetGraphic.name : "(null)")} hasImage={(img != null)} imgRaycast={(img != null && img.raycastTarget)}", this);
                                }
                                catch { }
                            }
#endif
                            _selectedSlotIndex = -1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.Log($"[INV][SEL SET] _selectedSlotIndex={_selectedSlotIndex}", this);
#endif
                            ClearSelection();
                            UpdateSelectionVisuals();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            Debug.Log($"[INV][SELECT] slot={_selectedSlotIndex}", this);
#endif
                        });
                    }
                }

                rowIndex++;
                _spawnedRows.Add(go);
            }

            // Keep selection valid.
            // If nothing is selected, keep it empty (do not auto-select first).
            if (!string.IsNullOrWhiteSpace(_selectedItemId))
            {
                // If selected item no longer exists, fall back to first.
                int selCount = snap.TryGetValue(_selectedItemId, out var sc) ? sc : 0;
                if (selCount <= 0 && first != null)
                {
                    Select(firstId, firstCount);
                    _selectedSlotIndex = 0;
                }
                else if (_selectedSlotIndex < 0)
                {
                    // Selection exists in inventory but is filtered out by current tab.
                    if (first != null)
                    {
                        Select(firstId, firstCount);
                        _selectedSlotIndex = 0;
                    }
                    else
                    {
                        _selectedItemId = null;
                        _selectedDef = null;
                        _selectedCount = 0;
                        detailsUI?.Clear();
                        _selectedSlotIndex = -1;
                    }
                }
            }
            else
            {
                _selectedSlotIndex = -1;
            }

            // Ensure selection visuals survive RefreshList rebuild.
            UpdateSelectionVisuals();

            // Force rebuild now that children exist.
            try
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
                if (scrollRect != null && scrollRect.viewport != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.viewport);
                Canvas.ForceUpdateCanvases();
            }
            catch { }

            BringListToFront();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                RectTransform firstRowRt = null;
                for (int i = 0; i < contentRoot.childCount; i++)
                {
                    var ch = contentRoot.GetChild(i);
                    if (ch == null) continue;
                    if (rowTemplate != null && ch == rowTemplate.transform) continue;
                    firstRowRt = ch as RectTransform;
                    if (firstRowRt != null) break;
                }

                var anchors = $"({contentRoot.anchorMin}->{contentRoot.anchorMax})";
                var pivot = contentRoot.pivot;

                string firstRowInfo = "(no rows)";
                if (firstRowRt != null)
                    firstRowInfo = $"firstRow='{firstRowRt.name}' localPos={firstRowRt.localPosition} anchoredPos={firstRowRt.anchoredPosition}";

                if (InventoryUiDebugEnabled) Debug.Log($"[InventoryUI TRACE] PostRebuild content anchors={anchors} pivot={pivot} {firstRowInfo}", this);
            }
            catch { }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                int rowsUnderContent = 0;
                for (int i = 0; i < contentRoot.childCount; i++)
                {
                    var ch = contentRoot.GetChild(i);
                    if (ch == null) continue;
                    if (rowTemplate != null && ch == rowTemplate.transform) continue;
                    rowsUnderContent++;
                }

                var vpSize = scrollRect != null && scrollRect.viewport != null ? scrollRect.viewport.rect.size : Vector2.zero;
                var cSize = contentRoot != null ? contentRoot.rect.size : Vector2.zero;

                if (InventoryUiDebugEnabled) Debug.Log($"[PlayerInventoryUI] Rendered={renderedStacks} rowsUnderContent={rowsUnderContent} viewportSize={vpSize} contentRectSize={cSize}", this);
            }
            catch { }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (InventoryUiDebugEnabled) Debug.Log("[InventoryUI TRACE] RefreshList END frame=" + Time.frameCount + " rowsCreated=" + createdRowCount, this);
#endif
        }

        private void RefreshDetails()
        {
            if (detailsUI == null)
                return;

            if (string.IsNullOrWhiteSpace(_selectedItemId))
            {
                detailsUI.Clear();
                RefreshEquipButtonState(null);
                return;
            }

            EnsureInventory();
            int count = _inventory != null ? _inventory.Count(_selectedItemId) : _selectedCount;
            string detailsId = _selectedItemId;
            var def = ResolveItemDefinition(_selectedItemId);

            // Rolled loot instances (ri_...) use Loot V2 details (includes iLvl + set display).
            if (!string.IsNullOrWhiteSpace(_selectedItemId) && _selectedItemId.StartsWith("ri_", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var reg = Abyssbound.Loot.LootRegistryRuntime.GetOrCreate();
                    if (reg != null && reg.TryGetRolledInstance(_selectedItemId, out var inst) && inst != null)
                    {
                        detailsUI.SetLootInstance(inst, reg, count);
                        RefreshEquipButtonState(null);
                        return;
                    }
                }
                catch { }
            }

            detailsUI.Set(def, detailsId, count);
            RefreshEquipButtonState(def);
        }

        private void Select(string itemId, int count)
        {
            _selectedItemId = itemId;
            _selectedCount = Mathf.Max(0, count);
            _selectedDef = ResolveItemDefinition(itemId);

            // Update highlight immediately without rebuilding the list.
            UpdateSelectionHighlightVisuals();

            RefreshDetails();
        }

        private void ClearSelection()
        {
            _selectedItemId = null;
            _selectedDef = null;
            _selectedCount = 0;

            UpdateSelectionHighlightVisuals();
            RefreshDetails();
        }

        private void EnsureEquipment()
        {
            if (_equipment != null)
                return;

            try
            {
                _equipment = PlayerEquipmentResolver.GetOrFindOrCreate();
            }
            catch
            {
                _equipment = null;
            }
        }

        private void EnsureEquipButton()
        {
            if (_equipButton != null)
                return;

            if (detailsUI == null)
                return;

            var existing = detailsUI.transform.Find("EquipButton");
            if (existing != null)
            {
                _equipButton = existing.GetComponent<Button>();
                _equipButtonText = existing.GetComponentInChildren<TMP_Text>(true);
                if (_equipButton != null)
                {
                    _equipButton.onClick.RemoveAllListeners();
                    _equipButton.onClick.AddListener(OnEquipPressed);
                }
                return;
            }

            var go = new GameObject("EquipButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(detailsUI.transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.06f, 0.01f);
            rt.anchorMax = new Vector2(0.94f, 0.07f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = Color.white;

            _equipButton = go.GetComponent<Button>();
            _equipButton.onClick.RemoveAllListeners();
            _equipButton.onClick.AddListener(OnEquipPressed);

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(go.transform, false);
            var lrt = label.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            var tmp = label.GetComponent<TextMeshProUGUI>();
            tmp.text = "Equip";
            tmp.fontSize = 22;
            tmp.color = Color.black;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;

            _equipButtonText = tmp;
            RefreshEquipButtonState(_selectedDef);
        }

        private void RefreshEquipButtonState(ItemDefinition selectedDef)
        {
            if (_equipButton == null)
                return;

            bool canEquip = CanEquipSelected(selectedDef, _selectedItemId);

            // UX: only show the button when it can actually do something.
            bool show = canEquip;
            try { _equipButton.gameObject.SetActive(show); } catch { }

            if (!show)
                return;

            _equipButton.interactable = true;
            if (_equipButtonText != null)
                _equipButtonText.text = "Equip";
        }

        private void OnEquipPressed()
        {
            EnsureEquipment();

            // Same equip attempt as hotkey.
            TryEquipSelected();
        }

        private void ClearRows()
        {
            if (contentRoot == null)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                int diagChildCountBefore = contentRoot.childCount;
                int diagSnapCount = -1;
                try
                {
                    if (_inventory != null)
                    {
                        var s = _inventory.GetAllItemsSnapshot();
                        diagSnapCount = s != null ? s.Count : 0;
                    }
                    else
                    {
                        diagSnapCount = 0;
                    }
                }
                catch { }

                var st = new System.Diagnostics.StackTrace(true).ToString();
                if (InventoryUiDebugEnabled)
                {
                    Debug.Log(
                        "[InventoryUI TRACE] ClearRows() frame=" + Time.frameCount +
                        " contentChildCountBefore=" + diagChildCountBefore +
                        " snapshotCount=" + diagSnapCount +
                        "\n" + st,
                        this);
                }
            }
            catch { }
#endif

            var templateTf = rowTemplate != null ? rowTemplate.transform : null;

            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                var child = contentRoot.GetChild(i);
                if (child == null) continue;
                if (templateTf != null && child == templateTf) continue;
                Destroy(child.gameObject);
            }

            _spawnedRows.Clear();
            _spawnedSlotViews.Clear();
        }

        private int FindSelectedSlotIndexInVisibleStacks(List<(string itemId, int count, ItemDefinition def)> visibleStacks, string selectedItemId)
        {
            if (string.IsNullOrWhiteSpace(selectedItemId) || visibleStacks == null)
                return -1;

            for (int i = 0; i < visibleStacks.Count && i < InventoryGridSlots; i++)
            {
                if (string.Equals(visibleStacks[i].itemId, selectedItemId, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private void UpdateSelectionVisualsBySlotIndex(int selectedIndexOrMinus1)
        {
            // Iterate instantiated slot views and set selection state.
            for (int i = 0; i < _spawnedSlotViews.Count; i++)
            {
                var row = _spawnedSlotViews[i];
                if (row == null) continue;
                row.SetSelected(i == selectedIndexOrMinus1);
            }
        }

        private void UpdateSelectionVisuals()
        {
            if (contentRoot == null)
                return;

            // Requirement: iterate all PlayerInventoryRowUI instances under contentRoot.
            try
            {
                var rows = contentRoot.GetComponentsInChildren<PlayerInventoryRowUI>(includeInactive: false);
                if (rows == null)
                    return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[INV][SEL APPLY] applying selectedSlot={_selectedSlotIndex} to rows={rows.Length}", this);
#endif

                for (int i = 0; i < rows.Length; i++)
                {
                    var row = rows[i];
                    if (row == null) continue;
                    row.SetSelected(row.SlotIndex == _selectedSlotIndex);
                }
            }
            catch
            {
                // Best-effort only.
            }
        }

        private void EnsureInventory()
        {
            var inv = ResolveInventory(out var source);
            _inventorySource = source;

            if (_inventory == inv)
                return;

            // Swap subscription when inventory instance changes.
            if (_inventory != null)
                _inventory.Changed -= OnInventoryChanged;

            _inventory = inv;

            if (_inventory != null && _isOpen)
            {
                _inventory.Changed -= OnInventoryChanged;
                _inventory.Changed += OnInventoryChanged;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_isOpen)
            {
                try
                {
                    var id = _inventory != null ? _inventory.GetInstanceID() : 0;
                    var goName = _inventory != null ? _inventory.gameObject.name : "(null)";
                    if (InventoryUiDebugEnabled) Debug.Log($"[PlayerInventoryUI] Open resolved inventory instanceId={id} goPath='{goName}'", this);
                }
                catch { }
            }
#endif
        }

        // IMPORTANT: This resolver matches your current project approach; keep it.
        // If you later finalize a centralized resolver class, you can simplify this to call it.
        private PlayerInventory ResolveInventory(out string source)
        {
            source = null;

            // Centralized resolver: keep UI and gameplay (Fishing/Gathering/Loot) on the same PlayerInventory instance.
            try
            {
                var inv = Game.Systems.PlayerInventoryResolver.GetOrFindWithDiagnostics(out source);
                if (inv != null)
                    return inv;
            }
            catch
            {
                // Fall back to legacy resolver below.
                source = null;
            }

            // 1) Prefer player authority chain.
            try
            {
                if (_inputAuthority != null)
                {
                    var inv = _inputAuthority.GetComponentInParent<PlayerInventory>();
                    if (inv == null) inv = _inputAuthority.GetComponentInChildren<PlayerInventory>();
                    if (inv != null)
                    {
                        source = "PlayerInputAuthority(chain)";
                        return inv;
                    }
                }
            }
            catch { }

            // 2) Try best-by-items among active inventories.
            try
            {
#if UNITY_2022_2_OR_NEWER
                var all = FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None);
#else
                var all = FindObjectsOfType<PlayerInventory>();
#endif
                if (all != null && all.Length > 0)
                {
                    PlayerInventory best = null;
                    int bestTotalItems = -1;
                    int bestStacks = -1;

                    foreach (var inv in all)
                    {
                        if (inv == null || !inv.isActiveAndEnabled) continue;

                        int stacks = 0;
                        int totalItems = 0;

                        try
                        {
                            var snap = inv.GetAllItemsSnapshot();
                            if (snap != null)
                            {
                                stacks = snap.Count;
                                foreach (var kv in snap)
                                    totalItems += Mathf.Max(0, kv.Value);
                            }
                        }
                        catch { }

                        if (totalItems > bestTotalItems || (totalItems == bestTotalItems && stacks > bestStacks))
                        {
                            best = inv;
                            bestTotalItems = totalItems;
                            bestStacks = stacks;
                        }
                    }

                    if (best != null)
                    {
                        source = all.Length == 1 ? "FindObjects(single)" : "FindObjects(best-by-items)";
                        return best;
                    }
                }
            }
            catch { }

            // 3) Last resort.
            try
            {
#if UNITY_2022_2_OR_NEWER
                var inv = FindFirstObjectByType<PlayerInventory>();
#else
                var inv = FindObjectOfType<PlayerInventory>();
#endif
                if (inv != null)
                {
                    source = "FindFirstObjectByType";
                    return inv;
                }
            }
            catch { }

            return null;
        }

        private void EnsureScrollRectWiring()
        {
            if (scrollRect == null)
                return;

            // Try to infer contentRoot if missing.
            try
            {
                if (scrollRect.content == null && contentRoot != null)
                    scrollRect.content = contentRoot;

                if (scrollRect.content == null && contentRoot == null)
                    contentRoot = scrollRect.content;
            }
            catch { }

            // Infer viewport if missing.
            try
            {
                if (scrollRect.viewport == null)
                {
                    RectTransform candidate = null;

                    try
                    {
                        var p = contentRoot != null ? contentRoot.parent as RectTransform : null;
                        if (p != null && (p.name == "Viewport" || p.GetComponent<Mask>() != null || p.GetComponent<RectMask2D>() != null))
                            candidate = p;
                    }
                    catch { }

                    if (candidate == null)
                    {
                        try
                        {
                            var t = scrollRect.transform.Find("Viewport") as RectTransform;
                            if (t != null) candidate = t;
                        }
                        catch { }
                    }

                    if (candidate != null)
                        scrollRect.viewport = candidate;
                }

                if (scrollRect.viewport != null)
                    MakeViewportTransparent(scrollRect.viewport);
            }
            catch { }

            scrollRect.horizontal = false;
            scrollRect.vertical = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_isOpen && !_loggedScrollWiringForThisOpen)
            {
                _loggedScrollWiringForThisOpen = true;
                try
                {
                    var vp = scrollRect.viewport;
                    var vpName = vp != null ? vp.name : "(null)";
                    if (InventoryUiDebugEnabled) Debug.Log($"[PlayerInventoryUI] ScrollRect wiring viewport={vpName} content={(scrollRect.content != null ? scrollRect.content.name : "(null)")} scrollRect={(scrollRect != null ? scrollRect.name : "(null)")}", this);

                    if (vp != null)
                        if (InventoryUiDebugEnabled) Debug.Log($"[PlayerInventoryUI] Viewport Rect anchors=({vp.anchorMin}->{vp.anchorMax}) sizeDelta={vp.sizeDelta} offsetMin={vp.offsetMin} offsetMax={vp.offsetMax} pivot={vp.pivot}", this);

                    if (contentRoot != null)
                        if (InventoryUiDebugEnabled) Debug.Log($"[PlayerInventoryUI] Content Rect anchors=({contentRoot.anchorMin}->{contentRoot.anchorMax}) pos={contentRoot.anchoredPosition} sizeDelta={contentRoot.sizeDelta} offsetMin={contentRoot.offsetMin} offsetMax={contentRoot.offsetMax} pivot={contentRoot.pivot}", this);
                }
                catch { }
            }
#endif
        }

        private void BringListToFront()
        {
            try
            {
                if (scrollRect == null) return;
                var t = scrollRect.transform;
                // Bring the whole scroll view to the front of its parent (Unity UI draws later siblings on top)
                t.SetAsLastSibling();

                // Also bring viewport/content just in case theyâ€™re nested under a weird layout wrapper
                if (scrollRect.viewport != null) scrollRect.viewport.SetAsLastSibling();
                if (contentRoot != null) contentRoot.SetAsLastSibling();
            }
            catch { }
        }

        /// <summary>
        /// Hard-enforces a known-good ScrollView layout so rows can't exist-but-not-render.
        /// This intentionally overrides bad inspector values during dev.
        /// </summary>
        private void EnsureScrollViewLayoutHard()
        {
            if (scrollRect == null || contentRoot == null)
                return;

            // Viewport must stretch and must mask.
            if (scrollRect.viewport != null)
            {
                var vp = scrollRect.viewport;
                try
                {
                    vp.anchorMin = new Vector2(0f, 0f);
                    vp.anchorMax = new Vector2(1f, 1f);
                    vp.pivot = new Vector2(0.5f, 0.5f);
                    vp.offsetMin = Vector2.zero;
                    vp.offsetMax = Vector2.zero;
                }
                catch { }

                var vpGo = vp.gameObject;

                // Force Viewport masking to RectMask2D (remove Mask).
                try
                {
                    var mask = vpGo.GetComponent<Mask>();
                    if (mask != null)
                    {
                        if (Application.isPlaying) Destroy(mask);
                        else DestroyImmediate(mask);
                    }
                }
                catch { }

                try
                {
                    if (vpGo.GetComponent<RectMask2D>() == null)
                        vpGo.AddComponent<RectMask2D>();

                    // RectMask2D does not require an Image. If one exists, force it fully transparent
                    // to avoid a one-frame white flash on open.
                    var img = vpGo.GetComponent<Image>();
                    if (img != null)
                    {
                        var c = img.color;
                        if (c.a > 0.001f)
                            img.color = new Color(c.r, c.g, c.b, 0f);
                    }
                }
                catch { }
            }

            // Content should stretch (GridLayoutGroup will align items).
            try
            {
                contentRoot.anchorMin = new Vector2(0f, 0f);
                contentRoot.anchorMax = new Vector2(1f, 1f);
                contentRoot.pivot = new Vector2(0.5f, 0.5f);
                contentRoot.offsetMin = Vector2.zero;
                contentRoot.offsetMax = Vector2.zero;
            }
            catch { }

            // Layout components on Content: fixed 4x7 grid.
            try
            {
                // Remove list layout components if present.
                var vlg = contentRoot.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    // IMPORTANT: in play mode we must remove immediately; Destroy() is deferred and will
                    // prevent adding GridLayoutGroup in the same frame.
                    try { DestroyImmediate(vlg); } catch { if (Application.isPlaying) Destroy(vlg); else DestroyImmediate(vlg); }
                }

                var csf = contentRoot.GetComponent<ContentSizeFitter>();
                if (csf != null)
                {
                    // IMPORTANT: same reasoning as above.
                    try { DestroyImmediate(csf); } catch { if (Application.isPlaying) Destroy(csf); else DestroyImmediate(csf); }
                }

                // If something still blocks, bail out (and warn once) rather than spamming the console.
                if (contentRoot.GetComponent<VerticalLayoutGroup>() != null || contentRoot.GetComponent<ContentSizeFitter>() != null)
                {
                    if (!_warnedContentLayoutConflict)
                    {
                        _warnedContentLayoutConflict = true;
                        Debug.LogWarning("[PlayerInventoryUI] ContentRoot still has list-layout components; cannot ensure GridLayoutGroup this frame.", this);
                    }
                    return;
                }

                var grid = contentRoot.GetComponent<GridLayoutGroup>();
                if (grid == null) grid = contentRoot.gameObject.AddComponent<GridLayoutGroup>();

                // Border around the grid pane (not per-slot lines).
                EnsureGridPaneBorder(scrollRect.viewport);

                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = InventoryGridColumns;
                grid.startAxis = GridLayoutGroup.Axis.Horizontal;
                grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
                grid.childAlignment = TextAnchor.UpperLeft;
                grid.spacing = new Vector2(4f, 4f);
                grid.padding = new RectOffset(8, 8, 8, 8);

                // Compute a TRUE square cell size that fits 4x7 in the viewport.
                float cell = 64f;
                try
                {
                    var vp = scrollRect.viewport;
                    if (vp != null)
                    {
                        float padX = grid.padding.left + grid.padding.right;
                        float padY = grid.padding.top + grid.padding.bottom;
                        float availW = Mathf.Max(0f, vp.rect.width - padX - grid.spacing.x * (InventoryGridColumns - 1));
                        float availH = Mathf.Max(0f, vp.rect.height - padY - grid.spacing.y * (InventoryGridRows - 1));

                        float cw = Mathf.Floor(availW / InventoryGridColumns);
                        float ch = Mathf.Floor(availH / InventoryGridRows);
                        cell = Mathf.Clamp(Mathf.Floor(Mathf.Min(cw, ch)), 48f, 220f);
                    }
                }
                catch { }

                // Pixel-perfect (prefer even) to keep 1px/2px outlines consistent.
                int size = Mathf.FloorToInt(cell);
                size = Mathf.Clamp(size, 48, 220);
                if ((size % 2) == 1) size -= 1;
                if (size < 48) size = 48;
                cell = size;

                grid.cellSize = new Vector2(cell, cell);

                // No scrolling for a fixed 4x7 inventory.
                scrollRect.horizontal = false;
                scrollRect.vertical = false;
            }
            catch { }

            // Ensure scrollRect.content is contentRoot.
            try
            {
                if (scrollRect.content != contentRoot)
                    scrollRect.content = contentRoot;
            }
            catch { }
        }

        private static void EnsureGridPaneBorder(RectTransform viewport)
        {
            if (viewport == null)
                return;

            try
            {
                // Create (or reuse) a simple border overlay in the viewport.
                Transform borderTf = null;
                try { borderTf = viewport.Find("GridPaneBorder"); } catch { }

                GameObject borderGo;
                if (borderTf == null)
                {
                    borderGo = new GameObject("GridPaneBorder", typeof(RectTransform));
                    borderGo.transform.SetParent(viewport, false);
                }
                else
                {
                    borderGo = borderTf.gameObject;
                }

                var brt = borderGo.GetComponent<RectTransform>();
                brt.anchorMin = Vector2.zero;
                brt.anchorMax = Vector2.one;
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;
                brt.pivot = new Vector2(0.5f, 0.5f);

                // Keep border above content but not blocking interaction.
                borderGo.transform.SetAsLastSibling();

                const float thickness = 1f;
                var lineColor = new Color(1f, 1f, 1f, 0.65f);

                EnsureBorderLine(borderGo.transform, "TopLine", lineColor,
                    anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
                    pivot: new Vector2(0.5f, 1f), sizeDelta: new Vector2(0f, thickness), anchoredPos: Vector2.zero);

                EnsureBorderLine(borderGo.transform, "BottomLine", lineColor,
                    anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0f),
                    pivot: new Vector2(0.5f, 0f), sizeDelta: new Vector2(0f, thickness), anchoredPos: Vector2.zero);

                EnsureBorderLine(borderGo.transform, "LeftLine", lineColor,
                    anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0f, 1f),
                    pivot: new Vector2(0f, 0.5f), sizeDelta: new Vector2(thickness, 0f), anchoredPos: Vector2.zero);

                EnsureBorderLine(borderGo.transform, "RightLine", lineColor,
                    anchorMin: new Vector2(1f, 0f), anchorMax: new Vector2(1f, 1f),
                    pivot: new Vector2(1f, 0.5f), sizeDelta: new Vector2(thickness, 0f), anchoredPos: Vector2.zero);
            }
            catch
            {
                // Best-effort only.
            }
        }

        private static void EnsureBorderLine(
            Transform parent,
            string name,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 sizeDelta,
            Vector2 anchoredPos)
        {
            if (parent == null || string.IsNullOrWhiteSpace(name))
                return;

            Transform tf = null;
            try { tf = parent.Find(name); } catch { }

            GameObject go;
            if (tf == null)
            {
                go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
            }
            else
            {
                go = tf.gameObject;
                if (go.GetComponent<Image>() == null)
                    go.AddComponent<Image>();
            }

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPos;

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        /// <summary>
        /// This is the missing piece in your current setup:
        /// your rows exist and are clickable, but their graphics/text are invisible.
        /// We force them visible here to eliminate alpha/canvasgroup regressions.
        /// </summary>
        private void ForceRowVisible(GameObject rowGo)
        {
            if (rowGo == null) return;

            // CanvasGroups can hide everything.
            try
            {
                var cgs = rowGo.GetComponentsInChildren<CanvasGroup>(true);
                foreach (var cg in cgs)
                {
                    if (cg == null) continue;
                    cg.alpha = 1f;
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                    cg.ignoreParentGroups = false;
                }
            }
            catch { }

            // Force all UI Graphics visible.
            try
            {
                var graphics = rowGo.GetComponentsInChildren<Graphic>(true);
                foreach (var g in graphics)
                {
                    if (g == null) continue;
                    var c = g.color;
                    if (c.a < 0.99f)
                        g.color = new Color(c.r, c.g, c.b, 1f);
                    g.raycastTarget = true; // keep clicking working
                    g.enabled = true;
                    if (!g.gameObject.activeSelf) g.gameObject.SetActive(true);
                }
            }
            catch { }

            // Force all TMP text visible and non-tiny.
            try
            {
                var tmps = rowGo.GetComponentsInChildren<TMP_Text>(true);
                foreach (var t in tmps)
                {
                    if (t == null) continue;

                    // Ensure active/enabled.
                    if (!t.gameObject.activeSelf) t.gameObject.SetActive(true);
                    t.enabled = true;

                    // Ensure alpha is 1.
                    t.alpha = 1f;

                    // Ensure color alpha is 1.
                    var c = t.color;
                    if (c.a < 0.99f)
                        t.color = new Color(c.r, c.g, c.b, 1f);

                    // If the text is effectively black-on-black, nudge to white (dev-safe).
                    // We do this only if it's very dark.
                    if (t.color.r < 0.15f && t.color.g < 0.15f && t.color.b < 0.15f)
                        t.color = new Color(1f, 1f, 1f, 1f);

                    // Font size floor.
                    if (t.fontSize < 18f)
                        t.fontSize = 22f;
                }
            }
            catch { }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // One-time â€œwhat was invisible?â€ report per open.
            if (_isOpen && !_loggedFirstRowVisibilityThisOpen)
            {
                _loggedFirstRowVisibilityThisOpen = true;
                try
                {
                    var anyTmp = rowGo.GetComponentInChildren<TMP_Text>(true);
                    var anyGraphic = rowGo.GetComponentInChildren<Graphic>(true);
                    var cg = rowGo.GetComponentInChildren<CanvasGroup>(true);

                    if (InventoryUiDebugEnabled)
                    {
                        Debug.Log(
                            $"[PlayerInventoryUI] FirstRowVisibilityReport row='{rowGo.name}' " +
                            $"hasTMP={(anyTmp != null)} tmpText='{(anyTmp != null ? anyTmp.text : "(null)")}' tmpColor={(anyTmp != null ? anyTmp.color.ToString() : "(n/a)")} tmpAlpha={(anyTmp != null ? anyTmp.alpha : -1f)} " +
                            $"hasGraphic={(anyGraphic != null)} graphicColor={(anyGraphic != null ? anyGraphic.color.ToString() : "(n/a)")} " +
                            $"hasCanvasGroup={(cg != null)} cgAlpha={(cg != null ? cg.alpha : -1f)}",
                            this);
                    }
                }
                catch { }
            }
#endif
        }

        private static bool EqualsIgnoreCase(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static TMP_Text FindTmpByNameHint(GameObject rootGo, params string[] nameHints)
        {
            if (rootGo == null || nameHints == null || nameHints.Length == 0)
                return null;

            TMP_Text best = null;

            try
            {
                var tmps = rootGo.GetComponentsInChildren<TMP_Text>(true);
                foreach (var t in tmps)
                {
                    if (t == null) continue;
                    var n = t.gameObject != null ? t.gameObject.name : null;
                    if (string.IsNullOrWhiteSpace(n)) continue;

                    for (int i = 0; i < nameHints.Length; i++)
                    {
                        var hint = nameHints[i];
                        if (string.IsNullOrWhiteSpace(hint)) continue;
                        if (EqualsIgnoreCase(n, hint) || n.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                            return t;
                    }

                    // fallback if nothing matches: take the largest font TMP as best guess
                    if (best == null || t.fontSize > best.fontSize)
                        best = t;
                }
            }
            catch { }

            return best;
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = Mathf.Clamp01(a);
            return c;
        }

        private Color GetRowBaseColor()
        {
            try
            {
                if (rowTemplate != null)
                {
                    var img = rowTemplate.GetComponent<Image>();
                    if (img != null)
                    {
                        var c = img.color;
                        // Avoid fully transparent base; default to white if it is.
                        if (c.a <= 0.001f)
                            return Color.white;
                        return new Color(c.r, c.g, c.b, 1f);
                    }
                }
            }
            catch { }

            return Color.white;
        }

        private Image EnsureRowBackgroundImage(GameObject rowGo)
        {
            if (rowGo == null) return null;

            Image img = null;
            try { img = rowGo.GetComponent<Image>(); } catch { }
            if (img == null)
            {
                try { img = rowGo.AddComponent<Image>(); } catch { }
            }

            if (img != null)
            {
                try
                {
                    img.raycastTarget = true;

                    // Ensure a sane baseline RGB, but leave alpha to styling.
                    var baseC = GetRowBaseColor();
                    img.color = new Color(baseC.r, baseC.g, baseC.b, img.color.a);
                }
                catch { }
            }

            return img;
        }

        private static void EnsureSelectedBar(GameObject rowGo, bool enabled, Color baseColor)
        {
            if (rowGo == null) return;

            Transform barTf = null;
            try { barTf = rowGo.transform.Find("SelectedBar"); } catch { }

            if (!enabled)
            {
                if (barTf != null)
                {
                    try { barTf.gameObject.SetActive(false); } catch { }
                }
                return;
            }

            GameObject barGo = null;
            if (barTf == null)
            {
                try
                {
                    barGo = new GameObject("SelectedBar", typeof(RectTransform), typeof(Image));
                    barGo.transform.SetParent(rowGo.transform, false);
                    barTf = barGo.transform;
                }
                catch { return; }
            }
            else
            {
                barGo = barTf.gameObject;
            }

            try { barGo.SetActive(true); } catch { }

            try
            {
                var rt = barTf as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = new Vector2(4f, 0f);
                }
            }
            catch { }

            try
            {
                var img = barGo.GetComponent<Image>();
                if (img != null)
                {
                    img.raycastTarget = false;
                    img.color = WithAlpha(baseColor, 0.90f);
                }
            }
            catch { }
        }

        private static void ConfigureButtonColors(Button btn, Color normal)
        {
            if (btn == null) return;

            try
            {
                var cb = btn.colors;

                // Subtle adjustments: slightly brighter on hover, slightly darker on press.
                var highlighted = normal;
                highlighted.r = Mathf.Clamp01(highlighted.r + 0.06f);
                highlighted.g = Mathf.Clamp01(highlighted.g + 0.06f);
                highlighted.b = Mathf.Clamp01(highlighted.b + 0.06f);
                highlighted.a = Mathf.Clamp01(highlighted.a + 0.06f);

                var pressed = normal;
                pressed.r = Mathf.Clamp01(pressed.r - 0.05f);
                pressed.g = Mathf.Clamp01(pressed.g - 0.05f);
                pressed.b = Mathf.Clamp01(pressed.b - 0.05f);
                pressed.a = Mathf.Clamp01(pressed.a - 0.06f);

                cb.normalColor = normal;
                cb.highlightedColor = highlighted;
                cb.pressedColor = pressed;
                cb.selectedColor = highlighted;
                cb.disabledColor = new Color(normal.r, normal.g, normal.b, Mathf.Clamp01(normal.a * 0.6f));

                btn.colors = cb;

                if (btn.transition == Selectable.Transition.None)
                    btn.transition = Selectable.Transition.ColorTint;
            }
            catch { }
        }

        private static void ApplyTextReadability(GameObject rowGo)
        {
            if (rowGo == null) return;

            // Grid slots are icon+count only; don't modify TMP sizing/colors here.
            try
            {
                var rowUi = rowGo.GetComponent<PlayerInventoryRowUI>();
                if (rowUi != null && rowUi.IsGridMode)
                    return;
            }
            catch { }

            try
            {
                var nameTmp = FindTmpByNameHint(rowGo, "Name", "ItemName", "Title", "Label");
                if (nameTmp != null)
                {
                    // Keep names readable but don't blow up grid tiles.
                    if (nameTmp.fontSize < 16f)
                        nameTmp.fontSize = 16f;

                    var c = nameTmp.color;
                    nameTmp.color = new Color(c.r, c.g, c.b, 1f);

                    var m = nameTmp.margin;
                    if (m.x < 12f) m.x = 12f;
                    nameTmp.margin = m;
                }

                var countTmp = FindTmpByNameHint(rowGo, "Count", "Qty", "Quantity", "Stack");
                if (countTmp != null)
                {
                    if (countTmp.fontSize < 14f)
                        countTmp.fontSize = 14f;
                    var c2 = countTmp.color;
                    countTmp.color = new Color(c2.r, c2.g, c2.b, 0.95f);
                }
            }
            catch { }
        }

        private void ApplyRowVisualStyling(GameObject rowGo, int rowIndex, bool isSelected)
        {
            if (rowGo == null) return;

            bool isGrid = false;
            try
            {
                var rowUi = rowGo.GetComponent<PlayerInventoryRowUI>();
                isGrid = rowUi != null && rowUi.IsGridMode;
            }
            catch { }

            // Normal shading
            const float evenAlpha = 0.18f;
            const float oddAlpha = 0.26f;
            const float selectedAlpha = 0.45f;

            float normalAlpha = (rowIndex % 2 == 0) ? evenAlpha : oddAlpha;
            float a = isSelected ? selectedAlpha : normalAlpha;

            var bg = EnsureRowBackgroundImage(rowGo);
            var baseColor = GetRowBaseColor();

            // Raycast fix: tiles must always have a raycastable graphic.
            try
            {
                if (bg != null)
                    bg.raycastTarget = true;
            }
            catch { }

            // For grid tiles, background/hover is handled by PlayerInventoryRowUI.
            // Driving bg.color here can cause one-frame flashes on open.
            if (!isGrid && bg != null)
            {
                try
                {
                    bg.color = WithAlpha(new Color(baseColor.r, baseColor.g, baseColor.b, 1f), a);
                }
                catch { }
            }

            // Optional accent bar for selected
            if (!isGrid)
                EnsureSelectedBar(rowGo, isSelected, baseColor);
            else
                EnsureSelectedBar(rowGo, false, baseColor);

            // Grid slot border (subtle) + stronger when selected.
            if (isGrid)
            {
                // Border/hover/selection visuals are handled by PlayerInventoryRowUI.
            }

            // Hover styling only if there is a Button
            try
            {
                var btn = rowGo.GetComponent<Button>();
                if (btn != null)
                {
                    if (bg != null && btn.targetGraphic == null)
                        btn.targetGraphic = bg;

                    if (isGrid)
                    {
                        // Prevent Unity's Selectable tinting from flashing tiles.
                        btn.transition = Selectable.Transition.None;
                    }
                    else
                    {
                        ConfigureButtonColors(btn, bg != null ? bg.color : WithAlpha(baseColor, a));
                    }
                }
            }
            catch { }

            ApplyTextReadability(rowGo);
        }

        private void UpdateSelectionHighlightVisuals()
        {
            if (contentRoot == null)
                return;

            int rowIndex = 0;
            var templateTf = rowTemplate != null ? rowTemplate.transform : null;

            for (int i = 0; i < contentRoot.childCount; i++)
            {
                var ch = contentRoot.GetChild(i);
                if (ch == null) continue;
                if (templateTf != null && ch == templateTf) continue;

                var go = ch.gameObject;
                bool isSelected = !string.IsNullOrWhiteSpace(_selectedItemId) && go != null && go.name == $"Row_{_selectedItemId}";
                ApplyRowVisualStyling(go, rowIndex, isSelected);

                try
                {
                    var rowUi = go != null ? go.GetComponent<PlayerInventoryRowUI>() : null;
                    if (rowUi != null)
                    {
                        // Grid selection is slot-index based (stable), not name-based.
                        if (rowUi.IsGridMode)
                            rowUi.SetSelected(rowUi.SlotIndex == _selectedSlotIndex);
                        else
                            rowUi.SetSelected(isSelected);
                    }
                }
                catch { }

                rowIndex++;
            }
        }

        private void EnsureCanvasVisibility()
        {
            // Make sure root is active and visible if someone zeroed a CanvasGroup higher up.
            try
            {
                if (root == null) return;

                var cgs = root.GetComponentsInChildren<CanvasGroup>(true);
                foreach (var cg in cgs)
                {
                    if (cg == null) continue;
                    if (cg.alpha < 1f) cg.alpha = 1f;
                    cg.blocksRaycasts = true;
                    cg.interactable = true;
                }
            }
            catch { }
        }

        private static void MakeViewportTransparent(RectTransform viewport)
        {
            if (viewport == null)
                return;

            try
            {
                var img = viewport.GetComponent<Image>();
                if (img == null)
                    return;

                var c = img.color;
                if (c.a > 0.001f)
                    img.color = new Color(c.r, c.g, c.b, 0f);
            }
            catch { }
        }

        private Dictionary<string, ItemDefinition> BuildItemDefinitionIndex()
        {
            var map = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);

            static bool HasIcon(ItemDefinition d)
            {
                try { return d != null && d.icon != null; } catch { return false; }
            }

            try
            {
#if UNITY_2022_2_OR_NEWER
                var shops = FindObjectsByType<Abyss.Shop.MerchantShop>(FindObjectsSortMode.None);
#else
                var shops = FindObjectsOfType<Abyss.Shop.MerchantShop>();
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
                            if (string.IsNullOrWhiteSpace(id))
                                continue;

                            if (!map.TryGetValue(id, out var existing) || existing == null)
                            {
                                map[id] = def;
                            }
                            else
                            {
                                // Prefer the definition that actually has an icon assigned.
                                if (!HasIcon(existing) && HasIcon(def))
                                    map[id] = def;
                            }
                        }
                    }
                }

                var loaded = Resources.FindObjectsOfTypeAll<ItemDefinition>();
                if (loaded != null)
                {
                    foreach (var def in loaded)
                    {
                        if (def == null) continue;
                        var id = ResolveItemId(def);
                        if (string.IsNullOrWhiteSpace(id))
                            continue;

                        if (!map.TryGetValue(id, out var existing) || existing == null)
                        {
                            map[id] = def;
                        }
                        else
                        {
                            if (!HasIcon(existing) && HasIcon(def))
                                map[id] = def;
                        }
                    }
                }
            }
            catch { }

            return map;
        }

        private ItemDefinition ResolveItemDefinition(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            _itemDefById ??= BuildItemDefinitionIndex();
            if (_itemDefById != null && _itemDefById.TryGetValue(itemId, out var def))
            {
                // If we found a definition but it doesn't have an icon, it may be a stale/duplicate instance.
                // Rebuild the index once and retry.
                try
                {
                    if (def != null && def.icon == null)
                    {
                        _itemDefById = BuildItemDefinitionIndex();
                        if (_itemDefById != null && _itemDefById.TryGetValue(itemId, out var refreshed))
                            return refreshed;
                    }
                }
                catch { }

                return def;
            }

            return null;
        }

        private string ResolveItemId(ItemDefinition def)
        {
            if (def == null) return null;

            // Best-effort: match your existing item ID convention.
            // If your ItemDefinition has a canonical ID field, prefer that.
            try
            {
                // Common patterns: def.id, def.itemId, def.name
                var t = def.GetType();

                var f = t.GetField("id");
                if (f != null)
                {
                    var v = f.GetValue(def) as string;
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }

                var p = t.GetProperty("id");
                if (p != null && p.PropertyType == typeof(string))
                {
                    var v = p.GetValue(def) as string;
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }

                f = t.GetField("itemId");
                if (f != null)
                {
                    var v = f.GetValue(def) as string;
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }

                p = t.GetProperty("itemId");
                if (p != null && p.PropertyType == typeof(string))
                {
                    var v = p.GetValue(def) as string;
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }
            }
            catch { }

            // Fallback: sanitized name.
            return def.name != null ? def.name.Trim() : null;
        }

        private bool WasTogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            try
            {
                return Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame;
            }
            catch { return false; }
#else
            return Input.GetKeyDown(KeyCode.I);
#endif
        }

        private void EnsureTabs()
        {
            if (scrollRect == null || root == null)
                return;

            if (tabsRoot == null)
            {
                // Try to find an existing tabs root.
                var found = root.transform.Find("Tabs");
                if (found != null)
                    tabsRoot = found as RectTransform;
            }

            if (tabsRoot == null)
            {
                // Create a minimal tab bar above the ScrollRect.
                var parent = scrollRect.transform.parent as RectTransform;
                if (parent == null)
                    parent = root.transform as RectTransform;
                if (parent == null)
                    return;

                var tabsGo = new GameObject("Tabs", typeof(RectTransform));
                tabsRoot = tabsGo.GetComponent<RectTransform>();
                tabsRoot.SetParent(parent, false);

                // Insert just above the scroll rect if possible.
                try
                {
                    int idx = scrollRect.transform.GetSiblingIndex();
                    tabsRoot.SetSiblingIndex(Mathf.Max(0, idx));
                }
                catch { }

                var hlg = tabsGo.AddComponent<HorizontalLayoutGroup>();
                hlg.childForceExpandHeight = false;
                hlg.childForceExpandWidth = true;
                hlg.childControlHeight = true;
                hlg.childControlWidth = true;
                hlg.spacing = 6f;
                hlg.padding = new RectOffset(8, 8, 6, 6);

                var fitter = tabsGo.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // Background tint to separate from list.
                var bg = tabsGo.AddComponent<Image>();
                bg.color = new Color(0.08f, 0.08f, 0.08f, 0.65f);
            }

            if (_tabWeapons != null && _tabMaterials != null && _tabConsumables != null && _tabSkilling != null)
            {
                RefreshTabVisuals();
                return;
            }

            CreateOrBindTabButtons();
            RefreshTabVisuals();
        }

        private void CreateOrBindTabButtons()
        {
            if (tabsRoot == null)
                return;

            // If children already exist, attempt to bind by name.
            if (tabsRoot.childCount > 0)
            {
                _tabWeapons = FindButtonUnder(tabsRoot, "Tab_WeaponsGear") ?? _tabWeapons;
                _tabMaterials = FindButtonUnder(tabsRoot, "Tab_Materials") ?? _tabMaterials;
                _tabConsumables = FindButtonUnder(tabsRoot, "Tab_Consumables") ?? _tabConsumables;
                _tabSkilling = FindButtonUnder(tabsRoot, "Tab_Skilling") ?? _tabSkilling;
            }

            _tabWeapons ??= CreateTabButton(tabsRoot, "Tab_WeaponsGear", "Weapons/Gear", out _tabWeaponsText);
            _tabMaterials ??= CreateTabButton(tabsRoot, "Tab_Materials", "Materials", out _tabMaterialsText);
            _tabConsumables ??= CreateTabButton(tabsRoot, "Tab_Consumables", "Consumables", out _tabConsumablesText);
            _tabSkilling ??= CreateTabButton(tabsRoot, "Tab_Skilling", "Skilling", out _tabSkillingText);

            WireTab(_tabWeapons, InventoryTab.WeaponsGear);
            WireTab(_tabMaterials, InventoryTab.Materials);
            WireTab(_tabConsumables, InventoryTab.Consumables);
            WireTab(_tabSkilling, InventoryTab.Skilling);
        }

        private void WireTab(Button button, InventoryTab tab)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (_activeTab == tab) return;
                _activeTab = tab;
                RefreshTabVisuals();
                RefreshList();
                RefreshDetails();
            });
        }

        private void RefreshTabVisuals()
        {
            ApplyTabVisual(_tabWeapons, _tabWeaponsText, _activeTab == InventoryTab.WeaponsGear);
            ApplyTabVisual(_tabMaterials, _tabMaterialsText, _activeTab == InventoryTab.Materials);
            ApplyTabVisual(_tabConsumables, _tabConsumablesText, _activeTab == InventoryTab.Consumables);
            ApplyTabVisual(_tabSkilling, _tabSkillingText, _activeTab == InventoryTab.Skilling);
        }

        private static void ApplyTabVisual(Button button, TMP_Text label, bool selected)
        {
            if (button != null)
            {
                var img = button.GetComponent<Image>();
                if (img != null)
                    img.color = selected
                        ? new Color(0.18f, 0.18f, 0.18f, 0.95f)
                        : new Color(0.12f, 0.12f, 0.12f, 0.80f);
            }

            if (label != null)
                label.color = selected ? new Color(0.95f, 0.95f, 0.95f, 1f) : new Color(0.75f, 0.75f, 0.75f, 1f);
        }

        private Button CreateTabButton(RectTransform parent, string name, string label, out TMP_Text labelText)
        {
            labelText = null;
            if (parent == null) return null;

            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.12f, 0.12f, 0.12f, 0.80f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 34f;
            le.minHeight = 34f;
            le.flexibleHeight = 0f;
            le.flexibleWidth = 1f;

            // Label
            var textGo = new GameObject("Label", typeof(RectTransform));
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.SetParent(rt, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.fontSize = 18f;
            tmp.color = new Color(0.75f, 0.75f, 0.75f, 1f);
            if (titleText != null && titleText.font != null)
                tmp.font = titleText.font;

            labelText = tmp;
            return btn;
        }

        private static Button FindButtonUnder(RectTransform parent, string childName)
        {
            if (parent == null) return null;
            var t = parent.Find(childName);
            if (t == null) return null;
            return t.GetComponent<Button>();
        }

        private bool PassesTabFilter(ItemDefinition def, string itemId)
        {
            // Unknown items: keep visible under Weapons/Gear so they don't disappear.
            if (def == null)
                return _activeTab == InventoryTab.WeaponsGear;

            return _activeTab switch
            {
                InventoryTab.WeaponsGear => def.itemType == AbyssItemType.Weapon || def.itemType == AbyssItemType.Misc,
                InventoryTab.Materials => def.itemType == AbyssItemType.Workshop,
                InventoryTab.Consumables => def.itemType == AbyssItemType.Consumable,
                InventoryTab.Skilling => def.itemType == AbyssItemType.Skilling,
                _ => true
            };
        }
    }
}

```

## 11) Full Source: PlayerInventoryRowUI.cs
Path: c:\Users\Jordon\UnityAIAgent\Assets\Abyss\Inventory\PlayerInventoryRowUI.cs
```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Abyss.Items;
using Abyssbound.Loot;

using AbyssItemRarity = Abyss.Items.ItemRarity;

namespace Abyss.Inventory
{
    public sealed class PlayerInventoryRowUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private const bool INV_DEBUG = false;

        [Header("Fill (Raycast Target)")]
        [SerializeField] private Image background;

        [Header("Borders (Legacy / Disabled)")]
        [SerializeField] private Image hoverBorderImage;
        [SerializeField] private Image selectedBorderImage;

        [Header("Borders (4-Line, Grid Mode)")]
        [SerializeField] private RectTransform borderRoot;
        [SerializeField] private Image borderTop;
        [SerializeField] private Image borderBottom;
        [SerializeField] private Image borderLeft;
        [SerializeField] private Image borderRight;

        [Header("Optional Visuals")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image rarityStrip;

        [Header("Text")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text countText;

        [SerializeField] private Button button;

        private Action _onClick;
        private bool _colorsInitialized;
        private Color _listBaseColor;
        private Color _listHoverColor;

        private bool _isHovered;
        private bool _isSelected;
        private bool _isGridMode;

        private bool _hasItem;
        private int _boundCount;

        private Color _rarityBorderRgb = Color.white;

        // [INV] Debug context (set by PlayerInventoryUI during RefreshList)
        private int _debugSlotIndex = -1;
        private bool _debugIsEmpty;

        private ItemTooltipTrigger _tooltipTrigger;

        public int SlotIndex { get; private set; } = -1;
        public bool IsEmpty => !_hasItem;

        public void SetSlotIndex(int index)
        {
            SlotIndex = index;
        }

        private Color _baseNameColor;

        // Grid visuals (explicit OSRS-style rules)
        private static readonly Color GridEmptyFill = new(0.18f, 0.18f, 0.18f, 1f);
        private static readonly Color GridOccupiedFill = new(0.24f, 0.24f, 0.24f, 1f);
        // User requirement: Selected > Hover > Normal with explicit alphas.
        private static readonly Color GridBorderNormalColor = new(1f, 1f, 1f, 0.18f);
        private static readonly Color GridHoverBorderColor = new(1f, 1f, 1f, 0.45f);
        private static readonly Color GridSelectedBorderColor = new(1f, 1f, 1f, 0.90f);
        private const float GridSelectedFillBrighten = 0.06f;

        // User requirement: consistent borders across all resolutions.
        // Force thickness to 2px (avoid 1px subpixel disappearance).
        private const float BorderThicknessNormal = 2f;
        private const float BorderThicknessSelected = 2f;

        private bool _gridBordersInitialized;

        private static Sprite s_WhiteSprite;

        // ItemDefinition lookup cache (covers cases where PlayerInventoryUI provides only a string id/name).
        // We keep this local to the row so icon binding can't silently fail.
        private static Dictionary<string, ItemDefinition> s_ItemDefByKey;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly HashSet<string> s_WarnedMissingIconImageByItemId = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> s_WarnedMissingIconByItemId = new(StringComparer.OrdinalIgnoreCase);

        private static void WarnOnce(HashSet<string> cache, string key, string message, UnityEngine.Object context)
        {
            try
            {
                if (cache == null) return;
                key ??= "(null)";
                if (cache.Contains(key)) return;
                cache.Add(key);
                Debug.LogWarning(message, context);
            }
            catch { }
        }
#endif

        private static ItemDefinition ResolveItemDefinitionFallback(string keyA, string keyB)
        {
            try
            {
                if (s_ItemDefByKey == null)
                {
                    s_ItemDefByKey = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);

                    var defs = Resources.FindObjectsOfTypeAll<ItemDefinition>();
                    if (defs != null)
                    {
                        foreach (var def in defs)
                        {
                            if (def == null) continue;

                            string id = null;
                            try { id = def.itemId; } catch { }

                            string displayName = null;
                            try { displayName = def.displayName; } catch { }

                            TryAddDefKey(id, def);
                            TryAddDefKey(displayName, def);
                            TryAddDefKey(def.name, def);
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(keyA) && s_ItemDefByKey.TryGetValue(keyA, out var a) && a != null)
                    return a;

                if (!string.IsNullOrWhiteSpace(keyB) && s_ItemDefByKey.TryGetValue(keyB, out var b) && b != null)
                    return b;
            }
            catch { }

            return null;
        }

        private static void TryAddDefKey(string key, ItemDefinition def)
        {
            if (string.IsNullOrWhiteSpace(key) || def == null)
                return;

            try
            {
                // Prefer a definition that has an icon.
                if (s_ItemDefByKey.TryGetValue(key, out var existing) && existing != null)
                {
                    bool existingHasIcon = false;
                    bool defHasIcon = false;
                    try { existingHasIcon = existing.icon != null; } catch { }
                    try { defHasIcon = def.icon != null; } catch { }

                    if (!existingHasIcon && defHasIcon)
                        s_ItemDefByKey[key] = def;

                    return;
                }

                s_ItemDefByKey[key] = def;
            }
            catch { }
        }

        private static Sprite GetOrCreateWhiteSprite()
        {
            if (s_WhiteSprite != null)
                return s_WhiteSprite;

            // Guaranteed runtime sprite (avoids built-in UI resources that may not exist).
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false, linear: false)
            {
                name = "RuntimeWhiteSpriteTex",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            s_WhiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            s_WhiteSprite.name = "RuntimeWhiteSprite";
            s_WhiteSprite.hideFlags = HideFlags.HideAndDontSave;
            return s_WhiteSprite;
        }

        public void SetDebugContext(int slotIndex, bool isEmpty)
        {
            _debugSlotIndex = slotIndex;
            _debugIsEmpty = isEmpty;
        }

        private void Awake()
        {
            EnsureDefaultColors();
            if (_isGridMode)
                EnsureGridBorderLines();
            RenderState();

            EnsureTooltipTrigger();

            if (nameText != null)
                _baseNameColor = nameText.color;

            _rarityBorderRgb = Color.white;
        }

        private void OnEnable()
        {
            // Snap to integer pixel grid (both immediately and after layout has positioned us).
            // GridLayoutGroup positions are applied after instantiation, so do both.
            try
            {
                var rt = transform as RectTransform;
                if (rt != null)
                {
                    SnapToPixelGrid(rt);
                    StartCoroutine(SnapAfterLayout(rt));
                }
            }
            catch { }
        }

        private IEnumerator SnapAfterLayout(RectTransform rt)
        {
            yield return null;

            try
            {
                if (rt != null)
                    SnapToPixelGrid(rt);

                if (_isGridMode)
                {
                    EnsureGridBorderLines();
                    RenderState();
                }
            }
            catch { }
        }

        private static void SnapToPixelGrid(RectTransform rt)
        {
            if (rt == null) return;
            var p = rt.anchoredPosition;
            p.x = Mathf.Round(p.x);
            p.y = Mathf.Round(p.y);
            rt.anchoredPosition = p;
        }

        public void Bind(ItemDefinition def, string fallbackItemId, int count, Action onClick)
        {
            ResolveBackgroundImage();
            EnsureDefaultColors();

            if (_isGridMode)
                EnsureGridElements();

            string display = def != null
                ? (string.IsNullOrWhiteSpace(def.displayName) ? ResolveFallbackName(def, fallbackItemId) : def.displayName)
                : (string.IsNullOrWhiteSpace(fallbackItemId) ? "(Unknown)" : fallbackItemId);

            // Rolled loot items: show base item display name if available.
            if (def == null && !string.IsNullOrWhiteSpace(fallbackItemId))
            {
                try
                {
                    var reg = Abyssbound.Loot.LootRegistryRuntime.GetOrCreate();
                    if (reg != null && reg.TryResolveDisplay(fallbackItemId, out var lootName, out var lootIcon))
                    {
                        if (!string.IsNullOrWhiteSpace(lootName))
                            display = lootName;
                    }
                }
                catch { }
            }

            // Grid mode requirement: no item name text inside the cell.
            if (!_isGridMode)
            {
                if (nameText != null) nameText.text = display;
            }
            else
            {
                if (nameText != null) nameText.text = string.Empty;
            }

            int safeCount = Mathf.Max(0, count);
            _boundCount = safeCount;
            _hasItem = def != null || !string.IsNullOrWhiteSpace(fallbackItemId);

            // Stack binding (no try/catch; do not toggle GameObjects).
            if (countText != null)
            {
                bool showStack = safeCount > 1;
                countText.text = showStack ? $"x{safeCount}" : string.Empty;
                countText.enabled = showStack;
            }

            // Resolve ItemDefinition even if caller only supplies a string key (some inventories use display name keys).
            var resolvedDef = def != null ? def : ResolveItemDefinitionFallback(fallbackItemId, display);

            // Tooltip binding (hover): uses the resolved definition where possible.
            try
            {
                EnsureTooltipTrigger();
                if (_tooltipTrigger != null)
                    _tooltipTrigger.BindInventoryItem(resolvedDef, fallbackItemId, safeCount);
            }
            catch { }

            Sprite icon = null;
            AbyssItemRarity rarity = AbyssItemRarity.Common;
            try
            {
                if (resolvedDef != null)
                {
                    icon = resolvedDef.icon;
                    rarity = ItemRarityVisuals.Normalize(resolvedDef.rarity);
                }
            }
            catch { }

            // Default: keep the existing white border visuals.
            _rarityBorderRgb = Color.white;
            try
            {
                if (_hasItem)
                {
                    if (resolvedDef != null)
                    {
                        var c = RarityColorMap.GetColorOrDefault(rarity, Color.white);
                        _rarityBorderRgb = new Color(c.r, c.g, c.b, 1f);
                    }
                    else if (!string.IsNullOrWhiteSpace(fallbackItemId))
                    {
                        var reg = LootRegistryRuntime.GetOrCreate();
                        if (reg != null && reg.TryGetRolledInstance(fallbackItemId, out var inst) && inst != null)
                        {
                            var c = RarityColorMap.GetColorOrDefault(inst.rarityId, Color.white);
                            _rarityBorderRgb = new Color(c.r, c.g, c.b, 1f);
                        }
                    }
                }
            }
            catch { _rarityBorderRgb = Color.white; }

            if (icon == null && resolvedDef == null && !string.IsNullOrWhiteSpace(fallbackItemId))
            {
                try
                {
                    var reg = Abyssbound.Loot.LootRegistryRuntime.GetOrCreate();
                    if (reg != null && reg.TryResolveDisplay(fallbackItemId, out _, out var lootIcon) && lootIcon != null)
                        icon = lootIcon;
                }
                catch { }
            }

            // IMPORTANT: Non-rolled items (e.g., fish/materials) may exist only as Loot V2 base items.
            // Try resolving a direct base item by id so icons render for ALL items that have ItemDefinitionSO.icon.
            if (icon == null && !string.IsNullOrWhiteSpace(fallbackItemId))
            {
                try
                {
                    var reg = Abyssbound.Loot.LootRegistryRuntime.GetOrCreate();
                    if (reg != null && reg.TryGetItem(fallbackItemId, out var baseItem) && baseItem != null && baseItem.icon != null)
                        icon = baseItem.icon;
                }
                catch { }
            }

            // Icon binding (no try/catch; do not toggle GameObjects).
            if (iconImage != null)
            {
                bool hasIcon = icon != null;
                iconImage.enabled = hasIcon;
                iconImage.sprite = hasIcon ? icon : null;
                if (hasIcon)
                    iconImage.color = Color.white;

                // Ensure consistent rendering.
                iconImage.type = Image.Type.Simple;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
#pragma warning disable CS0162
            if (INV_DEBUG)
            {
                string id = !string.IsNullOrWhiteSpace(fallbackItemId)
                    ? fallbackItemId
                    : (resolvedDef != null ? resolvedDef.itemId : display);

                bool iconEnabled = iconImage != null && iconImage.enabled;
                bool iconActive = iconImage != null && iconImage.gameObject != null && iconImage.gameObject.activeInHierarchy;
                bool stackEnabled = countText != null && countText.enabled;
                bool stackActive = countText != null && countText.gameObject != null && countText.gameObject.activeInHierarchy;

                Debug.Log(
                    $"[INVDBG][ROW BIND] slot={SlotIndex} id='{id}' count={safeCount} iconNull={(icon == null)} iconEnabled={iconEnabled} iconActive={iconActive} stackEnabled={stackEnabled} stackActive={stackActive}",
                    this);
            }
#pragma warning restore CS0162
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_hasItem)
            {
                string id = null;
                try { id = resolvedDef != null && !string.IsNullOrWhiteSpace(resolvedDef.itemId) ? resolvedDef.itemId : fallbackItemId; } catch { id = fallbackItemId; }
                if (string.IsNullOrWhiteSpace(id)) id = display;

                if (iconImage == null)
                    WarnOnce(s_WarnedMissingIconImageByItemId, id, $"[INV][ICON] IconImage NULL for {id} row={gameObject.name}", this);

                if (icon == null)
                    WarnOnce(s_WarnedMissingIconByItemId, id, $"[INV][ICON] icon NULL for {id} row={gameObject.name}", this);
            }
#endif

            ApplyVisuals(icon, rarity);

            _onClick = onClick;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => _onClick?.Invoke());
            }

            _isHovered = false;
            RenderState();
        }

        public void BindEmpty()
        {
            BindEmpty(null);
        }

        public void BindEmpty(Action onClick)
        {
            ResolveBackgroundImage();
            EnsureDefaultColors();

            if (_isGridMode)
                EnsureGridElements();

            if (nameText != null) nameText.text = string.Empty;
            if (countText != null)
            {
                countText.text = string.Empty;
                countText.enabled = false;
            }

            _boundCount = 0;
            _hasItem = false;
            _rarityBorderRgb = Color.white;

            // Clear tooltip binding for empty slots.
            try
            {
                EnsureTooltipTrigger();
                if (_tooltipTrigger != null)
                    _tooltipTrigger.BindInventoryItem(null, null, 0);
            }
            catch { }

            _isHovered = false;
            _isSelected = false;

            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }

            if (rarityStrip != null)
            {
                rarityStrip.enabled = false;
                var rc = rarityStrip.color;
                rarityStrip.color = new Color(rc.r, rc.g, rc.b, 0f);
            }

            _onClick = onClick;
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                if (_onClick != null)
                    button.onClick.AddListener(() => _onClick?.Invoke());

                // In grid mode we want empty slots hoverable + clickable.
                button.interactable = _isGridMode;
            }

            RenderState();
        }

        private void EnsureTooltipTrigger()
        {
            if (_tooltipTrigger != null)
                return;

            try
            {
                _tooltipTrigger = GetComponent<ItemTooltipTrigger>();
                if (_tooltipTrigger == null)
                    _tooltipTrigger = gameObject.AddComponent<ItemTooltipTrigger>();
            }
            catch
            {
                _tooltipTrigger = null;
            }
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            RenderState();
        }

        public void SetHovered(bool hovered)
        {
            _isHovered = hovered;
            RenderState();
        }

        public void SetGridMode(bool enabled)
        {
            _isGridMode = enabled;

            if (_isGridMode)
            {
                ResolveBackgroundImage();
                EnsureGridElements();
                EnsureGridBorderLines();
                DisableLegacyBorders();
            }

            // In grid mode we prefer icon + count (name shown in details panel).
            if (nameText != null)
                nameText.gameObject.SetActive(!enabled);

            // Also hard-hide any other TMP labels in the slot (prevents cramped names if a template has extras).
            if (enabled)
            {
                try
                {
                    var tmps = GetComponentsInChildren<TMP_Text>(true);
                    if (tmps != null)
                    {
                        for (int i = 0; i < tmps.Length; i++)
                        {
                            var t = tmps[i];
                            if (t == null) continue;
                            if (t == countText) continue;
                            t.gameObject.SetActive(false);
                        }
                    }
                }
                catch { }
            }

            // Rarity strip is now driven by binding (enabled for items, disabled for empty).

            if (enabled)
            {
                try
                {
                    if (iconImage != null)
                    {
                        var rt = iconImage.rectTransform;
                        // ~70% size with padding so it doesn't touch borders.
                        rt.anchorMin = new Vector2(0.15f, 0.15f);
                        rt.anchorMax = new Vector2(0.85f, 0.85f);
                        rt.pivot = new Vector2(0.5f, 0.5f);
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                        iconImage.preserveAspect = true;
                        iconImage.raycastTarget = false;
                    }

                    if (countText != null)
                    {
                        var rt = countText.rectTransform;
                        rt.anchorMin = new Vector2(1f, 0f);
                        rt.anchorMax = new Vector2(1f, 0f);
                        rt.pivot = new Vector2(1f, 0f);
                        rt.anchoredPosition = new Vector2(-4f, 4f);

                        if (countText.fontSize > 16f)
                            countText.fontSize = 16f;

                        countText.textWrappingMode = TextWrappingModes.NoWrap;
                        countText.alignment = TextAlignmentOptions.BottomRight;
                        countText.raycastTarget = false;

                        var shadow = countText.GetComponent<Shadow>();
                        if (shadow == null) shadow = countText.gameObject.AddComponent<Shadow>();
                        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
                        shadow.effectDistance = new Vector2(1f, -1f);
                        shadow.useGraphicAlpha = true;
                    }
                }
                catch { }
            }
            else
            {
                if (button != null)
                    button.interactable = true;

                if (nameText != null)
                    nameText.gameObject.SetActive(true);
            }

            RenderState();
        }

        private void EnsureGridElements()
        {
            // Ensure we have an icon + count text for grid slots.
            // Keep this lightweight: only searches/creates when references are missing.

            try
            {
                if (iconImage == null)
                {
                    var t = transform.Find("Icon");
                    if (t != null) iconImage = t.GetComponent<Image>();
                }
            }
            catch { }

            try
            {
                if (countText == null)
                {
                    var t = transform.Find("Count");
                    if (t != null) countText = t.GetComponent<TMP_Text>();
                }
            }
            catch { }

            // If the template doesn't have these (older scenes), create them.
            if (iconImage == null)
            {
                try
                {
                    var go = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.layer = gameObject.layer;
                    go.transform.SetParent(transform, false);
                    iconImage = go.GetComponent<Image>();
                    iconImage.preserveAspect = true;
                }
                catch { }
            }

            if (countText == null)
            {
                try
                {
                    var go = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
                    go.layer = gameObject.layer;
                    go.transform.SetParent(transform, false);
                    var tmp = go.GetComponent<TextMeshProUGUI>();
                    tmp.text = string.Empty;
                    tmp.fontSize = 16f;
                    tmp.alignment = TextAlignmentOptions.BottomRight;
                    tmp.raycastTarget = false;
                    tmp.textWrappingMode = TextWrappingModes.NoWrap;
                    countText = tmp;
                }
                catch { }
            }

            try { if (iconImage != null) iconImage.raycastTarget = false; } catch { }
            try { if (countText != null) countText.raycastTarget = false; } catch { }
        }

        private void ApplyVisuals(Sprite icon, AbyssItemRarity rarity)
        {
            // Icon + stack binding is owned by Bind(). ApplyVisuals only handles
            // non-critical visuals (rarity strip/name tint) and should not toggle active state.

            rarity = ItemRarityVisuals.Normalize(rarity);
            var rarityColor = RarityColorMap.GetColorOrDefault(rarity, Color.white);

            if (rarityStrip != null)
            {
                rarityStrip.enabled = _hasItem;
                rarityStrip.color = _hasItem
                    ? rarityColor
                    : new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0f);
                if (!rarityStrip.gameObject.activeSelf)
                    rarityStrip.gameObject.SetActive(true);
            }
            else if (nameText != null)
            {
                if (_baseNameColor.a <= 0f)
                    _baseNameColor = nameText.color;

                nameText.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, _baseNameColor.a);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            RenderState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            RenderState();
        }

        private void EnsureDefaultColors()
        {
            if (_colorsInitialized)
                return;

            var baseC = background != null ? background.color : default;
            if (baseC.a <= 0f)
                baseC = new Color(0.10f, 0.10f, 0.10f, 0.85f);

            _listBaseColor = baseC;
            _listHoverColor = AddRgb(baseC, 0.10f);

            _colorsInitialized = true;
        }

        private Image ResolveBackgroundImage()
        {
            if (background != null)
                return background;

            // Prefer a dedicated inner fill image.
            try
            {
                var t = transform.Find("InnerBackground");
                if (t != null)
                {
                    var img = t.GetComponent<Image>();
                    if (img != null)
                    {
                        background = img;
                        return background;
                    }
                }
            }
            catch { }

            background = GetComponent<Image>();
            if (background != null)
                return background;

            // Otherwise, find the first suitable Image in children.
            try
            {
                var images = GetComponentsInChildren<Image>(true);
                if (images != null)
                {
                    for (int i = 0; i < images.Length; i++)
                    {
                        var img = images[i];
                        if (img == null) continue;
                        if (img == iconImage) continue;
                        if (img == rarityStrip) continue;
                        if (img == hoverBorderImage) continue;
                        if (img == selectedBorderImage) continue;
                        if (string.Equals(img.name, "HoverBorderImage", StringComparison.OrdinalIgnoreCase)) continue;
                        if (string.Equals(img.name, "SelectedBorderImage", StringComparison.OrdinalIgnoreCase)) continue;
                        if (string.Equals(img.name, "InnerBackground", StringComparison.OrdinalIgnoreCase)) continue;

                        background = img;
                        return background;
                    }
                }
            }
            catch { }

            return null;
        }

        private Image EnsureChildImage(string name)
        {
            return EnsureChildImage(transform, name);
        }

        private Image EnsureChildImage(Transform parent, string name)
        {
            try
            {
                var t = parent != null ? parent.Find(name) : null;
                if (t != null)
                {
                    var existing = t.GetComponent<Image>();
                    if (existing != null)
                        return existing;
                }
            }
            catch { }

            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gameObject.layer;
            go.transform.SetParent(parent != null ? parent : transform, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return go.GetComponent<Image>();
        }

        private RectTransform EnsureBorderRoot()
        {
            if (borderRoot != null)
                return borderRoot;

            try
            {
                var t = transform.Find("BorderRoot");
                if (t != null)
                {
                    borderRoot = t as RectTransform;
                    if (borderRoot == null) borderRoot = t.GetComponent<RectTransform>();
                    return borderRoot;
                }
            }
            catch { }

            var go = new GameObject("BorderRoot", typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            borderRoot = go.GetComponent<RectTransform>();
            borderRoot.anchorMin = Vector2.zero;
            borderRoot.anchorMax = Vector2.one;
            borderRoot.pivot = new Vector2(0.5f, 0.5f);
            borderRoot.offsetMin = Vector2.zero;
            borderRoot.offsetMax = Vector2.zero;
            return borderRoot;
        }

        private void EnsureGridInnerBackground()
        {
            // Avoid offsetting the slot root RectTransform (GridLayoutGroup owns it).
            // Force every slot to use the SAME fill hierarchy: an InnerBackground child inset by 2px.
            // This prevents mixed hierarchies (root Image vs child Image) from causing border inconsistencies.

            var inner = EnsureChildImage("InnerBackground");
            inner.raycastTarget = true;
            inner.type = Image.Type.Simple;

            // If we already had a background reference that isn't InnerBackground, migrate its visuals.
            try
            {
                if (background != null && background != inner)
                {
                    inner.sprite = background.sprite;
                    inner.material = background.material;
                    inner.color = background.color;
                }
                else
                {
                    var rootImg = GetComponent<Image>();
                    if (rootImg != null && rootImg != inner)
                    {
                        inner.sprite = rootImg.sprite;
                        inner.material = rootImg.material;
                        inner.color = rootImg.color;
                    }
                }
            }
            catch { }

            // Hide any root Image so it cannot cover the border lines.
            try
            {
                var rootImg = GetComponent<Image>();
                if (rootImg != null)
                {
                    rootImg.color = new Color(1f, 1f, 1f, 0f);
                    rootImg.raycastTarget = false;
                }
            }
            catch { }

            try
            {
                var rt = inner.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(2f, 2f);
                rt.offsetMax = new Vector2(-2f, -2f);
            }
            catch { }

            // Keep fill underneath everything else.
            try { inner.transform.SetAsFirstSibling(); } catch { }

            background = inner;
        }

        private Image FindOrCreateUniqueBorderLine(string name, RectTransform desiredParent, ref Image cache)
        {
            if (cache != null)
            {
                try
                {
                    if (desiredParent != null && cache.transform.parent != desiredParent)
                        cache.transform.SetParent(desiredParent, false);
                    cache.gameObject.SetActive(true);
                }
                catch { }
                return cache;
            }

            Image found = null;
            try
            {
                var images = GetComponentsInChildren<Image>(true);
                if (images != null)
                {
                    for (int i = 0; i < images.Length; i++)
                    {
                        var img = images[i];
                        if (img == null) continue;
                        if (!string.Equals(img.name, name, StringComparison.Ordinal))
                            continue;

                        if (found == null)
                        {
                            found = img;
                        }
                        else
                        {
                            // Disable duplicates to ensure every slot uses ONE consistent border set.
                            try { img.gameObject.SetActive(false); } catch { }
                        }
                    }
                }
            }
            catch { }

            if (found == null)
            {
                found = EnsureChildImage(desiredParent != null ? desiredParent.transform : transform, name);
            }

            cache = found;
            try
            {
                if (desiredParent != null && cache.transform.parent != desiredParent)
                    cache.transform.SetParent(desiredParent, false);
                cache.gameObject.SetActive(true);
            }
            catch { }

            return cache;
        }


        private void DisableLegacyBorders()
        {
            // Disable any previously attempted border approaches to prevent conflicts.
            try
            {
                if (hoverBorderImage != null) hoverBorderImage.enabled = false;
                if (selectedBorderImage != null) selectedBorderImage.enabled = false;

                var legacyHover = transform.Find("HoverBorderImage");
                if (legacyHover != null)
                {
                    var img = legacyHover.GetComponent<Image>();
                    if (img != null) img.enabled = false;
                }

                var legacySelected = transform.Find("SelectedBorderImage");
                if (legacySelected != null)
                {
                    var img = legacySelected.GetComponent<Image>();
                    if (img != null) img.enabled = false;
                }

                var oldHover = transform.Find("HoverBorder");
                if (oldHover != null) oldHover.gameObject.SetActive(false);

                var oldSelected = transform.Find("SelectedBorder");
                if (oldSelected != null) oldSelected.gameObject.SetActive(false);

                // Hard-disable by name as well (prefab leftovers / older runtime systems).
                var legacyNames = new[] { "HoverBorderImage", "SelectedBorderImage", "HoverBorder", "SelectedBorder" };
                for (int i = 0; i < legacyNames.Length; i++)
                {
                    var t = transform.Find(legacyNames[i]);
                    if (t != null) t.gameObject.SetActive(false);
                }
            }
            catch { }

            // Ensure any Outline on the root/background is off.
            try
            {
                var o1 = GetComponent<Outline>();
                if (o1 != null) o1.enabled = false;
            }
            catch { }

            try
            {
                if (background != null)
                {
                    var o2 = background.GetComponent<Outline>();
                    if (o2 != null) o2.enabled = false;
                }
            }
            catch { }
        }

        private void EnsureGridBorderLines()
        {
            if (!_isGridMode)
                return;

            ResolveBackgroundImage();
            EnsureGridInnerBackground();

            if (!_gridBordersInitialized)
            {
                // Make sure no legacy systems remain active.
                DisableLegacyBorders();
                _gridBordersInitialized = true;
            }

            var br = EnsureBorderRoot();
            try { if (br != null) br.transform.SetAsLastSibling(); } catch { }

            // IMPORTANT: reuse ANY existing border lines (even if they were created under a different parent)
            // and disable duplicates. This guarantees a single border construction codepath for every slot.
            borderTop = FindOrCreateUniqueBorderLine("BorderTop", br, ref borderTop);
            borderBottom = FindOrCreateUniqueBorderLine("BorderBottom", br, ref borderBottom);
            borderLeft = FindOrCreateUniqueBorderLine("BorderLeft", br, ref borderLeft);
            borderRight = FindOrCreateUniqueBorderLine("BorderRight", br, ref borderRight);

            SetupLine(borderTop);
            SetupLine(borderBottom);
            SetupLine(borderLeft);
            SetupLine(borderRight);

            // Ensure line geometry (anchors/pivots) is correct.
            try
            {
                ConfigureTop(borderTop.rectTransform, BorderThicknessNormal);
                ConfigureBottom(borderBottom.rectTransform, BorderThicknessNormal);
                ConfigureLeft(borderLeft.rectTransform, BorderThicknessNormal);
                ConfigureRight(borderRight.rectTransform, BorderThicknessNormal);
            }
            catch { }

            // Draw borders above everything but never block clicks.
            try
            {
                if (borderTop != null) borderTop.transform.SetAsLastSibling();
                if (borderBottom != null) borderBottom.transform.SetAsLastSibling();
                if (borderLeft != null) borderLeft.transform.SetAsLastSibling();
                if (borderRight != null) borderRight.transform.SetAsLastSibling();
            }
            catch { }
        }

        private static void SetupLine(Image img)
        {
            if (img == null)
                return;

            img.enabled = true;
            img.raycastTarget = false;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;

            // Ensure a valid source sprite so the Image actually draws.
            if (img.sprite == null)
                img.sprite = GetOrCreateWhiteSprite();
        }

        private static void ConfigureTop(RectTransform rt, float thickness)
        {
            if (rt == null) return;
            thickness = Mathf.Max(1f, Mathf.Round(thickness));
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = new Vector2(0f, -thickness);
            rt.offsetMax = new Vector2(0f, 0f);
        }

        private static void ConfigureBottom(RectTransform rt, float thickness)
        {
            if (rt == null) return;
            thickness = Mathf.Max(1f, Mathf.Round(thickness));
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, thickness);
        }

        private static void ConfigureLeft(RectTransform rt, float thickness)
        {
            if (rt == null) return;
            thickness = Mathf.Max(1f, Mathf.Round(thickness));
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(thickness, 0f);
        }

        private static void ConfigureRight(RectTransform rt, float thickness)
        {
            if (rt == null) return;
            thickness = Mathf.Max(1f, Mathf.Round(thickness));
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = new Vector2(-thickness, 0f);
            rt.offsetMax = new Vector2(0f, 0f);
        }

        private void RenderState()
        {
            ResolveBackgroundImage();
            if (background == null)
                return;

            EnsureDefaultColors();

            bool isEmpty = !_hasItem;
            bool isHovered = _isHovered;
            bool isSelected = _isSelected;

            // Count visibility.
            if (countText != null)
            {
                if (isEmpty)
                {
                    if (!string.IsNullOrEmpty(countText.text))
                        countText.text = string.Empty;
                    countText.enabled = false;
                }
                else
                {
                    bool showStack = _boundCount > 1;
                    countText.text = showStack ? $"x{Mathf.Max(0, _boundCount)}" : string.Empty;
                    countText.enabled = showStack;
                }
            }

            // Icon visibility.
            if (_isGridMode && iconImage != null && isEmpty)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }

            // Ensure raycast target graphic.
            try
            {
                if (button == null)
                    button = GetComponent<Button>();

                if (_isGridMode)
                {
                    EnsureGridBorderLines();
                    DisableLegacyBorders();

                    // Force borders to top every frame.
                    try { if (borderRoot != null) borderRoot.transform.SetAsLastSibling(); } catch { }

                    background.raycastTarget = true;
                    if (button != null) button.targetGraphic = background;
                }
                else
                {
                    if (background != null)
                        background.raycastTarget = true;
                    if (button != null && button.targetGraphic == null)
                        button.targetGraphic = background;

                    // Hide grid borders in list mode.
                    if (borderTop != null) borderTop.enabled = false;
                    if (borderBottom != null) borderBottom.enabled = false;
                    if (borderLeft != null) borderLeft.enabled = false;
                    if (borderRight != null) borderRight.enabled = false;
                }
            }
            catch { }

            if (!_isGridMode)
            {
                background.color = isHovered ? _listHoverColor : _listBaseColor;
                return;
            }

            // Grid fill.
            var baseFill = isEmpty ? GridEmptyFill : GridOccupiedFill;
            background.color = isSelected ? AddRgb(baseFill, GridSelectedFillBrighten) : baseFill;

            // Borders: Selected > Hover > Normal. Geometry stays constant.
            try
            {
                float thickness = BorderThicknessNormal;

                if (borderTop != null)
                {
                    borderTop.enabled = true;
                    ConfigureTop(borderTop.rectTransform, thickness);
                }
                if (borderBottom != null)
                {
                    borderBottom.enabled = true;
                    ConfigureBottom(borderBottom.rectTransform, thickness);
                }
                if (borderLeft != null)
                {
                    borderLeft.enabled = true;
                    ConfigureLeft(borderLeft.rectTransform, thickness);
                }
                if (borderRight != null)
                {
                    borderRight.enabled = true;
                    ConfigureRight(borderRight.rectTransform, thickness);
                }

                float a = GridBorderNormalColor.a;
                if (isSelected) a = GridSelectedBorderColor.a;
                else if (isHovered) a = GridHoverBorderColor.a;

                // Keep existing interaction alphas, but tint hue by rarity for occupied slots.
                var rgb = isEmpty ? Color.white : _rarityBorderRgb;
                Color borderColor = new Color(rgb.r, rgb.g, rgb.b, a);

                if (borderTop != null) borderTop.color = borderColor;
                if (borderBottom != null) borderBottom.color = borderColor;
                if (borderLeft != null) borderLeft.color = borderColor;
                if (borderRight != null) borderRight.color = borderColor;
            }
            catch { }
        }

        private static Color AddRgb(Color c, float amount)
        {
            return new Color(
                Mathf.Clamp01(c.r + amount),
                Mathf.Clamp01(c.g + amount),
                Mathf.Clamp01(c.b + amount),
                c.a);
        }

        private static string ResolveFallbackName(ItemDefinition def, string fallbackItemId)
        {
            if (def == null) return string.IsNullOrWhiteSpace(fallbackItemId) ? "(Unknown)" : fallbackItemId;
            if (!string.IsNullOrWhiteSpace(def.itemId)) return def.itemId;
            if (!string.IsNullOrWhiteSpace(def.name)) return def.name;
            return string.IsNullOrWhiteSpace(fallbackItemId) ? "(Unknown)" : fallbackItemId;
        }

        public Button Button => button;
        public bool CanShowIcon => iconImage != null;
        public bool IsGridMode => _isGridMode;
    }
}

```

## 12) Full Source: BuildPlayerInventoryUIEditor.cs
Path: c:\Users\Jordon\UnityAIAgent\Assets\Editor\BuildPlayerInventoryUIEditor.cs
```csharp
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Abyss.Inventory.EditorTools
{
    public static class BuildPlayerInventoryUIEditor
    {
            [MenuItem("Tools/Abyssbound/Content/UI/Build Player Inventory UI (Editor)")]
        public static void Build()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("Run this in Edit Mode (not Play Mode).");
                return;
            }

            if (TryUpgradeExisting(out var upgradeSummary))
            {
                Debug.Log(upgradeSummary);
                return;
            }

            BuildFresh();
        }

        private static void BuildFresh()
        {
            DestroySceneObjectsByName("PlayerInventoryUICanvas");
            DestroySceneObjectsByName("PlayerInventoryUIRoot");
            DestroySceneObjectsByName("PlayerInventoryUI");

            var canvasGO = new GameObject("PlayerInventoryUICanvas", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(canvasGO, "Create PlayerInventoryUICanvas");

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // keep above most gameplay UI
            canvasGO.AddComponent<GraphicRaycaster>();

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            StretchFullScreen(canvasGO.GetComponent<RectTransform>());

            var root = new GameObject("PlayerInventoryUIRoot", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Create PlayerInventoryUIRoot");
            root.transform.SetParent(canvasGO.transform, false);
            StretchFullScreen(root.GetComponent<RectTransform>());

            // Backdrop
            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(root.transform, false);
            StretchFullScreen(backdrop.GetComponent<RectTransform>());
            backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            // Panel
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var panelRt = panel.GetComponent<RectTransform>();
            SetAnchors(panelRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            panelRt.sizeDelta = new Vector2(1100, 650);
            panel.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

            // UI controller host
            var uiGO = new GameObject("PlayerInventoryUI", typeof(RectTransform), typeof(Abyss.Inventory.PlayerInventoryUI));
            uiGO.transform.SetParent(canvasGO.transform, false);
            StretchFullScreen(uiGO.GetComponent<RectTransform>());

            // MVP: equip button should equip visually only (no inventory consumption yet).
            try
            {
                if (uiGO.GetComponent<Abyss.Equipment.InventoryEquipButtonMvpAdapter>() == null)
                    uiGO.AddComponent<Abyss.Equipment.InventoryEquipButtonMvpAdapter>();
            }
            catch { }

            // Header title
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(panel.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            SetAnchors(titleRt, new Vector2(0.04f, 0.90f), new Vector2(0.50f, 0.98f));
            SetOffsets(titleRt, 0, 0, 0, 0);
            var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "Inventory";
            titleTmp.fontSize = 36;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = Color.white;
            titleTmp.alignment = TextAlignmentOptions.Left;

            // Character tabs (Inventory / Equipment)
            var characterTabs = new GameObject("CharacterTabs", typeof(RectTransform));
            characterTabs.transform.SetParent(panel.transform, false);
            var tabsRt = characterTabs.GetComponent<RectTransform>();
            SetAnchors(tabsRt, new Vector2(0.52f, 0.90f), new Vector2(0.88f, 0.98f));
            SetOffsets(tabsRt, 0, 0, 0, 0);

            var tabInventoryGo = new GameObject("Tab_Inventory", typeof(RectTransform), typeof(Image), typeof(Button));
            tabInventoryGo.transform.SetParent(characterTabs.transform, false);
            var tabInvRt = tabInventoryGo.GetComponent<RectTransform>();
            SetAnchors(tabInvRt, new Vector2(0f, 0f), new Vector2(0.5f, 1f));
            SetOffsets(tabInvRt, 0, 0, 0, 0);
            tabInventoryGo.GetComponent<Image>().color = new Color(0.20f, 0.20f, 0.20f, 0.95f);
            var tabInvBtn = tabInventoryGo.GetComponent<Button>();
            EnsureButtonLabel(tabInventoryGo, "Inventory", 18);

            var tabEquipmentGo = new GameObject("Tab_Equipment", typeof(RectTransform), typeof(Image), typeof(Button));
            tabEquipmentGo.transform.SetParent(characterTabs.transform, false);
            var tabEqRt = tabEquipmentGo.GetComponent<RectTransform>();
            SetAnchors(tabEqRt, new Vector2(0.5f, 0f), new Vector2(1f, 1f));
            SetOffsets(tabEqRt, 0, 0, 0, 0);
            tabEquipmentGo.GetComponent<Image>().color = new Color(0.16f, 0.16f, 0.16f, 0.95f);
            var tabEqBtn = tabEquipmentGo.GetComponent<Button>();
            EnsureButtonLabel(tabEquipmentGo, "Equipment", 18);

            // Close button
            var closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(panel.transform, false);
            var closeRt = closeGo.GetComponent<RectTransform>();
            SetAnchors(closeRt, new Vector2(0.90f, 0.92f), new Vector2(0.98f, 0.98f));
            SetOffsets(closeRt, 0, 0, 0, 0);
            closeGo.GetComponent<Image>().color = Color.white;
            var closeBtn = closeGo.GetComponent<Button>();
            EnsureButtonLabel(closeGo, "X", 28);

            // Gold text
            var goldGo = new GameObject("GoldText", typeof(RectTransform), typeof(TextMeshProUGUI));
            goldGo.transform.SetParent(panel.transform, false);
            var goldRt = goldGo.GetComponent<RectTransform>();
            SetAnchors(goldRt, new Vector2(0.04f, 0.84f), new Vector2(0.50f, 0.90f));
            SetOffsets(goldRt, 0, 0, 0, 0);
            var goldTmp = goldGo.GetComponent<TextMeshProUGUI>();
            goldTmp.text = "Gold: 0";
            goldTmp.fontSize = 24;
            goldTmp.color = Color.white;
            goldTmp.alignment = TextAlignmentOptions.Left;

            // ScrollView (left)
            var scrollView = new GameObject("ItemsScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollView.transform.SetParent(panel.transform, false);
            var svRt = scrollView.GetComponent<RectTransform>();
            ConfigureSplitRegionRect(svRt, new Vector2(0.04f, 0.06f), new Vector2(0.60f, 0.84f));
            scrollView.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollView.transform, false);
            var viewportRt = viewport.GetComponent<RectTransform>();
            StretchFullScreen(viewportRt);
            viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            EnsureGridInsetPanel(viewport.transform);

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            ConfigureContentRect(contentRt);

            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 6f;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childAlignment = TextAnchor.UpperLeft;

            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            var scrollRect = scrollView.GetComponent<ScrollRect>();
            scrollRect.viewport = viewportRt;
            scrollRect.content = contentRt;
            scrollRect.horizontal = false;

            // Prevent scroll container from being shrunk by layout components.
            RemoveIfPresent<ContentSizeFitter>(scrollView);
            RemoveIfPresent<LayoutElement>(scrollView);
            RemoveIfPresent<HorizontalLayoutGroup>(scrollView);
            RemoveIfPresent<VerticalLayoutGroup>(scrollView);

            // Details (right)
            var details = new GameObject("DetailsPanel", typeof(RectTransform), typeof(Image), typeof(Abyss.Inventory.PlayerInventoryDetailsUI));
            details.transform.SetParent(panel.transform, false);
            var detailsRt = details.GetComponent<RectTransform>();
            ConfigureSplitRegionRect(detailsRt, new Vector2(0.62f, 0.06f), new Vector2(0.98f, 0.84f));
            details.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

            // Details icon
            var dIcon = new GameObject("DetailIcon", typeof(RectTransform), typeof(Image));
            dIcon.transform.SetParent(details.transform, false);
            var dIconRt = dIcon.GetComponent<RectTransform>();
            SetAnchors(dIconRt, new Vector2(0.06f, 0.80f), new Vector2(0.22f, 0.94f));
            SetOffsets(dIconRt, 0, 0, 0, 0);
            var dIconImg = dIcon.GetComponent<Image>();
            dIconImg.color = Color.white;
            dIconImg.preserveAspect = true;
            dIcon.SetActive(false);

            var dName = CreateDetailsText(details.transform, "DetailName", new Vector2(0.26f, 0.87f), new Vector2(0.94f, 0.96f), 28, FontStyles.Bold);
            var dRarity = CreateDetailsText(details.transform, "DetailRarity", new Vector2(0.26f, 0.80f), new Vector2(0.94f, 0.86f), 20, FontStyles.Normal);
            var dCount = CreateDetailsText(details.transform, "DetailCount", new Vector2(0.06f, 0.74f), new Vector2(0.94f, 0.80f), 20, FontStyles.Normal);

            var dDesc = CreateDetailsText(details.transform, "DetailDescription", new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.72f), 18, FontStyles.Normal);
            dDesc.textWrappingMode = TextWrappingModes.Normal;
            dDesc.alignment = TextAlignmentOptions.TopLeft;

            // Row template (disabled)
            var rowTemplate = BuildRowTemplate(content.transform);
            rowTemplate.SetActive(false);

            // Wire references
            var ui = uiGO.GetComponent<Abyss.Inventory.PlayerInventoryUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("root").objectReferenceValue = root;
            so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            so.FindProperty("characterInventoryTabButton").objectReferenceValue = tabInvBtn;
            so.FindProperty("characterEquipmentTabButton").objectReferenceValue = tabEqBtn;
            so.FindProperty("titleText").objectReferenceValue = titleTmp;
            so.FindProperty("goldText").objectReferenceValue = goldTmp;
            so.FindProperty("scrollRect").objectReferenceValue = scrollRect;
            so.FindProperty("contentRoot").objectReferenceValue = contentRt;
            so.FindProperty("rowTemplate").objectReferenceValue = rowTemplate.GetComponent<Abyss.Inventory.PlayerInventoryRowUI>();
            so.FindProperty("detailsUI").objectReferenceValue = details.GetComponent<Abyss.Inventory.PlayerInventoryDetailsUI>();
            so.ApplyModifiedProperties();

            var detailsSo = new SerializedObject(details.GetComponent<Abyss.Inventory.PlayerInventoryDetailsUI>());
            detailsSo.FindProperty("iconImage").objectReferenceValue = dIconImg;
            detailsSo.FindProperty("nameText").objectReferenceValue = dName;
            detailsSo.FindProperty("rarityText").objectReferenceValue = dRarity;
            detailsSo.FindProperty("countText").objectReferenceValue = dCount;
            detailsSo.FindProperty("descriptionText").objectReferenceValue = dDesc;
            detailsSo.ApplyModifiedProperties();

            // Default inactive
            root.SetActive(false);

            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[BuildPlayerInventoryUIEditor] Built Player Inventory UI.");
        }

        private static bool TryUpgradeExisting(out string summary)
        {
            summary = string.Empty;

            var all = Resources.FindObjectsOfTypeAll<Abyss.Inventory.PlayerInventoryUI>();
            if (all == null || all.Length == 0)
                return false;

            var sceneUis = all
                .Where(ui => ui != null && ui.gameObject != null)
                .Where(ui => ui.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(ui.gameObject))
                .ToArray();

            if (sceneUis.Length == 0)
                return false;

            int wired = 0;
            int created = 0;

            foreach (var ui in sceneUis)
            {
                if (ui == null) continue;

                // MVP: ensure Equip uses visual-only equipment state (no inventory consumption yet).
                try
                {
                    if (ui.GetComponent<Abyss.Equipment.InventoryEquipButtonMvpAdapter>() == null)
                    {
                        ui.gameObject.AddComponent<Abyss.Equipment.InventoryEquipButtonMvpAdapter>();
                        created++;
                    }
                }
                catch { }

                if (TryWireByName(ui, ref created))
                    wired++;
            }

            summary = $"[BuildPlayerInventoryUIEditor] Upgraded existing PlayerInventoryUI(s): uiWired={wired}/{sceneUis.Length}, sceneObjectsCreated={created}.";
            return true;
        }

        private static bool TryWireByName(Abyss.Inventory.PlayerInventoryUI ui, ref int created)
        {
            // Find the canvas/root/panel by convention.
            var canvas = GameObject.Find("PlayerInventoryUICanvas");
            var root = GameObject.Find("PlayerInventoryUIRoot");
            if (canvas == null || root == null)
                return false;

            var panel = FindDeepChild(root.transform, "Panel");
            if (panel == null)
                return false;

            var closeBtn = FindDeepChild(panel, "CloseButton")?.GetComponent<Button>();
            var title = FindDeepChild(panel, "Title")?.GetComponent<TextMeshProUGUI>();
            var gold = FindDeepChild(panel, "GoldText")?.GetComponent<TextMeshProUGUI>();

            var scrollRect = FindDeepChild(panel, "ItemsScrollView")?.GetComponent<ScrollRect>();
            var content = FindDeepChild(panel, "Content")?.GetComponent<RectTransform>();

            if (scrollRect != null)
                UpgradeScrollHierarchy(panel.gameObject, scrollRect, ref created);

            if (content != null)
            {
                EnsureListContentLayoutComponents(content, ref created);
                ConfigureContentRect(content);
            }

            if (content != null)
                EnsureListContentLayoutComponents(content, ref created);

            var detailsPanel = FindDeepChild(panel, "DetailsPanel");
            var details = detailsPanel != null ? detailsPanel.GetComponent<Abyss.Inventory.PlayerInventoryDetailsUI>() : null;

            // Ensure row template exists.
            GameObject rowTemplate = FindDeepChild(panel, "RowTemplate") != null ? FindDeepChild(panel, "RowTemplate").gameObject : null;
            if (rowTemplate == null && content != null)
            {
                rowTemplate = BuildRowTemplate(content);
                rowTemplate.name = "RowTemplate";
                rowTemplate.SetActive(false);
                created++;
            }
            else if (rowTemplate != null)
            {
                var rt = rowTemplate.GetComponent<RectTransform>();
                ConfigureRowTemplateRect(rt);

                var le = rowTemplate.GetComponent<LayoutElement>();
                if (le == null) le = rowTemplate.AddComponent<LayoutElement>();
                le.minHeight = 56f;
                le.preferredHeight = 56f;
                le.flexibleHeight = 0f;

                if (rowTemplate.activeSelf)
                    rowTemplate.SetActive(false);
            }

            // Ensure RowTemplate's PlayerInventoryRowUI has Icon wired (child named "Icon").
            try
            {
                if (rowTemplate != null)
                {
                    var rowUi = rowTemplate.GetComponent<Abyss.Inventory.PlayerInventoryRowUI>();
                    var iconImg = rowTemplate.transform.Find("Icon") != null
                        ? rowTemplate.transform.Find("Icon").GetComponent<Image>()
                        : null;

                    var rarityStripImg = rowTemplate.transform.Find("RarityStrip") != null
                        ? rowTemplate.transform.Find("RarityStrip").GetComponent<Image>()
                        : null;

                    if (rowUi != null && iconImg != null)
                    {
                        var rowSo = new SerializedObject(rowUi);
                        var p = rowSo.FindProperty("iconImage");
                        if (p != null && p.objectReferenceValue == null)
                        {
                            p.objectReferenceValue = iconImg;
                            rowSo.ApplyModifiedPropertiesWithoutUndo();
                            EditorUtility.SetDirty(rowUi);
                        }
                    }

                    if (rowUi != null && rarityStripImg != null)
                    {
                        var rowSo = new SerializedObject(rowUi);
                        var p = rowSo.FindProperty("rarityStrip");
                        if (p != null && p.objectReferenceValue == null)
                        {
                            p.objectReferenceValue = rarityStripImg;
                            rowSo.ApplyModifiedPropertiesWithoutUndo();
                            EditorUtility.SetDirty(rowUi);
                        }
                    }
                }
            }
            catch { }

            if (detailsPanel != null && details == null)
                details = detailsPanel.gameObject.AddComponent<Abyss.Inventory.PlayerInventoryDetailsUI>();

            // Ensure detail children exist.
            if (detailsPanel != null)
                EnsureDetailsChildren(detailsPanel.gameObject, ref created);

            var rootGo = root;

            var so = new SerializedObject(ui);
            if (so.FindProperty("root") != null) so.FindProperty("root").objectReferenceValue = rootGo;
            if (so.FindProperty("closeButton") != null) so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            if (so.FindProperty("titleText") != null) so.FindProperty("titleText").objectReferenceValue = title;
            if (so.FindProperty("goldText") != null) so.FindProperty("goldText").objectReferenceValue = gold;
            if (so.FindProperty("scrollRect") != null) so.FindProperty("scrollRect").objectReferenceValue = scrollRect;
            if (so.FindProperty("contentRoot") != null) so.FindProperty("contentRoot").objectReferenceValue = content;
            if (so.FindProperty("rowTemplate") != null) so.FindProperty("rowTemplate").objectReferenceValue = rowTemplate != null ? rowTemplate.GetComponent<Abyss.Inventory.PlayerInventoryRowUI>() : null;
            if (so.FindProperty("detailsUI") != null) so.FindProperty("detailsUI").objectReferenceValue = details;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (details != null)
            {
                var icon = FindDeepChild(detailsPanel, "DetailIcon")?.GetComponent<Image>();
                var dName = FindDeepChild(detailsPanel, "DetailName")?.GetComponent<TextMeshProUGUI>();
                var dRarity = FindDeepChild(detailsPanel, "DetailRarity")?.GetComponent<TextMeshProUGUI>();
                var dCount = FindDeepChild(detailsPanel, "DetailCount")?.GetComponent<TextMeshProUGUI>();
                var dDesc = FindDeepChild(detailsPanel, "DetailDescription")?.GetComponent<TextMeshProUGUI>();

                var dso = new SerializedObject(details);
                dso.FindProperty("iconImage").objectReferenceValue = icon;
                dso.FindProperty("nameText").objectReferenceValue = dName;
                dso.FindProperty("rarityText").objectReferenceValue = dRarity;
                dso.FindProperty("countText").objectReferenceValue = dCount;
                dso.FindProperty("descriptionText").objectReferenceValue = dDesc;
                dso.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(details);
            }

            EditorUtility.SetDirty(ui);
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return true;
        }

        private static void EnsureDetailsChildren(GameObject detailsPanel, ref int created)
        {
            if (detailsPanel == null) return;

            if (FindDeepChild(detailsPanel, "DetailIcon") == null)
            {
                var dIcon = new GameObject("DetailIcon", typeof(RectTransform), typeof(Image));
                dIcon.transform.SetParent(detailsPanel.transform, false);
                var rt = dIcon.GetComponent<RectTransform>();
                SetAnchors(rt, new Vector2(0.06f, 0.80f), new Vector2(0.22f, 0.94f));
                SetOffsets(rt, 0, 0, 0, 0);
                var img = dIcon.GetComponent<Image>();
                img.color = Color.white;
                img.preserveAspect = true;
                dIcon.SetActive(false);
                created++;
            }

            EnsureText(detailsPanel.transform, "DetailName", new Vector2(0.26f, 0.87f), new Vector2(0.94f, 0.96f), 28, FontStyles.Bold, ref created);
            EnsureText(detailsPanel.transform, "DetailRarity", new Vector2(0.26f, 0.80f), new Vector2(0.94f, 0.86f), 20, FontStyles.Normal, ref created);
            EnsureText(detailsPanel.transform, "DetailCount", new Vector2(0.06f, 0.74f), new Vector2(0.94f, 0.80f), 20, FontStyles.Normal, ref created);

            var desc = EnsureText(detailsPanel.transform, "DetailDescription", new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.72f), 18, FontStyles.Normal, ref created);
            desc.textWrappingMode = TextWrappingModes.Normal;
            desc.alignment = TextAlignmentOptions.TopLeft;
        }

        private static TextMeshProUGUI EnsureText(Transform parent, string name, Vector2 min, Vector2 max, int size, FontStyles style, ref int created)
        {
            var existing = FindDeepChild(parent, name);
            if (existing == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(parent, false);
                var rt = go.GetComponent<RectTransform>();
                SetAnchors(rt, min, max);
                SetOffsets(rt, 0, 0, 0, 0);
                var tmp = go.GetComponent<TextMeshProUGUI>();
                tmp.text = string.Empty;
                tmp.fontSize = size;
                tmp.fontStyle = style;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Left;
                created++;
                return tmp;
            }

            return existing.GetComponent<TextMeshProUGUI>();
        }

        private static TextMeshProUGUI CreateDetailsText(Transform parent, string name, Vector2 min, Vector2 max, int size, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            SetAnchors(rt, min, max);
            SetOffsets(rt, 0, 0, 0, 0);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = string.Empty;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;
            return tmp;
        }

        private static GameObject BuildRowTemplate(Transform contentParent)
        {
            var row = new GameObject("RowTemplate", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement), typeof(Outline), typeof(Abyss.Inventory.PlayerInventoryRowUI));
            row.transform.SetParent(contentParent, false);

            var rt = row.GetComponent<RectTransform>();
            ConfigureRowTemplateRect(rt);

            var layout = row.GetComponent<LayoutElement>();
            layout.minHeight = 56f;
            layout.preferredHeight = 56f;
            layout.flexibleHeight = 0f;

            var bg = row.GetComponent<Image>();
            bg.color = new Color(0.10f, 0.10f, 0.10f, 0.85f);

            // Outline (used for hover/selected borders in grid mode)
            var outline = row.GetComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.15f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;

            // Rarity strip
            var strip = new GameObject("RarityStrip", typeof(RectTransform), typeof(Image));
            strip.transform.SetParent(row.transform, false);
            var stripRt = strip.GetComponent<RectTransform>();
            SetAnchors(stripRt, new Vector2(0f, 0f), new Vector2(0f, 1f));
            stripRt.sizeDelta = new Vector2(6, 0);
            stripRt.anchoredPosition = new Vector2(3, 0);
            strip.GetComponent<Image>().color = Color.white;

            // Icon
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(row.transform, false);
            var iconRt = icon.GetComponent<RectTransform>();
            SetAnchors(iconRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            iconRt.sizeDelta = new Vector2(40, 40);
            iconRt.anchoredPosition = new Vector2(32, 0);
            var iconImg = icon.GetComponent<Image>();
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            icon.SetActive(false);

            // Name
            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(row.transform, false);
            var nameRt = nameGo.GetComponent<RectTransform>();
            SetAnchors(nameRt, new Vector2(0.10f, 0f), new Vector2(0.78f, 1f));
            SetOffsets(nameRt, 0, 0, 0, 0);
            var nameTmp = nameGo.GetComponent<TextMeshProUGUI>();
            nameTmp.text = "Item";
            nameTmp.fontSize = 22;
            nameTmp.color = Color.white;
            nameTmp.alignment = TextAlignmentOptions.Left;
            nameTmp.raycastTarget = false;

            // Count
            var countGo = new GameObject("Count", typeof(RectTransform), typeof(TextMeshProUGUI));
            countGo.transform.SetParent(row.transform, false);
            var countRt = countGo.GetComponent<RectTransform>();
            SetAnchors(countRt, new Vector2(0.78f, 0f), new Vector2(0.97f, 1f));
            SetOffsets(countRt, 0, 0, 0, 0);
            var countTmp = countGo.GetComponent<TextMeshProUGUI>();
            countTmp.text = "x1";
            countTmp.fontSize = 22;
            countTmp.color = Color.white;
            countTmp.alignment = TextAlignmentOptions.Right;
            countTmp.raycastTarget = false;

            // Wire row UI
            var rowUi = row.GetComponent<Abyss.Inventory.PlayerInventoryRowUI>();
            var so = new SerializedObject(rowUi);
            so.FindProperty("background").objectReferenceValue = bg;
            // PlayerInventoryRowUI no longer stores an Outline reference; it self-manages borders at runtime.
            so.FindProperty("iconImage").objectReferenceValue = iconImg;
            so.FindProperty("rarityStrip").objectReferenceValue = strip.GetComponent<Image>();
            so.FindProperty("nameText").objectReferenceValue = nameTmp;
            so.FindProperty("countText").objectReferenceValue = countTmp;
            so.FindProperty("button").objectReferenceValue = row.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();

            return row;
        }

        private static void ConfigureRowTemplateRect(RectTransform rt)
        {
            if (rt == null) return;
            // Stretch horizontally for VerticalLayoutGroup.
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 56f);
        }

        private static void EnsureListContentLayoutComponents(RectTransform content, ref int created)
        {
            if (content == null) return;

            var vlg = content.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
            {
                vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
                created++;
            }

            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 6f;
            vlg.padding = vlg.padding ?? new RectOffset(8, 8, 8, 8);
            vlg.childAlignment = TextAnchor.UpperLeft;

            var fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                created++;
            }
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        private static void UpgradeScrollHierarchy(GameObject panel, ScrollRect scrollRect, ref int created)
        {
            if (panel == null || scrollRect == null) return;

            var scrollGo = scrollRect.gameObject;
            var svRt = scrollGo.GetComponent<RectTransform>();
            ConfigureSplitRegionRect(svRt, new Vector2(0.04f, 0.06f), new Vector2(0.60f, 0.84f));

            // Remove layout components that can collapse size.
            RemoveIfPresent<ContentSizeFitter>(scrollGo);
            RemoveIfPresent<LayoutElement>(scrollGo);
            RemoveIfPresent<HorizontalLayoutGroup>(scrollGo);
            RemoveIfPresent<VerticalLayoutGroup>(scrollGo);

            // Ensure viewport exists and stretches.
            GameObject viewportGo = FindDeepChild(scrollGo, "Viewport");
            if (viewportGo == null)
            {
                viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
                viewportGo.transform.SetParent(scrollGo.transform, false);
                created++;
            }

            var viewportRt = viewportGo.GetComponent<RectTransform>();
            StretchFullScreen(viewportRt);
            var vpImg = viewportGo.GetComponent<Image>();
            if (vpImg == null) vpImg = viewportGo.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0);
            var mask = viewportGo.GetComponent<Mask>();
            if (mask == null) mask = viewportGo.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            EnsureGridInsetPanel(viewportGo.transform);

            // Ensure content is child of viewport.
            var contentGo = FindDeepChild(scrollGo, "Content");
            if (contentGo == null)
            {
                contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                contentGo.transform.SetParent(viewportGo.transform, false);
                created++;
            }
            if (contentGo != null && contentGo.transform.parent != viewportGo.transform)
                contentGo.transform.SetParent(viewportGo.transform, false);

            scrollRect.viewport = viewportRt;
            var contentRt = contentGo != null ? contentGo.GetComponent<RectTransform>() : null;
            if (contentRt != null)
            {
                ConfigureContentRect(contentRt);
                int localCreated = 0;
                EnsureListContentLayoutComponents(contentRt, ref localCreated);
                if (localCreated > 0) created += localCreated;

                scrollRect.content = contentRt;
            }

            // Ensure scroll directions are correct.
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
        }

        private static void ConfigureSplitRegionRect(RectTransform rt, Vector2 min, Vector2 max)
        {
            if (rt == null) return;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            SetOffsets(rt, 0, 0, 0, 0);
        }

        private static void ConfigureContentRect(RectTransform contentRt)
        {
            if (contentRt == null) return;

            // Stretch to fill the viewport; vertical position is driven by layout.
            contentRt.anchorMin = new Vector2(0f, 0f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;
        }

        private static void RemoveIfPresent<T>(GameObject go) where T : Component
        {
            if (go == null) return;
            var c = go.GetComponent<T>();
            if (c == null) return;
            UnityEngine.Object.DestroyImmediate(c);
        }

        private static void EnsureButtonLabel(GameObject buttonGo, string text, int fontSize)
        {
            if (buttonGo == null) return;
            var existing = buttonGo.transform.Find("Text");
            GameObject labelGo = existing != null ? existing.gameObject : null;
            if (labelGo == null)
            {
                labelGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelGo.transform.SetParent(buttonGo.transform, false);
                StretchFullScreen(labelGo.GetComponent<RectTransform>());
            }

            var tmp = labelGo.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.black;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            tmp.extraPadding = true;
        }

        private static void EnsureGridInsetPanel(Transform viewport)
        {
            if (viewport == null) return;

            var existing = viewport.Find("GridInsetPanel");
            GameObject panelGo;
            if (existing != null)
                panelGo = existing.gameObject;
            else
                panelGo = new GameObject("GridInsetPanel", typeof(RectTransform), typeof(Image), typeof(Outline));

            panelGo.transform.SetParent(viewport, false);
            panelGo.transform.SetAsFirstSibling(); // behind Content

            var rt = panelGo.GetComponent<RectTransform>();
            StretchFullScreen(rt);

            var img = panelGo.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.18f);
            img.raycastTarget = false;

            var outline = panelGo.GetComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.10f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = false;
        }

        private static GameObject FindDeepChild(GameObject root, string name)
        {
            if (root == null) return null;
            return FindDeepChild(root.transform, name)?.gameObject;
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeepChild(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        private static void DestroySceneObjectsByName(string name)
        {
            var go = GameObject.Find(name);
            if (go == null) return;
            Undo.DestroyObjectImmediate(go);
        }

        private static void StretchFullScreen(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
        {
            if (rt == null) return;
            rt.anchorMin = min;
            rt.anchorMax = max;
        }

        private static void SetOffsets(RectTransform rt, float left, float right, float top, float bottom)
        {
            if (rt == null) return;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }
    }
}

```

