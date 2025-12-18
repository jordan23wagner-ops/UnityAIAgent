using UnityEngine;
using Abyss.Shop;

namespace Abyss.Dev
{
    public sealed class DevGoldCheat : MonoBehaviour
    {
        [SerializeField] private KeyCode key = KeyCode.G;
        [SerializeField] private int amount = 250;

        private void Update()
        {
            if (Input.GetKeyDown(key))
            {
                var wallet = PlayerGoldWallet.Instance;
                if (wallet != null) wallet.AddGold(amount);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
#if UNITY_2022_2_OR_NEWER
            if (FindAnyObjectByType<DevGoldCheat>() != null) return;
#else
            if (FindObjectOfType<DevGoldCheat>() != null) return;
#endif
            var go = new GameObject("DevGoldCheat");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            go.AddComponent<DevGoldCheat>();
        }
    }
}
