using UnityEngine;
using UnityEngine.Serialization;

namespace Abyssbound.WorldInteraction
{
    public class WorldInteractable : MonoBehaviour
    {
        [Header("World Interaction")]
        [SerializeField] private string displayName = "Interactable";

        [FormerlySerializedAs("interactRange")]
        [SerializeField] private float interactionRange = 3f;

        [SerializeField] private bool requiresRange = true;
        [SerializeField] private Renderer[] highlightRenderers;

        public string DisplayName => displayName;
        public float InteractionRange => interactionRange;
        public bool RequiresRange => requiresRange;
        public Renderer[] HighlightRenderers => highlightRenderers;

        public virtual bool CanInteract(GameObject interactor, out string reason)
        {
            reason = null;

            if (!requiresRange)
                return true;

            var interactorTransform = interactor != null ? interactor.transform : null;
            var interactorPos = interactorTransform != null ? interactorTransform.position : transform.position;

            float d = Vector3.Distance(transform.position, interactorPos);
            if (d > interactionRange)
            {
                reason = $"Too far ({d:0.0}m > {interactionRange:0.0}m)";
                return false;
            }

            return true;
        }

        public virtual void Interact(GameObject interactor)
        {
        }

        public virtual Bounds GetHoverBounds()
        {
            if (TryGetRendererBounds(out var rendererBounds))
                return rendererBounds;

            if (TryGetColliderBounds(out var colliderBounds))
                return colliderBounds;

            return new Bounds(transform.position, Vector3.one * 0.25f);
        }

        public virtual string GetHoverText()
        {
            return displayName;
        }

        protected void SetDisplayName(string value)
        {
            displayName = value;
        }

        protected void SetInteractionRange(float value)
        {
            interactionRange = value;
        }

        protected void SetRequiresRange(bool value)
        {
            requiresRange = value;
        }

        protected void SetHighlightRenderers(Renderer[] value)
        {
            highlightRenderers = value;
        }

        private bool TryGetRendererBounds(out Bounds bounds)
        {
            bool hasBounds = false;
            bounds = default;

            if (highlightRenderers != null)
            {
                for (int i = 0; i < highlightRenderers.Length; i++)
                {
                    var r = highlightRenderers[i];
                    if (r == null) continue;

                    if (!hasBounds)
                    {
                        bounds = r.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(r.bounds);
                    }
                }
            }

            if (!hasBounds)
            {
                var any = GetComponentInChildren<Renderer>(true);
                if (any != null)
                {
                    bounds = any.bounds;
                    return true;
                }

                return false;
            }

            return true;
        }

        private bool TryGetColliderBounds(out Bounds bounds)
        {
            bool hasBounds = false;
            bounds = default;

            var colliders = GetComponentsInChildren<Collider>(true);
            if (colliders == null || colliders.Length == 0)
                return false;

            for (int i = 0; i < colliders.Length; i++)
            {
                var c = colliders[i];
                if (c == null) continue;

                if (!hasBounds)
                {
                    bounds = c.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(c.bounds);
                }
            }

            return hasBounds;
        }

        private void OnDrawGizmosSelected()
        {
            if (!requiresRange)
                return;

            if (interactionRange <= 0f)
                return;

            Gizmos.color = new Color(0f, 1f, 1f, 0.65f);
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}
