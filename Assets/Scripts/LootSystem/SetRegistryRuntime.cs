using System;
using System.Collections.Generic;
using UnityEngine;

namespace Abyssbound.Loot
{
    [DisallowMultipleComponent]
    public sealed class SetRegistryRuntime : MonoBehaviour
    {
        private const string SetsResourcesPath = "Loot/Sets";

        public static SetRegistryRuntime Instance { get; private set; }

        private readonly Dictionary<string, SetDefinitionSO> _setsById = new(StringComparer.OrdinalIgnoreCase);
        private bool _built;

        public static SetRegistryRuntime GetOrCreate()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("SetRegistryRuntime", typeof(SetRegistryRuntime));
            DontDestroyOnLoad(go);
            return go.GetComponent<SetRegistryRuntime>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildIfNeeded();
        }

        private void BuildIfNeeded()
        {
            if (_built) return;
            _built = true;

            _setsById.Clear();

            SetDefinitionSO[] all = null;
            try { all = Resources.LoadAll<SetDefinitionSO>(SetsResourcesPath); } catch { all = null; }
            if (all == null || all.Length == 0) return;

            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i];
                if (s == null) continue;
                if (string.IsNullOrWhiteSpace(s.id)) continue;
                if (!_setsById.ContainsKey(s.id))
                    _setsById[s.id] = s;
            }
        }

        public bool TryGetSet(string id, out SetDefinitionSO set)
        {
            set = null;
            if (string.IsNullOrWhiteSpace(id)) return false;
            BuildIfNeeded();
            return _setsById.TryGetValue(id, out set) && set != null;
        }

        public IReadOnlyDictionary<string, SetDefinitionSO> GetAllSets()
        {
            BuildIfNeeded();
            return _setsById;
        }
    }
}
