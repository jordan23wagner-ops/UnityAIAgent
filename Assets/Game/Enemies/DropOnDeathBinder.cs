using UnityEngine;

public class DropOnDeathBinder : MonoBehaviour
{
    private void Awake()
    {
        TryBind();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        TryBind();
    }
#endif

    private void TryBind()
    {
        var dropOnDeath = GetComponent<DropOnDeath>();
        if (dropOnDeath == null)
            dropOnDeath = GetComponentInParent<DropOnDeath>();

        if (dropOnDeath == null)
            return;

        // ZoneLootTable is authoritative when assigned; do not bind legacy DropTable.
        if (dropOnDeath.zoneLootTable != null)
            return;

        var loots = GetComponents<EnemyLoot>();
        EnemyLoot loot = null;
        if (loots != null)
        {
            for (int i = 0; i < loots.Length; i++)
            {
                if (loots[i] != null && loots[i].DropTable != null)
                {
                    if (!loots[i].enabled) continue;
                    loot = loots[i];
                    break;
                }
            }
            if (loot == null && loots.Length > 0)
                loot = loots[0];
        }

        if (loot == null)
            loot = GetComponentInParent<EnemyLoot>();

        if (loot != null && !loot.enabled)
            loot = null;

        if (loot == null)
            return;

        var dt = loot.DropTable;
        if (dt == null)
            return;

        if (dropOnDeath.dropTable == null || dropOnDeath.dropTable != dt)
            dropOnDeath.dropTable = dt;
    }
}
