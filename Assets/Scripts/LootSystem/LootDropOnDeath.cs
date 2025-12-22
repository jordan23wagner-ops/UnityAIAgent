using Abyssbound.Loot;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LootDropOnDeath : MonoBehaviour
{
    [Header("Loot")]
    public LootTableSO lootTable;
    [Min(1)] public int itemLevel = 1;
    public int? seed;

    [Header("Pickup")]
    public WorldItemPickup pickupPrefab;
    [Min(0f)] public float scatterRadius = 0.35f;

    private EnemyHealth _health;

    private void OnEnable()
    {
        _health = GetComponentInParent<EnemyHealth>();
        if (_health != null)
        {
            _health.OnDeath -= OnEnemyDeath;
            _health.OnDeath += OnEnemyDeath;
        }
    }

    private void OnDisable()
    {
        if (_health != null)
            _health.OnDeath -= OnEnemyDeath;
    }

    private void OnEnemyDeath(EnemyHealth dead)
    {
        if (lootTable == null || pickupPrefab == null) return;

        var inst = LootRollerV2.RollItem(lootTable, itemLevel, seed);
        if (inst == null) return;

        Vector3 pos = transform.position;
        if (scatterRadius > 0f)
        {
            var o = Random.insideUnitCircle * scatterRadius;
            pos += new Vector3(o.x, 0f, o.y);
        }

        var pickup = Instantiate(pickupPrefab, pos, Quaternion.identity);
        if (pickup != null)
            pickup.Initialize(inst);
    }
}
