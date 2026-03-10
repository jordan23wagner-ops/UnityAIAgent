using UnityEngine;
using System.Collections.Generic;
using Abyssbound.Loot;

namespace Abyssbound.Crafting
{
    [CreateAssetMenu(fileName = "NewVoidRecipe", menuName = "Abyssbound/Crafting/Void Recipe")]
    public class VoidForgeRecipeSO : ScriptableObject
    {
        public string recipeName;
        public ItemDefinitionSO resultItem;
        
        [Header("Requirements")]
        public int smithingLevelRequired;
        public List<Ingredient> ingredients;

        [System.Serializable]
        public struct Ingredient
        {
            public ItemDefinitionSO item;
            public int count;
        }
    }
}
