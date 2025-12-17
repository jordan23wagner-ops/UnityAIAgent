using UnityEngine;

namespace Abyss.Town
{
    [DisallowMultipleComponent]
    public class TownWorldYClamp : MonoBehaviour
    {
        public float targetY = 1.0f;
        public float tolerance = 0.05f;

        private void OnEnable()
        {
            // Prefer ground sampling if available
            Vector3 pos = transform.position;
            float resolvedY = targetY;
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 10f, pos.z), Vector3.down, out hit, 50f))
            {
                resolvedY = hit.point.y;
            }

            if (Mathf.Abs(transform.position.y - resolvedY) > tolerance)
            {
                transform.position = new Vector3(transform.position.x, resolvedY, transform.position.z);
            }
        }
    }
}
