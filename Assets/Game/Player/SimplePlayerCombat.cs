using UnityEngine;
using Abyss.Dev;

public class SimplePlayerCombat : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float attackCooldownSeconds = 0.6f;
    [SerializeField] private float range = 1.75f;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    [Header("Target (optional)")]
    [SerializeField] private EnemyHealth selectedTarget;

    private float _nextAttackTime;

    public float Range => range;

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
        if (Time.time < _nextAttackTime)
            return;

        if (SelectedTarget != null)
        {
            var attackedTarget = SelectedTarget;
            if (!TryAttackSelectedTarget())
                return;

            _nextAttackTime = Time.time + Mathf.Max(0.05f, attackCooldownSeconds);
            if (attackedTarget != null)
                Debug.Log($"[Combat] You attacked {attackedTarget.name}", this);
            return;
        }

        var hits = Physics.OverlapSphere(transform.position, Mathf.Max(0.1f, range), hitMask, QueryTriggerInteraction.Collide);
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
        var hitPos = best.transform.position + Vector3.up * 1.2f;
        int dealt = Mathf.Max(1, damage);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DevCheats.GodModeEnabled)
            dealt = 999999;
#endif
        best.TakeDamage(dealt, hitPos);
    }

    private bool TryAttackSelectedTarget()
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
        float rangeSq = range * range;
        if (distSq > rangeSq)
        {
            if (debugLogs)
                Debug.Log($"[Combat] Attack rejected: out of range. xzDist={Mathf.Sqrt(distSq):0.00} range={range:0.00}", this);
            return false;
        }

        var hitPos = selectedTarget.transform.position + Vector3.up * 1.2f;
        int dealt = Mathf.Max(1, damage);
    #if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (DevCheats.GodModeEnabled)
            dealt = 999999;
    #endif
        selectedTarget.TakeDamage(dealt, hitPos);
        return true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, range));
    }
#endif
}
