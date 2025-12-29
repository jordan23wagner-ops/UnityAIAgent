using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyssbound.Skills.Fishing
{
    /// <summary>
    /// Ensures a small, deterministic set of FishingSpot instances exist at runtime for scenes
    /// that have not yet been baked.
    /// 
    /// If a [FishingSpots] root exists with children, this spawner does nothing.
    /// </summary>
    public static class FishingSpotAutoSpawner
    {
        public const string FishingSpotsRootName = "[FishingSpots]";

        private const string ConfigResourcesPath = "Skills/Fishing/FishingSkillConfig";

        // Tuned to be near typical spawn roots; these are only a fallback.
        private static readonly SpawnDef[] DefaultDefs =
        {
            new SpawnDef(localOffset: new Vector3(8f, 0f, 10f), tierIndex: 0, mobileSchool: false),
            new SpawnDef(localOffset: new Vector3(11f, 0f, 10f), tierIndex: 1, mobileSchool: false),
        };

        private readonly struct SpawnDef
        {
            public readonly Vector3 localOffset;
            public readonly int tierIndex;
            public readonly bool mobileSchool;

            public SpawnDef(Vector3 localOffset, int tierIndex, bool mobileSchool)
            {
                this.localOffset = localOffset;
                this.tierIndex = tierIndex;
                this.mobileSchool = mobileSchool;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureFishingSpotsExist()
        {
            if (!Application.isPlaying)
                return;

            try
            {
                if (HasBakedFishingSpots())
                    return;

                var cfg = LoadConfig();
                if (cfg == null)
                    return;

                var root = GetOrCreateRoot();
                var (anchorPos, anchorRot) = ResolveAnchorPose();

                for (int i = 0; i < DefaultDefs.Length; i++)
                {
                    var def = DefaultDefs[i];
                    var worldPos = anchorPos + (anchorRot * def.localOffset);
                    worldPos = SnapToGroundOrZero(worldPos);

                    var go = CreateFishingSpotGameObject(cfg, def.tierIndex, def.mobileSchool);
                    go.name = $"FishingSpot_{def.tierIndex}_Auto_{i}";
                    go.transform.SetPositionAndRotation(worldPos, Quaternion.identity);
                    go.transform.SetParent(root.transform, worldPositionStays: true);
                }
            }
            catch
            {
                // No hard failures; this is a convenience fallback.
            }
        }

        public static bool HasBakedFishingSpots()
        {
            var root = GameObject.Find(FishingSpotsRootName);
            if (root == null)
                return false;

            try
            {
                // Any children = scene has baked spots.
                return root.transform.childCount > 0;
            }
            catch
            {
                return false;
            }
        }

        private static FishingSkillConfigSO LoadConfig()
        {
            FishingSkillConfigSO cfg = null;
            try { cfg = Resources.Load<FishingSkillConfigSO>(ConfigResourcesPath); } catch { cfg = null; }

            if (cfg == null)
            {
                Debug.LogWarning($"[Fishing] No FishingSkillConfigSO found at Resources/{ConfigResourcesPath}.asset. Create one or bake fishing spots.");
            }

            return cfg;
        }

        private static GameObject GetOrCreateRoot()
        {
            var root = GameObject.Find(FishingSpotsRootName);
            if (root != null)
                return root;

            root = new GameObject(FishingSpotsRootName);
            return root;
        }

        private static (Vector3 pos, Quaternion rot) ResolveAnchorPose()
        {
            // Prefer stable, authored scene transforms.
            var anchor = GameObject.Find("Town_SpawnRoot")?.transform
                         ?? GameObject.Find("PlayerSpawn_Town")?.transform
                         ?? GameObject.Find("Player_Hero")?.transform;

            if (anchor != null)
                return (anchor.position, anchor.rotation);

            // Fall back: player input authority if present.
            try
            {
#if UNITY_2022_2_OR_NEWER
                var auth = Object.FindFirstObjectByType<Game.Input.PlayerInputAuthority>(FindObjectsInactive.Exclude);
#else
                var auth = Object.FindObjectOfType<Game.Input.PlayerInputAuthority>();
#endif
                if (auth != null)
                    return (auth.transform.position, auth.transform.rotation);
            }
            catch { }

            // Final fallback: origin.
            return (Vector3.zero, Quaternion.identity);
        }

        private static Vector3 SnapToGroundOrZero(Vector3 pos)
        {
            // Try to raycast down onto any collider in the scene.
            try
            {
                var rayStart = pos + Vector3.up * 200f;
                if (Physics.Raycast(rayStart, Vector3.down, out var hit, 500f, ~0, QueryTriggerInteraction.Ignore))
                {
                    pos.y = hit.point.y;
                    return pos;
                }
            }
            catch { }

            // Default to Y=0.
            pos.y = 0f;
            return pos;
        }

        private static GameObject CreateFishingSpotGameObject(FishingSkillConfigSO cfg, int tierIndex, bool mobileSchool)
        {
            var go = new GameObject("FishingSpot");

            // Match QA tool footprint.
            try
            {
                var col = go.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, 1f, 0f);
                col.size = new Vector3(1.5f, 2f, 1.5f);
            }
            catch { }

            var spot = go.AddComponent<FishingSpot>();
            TryConfigureSpot(spot, cfg, tierIndex, mobileSchool);

            return go;
        }

        private static void TryConfigureSpot(FishingSpot spot, FishingSkillConfigSO cfg, int tierIndex, bool mobileSchool)
        {
            if (spot == null) return;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

            try
            {
                var t = typeof(FishingSpot);

                var configField = t.GetField("config", flags);
                if (configField != null)
                    configField.SetValue(spot, cfg);

                var tierField = t.GetField("tierIndex", flags);
                if (tierField != null)
                    tierField.SetValue(spot, Mathf.Max(0, tierIndex));

                var mobileField = t.GetField("mobileSchool", flags);
                if (mobileField != null)
                    mobileField.SetValue(spot, mobileSchool);
            }
            catch { }
        }
    }
}
