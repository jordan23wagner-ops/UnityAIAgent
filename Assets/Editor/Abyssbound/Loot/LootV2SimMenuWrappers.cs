#if UNITY_EDITOR
using UnityEditor;

public static class LootV2SimMenuWrappers
{
    [MenuItem("Tools/Abyssbound/Loot/Simulate 200 Drops (Selected Enemy, Loot V2)")]
    public static void Sim200() => Simulate200DropsSelectedEnemy.SimulateLootV2OnlyForSelectedEnemy(200);

    [MenuItem("Tools/Abyssbound/Loot/Simulate 1000 Drops (Selected Enemy, Loot V2)")]
    public static void Sim1000() => Simulate200DropsSelectedEnemy.SimulateLootV2OnlyForSelectedEnemy(1000);

    [MenuItem("Tools/Abyssbound/Loot/Sim Loot V2 (Selected Enemy) — 200")]
    public static void Sim200Alt() => Simulate200DropsSelectedEnemy.SimulateLootV2OnlyForSelectedEnemy(200);

    [MenuItem("Tools/Abyssbound/Loot/Sim Loot V2 (Selected Enemy) — 1000")]
    public static void Sim1000Alt() => Simulate200DropsSelectedEnemy.SimulateLootV2OnlyForSelectedEnemy(1000);

    [MenuItem("Tools/Abyssbound/Loot/Copy Last Loot V2 Sim Report")]
    public static void CopyLastReport() => Simulate200DropsSelectedEnemy.CopyLastLootV2SimReportToClipboard();
}
#endif
