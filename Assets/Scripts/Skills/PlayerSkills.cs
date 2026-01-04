using UnityEngine;
using UnityEngine.Serialization;

namespace Abyssbound.Skills
{
    public sealed class PlayerSkills : MonoBehaviour
    {
        [Header("XP")]
        [SerializeField] private int miningXp;
        [FormerlySerializedAs("forgingXp")]
        [SerializeField] private int smithingXp;
        [SerializeField] private int woodcuttingXp;
        [SerializeField] private int woodworkingXp;

        public int GetXp(SkillType s)
        {
            return s switch
            {
                SkillType.Mining => miningXp,
                SkillType.Smithing => smithingXp,
                SkillType.Woodcutting => woodcuttingXp,
                SkillType.Woodworking => woodworkingXp,
                _ => 0
            };
        }

        public int GetLevel(SkillType s)
        {
            return SkillMath.GetLevel(GetXp(s));
        }

        public void AddXp(SkillType s, int amount, string source = null)
        {
            if (amount <= 0)
                return;

            int before = GetXp(s);
            int after = before + amount;

            switch (s)
            {
                case SkillType.Mining:
                    miningXp = after;
                    break;
                case SkillType.Smithing:
                    smithingXp = after;
                    break;
                case SkillType.Woodcutting:
                    woodcuttingXp = after;
                    break;
                case SkillType.Woodworking:
                    woodworkingXp = after;
                    break;
            }

            int lvl = SkillMath.GetLevel(after);

            var src = string.IsNullOrWhiteSpace(source) ? string.Empty : $" ({source})";
            Debug.Log($"[XP] {s} +{amount} (Total={after}, Lvl={lvl}){src}");
        }

        public static PlayerSkills FindOrCreateOnPlayer()
        {
            GameObject player = null;
            try
            {
                player = GameObject.FindGameObjectWithTag("Player");
            }
            catch
            {
                player = null;
            }

            if (player == null)
            {
                Debug.LogWarning("[XP] No Player object found (tag 'Player').");
                return null;
            }

            var skills = player.GetComponent<PlayerSkills>();
            if (skills != null)
                return skills;

            return player.AddComponent<PlayerSkills>();
        }
    }
}
