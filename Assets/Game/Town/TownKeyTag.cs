using UnityEngine;

namespace Game.Town
{
    public class TownKeyTag : MonoBehaviour
    {
        [SerializeField] private string key;
        public string Key => key;
        public void SetKey(string value) => key = value;
    }
}
