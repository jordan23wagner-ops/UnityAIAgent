using System;
using UnityEngine;

namespace Abyssbound.Combat
{
    public sealed class SimpleAttackProjectile : MonoBehaviour
    {
        private Transform _target;
        private Vector3 _targetPoint;
        private float _speed;
        private float _life;
        private float _maxLife;
        private float _impactDistance;
        private Action _onImpact;

        public static SimpleAttackProjectile Spawn(
            WeaponAttackType type,
            Vector3 startPos,
            Transform target,
            Vector3 targetPointFallback,
            float speed,
            float impactDistance,
            float lifetimeSeconds,
            Action onImpact)
        {
            var go = CreateVisual(type);
            go.transform.position = startPos;

            var proj = go.AddComponent<SimpleAttackProjectile>();
            proj._target = target;
            proj._targetPoint = target != null ? target.position : targetPointFallback;
            proj._speed = Mathf.Max(0.1f, speed);
            proj._impactDistance = Mathf.Max(0.05f, impactDistance);
            proj._maxLife = Mathf.Max(0.05f, lifetimeSeconds);
            proj._onImpact = onImpact;
            return proj;
        }

        private static GameObject CreateVisual(WeaponAttackType type)
        {
            GameObject go;
            if (type == WeaponAttackType.Magic)
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            else
                go = GameObject.CreatePrimitive(PrimitiveType.Capsule);

            go.name = type == WeaponAttackType.Magic ? "MagicProjectile" : "RangedProjectile";

            try
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }
            catch { }

            // Keep it tiny and readable.
            if (type == WeaponAttackType.Magic)
                go.transform.localScale = Vector3.one * 0.18f;
            else
                go.transform.localScale = new Vector3(0.12f, 0.22f, 0.12f);

            return go;
        }

        private void Update()
        {
            _life += Time.deltaTime;
            if (_life >= _maxLife)
            {
                Impact();
                return;
            }

            if (_target != null)
            {
                try { _targetPoint = _target.position; }
                catch { }
            }

            var pos = transform.position;
            var to = _targetPoint - pos;
            var dist = to.magnitude;

            if (dist <= _impactDistance)
            {
                Impact();
                return;
            }

            if (dist > 0.0001f)
            {
                var dir = to / dist;
                transform.position = pos + dir * (_speed * Time.deltaTime);

                // Simple facing for ranged capsule.
                if (dir.sqrMagnitude > 0.0001f)
                {
                    try { transform.rotation = Quaternion.LookRotation(dir); } catch { }
                }
            }
        }

        private void Impact()
        {
            try { _onImpact?.Invoke(); }
            catch { }

            try { Destroy(gameObject); }
            catch { }
        }
    }
}
