using System;
using System.Reflection;
using Abyss.Waypoints;
using UnityEngine;
using UnityEngine.AI;

namespace Abyssbound.DeathDrop
{
    public static class RespawnHelper
    {
        private static readonly string[] s_ClearMethodNames =
        {
            "ClearTarget",
            "ResetTarget",
            "StopAttacking",
            "CancelAttack",
            "Cancel",
            "Stop",
            "ClearIntent",
            "CancelIntent",
        };

        private static readonly string[] s_TargetMemberNames =
        {
            "Target",
            "CurrentTarget",
            "target",
            "currentTarget",
        };

        public static bool TryGetTownSpawn(out Vector3 pos)
        {
            // 1) Waypoints (prefer activated town waypoint via internal resolver).
            try
            {
                var mgr = WaypointManager.Instance;
                if (mgr != null)
                {
                    var townWp = TryInvokePrivate<WaypointComponent>(mgr, "ResolveTownWaypoint");
                    if (townWp != null)
                    {
                        var sp = townWp.GetSpawnPoint();
                        if (sp != null)
                        {
                            pos = sp.position;
                            return true;
                        }

                        pos = townWp.transform.position;
                        return true;
                    }

                    var fallback = TryInvokePrivate<Transform>(mgr, "ResolveTownSpawnFallback");
                    if (fallback != null)
                    {
                        pos = fallback.position;
                        return true;
                    }
                }
            }
            catch { }

            // 1b) Waypoints (best-effort if reflection fails): any town waypoint.
            try
            {
                WaypointComponent[] all;
                all = UnityEngine.Object.FindObjectsByType<WaypointComponent>(FindObjectsSortMode.None);

                if (all != null)
                {
                    for (int i = 0; i < all.Length; i++)
                    {
                        var wp = all[i];
                        if (wp == null) continue;
                        if (!wp.IsTown) continue;

                        var sp = wp.GetSpawnPoint();
                        pos = sp != null ? sp.position : wp.transform.position;
                        return true;
                    }
                }
            }
            catch { }

            // 2) Named object fallback (works even if tag is not defined).
            try
            {
                var go = GameObject.Find("TownSpawn");
                if (go != null)
                {
                    pos = go.transform.position;
                    return true;
                }
            }
            catch { }

            // 3) TownSpawn tag (may not exist).
            try
            {
                var go = GameObject.FindGameObjectWithTag("TownSpawn");
                if (go != null)
                {
                    pos = go.transform.position;
                    return true;
                }
            }
            catch { }

            pos = Vector3.zero;
            return false;
        }

        public static void TeleportPlayerTo(Transform player, Vector3 pos)
        {
            if (player == null)
                return;

            player.position = pos;

            try
            {
                var rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    if (!rb.isKinematic)
                    {
                        try { rb.linearVelocity = Vector3.zero; } catch { }
                        try { rb.angularVelocity = Vector3.zero; } catch { }
                    }
                    else
                    {
                        try { rb.Sleep(); } catch { }
                    }
                }
            }
            catch { }

            try
            {
                var rb2d = player.GetComponent<Rigidbody2D>();
                if (rb2d != null)
                {
                    bool kinematic = false;
                    try { kinematic = rb2d.bodyType == RigidbodyType2D.Kinematic; } catch { kinematic = false; }

                    if (!kinematic)
                    {
                        try { rb2d.linearVelocity = Vector2.zero; } catch { }
                        try { rb2d.angularVelocity = 0f; } catch { }
                    }
                    else
                    {
                        try { rb2d.Sleep(); } catch { }
                    }
                }
            }
            catch { }
        }

        public static void RevivePlayer(PlayerHealth ph)
        {
            if (ph == null)
                return;

            try
            {
                // Existing public API restores health and clears IsDead (IsDead is CurrentHealth<=0).
                ph.ResetHealth();
            }
            catch { }
        }

        public static void ResetPlayerState(GameObject player)
        {
            if (player == null)
                return;

            // Input suppression window (minimal integration point).
            try { DeathDropManager.SuppressGameplayInputUntil = Time.unscaledTime + 0.2f; } catch { }

            // NavMeshAgent reset.
            try
            {
                var agent = player.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    try { agent.ResetPath(); } catch { }
                    try { agent.velocity = Vector3.zero; } catch { }

                    try
                    {
                        if (agent.enabled)
                        {
                            agent.enabled = false;
                            agent.enabled = true;
                        }
                    }
                    catch { }
                }
            }
            catch { }

            // Rigidbody reset.
            try
            {
                var rb = player.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic)
                {
                    try { rb.linearVelocity = Vector3.zero; } catch { }
                    try { rb.angularVelocity = Vector3.zero; } catch { }
                }
            }
            catch { }

            // Rigidbody2D reset.
            try
            {
                var rb2d = player.GetComponent<Rigidbody2D>();
                if (rb2d != null && rb2d.bodyType != RigidbodyType2D.Kinematic)
                {
                    try { rb2d.linearVelocity = Vector2.zero; } catch { }
                    try { rb2d.angularVelocity = 0f; } catch { }
                }
            }
            catch { }

            // Project-specific movement/target systems.
            try
            {
                var motor = player.GetComponent<PlayerMovementMotor>();
                if (motor != null)
                    motor.Clear();
            }
            catch { }

            try
            {
                var combat = player.GetComponent<CombatLoopController>();
                if (combat != null)
                    combat.ClearTarget();
            }
            catch { }

            // Reflective cancellation across all behaviours.
            MonoBehaviour[] behaviours = null;
            try { behaviours = player.GetComponents<MonoBehaviour>(); } catch { behaviours = null; }
            if (behaviours == null)
                return;

            for (int i = 0; i < behaviours.Length; i++)
            {
                var b = behaviours[i];
                if (b == null) continue;

                var t = b.GetType();

                // Invoke clear/cancel/stop methods if they exist and are parameterless.
                for (int m = 0; m < s_ClearMethodNames.Length; m++)
                {
                    try
                    {
                        var mi = t.GetMethod(s_ClearMethodNames[m], BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (mi == null) continue;
                        if (mi.GetParameters().Length != 0) continue;
                        mi.Invoke(b, null);
                    }
                    catch { }
                }

                // Null common target members when writable.
                for (int n = 0; n < s_TargetMemberNames.Length; n++)
                {
                    var name = s_TargetMemberNames[n];

                    try
                    {
                        var pi = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (pi != null && pi.CanWrite)
                            pi.SetValue(b, null);
                    }
                    catch { }

                    try
                    {
                        var fi = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (fi != null && !fi.IsInitOnly)
                            fi.SetValue(b, null);
                    }
                    catch { }
                }
            }
        }

        private static T TryInvokePrivate<T>(object instance, string methodName) where T : class
        {
            if (instance == null || string.IsNullOrWhiteSpace(methodName))
                return null;

            try
            {
                var mi = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (mi == null)
                    return null;

                var result = mi.Invoke(instance, null);
                return result as T;
            }
            catch
            {
                return null;
            }
        }
    }
}
