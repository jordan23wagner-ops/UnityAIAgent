using UnityEngine;
using Abyss.Legacy;

[CreateAssetMenu(menuName = "Abyssbound/Gates/Gate Definition")]
public class GateDefinition : ScriptableObject
{
    [Header("Gate Requirements")]
    public LegacyItemDefinition requiredItem;

    [Header("Feedback Text")]
    [TextArea]
    public string lockedHintText = "Abyssal Sigil is required to enter.";
}
