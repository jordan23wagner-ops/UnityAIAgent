using UnityEngine;

namespace Abyss.Shop
{
    [DisallowMultipleComponent]
    public sealed class MerchantDoorClickTarget : MonoBehaviour
    {
        [Header("Highlight")]
        [SerializeField] private Renderer[] highlightRenderers;

        [SerializeField] private Color highlightColor = Color.red;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private bool _highlighted;
        private MaterialPropertyBlock _mpb;

        private struct RendererState
        {
            public Renderer renderer;
            public bool hasColor;
            public bool hasBaseColor;
            public Color baseColor;
        }

        private RendererState[] _states;

        public string GetDisplayName()
        {
            var shop = GetComponentInParent<MerchantShop>();
            return shop != null ? shop.MerchantName : string.Empty;
        }

        public bool TryGetBounds(out Bounds bounds)
        {
            bounds = default;

            if (_states != null && _states.Length > 0)
            {
                bool hasAny = false;
                for (int i = 0; i < _states.Length; i++)
                {
                    var r = _states[i].renderer;
                    if (r == null) continue;
                    if (!hasAny) { bounds = r.bounds; hasAny = true; }
                    else bounds.Encapsulate(r.bounds);
                }

                if (hasAny) return true;
            }

            var col = GetComponent<Collider>();
            if (col != null)
            {
                bounds = col.bounds;
                return true;
            }

            return false;
        }

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            RebuildStates();
            SetHighlighted(false);

            // Minimal merchant tooltip integration:
            // World tooltip comes from WorldInteractable.GetHoverText(); attach a provider on the hit object.
            // Interaction is disabled in the provider so merchant clicks remain handled by MerchantShop.
            try
            {
                if (GetComponent<MerchantTooltipWorldInteractable>() == null)
                    gameObject.AddComponent<MerchantTooltipWorldInteractable>();
            }
            catch
            {
                // never throw
            }
        }

        public void SetHighlightRenderers(Renderer[] renderers)
        {
            highlightRenderers = renderers;

            // If this was called at runtime after Awake, rebuild state.
            if (_mpb != null)
                RebuildStates();
        }

        public void SetHighlightColor(Color color)
        {
            highlightColor = color;
            if (_highlighted)
                SetHighlighted(true);
        }

        private void RebuildStates()
        {
            if (highlightRenderers == null || highlightRenderers.Length == 0)
            {
                _states = System.Array.Empty<RendererState>();
                return;
            }

            _states = new RendererState[highlightRenderers.Length];
            for (int i = 0; i < highlightRenderers.Length; i++)
            {
                var r = highlightRenderers[i];
                if (r == null)
                {
                    _states[i] = new RendererState();
                    continue;
                }

                var mat = r.sharedMaterial;
                bool hasColor = mat != null && mat.HasProperty(ColorId);
                bool hasBaseColor = mat != null && mat.HasProperty(BaseColorId);

                Color baseCol = Color.white;
                if (mat != null)
                {
                    if (hasBaseColor) baseCol = mat.GetColor(BaseColorId);
                    else if (hasColor) baseCol = mat.GetColor(ColorId);
                }

                _states[i] = new RendererState
                {
                    renderer = r,
                    hasColor = hasColor,
                    hasBaseColor = hasBaseColor,
                    baseColor = baseCol
                };
            }
        }

        public void SetHighlighted(bool highlighted)
        {
            if (_highlighted == highlighted) return;
            _highlighted = highlighted;

            if (_states == null || _states.Length == 0) return;

            for (int i = 0; i < _states.Length; i++)
            {
                var st = _states[i];
                if (st.renderer == null) continue;

                st.renderer.GetPropertyBlock(_mpb);

                var c = highlighted ? highlightColor : st.baseColor;
                if (st.hasBaseColor) _mpb.SetColor(BaseColorId, c);
                else if (st.hasColor) _mpb.SetColor(ColorId, c);

                st.renderer.SetPropertyBlock(_mpb);
            }
        }

        private void OnDisable()
        {
            if (_highlighted)
                SetHighlighted(false);
        }
    }
}
