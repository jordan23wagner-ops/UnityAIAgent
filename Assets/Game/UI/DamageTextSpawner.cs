using UnityEngine;

public static class DamageTextSpawner
{
    public static void Spawn(int amount, Vector3 worldPos)
    {
        FloatingDamageTextManager.Spawn(amount, worldPos);
    }
}
