using UnityEngine;

namespace Abyss.Enemies
{
    [CreateAssetMenu(menuName = "Abyss/Enemies/Enemy Definition")]
    public class EnemyDefinition : ScriptableObject
    {
        public string enemyId;
        public string displayName;
        [TextArea] public string description;

        [Header("Visuals")]
        public GameObject prefab; // You'll drag this manually after import for now
        public Sprite icon;

        [Header("Combat Stats")]
        public float maxHealth = 100f;
        public float damage = 10f;
        public float moveSpeed = 3.5f;
        public float attackRange = 1.5f;
        public float attackCooldown = 1.5f;
        public float expReward = 10f;

        [Header("AI Behavior")]
        public float detectionRadius = 10f;
        public float chaseRadius = 15f;
    }
}