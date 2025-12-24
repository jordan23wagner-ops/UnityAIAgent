using UnityEngine;

namespace Abyssbound.Loot
{
    public static class LootQaSettings
    {
        private const string Key = "LootQA_ZoneItemLevel";
        private const string KeyDebugLogs = "LootQA_DebugLogs";

        private static bool _loaded;
        private static int _itemLevel = 1;
        private static bool _hasOverride;

        private static bool _debugLogsLoaded;
        private static bool _debugLogsEnabled;

        public static bool HasItemLevelOverride
        {
            get
            {
                EnsureLoaded();
                return _hasOverride;
            }
        }

        public static bool DebugLogsEnabled
        {
            get
            {
                EnsureDebugLoaded();
                return _debugLogsEnabled;
            }
            set
            {
                _debugLogsLoaded = true;
                _debugLogsEnabled = value;
                try { PlayerPrefs.SetInt(KeyDebugLogs, value ? 1 : 0); PlayerPrefs.Save(); } catch { }
            }
        }

        public static int ItemLevel
        {
            get
            {
                EnsureLoaded();
                return Mathf.Clamp(_itemLevel, 1, 20);
            }
            set
            {
                _loaded = true;
                _itemLevel = Mathf.Clamp(value, 1, 20);
                _hasOverride = true;
                try { PlayerPrefs.SetInt(Key, _itemLevel); PlayerPrefs.Save(); } catch { }
            }
        }

        public static bool TryGetItemLevelOverride(out int itemLevel, out string source)
        {
            EnsureLoaded();

            if (_hasOverride)
            {
                itemLevel = Mathf.Clamp(_itemLevel, 1, 20);
                source = "QA";
                return true;
            }

            itemLevel = 1;
            source = "Default";
            return false;
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            int v = 1;
            bool has = false;
            try { has = PlayerPrefs.HasKey(Key); } catch { has = false; }
            try { v = PlayerPrefs.GetInt(Key, 1); } catch { v = 1; }

            _hasOverride = has;
            _itemLevel = Mathf.Clamp(v, 1, 20);
        }

        private static void EnsureDebugLoaded()
        {
            if (_debugLogsLoaded) return;
            _debugLogsLoaded = true;

            int v = 0;
            try { v = PlayerPrefs.GetInt(KeyDebugLogs, 0); } catch { v = 0; }
            _debugLogsEnabled = v != 0;
        }
    }
}
