using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyss.Waypoints
{
    [Serializable]
    public sealed class WaypointSaveData
    {
        public List<string> activatedIds = new();

        public static WaypointSaveData Load(string playerPrefsKey)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerPrefsKey))
                    return new WaypointSaveData();

                if (!PlayerPrefs.HasKey(playerPrefsKey))
                    return new WaypointSaveData();

                string json = PlayerPrefs.GetString(playerPrefsKey, string.Empty);
                if (string.IsNullOrWhiteSpace(json))
                    return new WaypointSaveData();

                var data = JsonUtility.FromJson<WaypointSaveData>(json);
                return data ?? new WaypointSaveData();
            }
            catch
            {
                return new WaypointSaveData();
            }
        }

        public void Save(string playerPrefsKey)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(playerPrefsKey))
                    return;

                string json = JsonUtility.ToJson(this);
                PlayerPrefs.SetString(playerPrefsKey, json);
                PlayerPrefs.Save();
            }
            catch
            {
                // Best-effort persistence only for v1.
            }
        }

        public bool Contains(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || activatedIds == null)
                return false;

            for (int i = 0; i < activatedIds.Count; i++)
            {
                if (string.Equals(activatedIds[i], id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public void Add(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;

            activatedIds ??= new List<string>();
            if (Contains(id))
                return;

            activatedIds.Add(id);
        }

        public HashSet<string> ToSetOrdinalIgnoreCase()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (activatedIds == null)
                return set;

            for (int i = 0; i < activatedIds.Count; i++)
            {
                var id = activatedIds[i];
                if (!string.IsNullOrWhiteSpace(id))
                    set.Add(id);
            }

            return set;
        }
    }
}
