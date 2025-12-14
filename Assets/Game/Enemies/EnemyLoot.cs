using UnityEngine;

public class EnemyLoot : MonoBehaviour
{
    [Header("Loot")]
    [SerializeField] private DropTable dropTable;

    public DropTable DropTable => dropTable;
}
