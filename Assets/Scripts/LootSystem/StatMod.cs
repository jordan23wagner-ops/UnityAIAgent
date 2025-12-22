using System;

namespace Abyssbound.Loot
{
    [Serializable]
    public struct StatMod
    {
        public StatType stat;
        public float value;
        public bool percent;
    }
}
