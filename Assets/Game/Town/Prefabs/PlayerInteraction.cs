using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    public float interactDistance = 2.5f;
    public KeyCode interactKey = KeyCode.E;

    void Update()
    {
        if (Input.GetKeyDown(interactKey))
        {
            Ray ray = new Ray(transform.position + Vector3.up, transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
            {
                var interactable = hit.collider.GetComponent<MonoBehaviour>();
                if (interactable == null) return;
                var method = interactable.GetType().GetMethod("Interact");
                if (method != null)
                {
                    method.Invoke(interactable, null);
                }
            }
        }
    }
}
