using UnityEngine;

namespace Abyss.Waypoints
{
    public static class WaypointVisualBuilder
    {
        public const string VisualRootName = "__WaypointVisual";

        public static void EnsureVisual(WaypointComponent waypoint)
        {
            if (waypoint == null)
                return;

            var existing = waypoint.transform.Find(VisualRootName);
            if (!waypoint.ShowVisual)
            {
                if (existing != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        Object.DestroyImmediate(existing.gameObject);
                    else
                        Object.Destroy(existing.gameObject);
#else
                    Object.Destroy(existing.gameObject);
#endif
                }
                return;
            }

            if (existing != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Object.DestroyImmediate(existing.gameObject);
                else
                    Object.Destroy(existing.gameObject);
#else
                Object.Destroy(existing.gameObject);
#endif
            }

            var root = new GameObject(VisualRootName);
            root.transform.SetParent(waypoint.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            var mat = BuildSharedMaterial(waypoint.VisualColor);

            switch (waypoint.VisualStyle)
            {
                case WaypointComponent.WaypointVisualStyle.PlatformOnly:
                    BuildPlatform(root.transform, mat);
                    break;

                case WaypointComponent.WaypointVisualStyle.StarOnly:
                    BuildStar(root.transform, mat);
                    break;

                case WaypointComponent.WaypointVisualStyle.PlatformWithPillars:
                default:
                    BuildPlatform(root.transform, mat);
                    BuildPillars(root.transform, mat);
                    BuildStar(root.transform, mat);
                    break;
            }
        }

        private static Material BuildSharedMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            var mat = new Material(shader);

            // URP Lit uses _BaseColor; Standard uses _Color.
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);

            // Make it a bit emissive if available.
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 0.6f);
            }

            return mat;
        }

        private static void BuildPlatform(Transform parent, Material mat)
        {
            var platform = CreatePrimitive(PrimitiveType.Cylinder, "Platform", parent);
            platform.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            platform.transform.localScale = new Vector3(1.8f, 0.08f, 1.8f);
            AssignMaterial(platform, mat);
        }

        private static void BuildPillars(Transform parent, Material mat)
        {
            float radius = 0.95f;
            float y = 0.6f;
            Vector3 pillarScale = new Vector3(0.16f, 0.6f, 0.16f);

            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f * Mathf.Deg2Rad;
                var p = CreatePrimitive(PrimitiveType.Cylinder, $"Pillar_{i}", parent);
                p.transform.localScale = pillarScale;
                p.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                AssignMaterial(p, mat);
            }
        }

        private static void BuildStar(Transform parent, Material mat)
        {
            var star = new GameObject("Star");
            star.transform.SetParent(parent, false);
            star.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            star.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            star.transform.localScale = Vector3.one;

            // Two thin quads crossing = simple 3D "star".
            var a = CreatePrimitive(PrimitiveType.Quad, "Star_A", star.transform);
            a.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            a.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            AssignMaterial(a, mat);

            var b = CreatePrimitive(PrimitiveType.Quad, "Star_B", star.transform);
            b.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            b.transform.localRotation = Quaternion.Euler(90f, 90f, 0f);
            AssignMaterial(b, mat);
        }

        private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);

            // Visuals must not affect gameplay.
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Object.DestroyImmediate(col);
                else
                    Object.Destroy(col);
#else
                Object.Destroy(col);
#endif
            }

            var col2d = go.GetComponent<Collider2D>();
            if (col2d != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Object.DestroyImmediate(col2d);
                else
                    Object.Destroy(col2d);
#else
                Object.Destroy(col2d);
#endif
            }

            return go;
        }

        private static void AssignMaterial(GameObject go, Material mat)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = mat;
        }
    }
}
