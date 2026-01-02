using UnityEngine;

namespace Abyssbound.WorldInteraction
{
    public abstract class WorldInteractable : MonoBehaviour
    {
        [Header("World Interaction")]
        [SerializeField] private string displayName = "Interactable";
        [SerializeField] private Renderer[] highlightRenderers;

        public string DisplayName => displayName;
        public Renderer[] HighlightRenderers => highlightRenderers;

        public abstract bool CanInteract(Vector3 interactorPos);
        public abstract void Interact(GameObject interactor);

        protected void SetDisplayName(string value)
        {
            displayName = value;
        }

        protected void SetHighlightRenderers(Renderer[] value)
        {
            highlightRenderers = value;
        }
    }
}
