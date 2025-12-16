using UnityEngine;
using UnityEngine.InputSystem;

[System.Obsolete(
    "DebugPlayerMover_NewInput is deprecated. Movement is now handled by ClickToMove / Intent system.",
    false
)]
public class DebugPlayerMover_NewInput : MonoBehaviour
{
#pragma warning disable 0414
    [SerializeField] private float moveSpeed = 6f;
#pragma warning restore 0414
}
