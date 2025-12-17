using System.Collections.Generic;
using UnityEngine;

namespace Game.Town
{
    public sealed class TownRegistry : MonoBehaviour
    {
        private static TownRegistry _instance;

        public static TownRegistry Instance
        {
            get
            {
                if (_instance != null) return _instance;

                _instance = FindFirstObjectByType<TownRegistry>();
                if (_instance != null) return _instance;

                var go = new GameObject("TownRegistry");
                _instance = go.AddComponent<TownRegistry>();

                if (Application.isPlaying)
                    DontDestroyOnLoad(go);

                return _instance;
            }
        }

        [Header("Debug")]
        [SerializeField] private bool verboseLogs = true;
        [SerializeField] private int debugCount = 0;

        private readonly Dictionary<string, GameObject> _byKey = new Dictionary<string, GameObject>(128);

        public Transform SpawnRoot { get; private set; }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                DestroySafely(gameObject);
                return;
            }

            _instance = this;

            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);

            EnsureSpawnRoot();
        }

        public void EnsureSpawnRoot()
        {
            if (SpawnRoot != null) return;

            var rootGo = GameObject.Find("Town_SpawnRoot");
            if (rootGo == null) rootGo = new GameObject("Town_SpawnRoot");
            SpawnRoot = rootGo.transform;
        }

        public bool TryGet(string key, out GameObject go)
        {
            if (_byKey.TryGetValue(key, out go) && go != null) return true;
            go = null;
            return false;
        }

        public void RebuildIndexFromScene()
        {
            EnsureSpawnRoot();
            _byKey.Clear();

            if (SpawnRoot == null) return;

            var tags = SpawnRoot.GetComponentsInChildren<TownKeyTag>(true);
            foreach (var tag in tags)
            {
                if (tag == null) continue;
                if (string.IsNullOrWhiteSpace(tag.Key)) continue;

                if (_byKey.ContainsKey(tag.Key))
                {
                    if (verboseLogs)
                        Debug.LogWarning($"[TownRegistry] Duplicate key during rebuild: '{tag.Key}' on '{tag.gameObject.name}'. Keeping first.");
                    continue;
                }

                _byKey[tag.Key] = tag.gameObject;
            }

            debugCount = _byKey.Count;

            if (verboseLogs)
                Debug.Log($"[TownRegistry] Rebuilt index from scene. Keys={_byKey.Count}");
        }

        public GameObject RegisterOrKeep(string key, GameObject candidate)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                Debug.LogError("[TownRegistry] Attempted to register with null/empty key.");
                return candidate;
            }

            if (candidate == null)
            {
                Debug.LogError($"[TownRegistry] Attempted to register null candidate for key '{key}'.");
                return null;
            }

            EnsureSpawnRoot();

#if UNITY_EDITOR
            if (!Application.isPlaying && _byKey.Count == 0)
                RebuildIndexFromScene();
#endif

            if (_byKey.TryGetValue(key, out var existing) && existing != null)
            {
                if (verboseLogs)
                    Debug.LogWarning($"[TownRegistry] Duplicate spawn blocked. Key='{key}'. Keeping '{existing.name}', destroying '{candidate.name}'.");

                DestroySafely(candidate);
                return existing;
            }

            _byKey[key] = candidate;
            debugCount = _byKey.Count;

            var tag = candidate.GetComponent<TownKeyTag>();
            if (tag == null) tag = candidate.AddComponent<TownKeyTag>();
            tag.SetKey(key);

            candidate.name = $"{candidate.name} [TownKey:{key}]";
            candidate.transform.SetParent(SpawnRoot, true);

            if (verboseLogs)
                Debug.Log($"[TownRegistry] Registered key='{key}' => '{candidate.name}'");

            return candidate;
        }

        public void DestroyAllRegistered()
        {
            foreach (var kv in _byKey)
                if (kv.Value != null)
                    DestroySafely(kv.Value);

            _byKey.Clear();
            debugCount = 0;

            if (SpawnRoot != null)
                for (int i = SpawnRoot.childCount - 1; i >= 0; i--)
                    DestroySafely(SpawnRoot.GetChild(i).gameObject);

            if (verboseLogs)
                Debug.Log("[TownRegistry] DestroyAllRegistered complete.");
        }

        private static void DestroySafely(GameObject go)
        {
            if (go == null) return;

#if UNITY_EDITOR
            if (!Application.isPlaying) Object.DestroyImmediate(go);
            else Object.Destroy(go);
#else
            Object.Destroy(go);
#endif
        }
    }
}
