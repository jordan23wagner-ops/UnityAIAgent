using UnityEngine;

public enum PlayerIntentKind
{
    None = 0,
    MoveDestination = 1,
    CombatTarget = 2,
    InteractTarget = 3,
}

[System.Serializable]
public struct PlayerIntent
{
    public PlayerIntentKind kind;
    public Vector3 destination;
    public Transform target;

    public static PlayerIntent None()
    {
        return new PlayerIntent { kind = PlayerIntentKind.None, destination = Vector3.zero, target = null };
    }
}
