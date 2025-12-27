using UnityEngine;
using Abyssbound.Loot;

namespace Abyssbound.Combat
{
    public static class WeaponAttackTypeResolver
    {
        // Demonstrates-correctness helper: resolve weapon type from ItemInstance stat mods.
        // Fallback inference MUST exist and MUST NOT rely only on ItemDefinitionSO fields.
        public static WeaponAttackType ResolveWeaponAttackType(ItemInstance weaponInstance)
        {
            if (weaponInstance == null)
                return WeaponAttackType.Melee;

            LootRegistryRuntime registry = null;
            try
            {
                registry = LootRegistryRuntime.GetOrCreate();
                registry.BuildIfNeeded();
            }
            catch
            {
                registry = null;
            }

            if (registry == null)
                return WeaponAttackType.Melee;

            try
            {
                var mods = weaponInstance.GetAllStatMods(registry);
                if (mods != null)
                {
                    // Prefer Magic over Ranged if both somehow exist.
                    for (int i = 0; i < mods.Count; i++)
                    {
                        var m = mods[i];
                        if (m.percent) continue;
                        if (m.stat == StatType.MagicDamage && m.value > 0f)
                            return WeaponAttackType.Magic;
                    }

                    for (int i = 0; i < mods.Count; i++)
                    {
                        var m = mods[i];
                        if (m.percent) continue;
                        if (m.stat == StatType.RangedDamage && m.value > 0f)
                            return WeaponAttackType.Ranged;
                    }
                }
            }
            catch
            {
                // ignore
            }

            return WeaponAttackType.Melee;
        }
    }
}
