using System;
using System.Text;

namespace Abyssbound.Stats
{
    [Serializable]
    public struct PlayerDerivedStats
    {
        // Compatibility-first derived values (match existing gameplay concepts).
        public int baseDamage;
        public int equipmentDamageBonus;
        public int strengthMeleeDamageBonus;
        public int damageFinal;

        public int baseMaxHealth;
        public int equipmentMaxHealthBonus;
        public int maxHealth;

        public int equipmentDamageReductionFlat;
        public int totalDamageReductionFlat;

        public static PlayerDerivedStats Zero => default;

        public void Clear() => this = default;

        public string ToMultilineString()
        {
            var sb = new StringBuilder(256);
            sb.Append("DMG Base: ").Append(baseDamage).Append('\n');
            sb.Append("DMG EquipBonus: ").Append(equipmentDamageBonus).Append('\n');
            sb.Append("DMG StrBonus: ").Append(strengthMeleeDamageBonus).Append('\n');
            sb.Append("DMG Final: ").Append(damageFinal).Append('\n');

            sb.Append("HP Base: ").Append(baseMaxHealth).Append('\n');
            sb.Append("HP EquipBonus: ").Append(equipmentMaxHealthBonus).Append('\n');
            sb.Append("HP Max: ").Append(maxHealth).Append('\n');

            sb.Append("DR Flat Equip: ").Append(equipmentDamageReductionFlat).Append('\n');
            sb.Append("DR Flat Total: ").Append(totalDamageReductionFlat);
            return sb.ToString();
        }
    }
}
