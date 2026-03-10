using UnityEngine;
using System.Collections.Generic;
using Abyssbound.Loot;
using Abyssbound.Loot.World;
using Abyssbound.Loot.SetDrops;
using Game.Systems;

namespace Abyssbound.Crafting
{
    public class VoidForgeManager : MonoBehaviour
    {
        public static VoidForgeManager Instance { get; private set; }

        [Header("Config")]
        public WorldDropConfigSO worldDropConfig;
        
        private PlayerInventory _inventory;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
            
            _inventory = Game.Systems.PlayerInventoryResolver.GetOrFind();
        }

        public bool CanCraft(VoidForgeRecipeSO recipe)
        {
            if (recipe == null || _inventory == null) return false;

            // 1. Check Smithing Level (Assuming a PlayerStats component exists)
            // if (PlayerStats.Instance.SmithingLevel < recipe.smithingLevelRequired) return false;

            // 2. Check Ingredients
            foreach (var ingredient in recipe.ingredients)
            {
                if (_inventory.GetCount(ingredient.item.id) < ingredient.count)
                    return false;
            }

            return true;
        }

        public void Craft(VoidForgeRecipeSO recipe)
        {
            if (!CanCraft(recipe))
            {
                Debug.LogWarning($"[Void Forge] Cannot craft {recipe.recipeName}. Requirements not met.");
                return;
            }

            // 1. Remove Ingredients
            foreach (var ingredient in recipe.ingredients)
            {
                _inventory.TryRemove(ingredient.item.id, ingredient.count);
            }

            // 2. Grant Result
            var reg = LootRegistryRuntime.GetOrCreate();
            var inst = new ItemInstance
            {
                baseItemId = recipe.resultItem.id,
                rarityId = "Rare", // Default for Void Crafted
                itemLevel = 1,
                baseScalar = 1.2f, // High-quality craft
                affixes = new List<AffixRoll>()
            };

            var rolledId = reg.RegisterRolledInstance(inst);
            _inventory.Add(rolledId, 1);

            Debug.Log($"[Void Forge] Successfully crafted {recipe.recipeName}!");
        }
    }
}
