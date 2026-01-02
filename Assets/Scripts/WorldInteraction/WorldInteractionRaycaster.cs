using UnityEngine;

namespace Abyssbound.WorldInteraction
{
    public sealed class WorldInteractionRaycaster : MonoBehaviour
    {
        [Header("Raycast")]
        [SerializeField] private Camera rayCamera;
        [SerializeField] private float maxDistance = 200f;

        // Uses DefaultRaycastLayers so built-in "Ignore Raycast" layer is excluded.
        [SerializeField] private LayerMask raycastLayers = Physics.DefaultRaycastLayers;

        [Header("Wiring")]
        [SerializeField] private WorldHoverHighlighter hoverHighlighter;
        [SerializeField] private GameObject interactorOverride;

        private RaycastHit[] hitsBuffer = new RaycastHit[32];

        private void Reset()
        {
            rayCamera = Camera.main;
            hoverHighlighter = GetComponent<WorldHoverHighlighter>();
        }

        private void Update()
        {
            var cam = rayCamera != null ? rayCamera : Camera.main;
            if (cam == null)
                return;

            var ray = cam.ScreenPointToRay(Input.mousePosition);

            int hitCount = Physics.RaycastNonAlloc(
                ray,
                hitsBuffer,
                maxDistance,
                raycastLayers,
                QueryTriggerInteraction.Collide);

            WorldInteractable best = null;
            float bestDistance = float.PositiveInfinity;
            Vector3 bestPoint = default;

            for (int i = 0; i < hitCount; i++)
            {
                var hit = hitsBuffer[i];
                if (hit.collider == null)
                    continue;

                var candidate = hit.collider.GetComponentInParent<WorldInteractable>();
                if (candidate == null)
                    continue;

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    bestPoint = hit.point;
                    best = candidate;
                }
            }

            if (hoverHighlighter != null)
            {
                hoverHighlighter.UpdateHoverCandidate(best, bestPoint, bestDistance, cam);
            }

            if (best != null && Input.GetMouseButtonDown(0))
            {
                var interactor = interactorOverride != null ? interactorOverride : gameObject;
                var interactorPos = interactor != null ? interactor.transform.position : transform.position;

                if (best.CanInteract(interactorPos))
                {
                    best.Interact(interactor);
                }
            }
        }

        public void SetHighlighter(WorldHoverHighlighter highlighter)
        {
            hoverHighlighter = highlighter;
        }
    }
}
