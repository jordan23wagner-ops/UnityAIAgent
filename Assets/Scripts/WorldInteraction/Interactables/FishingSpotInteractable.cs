using System;
using System.Collections;
using System.Collections.Generic;
using Abyss.Items;
using Abyssbound.Skills.Gathering;
using Abyssbound.Skills.Fishing;
using Abyss.Equipment;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Abyssbound.WorldInteraction
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FishingSpotInteractable : WorldInteractable
    {
        private const bool DebugInventoryToolIdLog = true;
        private const string HighlightProxyName = "HighlightProxy";
        private const bool CheckEquippedAsFallback = true;
        private const bool DEBUG_COLLIDER = false;

#if UNITY_EDITOR
        private const string HighlightProxyMaterialPath = "Assets/Resources/Materials/WorldInteractionHighlightProxy.mat";
#endif

        [Header("Fishing")]
        [SerializeField] private string spotType = "Shrimp Spot";

        public string SpotType => spotType;

        private void Reset()
        {
            SetDisplayName("Fishing");
            SetRequiresRange(true);
            SetInteractionRange(3f);

            TryInferSpotType();

            EnsureSingleInteractionTriggerCollider();
            EnsureHighlightSetup();
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(spotType))
                TryInferSpotType();

            EnsureSingleInteractionTriggerCollider();
            EnsureHighlightSetup();
        }

        private void OnEnable()
        {
            // IMPORTANT:
            // Fishing spots often have their trigger collider on a baked child GameObject.
            // Do not create/enable a parent sphere and disable the child sphere; that breaks interaction.
            // Collider enforcement is handled by FishingSpotColliderEnforcer on the collider owner.
            EnsureSingleInteractionTriggerCollider();
            EnsureHighlightSetup();
        }

        public void SetSpotType(string value)
        {
            spotType = string.IsNullOrWhiteSpace(value) ? spotType : value;
        }

        public override string GetHoverText()
        {
            var t = string.IsNullOrWhiteSpace(spotType) ? "Fishing Spot" : spotType;
            return $"Fish: {t}";
        }

        public override bool CanInteract(GameObject interactor, out string reason)
        {
            // IMPORTANT:
            // Range gating for WorldInteraction is handled by WorldInteractionRaycaster using the hovered
            // RaycastHit collider bounds center. The base implementation checks distance from transform.position,
            // which can be wrong for grouped/parented fishing spots and caused bogus "Too far (447m)" rejects.
            // Fishing should only do fishing-specific gating here (tool checks, etc.).
            reason = null;

            // Tool gating (standardized): fishing requires basic fishing rod.
            try
            {
                string invSource = null;
                var inv = Game.Systems.PlayerInventoryResolver.GetOrFindWithDiagnostics(out invSource);
                if (inv == null)
                {
                    reason = "No inventory";
                    WorldInteractionFeedback.LogBlocked(reason, $"fish {SpotTypeForLog()}", this);
                    return false;
                }

                var required = ItemIds.FishingRodBasic;
                bool hasInInventory = inv.Has(required, 1);

                // Be resilient to case-only mismatches (some older debug / content flows may have added keys with different casing).
                string matchedInventoryId = null;
                if (!hasInInventory)
                {
                    TryHasCaseInsensitive(inv, required, out matchedInventoryId, out hasInInventory);
                }

                bool hasEquipped = false;
                string equippedSource = null;
                if (!hasInInventory && CheckEquippedAsFallback)
                {
                    hasEquipped = TryHasEquipped(required, out equippedSource);
                }

                if (!hasInInventory && !hasEquipped)
                {
                    reason = "missing Fishing Rod";

                    if (DebugInventoryToolIdLog)
                    {
                        try
                        {
                            var invToolIds = DescribeToolLikeIds(inv);
                            var eqToolIds = DescribeEquippedToolLikeIds();
                            var scope = CheckEquippedAsFallback ? "Inventory+Equipped" : "InventoryOnly";

                            Debug.Log(
                                $"[Fishing] Missing rod. RequiredRodId={required} CheckScope={scope} " +
                                $"InventorySource={invSource ?? "(unknown)"} EquippedSource={equippedSource ?? "(none)"} " +
                                $"Inventory contains tool ids: {invToolIds} Equipped contains tool ids: {eqToolIds}",
                                this);
                        }
                        catch { }
                    }

                    WorldInteractionFeedback.LogBlocked(reason, $"fish {SpotTypeForLog()}", this);
                    return false;
                }

                // Helpful one-time diagnostic if the item exists but with different casing.
                if (!string.IsNullOrWhiteSpace(matchedInventoryId) && !string.Equals(matchedInventoryId, required, System.StringComparison.Ordinal))
                {
                    Debug.LogWarning($"[Fishing] Rod present but inventory key casing differs. RequiredRodId={required} MatchedInventoryId={matchedInventoryId}", this);
                }
            }
            catch
            {
                reason = "missing Fishing Rod";
                WorldInteractionFeedback.LogBlocked(reason, $"fish {SpotTypeForLog()}", this);
                return false;
            }

            reason = null;
            return true;
        }

        public override void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor, out _))
                return;

            // Bridge WorldInteraction click -> legacy fishing loop.
            FishingSpot spot = null;
            try { spot = GetComponentInChildren<FishingSpot>(includeInactive: true); }
            catch { spot = null; }

            if (spot == null)
            {
                var t = string.IsNullOrWhiteSpace(spotType) ? name : spotType;
                Debug.LogWarning($"[Fishing] No FishingSpot component found under '{t}'.", this);
                return;
            }

            GatheringSkillController controller = null;
            try
            {
                if (interactor != null)
                    controller = interactor.GetComponentInParent<GatheringSkillController>();
            }
            catch { controller = null; }

            if (controller == null)
                controller = GatheringSkillController.GetOrAttachToPlayer();

            if (controller == null)
            {
                Debug.LogWarning("[Fishing] Missing GatheringSkillController.", this);
                return;
            }

            controller.StartGathering(spot);
        }

        private string SpotTypeForLog()
        {
            return string.IsNullOrWhiteSpace(spotType) ? "Fishing Spot" : spotType;
        }

        private void EnsureSingleInteractionTriggerCollider()
        {
            // IMPORTANT:
            // Fishing spots commonly keep their SphereCollider on a baked child.
            // Historically this method disabled all trigger colliders except one, which could disable the child sphere
            // and leave the spot non-interactable in Play Mode.
            //
            // Permanent policy for fishing:
            // - Never disable SphereColliders here.
            // - Disable BoxCollider triggers (they are not used for fishing interaction).
            // - If a FishingSpotColliderEnforcer exists anywhere in this hierarchy, it owns collider invariants.
            try
            {
                try
                {
                    var enforcer = GetComponentInChildren<FishingSpotColliderEnforcer>(includeInactive: true);
                    if (enforcer != null)
                        return;
                }
                catch { }

                var all = GetComponentsInChildren<Collider>(includeInactive: true);
                if (all == null || all.Length == 0)
                    return;
                for (int i = 0; i < all.Length; i++)
                {
                    var c = all[i];
                    if (c == null) continue;

                    // Fishing interaction uses SphereCollider triggers. Only disable BoxCollider triggers.
                    if (c is BoxCollider)
                    {
                        try { c.isTrigger = true; } catch { }
                        try { c.enabled = false; } catch { }
                    }
                    else if (c is SphereCollider)
                    {
                        // Ensure sphere triggers stay enabled.
                        try { c.isTrigger = true; } catch { }
                        try { c.enabled = true; } catch { }
                    }
                }
            }
            catch { }
        }

        private static void TryHasCaseInsensitive(PlayerInventory inv, string requiredId, out string matchedId, out bool has)
        {
            matchedId = null;
            has = false;
            if (inv == null || string.IsNullOrWhiteSpace(requiredId)) return;

            IReadOnlyDictionary<string, int> snap = null;
            try { snap = inv.GetAllItemsSnapshot(); } catch { snap = null; }
            if (snap == null || snap.Count == 0) return;

            foreach (var kv in snap)
            {
                var id = kv.Key;
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (kv.Value <= 0) continue;
                if (string.Equals(id, requiredId, System.StringComparison.OrdinalIgnoreCase))
                {
                    matchedId = id;
                    has = true;
                    return;
                }
            }
        }

        private static bool TryHasEquipped(string requiredId, out string source)
        {
            source = null;
            if (string.IsNullOrWhiteSpace(requiredId)) return false;

            PlayerEquipment eq = null;
            try
            {
                var hero = GameObject.Find("Player_Hero");
                if (hero != null)
                {
                    eq = hero.GetComponentInChildren<PlayerEquipment>(true);
                    if (eq != null) source = "Player_Hero";
                }
            }
            catch { eq = null; }

            if (eq == null)
            {
                try
                {
#if UNITY_2022_2_OR_NEWER
                    eq = UnityEngine.Object.FindFirstObjectByType<PlayerEquipment>(FindObjectsInactive.Exclude);
#else
                    eq = UnityEngine.Object.FindObjectOfType<PlayerEquipment>();
#endif
                    if (eq != null) source = "FindFirstObjectByType";
                }
                catch { eq = null; }
            }

            if (eq == null) return false;

            try
            {
                // Equipment slots are stored as strings on PlayerEquipment; check all slots.
                foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
                {
                    if (slot == EquipmentSlot.None) continue;
                    var id = eq.Get(slot);
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    if (string.Equals(id, requiredId, System.StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }

            return false;
        }

        private static string DescribeToolLikeIds(PlayerInventory inv)
        {
            if (inv == null) return "<none>";

            IReadOnlyDictionary<string, int> snap = null;
            try { snap = inv.GetAllItemsSnapshot(); } catch { snap = null; }

            if (snap == null || snap.Count == 0) return "<none>";

            var toolIds = new System.Collections.Generic.List<string>(8);
            foreach (var kv in snap)
            {
                var id = kv.Key;
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (kv.Value <= 0) continue;

                var lower = id.ToLowerInvariant();
                if (lower.StartsWith("tool_") || lower.Contains("rod") || lower.Contains("pickaxe") || lower.Contains("axe"))
                    toolIds.Add(id);
            }

            if (toolIds.Count == 0)
            {
                // Fallback: if we couldn't detect any tools, print all IDs.
                toolIds.Capacity = Mathf.Max(toolIds.Capacity, snap.Count);
                foreach (var kv in snap)
                {
                    if (kv.Value <= 0) continue;
                    if (!string.IsNullOrWhiteSpace(kv.Key))
                        toolIds.Add(kv.Key);
                }
            }

            toolIds.Sort(System.StringComparer.OrdinalIgnoreCase);
            return string.Join(", ", toolIds);
        }

        private static string DescribeEquippedToolLikeIds()
        {
            try
            {
                if (!TryGetEquipment(out var eq))
                    return "<none>";

                var ids = new System.Collections.Generic.List<string>(8);
                foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
                {
                    if (slot == EquipmentSlot.None) continue;
                    var id = eq.Get(slot);
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    var lower = id.ToLowerInvariant();
                    if (lower.StartsWith("tool_") || lower.Contains("rod") || lower.Contains("pickaxe") || lower.Contains("axe"))
                        ids.Add(id);
                }

                if (ids.Count == 0) return "<none>";
                ids.Sort(System.StringComparer.OrdinalIgnoreCase);
                return string.Join(", ", ids);
            }
            catch
            {
                return "<none>";
            }
        }

        private static bool TryGetEquipment(out PlayerEquipment eq)
        {
            eq = null;
            try
            {
                var hero = GameObject.Find("Player_Hero");
                if (hero != null)
                {
                    eq = hero.GetComponentInChildren<PlayerEquipment>(true);
                    if (eq != null) return true;
                }
            }
            catch { eq = null; }

            try
            {
#if UNITY_2022_2_OR_NEWER
                eq = UnityEngine.Object.FindFirstObjectByType<PlayerEquipment>(FindObjectsInactive.Exclude);
#else
                eq = UnityEngine.Object.FindObjectOfType<PlayerEquipment>();
#endif
            }
            catch { eq = null; }

            return eq != null;
        }

        private void EnsureHighlightSetup()
        {
            // Ensure HighlightRenderers is never empty for fishing spots.
            // If no child renderers exist (common for empty triggers), create a HighlightProxy renderer.
            try
            {
                var existing = HighlightRenderers;
                if (existing != null)
                {
                    bool any = false;
                    for (int i = 0; i < existing.Length; i++)
                    {
                        var r = existing[i];
                        if (r == null) continue;
                        any = true;

                        // Proxy should be hidden unless hovered.
                        try
                        {
                            if (r.gameObject != null && string.Equals(r.gameObject.name, HighlightProxyName, System.StringComparison.Ordinal))
                                r.enabled = false;
                        }
                        catch { }
                    }

                    if (any)
                        return;
                }
            }
            catch { }

            Renderer[] rs = null;
            try { rs = GetComponentsInChildren<Renderer>(includeInactive: true); }
            catch { rs = null; }

            if (rs != null && rs.Length > 0)
            {
                SetHighlightRenderers(rs);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    EditorUtility.SetDirty(this);
#endif
                return;
            }

            var proxyRenderer = EnsureHighlightProxyRenderer();
            if (proxyRenderer != null)
            {
                SetHighlightRenderers(new[] { proxyRenderer });
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    EditorUtility.SetDirty(this);
#endif
            }
        }

        private Renderer EnsureHighlightProxyRenderer()
        {
            Transform proxyTf = null;
            try { proxyTf = transform.Find(HighlightProxyName); } catch { proxyTf = null; }

            GameObject proxyGo;
            if (proxyTf == null)
            {
                proxyGo = new GameObject(HighlightProxyName);
                proxyGo.transform.SetParent(transform, worldPositionStays: false);
            }
            else
            {
                proxyGo = proxyTf.gameObject;
            }

            if (proxyGo == null)
                return null;

            // Keep it on the same layer.
            try { proxyGo.layer = gameObject.layer; } catch { }

            // Ensure no collider (must not affect raycasts).
            try
            {
                var cols = proxyGo.GetComponentsInChildren<Collider>(includeInactive: true);
                if (cols != null)
                {
                    for (int i = 0; i < cols.Length; i++)
                    {
                        var c = cols[i];
                        if (c == null) continue;
#if UNITY_EDITOR
                        if (!Application.isPlaying)
                            DestroyImmediate(c);
                        else
                            Destroy(c);
#else
                        Destroy(c);
#endif
                    }
                }
            }
            catch { }

            var mf = proxyGo.GetComponent<MeshFilter>();
            if (mf == null) mf = proxyGo.AddComponent<MeshFilter>();

            var mr = proxyGo.GetComponent<MeshRenderer>();
            if (mr == null) mr = proxyGo.AddComponent<MeshRenderer>();

            // Use built-in mesh (no collider).
            try
            {
                var mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                if (mesh == null) mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                mf.sharedMesh = mesh;
            }
            catch { }

            // Place at trigger center + a small Y offset.
            var localCenter = Vector3.zero;
            var sizeXZ = new Vector2(1.0f, 1.0f);

            try
            {
                var sphere = GetComponent<SphereCollider>();
                if (sphere != null)
                {
                    localCenter = sphere.center;
                    float d = Mathf.Max(0.5f, sphere.radius * 2f);
                    sizeXZ = new Vector2(d, d);
                }
                else
                {
                    var box = GetComponent<BoxCollider>();
                    if (box != null)
                    {
                        localCenter = box.center;
                        sizeXZ = new Vector2(Mathf.Max(0.5f, box.size.x), Mathf.Max(0.5f, box.size.z));
                    }
                    else
                    {
                        // Fallback to any collider bounds.
                        var any = GetComponent<Collider>();
                        if (any != null)
                        {
                            var world = any.bounds.center;
                            localCenter = transform.InverseTransformPoint(world);
                            var s = any.bounds.size;
                            sizeXZ = new Vector2(Mathf.Max(0.5f, s.x), Mathf.Max(0.5f, s.z));
                        }
                    }
                }
            }
            catch { }

            proxyGo.transform.localPosition = localCenter + new Vector3(0f, 0.10f, 0f);
            proxyGo.transform.localRotation = Quaternion.identity;

            float sx = Mathf.Clamp(sizeXZ.x, 0.75f, 1.25f);
            float sz = Mathf.Clamp(sizeXZ.y, 0.75f, 1.25f);
            proxyGo.transform.localScale = new Vector3(sx, 0.06f, sz);

            // Assign a simple material (shared) so MPB highlight color can show.
            try
            {
                var mat = ResolveHighlightProxyMaterial();
                if (mat != null)
                    mr.sharedMaterial = mat;
            }
            catch { }

            // Proxy should only be visible on hover.
            try { mr.enabled = false; } catch { }

            return mr;
        }

        private static Material ResolveHighlightProxyMaterial()
        {
#if UNITY_EDITOR
            // Prefer a persistent asset so Edit Mode visuals survive domain reloads.
            try
            {
                var existing = AssetDatabase.LoadAssetAtPath<Material>(HighlightProxyMaterialPath);
                if (existing != null)
                    return existing;

                // Create folder if needed.
                var dir = System.IO.Path.GetDirectoryName(HighlightProxyMaterialPath)?.Replace('\\', '/');
                if (!string.IsNullOrWhiteSpace(dir) && !AssetDatabase.IsValidFolder(dir))
                {
                    // ensure Assets/Resources/Materials exists
                    if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
                    if (!AssetDatabase.IsValidFolder("Assets/Resources/Materials")) AssetDatabase.CreateFolder("Assets/Resources", "Materials");
                }

                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");

                if (shader == null)
                    return null;

                var mat = new Material(shader);
                mat.name = "WorldInteractionHighlightProxy";

                // Default low-contrast; actual highlight is driven via MPB in WorldHoverHighlighter.
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.10f));
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(1f, 1f, 1f, 0.10f));

                AssetDatabase.CreateAsset(mat, HighlightProxyMaterialPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return mat;
            }
            catch { }
#endif

            // Runtime fallback (no asset creation).
            try
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");

                if (shader == null)
                    return null;

                var mat = new Material(shader);
                mat.name = "WorldInteractionHighlightProxy_Runtime";
                mat.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.10f));
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(1f, 1f, 1f, 0.10f));
                return mat;
            }
            catch { return null; }
        }

        private void TryInferSpotType()
        {
            // Try to infer from attached FishingSpot tier config, otherwise from name.
            try
            {
                var spot = GetComponent<FishingSpot>();
                if (spot != null && spot.TryGetFishingAction(out _, out _, out var yieldItemId, out _))
                {
                    var inferred = InferFromYieldId(yieldItemId);
                    if (!string.IsNullOrWhiteSpace(inferred))
                    {
                        spotType = inferred;
                        return;
                    }
                }
            }
            catch { }

            try
            {
                var inferred = InferFromName(gameObject != null ? gameObject.name : string.Empty);
                if (!string.IsNullOrWhiteSpace(inferred))
                    spotType = inferred;
            }
            catch { }
        }

        private static string InferFromYieldId(string yieldItemId)
        {
            if (string.IsNullOrWhiteSpace(yieldItemId))
                return null;

            var id = yieldItemId.ToLowerInvariant();
            if (id.Contains("shrimp")) return "Shrimp Spot";
            if (id.Contains("anchovy")) return "Anchovy Spot";
            if (id.Contains("sardine")) return "Sardine Spot";
            if (id.Contains("trout")) return "Trout Spot";
            if (id.Contains("salmon")) return "Salmon Spot";
            if (id.Contains("lobster")) return "Lobster Spot";
            if (id.Contains("tuna")) return "Tuna Spot";
            if (id.Contains("sword")) return "Swordfish Spot";

            return null;
        }

        private static string InferFromName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var n = name.ToLowerInvariant();
            if (n.Contains("shrimp")) return "Shrimp Spot";
            if (n.Contains("anchovy")) return "Anchovy Spot";
            if (n.Contains("sardine")) return "Sardine Spot";
            if (n.Contains("trout")) return "Trout Spot";
            if (n.Contains("salmon")) return "Salmon Spot";
            if (n.Contains("lobster")) return "Lobster Spot";
            if (n.Contains("tuna")) return "Tuna Spot";
            if (n.Contains("sword")) return "Swordfish Spot";

            // Generic fallback.
            if (n.Contains("fish") || n.Contains("fishing")) return "Fishing Spot";

            return null;
        }
    }
}
