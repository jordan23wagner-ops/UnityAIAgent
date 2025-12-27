using UnityEngine;
using Abyss.Dev;
using Abyssbound.Combat;
using Abyssbound.Stats;
using Abyssbound.Loot;
using Abyss.Equipment;
using Abyss.Items;

public class SimplePlayerCombat : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float attackCooldownSeconds = 0.6f;
    [SerializeField] private float range = 1.75f;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Attack Type")]
    [SerializeField] private WeaponAttackType attackType = WeaponAttackType.Melee;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    [Header("Target (optional)")]
    [SerializeField] private EnemyHealth selectedTarget;

    private float _nextAttackTime;

    private PlayerCombatStats _stats;
    private bool _warnedMissingStats;
    private PlayerStatsRuntime _statsRuntime;
    private bool _loggedMissingMissText;
    private bool _subscribedToLevelUps;
    private bool _loggedMissingLevelUpText;

    private PlayerEquipment _equipment;
    private LootRegistryRuntime _lootRegistry;

    private ProjectileMover _arrowPrefab;
    private ProjectileMover _magicPrefab;
    private Transform _projectileMuzzle;

    private string _cachedWeaponId;
    private string _cachedWeaponName;

    private bool _loggedMissingArrowProjectilePrefab;
    private bool _loggedMissingMagicProjectilePrefab;

    public WeaponAttackType CurrentAttackType => GetEffectiveAttackType();

    public float Range => GetEffectiveAttackRange(GetEffectiveAttackType());

    public EnemyHealth SelectedTarget
    {
        get => selectedTarget;
        set => selectedTarget = value;
    }

    public void SetSelectedTarget(EnemyHealth target)
    {
        SelectedTarget = target;
    }

    public void TryAttack()
    {
        EnsureLevelUpSubscription();
        if (Time.time < _nextAttackTime)
            return;

        var effectiveType = GetEffectiveAttackType();
        float effectiveRange = GetEffectiveAttackRange(effectiveType);

        if (SelectedTarget != null)
        {
            var attackedTarget = SelectedTarget;
            if (!TryAttackSelectedTarget(effectiveType, effectiveRange))
                return;

            _nextAttackTime = Time.time + Mathf.Max(0.05f, attackCooldownSeconds);
            if (debugLogs && attackedTarget != null)
                Debug.Log($"[Combat] You attacked {attackedTarget.name}", this);
            return;
        }

        var hits = Physics.OverlapSphere(transform.position, Mathf.Max(0.1f, effectiveRange), hitMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
            return;

        EnemyHealth best = null;
        float bestDistSq = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            var c = hits[i];
            if (c == null) continue;

            var eh = c.GetComponentInParent<EnemyHealth>();
            if (eh == null) continue;

            float d = (eh.transform.position - transform.position).sqrMagnitude;
            if (d < bestDistSq)
            {
                bestDistSq = d;
                best = eh;
            }
        }

        if (best == null)
            return;

        _nextAttackTime = Time.time + Mathf.Max(0.05f, attackCooldownSeconds);
        PerformAttack(best, effectiveType);
    }

    private bool TryAttackSelectedTarget(WeaponAttackType effectiveType, float effectiveRange)
    {
        if (selectedTarget == null)
            return false;

        if (selectedTarget.IsDead)
            return false;

        // Match CombatLoopController: XZ plane only (ignore Y).
        Vector3 myPos = transform.position;
        Vector3 targetPos = selectedTarget.transform.position;
        float dx = targetPos.x - myPos.x;
        float dz = targetPos.z - myPos.z;
        float distSq = (dx * dx) + (dz * dz);
        float rangeSq = effectiveRange * effectiveRange;
        if (distSq > rangeSq)
        {
            if (debugLogs)
                Debug.Log($"[Combat] Attack rejected: out of range. xzDist={Mathf.Sqrt(distSq):0.00} range={effectiveRange:0.00} type={effectiveType}", this);
            return false;
        }

        EnsureLevelUpSubscription();

        PerformAttack(selectedTarget, effectiveType);
        return true;
    }

    private void PerformAttack(EnemyHealth target, WeaponAttackType effectiveType)
    {
        if (target == null || target.IsDead)
            return;

        var styleStat = GetStyleSkillStat(effectiveType);
        bool hit = RollHit(target, effectiveType);
        int dealt = 0;

        if (hit)
        {
            dealt = Mathf.Max(1, GetDamageForAttack());
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevCheats.GodModeEnabled)
                dealt = 999999;
#endif
        }

        var hitPos = target.transform.position + Vector3.up * CombatAttackTuning.ProjectileSpawnHeight;

        if (CombatQaFlags.AttackDebugLogs)
        {
            var wname = GetEquippedWeaponNameSafe();
            bool willSpawnProjectile = effectiveType != WeaponAttackType.Melee && CombatQaFlags.ProjectileVisualsEnabled;
            Debug.Log($"[Combat] Weapon={wname} Type={effectiveType} Range={GetEffectiveAttackRange(effectiveType):0.00} SpawnProjectile={(willSpawnProjectile ? "true" : "false")}", this);
        }

        if (effectiveType == WeaponAttackType.Melee)
        {
            if (!hit)
            {
                ShowMiss(target, hitPos);
                return;
            }

            if (TtkQaTracker.Instance != null)
                TtkQaTracker.Instance.NotifyPlayerHit(target);

            target.TakeDamage(dealt, hitPos);
            AwardCombatXpForDamageDealt(target, dealt, styleStat);
            return;
        }

        // If visuals are disabled, keep combat logic snappy (apply immediately).
        if (!CombatQaFlags.ProjectileVisualsEnabled)
        {
            if (!hit)
            {
                ShowMiss(target, hitPos);
                return;
            }

            if (TtkQaTracker.Instance != null)
                TtkQaTracker.Instance.NotifyPlayerHit(target);

            target.TakeDamage(dealt, hitPos);
            AwardCombatXpForDamageDealt(target, dealt, styleStat);
            return;
        }

        var startPos = GetProjectileOrigin();
        var aimPos = GetEnemyAimPoint(target);

        var prefab = effectiveType == WeaponAttackType.Magic
            ? GetMagicProjectilePrefab()
            : GetArrowProjectilePrefab();

        if (prefab == null)
        {
            // Fallback: no prefab, but still do the immediate damage.
            if (!hit)
            {
                ShowMiss(target, hitPos);
                return;
            }

            if (TtkQaTracker.Instance != null)
                TtkQaTracker.Instance.NotifyPlayerHit(target);

            target.TakeDamage(dealt, hitPos);
            AwardCombatXpForDamageDealt(target, dealt, styleStat);
            return;
        }

        var go = Instantiate(prefab.gameObject, startPos, Quaternion.identity);
        var mover = go.GetComponent<ProjectileMover>();
        if (mover == null)
            mover = go.AddComponent<ProjectileMover>();

        var targetTransform = target != null ? target.transform : null;
        var targetOffset = targetTransform != null ? (aimPos - targetTransform.position) : Vector3.zero;

        mover.Init(
            target: targetTransform,
            targetPosFallback: aimPos,
            targetOffset: targetOffset,
            projectileSpeed: CombatAttackTuning.ProjectileSpeed,
            onImpact: () =>
            {
                if (target == null || target.IsDead)
                    return;

                var impactPos = GetEnemyAimPoint(target);
                if (!hit)
                {
                    ShowMiss(target, impactPos);
                    return;
                }

                if (TtkQaTracker.Instance != null)
                    TtkQaTracker.Instance.NotifyPlayerHit(target);

                target.TakeDamage(dealt, impactPos);
                AwardCombatXpForDamageDealt(target, dealt, styleStat);
            }
        );
    }

    private Vector3 GetProjectileOrigin()
    {
        if (_projectileMuzzle == null)
            _projectileMuzzle = TryFindMuzzle(transform);

        if (_projectileMuzzle != null)
            return _projectileMuzzle.position;

        return transform.position + Vector3.up * CombatAttackTuning.ProjectileSpawnHeight;
    }

    private static Transform TryFindMuzzle(Transform root)
    {
        if (root == null)
            return null;

        // Common child names; no hard dependency on any specific rig.
        var names = new[] { "Muzzle", "ProjectileOrigin", "Projectile_Muzzle", "WeaponMuzzle", "Weapon_Muzzle", "FirePoint", "Fire_Point" };
        for (int i = 0; i < names.Length; i++)
        {
            try
            {
                var t = FindChildRecursive(root, names[i]);
                if (t != null) return t;
            }
            catch { }
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null || string.IsNullOrWhiteSpace(name))
            return null;

        if (root.name == name)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            var found = FindChildRecursive(c, name);
            if (found != null) return found;
        }

        return null;
    }

    private static Vector3 GetEnemyAimPoint(EnemyHealth target)
    {
        if (target == null)
            return Vector3.zero;

        // Prefer collider bounds slightly above center (avoids hitting feet/pivot).
        try
        {
            var col = target.GetComponentInChildren<Collider>();
            if (col != null)
            {
                var b = col.bounds;
                var y = Mathf.Lerp(b.min.y, b.max.y, 0.65f);
                return new Vector3(b.center.x, y, b.center.z);
            }
        }
        catch { }

        return target.transform.position + Vector3.up * CombatAttackTuning.ProjectileSpawnHeight;
    }

    private ProjectileMover GetArrowProjectilePrefab()
    {
        if (_arrowPrefab != null)
            return _arrowPrefab;

        try { _arrowPrefab = Resources.Load<ProjectileMover>("Prefabs/Projectiles/Projectile_Arrow"); }
        catch { _arrowPrefab = null; }

        if (_arrowPrefab == null && !_loggedMissingArrowProjectilePrefab)
        {
            _loggedMissingArrowProjectilePrefab = true;
            Debug.LogError("[Combat] Missing projectile prefab at Resources/Prefabs/Projectiles/Projectile_Arrow.prefab", this);
        }

        return _arrowPrefab;
    }

    private ProjectileMover GetMagicProjectilePrefab()
    {
        if (_magicPrefab != null)
            return _magicPrefab;

        try { _magicPrefab = Resources.Load<ProjectileMover>("Prefabs/Projectiles/Projectile_MagicBolt"); }
        catch { _magicPrefab = null; }

        if (_magicPrefab == null && !_loggedMissingMagicProjectilePrefab)
        {
            _loggedMissingMagicProjectilePrefab = true;
            Debug.LogError("[Combat] Missing projectile prefab at Resources/Prefabs/Projectiles/Projectile_MagicBolt.prefab", this);
        }

        return _magicPrefab;
    }

    private void EnsureLevelUpSubscription()
    {
        if (_subscribedToLevelUps)
            return;

        if (_statsRuntime == null)
        {
            try
            {
                _statsRuntime = GetComponent<PlayerStatsRuntime>();
                if (_statsRuntime == null) _statsRuntime = GetComponentInParent<PlayerStatsRuntime>();
            }
            catch { _statsRuntime = null; }
        }

        if (_statsRuntime == null)
            return;

        try
        {
            _statsRuntime.OnLevelUp -= OnLevelUp;
            _statsRuntime.OnLevelUp += OnLevelUp;
            _subscribedToLevelUps = true;
        }
        catch
        {
            _subscribedToLevelUps = false;
        }
    }

    private void OnLevelUp(StatType stat, int newLevel)
    {
        var pos = transform.position + Vector3.up * 2.0f;
        try
        {
            var name = StatTypeCanonical.ToCanonicalPrimaryName(stat);
            FloatingDamageTextManager.ShowLevelUp(pos, name, newLevel);
        }
        catch
        {
            if (!_loggedMissingLevelUpText)
            {
                _loggedMissingLevelUpText = true;
                Debug.Log($"[XP] {stat} Level {newLevel}!", this);
            }
        }
    }

    private void AwardCombatXpForDamageDealt(EnemyHealth enemy, int finalDamage, StatType styleStat)
    {
        if (enemy == null)
            return;

        if (finalDamage <= 0)
            return;

        if (!XpAwardFlags.AwardAttackXp)
            return;

        if (_statsRuntime == null)
        {
            try
            {
                _statsRuntime = GetComponent<PlayerStatsRuntime>();
                if (_statsRuntime == null) _statsRuntime = GetComponentInParent<PlayerStatsRuntime>();
            }
            catch { _statsRuntime = null; }
        }

        if (_statsRuntime == null)
            return;

        float mult = EnemyTierResolver.GetXpMultiplier(enemy);

        int attackXpBase = finalDamage * CombatXpTuning.AttackXpPerDamage;
        int styleXpBase = finalDamage * CombatXpTuning.StyleXpPerDamage;

        int attackXp = Mathf.FloorToInt(attackXpBase * mult);
        int styleXp = Mathf.FloorToInt(styleXpBase * mult);

        if (attackXp <= 0 && styleXp <= 0)
            return;

        if (attackXp > 0)
        {
            try { _statsRuntime.AddXp(StatType.Attack, attackXp); } catch { }
        }
        if (styleXp > 0)
        {
            try { _statsRuntime.AddXp(styleStat, styleXp); } catch { }
        }

        try
        {
            var styleName = StatTypeCanonical.ToCanonicalPrimaryName(styleStat);
            FloatingDamageTextManager.ShowXpGainCombined(enemy, attackXp, styleXp, styleName);
        }
        catch { }
    }

    private StatType GetStyleSkillStat(WeaponAttackType effectiveType)
    {
        switch (effectiveType)
        {
            case WeaponAttackType.Ranged:
                return StatType.RangedSkill;
            case WeaponAttackType.Magic:
                return StatType.MagicSkill;
            default:
                return StatType.Strength;
        }
    }

    private bool RollHit(EnemyHealth enemy, WeaponAttackType effectiveType)
    {
        if (CombatQaFlags.AlwaysHit)
            return true;

        int totalAttack = GetTotalAccuracyStat(effectiveType);
        int enemyDef = EnemyDefenseResolver.GetEnemyDefenceLevel(enemy);

        float hitChance = StatCalculator.ComputeHitChance(totalAttack, enemyDef);
        return UnityEngine.Random.value <= hitChance;
    }

    private int GetTotalAccuracyStat(WeaponAttackType effectiveType)
    {
        if (_statsRuntime == null)
        {
            try
            {
                _statsRuntime = GetComponent<PlayerStatsRuntime>();
                if (_statsRuntime == null) _statsRuntime = GetComponentInParent<PlayerStatsRuntime>();
            }
            catch { _statsRuntime = null; }
        }

        if (_statsRuntime != null)
        {
            try
            {
                _statsRuntime.RebuildNow();
                switch (effectiveType)
                {
                    case WeaponAttackType.Ranged:
                        return Mathf.Max(1, _statsRuntime.TotalPrimary.ranged);
                    case WeaponAttackType.Magic:
                        return Mathf.Max(1, _statsRuntime.TotalPrimary.magic);
                    default:
                        return Mathf.Max(1, _statsRuntime.TotalPrimary.attack);
                }
            }
            catch
            {
                return 1;
            }
        }

        return 1;
    }

    private WeaponAttackType GetEffectiveAttackType()
    {
        TryResolveEquipmentAndRegistry();
        if (_equipment == null || _lootRegistry == null)
            return attackType;

        string equippedId = null;
        try { equippedId = _equipment.Get(EquipmentSlot.RightHand); }
        catch { equippedId = null; }

        if (string.IsNullOrWhiteSpace(equippedId))
        {
            try { equippedId = _equipment.Get(EquipmentSlot.LeftHand); }
            catch { equippedId = null; }
        }

        if (string.IsNullOrWhiteSpace(equippedId))
            return attackType;

        if (!string.Equals(_cachedWeaponId, equippedId))
        {
            _cachedWeaponId = equippedId;
            _cachedWeaponName = null;
        }

        ItemDefinitionSO baseItem = null;
        try
        {
            if (_lootRegistry.TryGetRolledInstance(equippedId, out var inst) && inst != null)
            {
                // Robust inference: base stats + affixes via stat-mod pipeline.
                var resolvedFromMods = WeaponAttackTypeResolver.ResolveWeaponAttackType(inst);
                if (resolvedFromMods != WeaponAttackType.Melee)
                    return resolvedFromMods;

                if (!string.IsNullOrWhiteSpace(inst.baseItemId) && _lootRegistry.TryGetItem(inst.baseItemId, out var bi))
                    baseItem = bi;
            }
            else
            {
                if (_lootRegistry.TryGetItem(equippedId, out var bi))
                    baseItem = bi;
            }
        }
        catch { baseItem = null; }

        if (baseItem != null)
        {
            // Still run through stat-mod inference even when we only have a base id.
            try
            {
                var pseudo = new ItemInstance { baseItemId = baseItem.id, rarityId = "Common", itemLevel = 1, baseScalar = 1f };
                var resolvedFromMods = WeaponAttackTypeResolver.ResolveWeaponAttackType(pseudo);
                if (resolvedFromMods != WeaponAttackType.Melee)
                    return resolvedFromMods;
            }
            catch { }
        }

        if (baseItem == null || baseItem.baseStats == null)
            return attackType;

        for (int i = 0; i < baseItem.baseStats.Count; i++)
        {
            var m = baseItem.baseStats[i];
            if (m.percent) continue;

            if (m.stat == StatType.MagicDamage)
                return WeaponAttackType.Magic;
            if (m.stat == StatType.RangedDamage)
                return WeaponAttackType.Ranged;
        }

        // Fallback heuristics (QA/test items may be minimal).
        try
        {
            var s = (baseItem.id + " " + baseItem.displayName).ToLowerInvariant();
            if (s.Contains("bow") || s.Contains("crossbow"))
                return WeaponAttackType.Ranged;
            if (s.Contains("staff") || s.Contains("wand") || s.Contains("tome") || s.Contains("spell") || s.Contains("rune"))
                return WeaponAttackType.Magic;
        }
        catch { }

        return WeaponAttackType.Melee;
    }

    private string GetEquippedWeaponNameSafe()
    {
        if (!string.IsNullOrWhiteSpace(_cachedWeaponName))
            return _cachedWeaponName;

        TryResolveEquipmentAndRegistry();

        var id = _cachedWeaponId;
        if (string.IsNullOrWhiteSpace(id) && _equipment != null)
        {
            try { id = _equipment.Get(EquipmentSlot.RightHand); }
            catch { id = null; }
            if (string.IsNullOrWhiteSpace(id))
            {
                try { id = _equipment.Get(EquipmentSlot.LeftHand); }
                catch { id = null; }
            }
        }

        if (string.IsNullOrWhiteSpace(id) || _lootRegistry == null)
            return "(none)";

        try
        {
            // Rolled instances.
            if (_lootRegistry.TryResolveDisplay(id, out var displayName, out var _))
            {
                _cachedWeaponName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
                return _cachedWeaponName;
            }

            // Base items.
            if (_lootRegistry.TryGetItem(id, out var baseItem) && baseItem != null)
            {
                _cachedWeaponName = string.IsNullOrWhiteSpace(baseItem.displayName) ? baseItem.id : baseItem.displayName;
                return _cachedWeaponName;
            }
        }
        catch { }

        return id;
    }

    private float GetEffectiveAttackRange(WeaponAttackType effectiveType)
    {
        switch (effectiveType)
        {
            case WeaponAttackType.Ranged:
                return CombatAttackTuning.RangedAttackRange;
            case WeaponAttackType.Magic:
                return CombatAttackTuning.MagicAttackRange;
            default:
                return range;
        }
    }

    private void TryResolveEquipmentAndRegistry()
    {
        if (_equipment == null)
        {
            try
            {
                _equipment = GetComponent<PlayerEquipment>();
                if (_equipment == null) _equipment = GetComponentInParent<PlayerEquipment>();
            }
            catch { _equipment = null; }

            if (_equipment == null)
            {
                // Combat component may live on a different object than equipment.
                try { _equipment = FindAnyObjectByType<PlayerEquipment>(); }
                catch { _equipment = null; }

                if (_equipment == null)
                {
                    try { _equipment = UnityEngine.Object.FindFirstObjectByType<PlayerEquipment>(FindObjectsInactive.Exclude); }
                    catch { _equipment = null; }
                }
            }
        }

        if (_lootRegistry == null)
        {
            try
            {
                _lootRegistry = LootRegistryRuntime.GetOrCreate();
                _lootRegistry.BuildIfNeeded();
            }
            catch { _lootRegistry = null; }
        }
    }

    private void ShowMiss(EnemyHealth enemy, Vector3 worldPos)
    {
        try
        {
            // Use the combat text system when present.
            // Prefer anchoring to the enemy head for clutter reduction.
            if (enemy != null)
                FloatingDamageTextManager.ShowMiss(enemy);
            else
                FloatingDamageTextManager.SpawnText("Miss", worldPos);
        }
        catch
        {
            if (!_loggedMissingMissText)
            {
                _loggedMissingMissText = true;
                Debug.Log("[Combat] Miss", this);
            }
        }
    }

    private int GetDamageForAttack()
    {
        // MVP: if PlayerCombatStats exists, use it; otherwise preserve legacy behavior.
        if (_stats == null)
        {
            try
            {
                _stats = GetComponent<PlayerCombatStats>();
                if (_stats == null) _stats = GetComponentInParent<PlayerCombatStats>();
            }
            catch { _stats = null; }
        }

        if (_stats != null)
            return _stats.DamageFinal;

        if (!_warnedMissingStats)
        {
            _warnedMissingStats = true;
            Debug.LogWarning($"[STATS] PlayerCombatStats missing; using fallback damage={damage}", this);
        }

        return damage;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var effectiveType = GetEffectiveAttackType();
        float r = Mathf.Max(0.1f, GetEffectiveAttackRange(effectiveType));

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, r);
    }

    private void OnDrawGizmos()
    {
        if (!CombatQaFlags.DrawAttackRanges)
            return;

        var effectiveType = GetEffectiveAttackType();
        float r = Mathf.Max(0.1f, GetEffectiveAttackRange(effectiveType));

        Gizmos.color = effectiveType == WeaponAttackType.Magic
            ? new Color(0.25f, 0.6f, 1f, 0.9f)
            : effectiveType == WeaponAttackType.Ranged
                ? new Color(0.25f, 1f, 0.25f, 0.9f)
                : new Color(1f, 0.25f, 0.25f, 0.9f);

        Gizmos.DrawWireSphere(transform.position, r);
    }
#endif
}
