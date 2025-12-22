using UnityEngine;

// Optional helper to ensure EnemyAggroChase exists on spawned enemies.
// - Safe to leave unused.
[DisallowMultipleComponent]
public sealed class EnsureEnemyAggroChaseOnSpawn : MonoBehaviour
{
    [Header("Optional Defaults")]
    [SerializeField] private bool applyDefaults;
    [SerializeField] private float defaultAggroRadius = 6f;
    [SerializeField] private float defaultLeashRadius = 12f;
    [SerializeField] private float defaultMoveSpeed = 3f;
    [SerializeField] private float defaultStopDistance = 1.6f;

    public void Ensure(GameObject enemyInstance)
    {
        if (enemyInstance == null)
            return;

        var aggro = enemyInstance.GetComponent<EnemyAggroChase>();
        if (aggro == null)
            aggro = enemyInstance.AddComponent<EnemyAggroChase>();

        if (applyDefaults && aggro != null)
            aggro.SetTuning(defaultAggroRadius, defaultLeashRadius, defaultMoveSpeed, defaultStopDistance);
    }
}
