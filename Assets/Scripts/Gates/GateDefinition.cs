using UnityEngine;

[CreateAssetMenu(menuName = "Abyssbound/Gates/Gate Definition")]
public class GateDefinition : ScriptableObject
{
    [Header("Gate Requirements")]
    public ItemDefinition requiredItem;

    [Header("Feedback Text")]
    [TextArea]
    public string lockedHintText = "Abyssal Sigil is required to enter.";
}
