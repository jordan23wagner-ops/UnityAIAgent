using UnityEngine;

namespace Abyssbound.Cooking
{
    [CreateAssetMenu(menuName = "Abyssbound/Cooking/Cooking Recipe", fileName = "NewCookingRecipe")]
    public sealed class CookingRecipeSO : ScriptableObject
    {
        public string recipeId;
        public string displayName;

        [Header("Input")]
        public string inputItemId;
        public int inputCount = 1;

        [Header("Output")]
        public string outputItemId;
        public int outputCount = 1;
    }
}
