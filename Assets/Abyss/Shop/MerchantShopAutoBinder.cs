using System;
using UnityEngine;

namespace Abyss.Shop
{
    /// <summary>
    /// Keeps scanning briefly to attach MerchantShop to any spawned TownKey merchants.
    /// Works with manual town setup (late spawns).
    /// </summary>
    public sealed class MerchantShopAutoBinder : MonoBehaviour
    {
        private const float ScanInterval = 0.5f;
        private const float ScanForSeconds = 12f;

        private float _timeLeft;
        private float _timer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            // Ensure one instance only
            var existing = FindAnyObjectByType<MerchantShopAutoBinder>();
            if (existing != null) return;

            var go = new GameObject("MerchantShopAutoBinder");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            go.AddComponent<MerchantShopAutoBinder>();
        }

        private void OnEnable()
        {
            _timeLeft = ScanForSeconds;
            _timer = 0f;

            // Ensure UI exists once so clicks have somewhere to go
            MerchantShopUI.EnsureUiExists();
        }

        private void Update()
        {
            if (_timeLeft <= 0f) return;

            _timeLeft -= Time.unscaledDeltaTime;
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = ScanInterval;

            TryBindAll();
        }

        private static void TryBindAll()
        {
            try
            {
                int attached = 0;

                // Find all TownKeyTag components and attach MerchantShop to merchant_* only
                var tags = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                foreach (var mb in tags)
                {
                    if (mb == null) continue;

                    var typeName = mb.GetType().Name;
                    if (!string.Equals(typeName, "TownKeyTag", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var go = mb.gameObject;
                    if (go == null) continue;

                    var key = ReadKey(mb);
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    if (!key.StartsWith("merchant_", StringComparison.OrdinalIgnoreCase)) continue;

                    if (go.GetComponent<MerchantShop>() == null)
                    {
                        go.AddComponent<MerchantShop>();
                        attached++;
                    }

                    if (go.GetComponent<Collider>() == null)
                    {
                        var box = go.AddComponent<BoxCollider>();
                        box.isTrigger = false;
                    }
                }

                if (attached > 0)
                    Debug.Log($"[MerchantShopAutoBinder] Attached MerchantShop to {attached} merchant(s).");
            }
            catch
            {
                // Swallow errors from destroyed objects during spawn churn
            }
        }

        private static string ReadKey(MonoBehaviour townKeyTag)
        {
            var t = townKeyTag.GetType();
            var p = t.GetProperty("Key");
            if (p != null && p.PropertyType == typeof(string))
                return p.GetValue(townKeyTag) as string;

            var f = t.GetField("Key");
            if (f != null && f.FieldType == typeof(string))
                return f.GetValue(townKeyTag) as string;

            // fallback common lowercase
            p = t.GetProperty("key");
            if (p != null && p.PropertyType == typeof(string))
                return p.GetValue(townKeyTag) as string;

            f = t.GetField("key");
            if (f != null && f.FieldType == typeof(string))
                return f.GetValue(townKeyTag) as string;

            return null;
        }
    }
}
