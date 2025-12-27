using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyssbound.Combat
{
    public interface IEnemyDefenceProvider
    {
        int DefenceLevel { get; }
    }

    public static class EnemyDefenseResolver
    {
        private static readonly HashSet<int> s_WarnedEnemyInstanceIds = new HashSet<int>();

        public static int GetEnemyDefenceLevel(EnemyHealth enemy)
        {
            if (enemy == null)
                return 1;

            // Prefer explicit providers.
            try
            {
                var providers = enemy.GetComponents<IEnemyDefenceProvider>();
                if (providers != null)
                {
                    for (int i = 0; i < providers.Length; i++)
                    {
                        var p = providers[i];
                        if (p == null) continue;
                        return Mathf.Max(1, p.DefenceLevel);
                    }
                }
            }
            catch { }

            // Fallback: minimal profile.
            EnemyCombatProfile profile = null;
            try { profile = enemy.GetComponent<EnemyCombatProfile>(); } catch { profile = null; }

            if (profile == null)
            {
                try { profile = enemy.gameObject.AddComponent<EnemyCombatProfile>(); }
                catch { profile = null; }
            }

            if (profile != null)
                return Mathf.Max(1, profile.defenceLevel);

            // Last resort.
            WarnOnce(enemy, "No enemy defence component found; assuming EnemyDefence=1.");
            return 1;
        }

        private static void WarnOnce(EnemyHealth enemy, string msg)
        {
            if (enemy == null)
                return;

            int id;
            try { id = enemy.GetInstanceID(); }
            catch { id = 0; }

            if (id != 0 && s_WarnedEnemyInstanceIds.Contains(id))
                return;

            if (id != 0)
                s_WarnedEnemyInstanceIds.Add(id);

            Debug.LogWarning($"[Accuracy] {msg} enemy='{enemy.name}'", enemy);
        }
    }
}
