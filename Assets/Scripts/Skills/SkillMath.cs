using UnityEngine;

namespace Abyssbound.Skills
{
    public static class SkillMath
    {
        // v1 curve (simple):
        // Level 1 at 0 xp; xp ~ 100*(level-1)^2.
        public static int GetLevel(int xp)
        {
            xp = Mathf.Max(0, xp);
            return Mathf.Max(1, Mathf.FloorToInt(Mathf.Sqrt(xp / 100f)) + 1);
        }

        // Alias to match older naming in docs.
        public static int LevelFromXp(int xp) => GetLevel(xp);

        public static int GetXpForLevel(int level)
        {
            level = Mathf.Max(1, level);
            int n = level - 1;
            return 100 * n * n;
        }
    }
}
