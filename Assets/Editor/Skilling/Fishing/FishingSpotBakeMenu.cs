#if UNITY_EDITOR
using Abyssbound.Skills.Fishing;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abyssbound.EditorTools.Skilling.Fishing
{
    public static class FishingSpotBakeMenu
    {
        [MenuItem("Tools/Skilling/Fishing/Bake Fishing Spots Into Scene")]
        public static void BakeFishingSpotsIntoScene()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[Fishing] Stop Play Mode before baking fishing spots into the scene.");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[Fishing] No valid active scene.");
                return;
            }

            // Root.
            var root = GameObject.Find(FishingSpotAutoSpawner.FishingSpotsRootName);
            if (root == null)
            {
                root = new GameObject(FishingSpotAutoSpawner.FishingSpotsRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create FishingSpots Root");
            }

            // Replace children.
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    var child = root.transform.GetChild(i);
                    if (child == null) continue;
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
            catch { }

            // Force runtime auto-spawner to run once by entering Play after baking.
            // We intentionally reuse its baked-root contract: if baked children exist, runtime spawning is suppressed.

            // Bake by temporarily running the same creation logic as the runtime spawner uses.
            // We do this by simulating a Play-mode spawn pass in Edit Mode.
            var cfg = Resources.Load<FishingSkillConfigSO>("Skills/Fishing/FishingSkillConfig");
            if (cfg == null)
            {
                Debug.LogWarning("[Fishing] Missing Resources/Skills/Fishing/FishingSkillConfig.asset. Create it first (or restore it) before baking.");
                return;
            }

            // Use the same anchor rules as the runtime spawner.
            var (anchorPos, anchorRot) = GetAnchorPoseForBake();

            var defs = new[]
            {
                (localOffset: new Vector3(8f, 0f, 10f), tierIndex: 0, mobileSchool: false),
                (localOffset: new Vector3(11f, 0f, 10f), tierIndex: 1, mobileSchool: false),
            };

            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];
                var worldPos = anchorPos + (anchorRot * d.localOffset);
                worldPos = SnapToGroundOrZero(worldPos);

                var go = new GameObject($"FishingSpot_{d.tierIndex}_Baked_{i}");
                Undo.RegisterCreatedObjectUndo(go, "Bake Fishing Spot");

                // Match QA tool footprint.
                var col = go.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0f, 1f, 0f);
                col.size = new Vector3(1.5f, 2f, 1.5f);

                var spot = go.AddComponent<FishingSpot>();
                TryConfigureSpot(spot, cfg, d.tierIndex, d.mobileSchool);

                go.transform.SetPositionAndRotation(worldPos, Quaternion.identity);
                go.transform.SetParent(root.transform, worldPositionStays: true);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[Fishing] Baked {defs.Length} fishing spot(s) under '{FishingSpotAutoSpawner.FishingSpotsRootName}' in scene '{scene.name}'.");
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

        private static (Vector3 pos, Quaternion rot) GetAnchorPoseForBake()
        {
            var anchor = GameObject.Find("Town_SpawnRoot")?.transform
                         ?? GameObject.Find("PlayerSpawn_Town")?.transform
                         ?? GameObject.Find("Player_Hero")?.transform;

            if (anchor != null)
                return (anchor.position, anchor.rotation);

            return (Vector3.zero, Quaternion.identity);
        }

        private static Vector3 SnapToGroundOrZero(Vector3 pos)
        {
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

            pos.y = 0f;
            return pos;
        }
    }
}
#endif
