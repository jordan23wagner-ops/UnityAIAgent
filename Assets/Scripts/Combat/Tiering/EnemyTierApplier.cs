using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Abyssbound.Combat.Tiering
{
    [DisallowMultipleComponent]
    public class EnemyTierApplier : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DistanceTierService tierService;
        [SerializeField] private Transform playerTransform;

        [Header("Options")]
        [SerializeField] private bool applyOnEnable = true;
        [SerializeField] private bool applyAtEndOfFrame = true;
        [SerializeField] private float applyDelaySeconds = 0f;
        [SerializeField] private bool verboseLogs = true;

        [Header("Debug (read-only)")]
        [SerializeField] private int appliedTierNumber = 1;
        [SerializeField] private float appliedDistance = 0f;

        public int AppliedTierNumber => appliedTierNumber;
        public float AppliedDistance => appliedDistance;

        private bool _applied;

        private bool _cachedBaseMelee;
        private float _baseMeleeDamage;

        private bool _cachedBaseHealthInt;
        private int _baseMaxHealthInt;

        private void OnEnable()
        {
            if (!applyOnEnable) return;
            StartCoroutine(ApplyWhenReady());
        }

        private IEnumerator ApplyWhenReady()
        {
            if (applyAtEndOfFrame) yield return new WaitForEndOfFrame();
            if (applyDelaySeconds > 0f) yield return new WaitForSeconds(applyDelaySeconds);
            ApplyTierOnce();
        }

        public void ApplyTierOnce()
        {
            if (_applied) return;
            _applied = true;

            if (tierService == null || playerTransform == null)
            {
                Debug.LogWarning($"[EnemyTierApplier] Missing refs on '{name}'. tierService? {(tierService != null)} playerTransform? {(playerTransform != null)}", this);
                return;
            }

            float distance = tierService.GetDistance(playerTransform.position);
            var def = tierService.GetTierDefinition(distance);
            int tier = tierService.GetTierIndex(distance);

            appliedDistance = distance;
            appliedTierNumber = tier;

            float hpMult = def.hpMult;
            float dmgMult = def.dmgMult;

            if (verboseLogs)
                Debug.Log($"[EnemyTierApplier] '{name}' tier={tier} dist={distance:F2} hpMult={hpMult:F2} dmgMult={dmgMult:F2}", this);

            var melee = GetComponent<EnemyMeleeAttack>();
            var enemyHealth = GetComponent<EnemyHealth>();
            if (enemyHealth == null)
                enemyHealth = GetComponentInChildren<EnemyHealth>(true);

            ApplyDamage(melee, dmgMult);

            if (enemyHealth != null)
            {
                if (!_cachedBaseHealthInt)
                {
                    _baseMaxHealthInt = enemyHealth.MaxHealth;
                    _cachedBaseHealthInt = true;
                }

                int before = enemyHealth.MaxHealth;
                int newMax = Mathf.RoundToInt(_baseMaxHealthInt * hpMult);
                enemyHealth.SetMaxHealthForQa(newMax);
                int after = enemyHealth.MaxHealth;

                if (verboseLogs)
                    Debug.Log($"[EnemyTierApplier] '{name}' SET EnemyHealth.MaxHealth: {before} -> {after}", this);
            }

            if (melee == null && enemyHealth == null && verboseLogs)
                Debug.LogWarning($"[EnemyTierApplier] '{name}' has no EnemyMeleeAttack or EnemyHealth. Nothing scaled.", this);
        }

        private void ApplyDamage(EnemyMeleeAttack melee, float dmgMult)
        {
            if (melee == null) return;

            var t = typeof(EnemyMeleeAttack);
            string[] damageNames = { "Damage", "damage", "BaseDamage", "baseDamage", "AttackDamage", "attackDamage" };

            if (!_cachedBaseMelee)
            {
                if (TryReadFloatMember(melee, t, damageNames, out var baseVal, out var member))
                {
                    _baseMeleeDamage = baseVal;
                    _cachedBaseMelee = true;
                    if (verboseLogs) Debug.Log($"[EnemyTierApplier] '{name}' cached base melee dmg {_baseMeleeDamage} from {member}.", this);
                }
                else
                {
                    Debug.LogWarning($"[EnemyTierApplier] '{name}' could not read melee damage from EnemyMeleeAttack.", this);
                    LogMembers(t, this);
                    return;
                }
            }

            float newDmg = _baseMeleeDamage * dmgMult;

            if (TryWriteFloatMember(melee, t, damageNames, newDmg, out var writeMember, out var before))
            {
                if (verboseLogs) Debug.Log($"[EnemyTierApplier] '{name}' SET EnemyMeleeAttack.{writeMember}: {before} -> {newDmg}", this);
            }
            else
            {
                Debug.LogWarning($"[EnemyTierApplier] '{name}' could not write melee damage on EnemyMeleeAttack.", this);
                LogMembers(t, this);
            }
        }

        private static bool TryReadFloatMember(object obj, Type type, string[] candidateNames, out float value, out string memberNameUsed)
        {
            value = 0f;
            memberNameUsed = "";
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var name in candidateNames)
            {
                var f = type.GetField(name, flags);
                if (f != null && IsNumeric(f.FieldType))
                {
                    var raw = f.GetValue(obj);
                    if (raw != null) { value = Convert.ToSingle(raw); memberNameUsed = f.Name; return true; }
                }

                var p = type.GetProperty(name, flags);
                if (p != null && p.CanRead && IsNumeric(p.PropertyType))
                {
                    var raw = p.GetValue(obj);
                    if (raw != null) { value = Convert.ToSingle(raw); memberNameUsed = p.Name; return true; }
                }
            }

            foreach (var f in type.GetFields(flags))
            {
                if (!IsNumeric(f.FieldType)) continue;
                if (candidateNames.Any(c => string.Equals(c, f.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    var raw = f.GetValue(obj);
                    if (raw != null) { value = Convert.ToSingle(raw); memberNameUsed = f.Name; return true; }
                }
            }

            foreach (var p in type.GetProperties(flags))
            {
                if (!p.CanRead || !IsNumeric(p.PropertyType)) continue;
                if (candidateNames.Any(c => string.Equals(c, p.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    var raw = p.GetValue(obj);
                    if (raw != null) { value = Convert.ToSingle(raw); memberNameUsed = p.Name; return true; }
                }
            }

            return false;
        }

        private static bool TryWriteFloatMember(object obj, Type type, string[] candidateNames, float newValue, out string memberNameUsed, out float beforeValue)
        {
            memberNameUsed = "";
            beforeValue = 0f;
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var name in candidateNames)
            {
                var f = type.GetField(name, flags);
                if (f != null && IsNumeric(f.FieldType) && !f.IsInitOnly)
                {
                    var rawBefore = f.GetValue(obj);
                    beforeValue = rawBefore != null ? Convert.ToSingle(rawBefore) : 0f;
                    f.SetValue(obj, Convert.ChangeType(newValue, f.FieldType));
                    memberNameUsed = f.Name;
                    return true;
                }

                var p = type.GetProperty(name, flags);
                if (p != null && p.CanWrite && IsNumeric(p.PropertyType))
                {
                    var rawBefore = p.GetValue(obj);
                    beforeValue = rawBefore != null ? Convert.ToSingle(rawBefore) : 0f;
                    p.SetValue(obj, Convert.ChangeType(newValue, p.PropertyType));
                    memberNameUsed = p.Name;
                    return true;
                }
            }

            foreach (var f in type.GetFields(flags))
            {
                if (!IsNumeric(f.FieldType) || f.IsInitOnly) continue;
                if (candidateNames.Any(c => string.Equals(c, f.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    var rawBefore = f.GetValue(obj);
                    beforeValue = rawBefore != null ? Convert.ToSingle(rawBefore) : 0f;
                    f.SetValue(obj, Convert.ChangeType(newValue, f.FieldType));
                    memberNameUsed = f.Name;
                    return true;
                }
            }

            foreach (var p in type.GetProperties(flags))
            {
                if (!p.CanWrite || !IsNumeric(p.PropertyType)) continue;
                if (candidateNames.Any(c => string.Equals(c, p.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    var rawBefore = p.GetValue(obj);
                    beforeValue = rawBefore != null ? Convert.ToSingle(rawBefore) : 0f;
                    p.SetValue(obj, Convert.ChangeType(newValue, p.PropertyType));
                    memberNameUsed = p.Name;
                    return true;
                }
            }

            return false;
        }

        private static bool IsNumeric(Type t)
        {
            return t == typeof(int) || t == typeof(float) || t == typeof(double) || t == typeof(long) || t == typeof(short);
        }

        private static void LogMembers(Type type, UnityEngine.Object ctx)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var fields = type.GetFields(flags).Select(f => $"{f.FieldType.Name} {f.Name}").OrderBy(s => s);
            var props = type.GetProperties(flags).Select(p => $"{p.PropertyType.Name} {p.Name} (R:{p.CanRead} W:{p.CanWrite})").OrderBy(s => s);

            Debug.Log($"[EnemyTierApplier] Members on {type.Name}:\nFields:\n- {string.Join("\n- ", fields)}\nProperties:\n- {string.Join("\n- ", props)}", ctx);
        }
    }
}
