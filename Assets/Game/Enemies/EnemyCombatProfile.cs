using UnityEngine;

// Minimal, opt-in combat metadata for enemies.
// Used as a robust fallback when no dedicated enemy stats component exists.
[DisallowMultipleComponent]
public sealed class EnemyCombatProfile : MonoBehaviour
{
    [Min(1)] public int defenceLevel = 1;

    [Tooltip("Optional label for QA (Trash/Elite/Boss).")]
    public string tier = "Trash";
}
