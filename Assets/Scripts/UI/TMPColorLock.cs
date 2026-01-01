using TMPro;
using UnityEngine;

namespace Abyssbound.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TMPColorLock : MonoBehaviour
    {
        [SerializeField] private Color32 lockedColor = new Color32(255, 255, 255, 255);

        private TMP_Text _tmp;

        private void Awake()
        {
            _tmp = GetComponent<TMP_Text>();
            Apply();
        }

        private void OnEnable()
        {
            if (_tmp == null)
                _tmp = GetComponent<TMP_Text>();
            Apply();
        }

        private void LateUpdate()
        {
            Apply();
        }

        private void Apply()
        {
            if (_tmp == null)
                return;

            try
            {
                if (((Color32)_tmp.color).Equals(lockedColor))
                    return;
            }
            catch { }

            try { _tmp.color = lockedColor; } catch { }
        }

        public void SetLockedColor(Color32 c)
        {
            lockedColor = c;
            Apply();
        }
    }
}
