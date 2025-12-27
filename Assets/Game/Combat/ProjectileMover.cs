using System;
using UnityEngine;

namespace Abyssbound.Combat
{
    [DisallowMultipleComponent]
    public sealed class ProjectileMover : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private float speed = 14f;
        [SerializeField] private float lifetimeSeconds = 1.75f;
        [SerializeField] private float impactDistance = 0.25f;
        [SerializeField] private bool faceVelocity;
        [SerializeField] private bool billboardToCamera = true;
        [SerializeField] private int sortingOrder = 50;
        [SerializeField] private float minUniformScale = 0.25f;
        [SerializeField] private float arrowUniformScale = 45f;
        [SerializeField] private float magicBoltUniformScale = 0.9375f;
        [SerializeField] private float magicBoltNoseOffsetDegrees = 180f;

        [Header("Render")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private ProjectileVisualKind visualKind = ProjectileVisualKind.Arrow;

        private Transform _target;
        private Vector3 _targetPos;
        private Vector3 _targetOffset;
        private float _life;
        private Action _onImpact;

        private Vector3 _spawnPos;

        private static Sprite _generatedArrow;
        private static Sprite _generatedBolt;

        public void Init(Transform target, Vector3 targetPosFallback, float projectileSpeed, Action onImpact)
        {
            Init(target, targetPosFallback, Vector3.zero, projectileSpeed, onImpact);
        }

        public void Init(Transform target, Vector3 targetPosFallback, Vector3 targetOffset, float projectileSpeed, Action onImpact)
        {
            _target = target;
            _targetOffset = targetOffset;
            _targetPos = target != null ? (target.position + _targetOffset) : targetPosFallback;
            _onImpact = onImpact;

            speed = Mathf.Max(0.1f, projectileSpeed);
            _life = 0f;

            _spawnPos = transform.position;

            EnsureRenderer();
            EnsureSprite();

            ApplyRenderDefaults();

            if (CombatQaFlags.ProjectileDebug)
            {
                Debug.DrawLine(_spawnPos, _targetPos, Color.cyan, 0.25f);
                var spriteInfo = spriteRenderer != null && spriteRenderer.sprite != null
                    ? $" sprite={spriteRenderer.sprite.rect.size} ppu={spriteRenderer.sprite.pixelsPerUnit:0.##}"
                    : "";
                Debug.Log($"[Projectile] type={visualKind} spawn={_spawnPos} target={_targetPos} speed={speed:0.00} scale={transform.localScale}{spriteInfo}", this);
            }
        }

        public void SetTargetPoint(Vector3 targetPos)
        {
            _target = null;
            _targetPos = targetPos;
        }

        private void Awake()
        {
            EnsureRenderer();
            EnsureSprite();
            ApplyRenderDefaults();
        }

        private void Update()
        {
            _life += Time.deltaTime;
            if (_life >= Mathf.Max(0.05f, lifetimeSeconds))
            {
                Impact();
                return;
            }

            if (_target != null)
            {
                try { _targetPos = _target.position + _targetOffset; }
                catch { }
            }

            var pos = transform.position;
            var to = _targetPos - pos;
            var dist = to.magnitude;

            if (dist <= Mathf.Max(0.05f, impactDistance))
            {
                Impact();
                return;
            }

            if (dist > 0.0001f)
            {
                var dir = to / dist;

                if (billboardToCamera)
                {
                    if (visualKind == ProjectileVisualKind.MagicBolt)
                        BillboardDirectional(dir);
                    else
                        Billboard();
                }

                transform.position = pos + dir * (Mathf.Max(0.1f, speed) * Time.deltaTime);

                // If we're billboarding, don't also rotate to velocity.
                if (!billboardToCamera && faceVelocity && dir.sqrMagnitude > 0.0001f)
                {
                    // If not billboarding, align to travel direction.
                    transform.rotation = Quaternion.LookRotation(dir);
                }
            }
        }

        private void ApplyRenderDefaults()
        {
            if (spriteRenderer == null)
                return;

            // Orientation: magic missiles should be directional but stay visible.
            if (visualKind == ProjectileVisualKind.MagicBolt)
            {
                billboardToCamera = true;
                faceVelocity = false;
            }

            spriteRenderer.enabled = true;
            spriteRenderer.sortingOrder = sortingOrder;

            if (minUniformScale > 0f)
            {
                var s = transform.localScale;
                var max = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                if (max < minUniformScale)
                    transform.localScale = Vector3.one * minUniformScale;
            }

            // Optional per-visualKind uniform scale override (useful for quick visibility testing).
            var desired = visualKind == ProjectileVisualKind.MagicBolt ? magicBoltUniformScale : arrowUniformScale;
            if (desired > 0f)
                transform.localScale = Vector3.one * desired;

            // Prefer a dedicated layer if present, else leave as-is.
            try
            {
                var layers = SortingLayer.layers;
                for (int i = 0; i < layers.Length; i++)
                {
                    if (string.Equals(layers[i].name, "VFX", StringComparison.OrdinalIgnoreCase))
                    {
                        spriteRenderer.sortingLayerName = "VFX";
                        return;
                    }
                }

                for (int i = 0; i < layers.Length; i++)
                {
                    if (string.Equals(layers[i].name, "UI", StringComparison.OrdinalIgnoreCase))
                    {
                        spriteRenderer.sortingLayerName = "UI";
                        return;
                    }
                }
            }
            catch { }
        }

        private void Billboard()
        {
            Camera cam = null;
            try { cam = Camera.main; } catch { cam = null; }
            if (cam == null)
                return;

            // Face the camera so the sprite is always visible in 3D/top-down.
            var fwd = cam.transform.forward;
            var up = cam.transform.up;
            if (fwd.sqrMagnitude < 0.0001f)
                return;

            transform.rotation = Quaternion.LookRotation(-fwd, up);
        }

        private void BillboardDirectional(Vector3 travelDir)
        {
            Camera cam = null;
            try { cam = Camera.main; } catch { cam = null; }
            if (cam == null)
                return;

            var camForward = cam.transform.forward;
            var camUp = cam.transform.up;
            var camRight = cam.transform.right;
            if (camForward.sqrMagnitude < 0.0001f)
                return;

            // Start from standard billboard orientation.
            var baseRot = Quaternion.LookRotation(-camForward, camUp);

            // Rotate within the camera plane so the sprite points along travel direction.
            var proj = Vector3.ProjectOnPlane(travelDir, camForward);
            if (proj.sqrMagnitude < 0.0001f)
            {
                transform.rotation = baseRot;
                return;
            }

            proj.Normalize();
            float angle = Vector3.SignedAngle(camRight, proj, camForward) + magicBoltNoseOffsetDegrees;
            var spin = Quaternion.AngleAxis(angle, camForward);
            transform.rotation = spin * baseRot;
        }

        private void Impact()
        {
            try { _onImpact?.Invoke(); }
            catch { }

            Destroy(gameObject);
        }

        private void EnsureRenderer()
        {
            if (spriteRenderer != null)
                return;

            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
                return;

            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        private void EnsureSprite()
        {
            if (spriteRenderer == null)
                return;

            if (spriteRenderer.sprite != null)
                return;

            spriteRenderer.sprite = visualKind == ProjectileVisualKind.MagicBolt
                ? GetOrCreateBoltSprite()
                : GetOrCreateArrowSprite();

            // Ensure we don't inherit a transparent/odd tint from prefab.
            if (visualKind == ProjectileVisualKind.MagicBolt)
                spriteRenderer.color = Color.red;
            else
                spriteRenderer.color = new Color(0f, 0.55f, 0f, 1f);
        }

        private static Sprite GetOrCreateArrowSprite()
        {
            if (_generatedArrow != null)
                return _generatedArrow;

            // White arrow-ish rectangle; scale/orientation handled by prefab/transform.
            var tex = new Texture2D(16, 4, TextureFormat.RGBA32, mipChain: false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            var pixels = new Color32[16 * 4];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            _generatedArrow = Sprite.Create(tex, new Rect(0, 0, 16, 4), new Vector2(0.5f, 0.5f), pixelsPerUnit: 32f);
            return _generatedArrow;
        }

        private static Sprite GetOrCreateBoltSprite()
        {
            if (_generatedBolt != null)
                return _generatedBolt;

            // Magic orb with trailing tail.
            // Tint is applied by SpriteRenderer.
            const int w = 48;
            const int h = 16;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            var pixels = new Color32[w * h];
            var clear = new Color32(255, 255, 255, 0);
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            static void Put(Color32[] px, int width, int height, int x, int y, byte a)
            {
                if (x < 0 || y < 0 || x >= width || y >= height)
                    return;

                int idx = y * width + x;
                if (a <= px[idx].a)
                    return;
                px[idx] = new Color32(255, 255, 255, a);
            }

            static void Plot(Color32[] px, int width, int height, int x, int y, int thickness, byte a)
            {
                int r = Mathf.Max(0, thickness / 2);
                for (int ox = -r; ox <= r; ox++)
                    for (int oy = -r; oy <= r; oy++)
                        Put(px, width, height, x + ox, y + oy, a);
            }

            static void FillCircle(Color32[] px, int width, int height, int cx, int cy, int radius, byte a)
            {
                int rr = radius * radius;
                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (x * x + y * y <= rr)
                            Put(px, width, height, cx + x, cy + y, a);
                    }
                }
            }

            static void CircleOutline(Color32[] px, int width, int height, int cx, int cy, int radius, byte a)
            {
                int rr = radius * radius;
                int rr2 = (radius - 1) * (radius - 1);
                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        int d = x * x + y * y;
                        if (d <= rr && d >= rr2)
                            Put(px, width, height, cx + x, cy + y, a);
                    }
                }
            }

            int cy = h / 2;

            // Note: With the current directional-billboard setup + default offset,
            // the "front" is on the LEFT side of the texture.

            // Orb head.
            const int orbX = 12;
            const int orbRadius = 5;
            FillCircle(pixels, w, h, orbX, cy, orbRadius, a: 255);
            CircleOutline(pixels, w, h, orbX, cy, orbRadius, a: 255);

            // Tail: tapered streak fading out to the right.
            const int tailStartX = orbX + orbRadius - 1;
            const int tailEndX = w - 2;
            int len = Mathf.Max(1, tailEndX - tailStartX);
            for (int x = tailStartX; x <= tailEndX; x++)
            {
                float t = (x - tailStartX) / (float)len; // 0..1
                int thickness = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(5f, 1f, t)), 1, 6);
                byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(220f, 0f, t)), 0, 255);
                Plot(pixels, w, h, x, cy, thickness, a);

                // Small "wisp" offset for a more magical feel.
                if (x % 4 == 0)
                    Plot(pixels, w, h, x, cy + 2, Mathf.Max(1, thickness - 2), (byte)(a / 2));
                if (x % 5 == 0)
                    Plot(pixels, w, h, x, cy - 2, Mathf.Max(1, thickness - 2), (byte)(a / 2));
            }

            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            _generatedBolt = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), pixelsPerUnit: 32f);
            return _generatedBolt;
        }
    }

    public enum ProjectileVisualKind
    {
        Arrow = 0,
        MagicBolt = 1,
    }
}
