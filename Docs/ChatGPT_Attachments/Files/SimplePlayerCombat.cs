using UnityEngine;
using UnityEngine.InputSystem;

public class SimplePlayerCombat : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private int damage = 1;
    [SerializeField] private float range = 1.75f;
    [SerializeField] private LayerMask hitMask = ~0;

    [Header("Target (optional)")]
    [SerializeField] private EnemyHealth selectedTarget;

    private void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        bool attackPressed = false;

        if (kb != null && kb.spaceKey.wasPressedThisFrame)
            attackPressed = true;

        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            attackPressed = true;

        if (attackPressed)
            TryAttack();
    }

    private void TryAttack()
    {
        if (TryAttackSelectedTarget())
            return;

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

        best.TakeDamage(Mathf.Max(1, damage));
    }

    private bool TryAttackSelectedTarget()
    {
        if (selectedTarget == null)
            return false;

        float distSq = (selectedTarget.transform.position - transform.position).sqrMagnitude;
        if (distSq > range * range)
            return false;

        selectedTarget.TakeDamage(Mathf.Max(1, damage));
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
