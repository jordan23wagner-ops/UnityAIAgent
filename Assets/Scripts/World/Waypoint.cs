using UnityEngine;

namespace Abyssbound.World
{
    public class Waypoint : MonoBehaviour
    {
        [Header("Waypoint Settings")]
        public string waypointName = "Deep Wilderness Ruins";
        public bool isUnlocked = false;

        [Header("Visual Feedback")]
        public Light auraLight;
        public Color lockedColor = Color.red;
        public Color unlockedColor = Color.cyan;

        private void Start()
        {
            UpdateVisuals();
        }

        private void OnTriggerEnter(Collider other)
        {
            // Simple check: Is the object hitting this the player?
            if (other.CompareTag("Player") && !isUnlocked)
            {
                UnlockWaypoint();
            }
        }

        private void UnlockWaypoint()
        {
            isUnlocked = true;
            UpdateVisuals();
            
            // SpawnPK style massive drop/unlock dopamine hit
            Debug.Log($"<color=cyan>[WAYPOINT UNLOCKED]</color> You survived the trek to: {waypointName}!");
            
            // TODO: Hook this into the Player's save data/fast travel menu later
        }

        private void UpdateVisuals()
        {
            if (auraLight != null)
            {
                auraLight.color = isUnlocked ? unlockedColor : lockedColor;
            }
        }
    }
}
