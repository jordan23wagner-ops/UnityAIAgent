using UnityEngine;

// HOW TO VERIFY
// 1) Enter Play Mode, click an enemy, let auto-attacks tick.
// 2) On each hit: floating damage numbers appear, rise + fade, then reuse (no new objects per hit).
// 3) Each alive enemy shows exactly one world-space health bar above it; it disappears on death and returns on re-enable.
// 4) When HP reaches 0: EnemyHealth fires death exactly once, disables colliders/visuals/AI, then SetActive(false) after ~0.35s.
// 5) No manual scene setup required: this bootstrap creates managers at runtime.
//
// Notes
// - To enable minimal debug logs, toggle the "Debug" bools on the runtime manager components at play time via Inspector
//   (they are created under the persistent UIRuntimeRoot).

public static class RuntimeBootstrap_CombatFeedback
{
    // PLAN
    // - Ensure a persistent runtime UI root exists.
    // - Ensure singleton managers exist: FloatingDamageTextManager + EnemyHealthBarManager.
    // - Managers auto-subscribe to EnemyHealth enable/damage/death events and use pools.

    private const string RetryRunnerName = "_BootstrapRetry";
    private static bool _retryScheduled;
    private static bool _loggedBootstrapFailure;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureCombatFeedbackSystems()
    {
        TryEnsureAll(allowScheduleRetry: true);
    }

    private static void TryEnsureAll(bool allowScheduleRetry)
    {
        try
        {
            WorldUiRoot.GetOrCreateRoot();
            FloatingDamageTextManager.EnsureExists();
            EnemyHealthBarManager.EnsureExists();
        }
        catch (System.Exception e)
        {
            if (!_loggedBootstrapFailure)
            {
                _loggedBootstrapFailure = true;
                Debug.LogWarning($"[RuntimeBootstrap] Combat feedback ensure failed (non-fatal): {e.Message}");
            }

            // Clear cached refs and retry once on next frame.
            WorldUiRoot.ResetForPlaymode();

            if (allowScheduleRetry)
                ScheduleRetryOnce();
        }
    }

    private static void ScheduleRetryOnce()
    {
        if (_retryScheduled)
            return;

        _retryScheduled = true;

        var existing = GameObject.Find(RetryRunnerName);
        if (existing == null)
        {
            existing = new GameObject(RetryRunnerName);
            existing.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(existing);
        }

        var runner = existing.GetComponent<RetryRunner>();
        if (runner == null)
            runner = existing.AddComponent<RetryRunner>();

        runner.Arm();
    }

    private sealed class RetryRunner : MonoBehaviour
    {
        private bool _armed;
        private bool _waitedOneFrame;

        public void Arm()
        {
            _armed = true;
            _waitedOneFrame = false;
            enabled = true;
        }

        private void Update()
        {
            if (!_armed)
            {
                enabled = false;
                return;
            }

            if (!_waitedOneFrame)
            {
                _waitedOneFrame = true;
                return;
            }

            _armed = false;

            // Retry once, then stop. Never blocks play mode.
            TryEnsureAll(allowScheduleRetry: false);
            enabled = false;

            // Keep object around (no Destroy), but inactive.
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }
    }
}
