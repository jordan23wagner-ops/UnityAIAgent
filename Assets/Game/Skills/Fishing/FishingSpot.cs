using Abyssbound.Skills.Gathering;
using UnityEngine;

namespace Abyssbound.Skills.Fishing
{
    [DisallowMultipleComponent]
    public sealed class FishingSpot : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private FishingSkillConfigSO config;
        [SerializeField] private int tierIndex;

        [Header("Debug Visuals")]
        [SerializeField] private bool showDebugVisuals = true;

        [Header("Mode")]
        [SerializeField] private bool mobileSchool;

        private bool _playerInRange;
        private GatheringSkillController _controller;

        private void Awake()
        {
            if (!showDebugVisuals)
                return;

            // If any child already has a renderer, assume visuals are provided.
            try
            {
                var existing = GetComponentsInChildren<Renderer>(includeInactive: true);
                if (existing != null)
                {
                    for (int i = 0; i < existing.Length; i++)
                    {
                        var r = existing[i];
                        if (r == null) continue;
                        if (r.transform == transform) continue;
                        return;
                    }
                }
            }
            catch { }

            CreateDebugVisualIfMissing();
        }

        private void Update()
        {
            if (!_playerInRange)
                return;

            if (_controller == null)
                return;

            if (Input.GetKeyDown(KeyCode.E))
            {
                _controller.StartGathering(this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!TryResolvePlayerRoot(other, out var playerRoot))
                return;

            if (playerRoot == null)
                return;

            _playerInRange = true;

            // Ensure the player has the GatheringSkillController component.
            try
            {
                _controller = playerRoot.GetComponent<GatheringSkillController>();
                if (_controller == null)
                    _controller = playerRoot.AddComponent<GatheringSkillController>();
            }
            catch
            {
                _controller = null;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!TryResolvePlayerRoot(other, out var playerRoot))
                return;

            if (!_playerInRange)
                return;

            _playerInRange = false;

            try { _controller?.StopGathering(this); }
            catch { }
        }

        private static bool TryResolvePlayerRoot(Collider other, out GameObject playerRoot)
        {
            playerRoot = null;
            if (other == null) return false;

            // Primary: PlayerInputAuthority (most reliable in this project).
            try
            {
                var auth = other.GetComponentInParent<Game.Input.PlayerInputAuthority>();
                if (auth != null)
                {
                    playerRoot = auth.gameObject;
                    return true;
                }
            }
            catch { }

            // Fallback: tagged player.
            try
            {
                if (other.CompareTag("Player"))
                {
                    playerRoot = other.gameObject;
                    return true;
                }
            }
            catch { }

            // Fallback: player stats component.
            try
            {
                var stats = other.GetComponentInParent<Abyssbound.Stats.PlayerStatsRuntime>();
                if (stats != null)
                {
                    playerRoot = stats.gameObject;
                    return true;
                }
            }
            catch { }

            return false;
        }

        public bool TryGetFishingAction(out float seconds, out int actionXp, out string yieldItemId, out int yieldAmount)
        {
            seconds = 0f;
            actionXp = 0;
            yieldItemId = string.Empty;
            yieldAmount = 0;

            if (config == null)
                return false;

            if (!config.TryGetTier(tierIndex, out var tier) || tier == null)
                return false;

            seconds = Mathf.Max(0.1f, tier.actionSeconds);
            if (mobileSchool)
                seconds *= 1.15f;

            actionXp = Mathf.Max(0, tier.actionXp);
            yieldItemId = tier.yieldItemId;
            yieldAmount = Mathf.Max(0, tier.yieldAmount);
            return true;
        }

        private void CreateDebugVisualIfMissing()
        {
            // Create a simple primitive marker that does not rely on any art.
            GameObject marker = null;
            try { marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder); }
            catch { marker = null; }
            if (marker == null)
                return;

            marker.name = "__FishingSpotDebug";
            marker.transform.SetParent(transform, worldPositionStays: false);
            marker.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            marker.transform.localRotation = Quaternion.identity;

            // Remove the primitive collider so it doesn't interfere with interaction/physics.
            try
            {
                var c = marker.GetComponent<Collider>();
                if (c != null) Destroy(c);
            }
            catch { }

            // Scale roughly to match trigger footprint.
            // (Cylinder is 1 unit diameter, 2 units height by default)
            var sizeXZ = new Vector2(1.25f, 1.25f);
            try
            {
                var box = GetComponent<BoxCollider>();
                if (box != null)
                {
                    sizeXZ = new Vector2(Mathf.Max(0.1f, box.size.x), Mathf.Max(0.1f, box.size.z));
                }
                else
                {
                    var any = GetComponent<Collider>();
                    if (any != null)
                    {
                        var b = any.bounds;
                        sizeXZ = new Vector2(Mathf.Max(0.1f, b.size.x), Mathf.Max(0.1f, b.size.z));
                    }
                }
            }
            catch { }

            marker.transform.localScale = new Vector3(sizeXZ.x, 0.10f, sizeXZ.y);

            // Make it clearly visible.
            try
            {
                var mr = marker.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) shader = Shader.Find("Standard");
                    if (shader == null) shader = Shader.Find("Unlit/Color");

                    if (shader != null)
                    {
                        var mat = new Material(shader);
                        mat.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

                        var color = new Color(0.1f, 0.9f, 1.0f, 0.85f);
                        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

                        mr.sharedMaterial = mat;
                    }
                }
            }
            catch { }

            try { marker.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild; }
            catch { }
        }

        public void Interact()
        {
            // Keep compatibility with click-to-move SendMessage("Interact") without adding UI/tool/inventory checks.
            if (_controller == null)
            {
                try { _controller = GatheringSkillController.GetOrAttachToPlayer(); }
                catch { _controller = null; }
            }

            if (_controller == null)
                return;

            _controller.StartGathering(this);
        }
    }
}
