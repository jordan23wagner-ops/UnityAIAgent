using UnityEngine;
using Game.Systems;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Abyssbound.Loot;

namespace Abyssbound.Crafting
{
    /// <summary>
    /// UI Controller for the Void Forge. 
    /// Displays recipes, requirements, and allows the player to craft Untradeable OP Gear.
    /// </summary>
    public class VoidForgeUI : MonoBehaviour
    {
        [Header("Recipe List")]
        public Transform recipeListRoot;
        public GameObject recipeButtonPrefab;

        [Header("Requirements Panel")]
        public TMP_Text recipeNameText;
        public TMP_Text smithingLevelText;
        public Transform ingredientListRoot;
        public GameObject ingredientRowPrefab;
        public Image resultIcon;
        public Button craftButton;

        [Header("Data")]
        public List<VoidForgeRecipeSO> allRecipes;
        
        private VoidForgeRecipeSO _selectedRecipe;

        private void Start()
        {
            PopulateRecipeList();
            craftButton.onClick.AddListener(OnCraftClicked);
            ClearSelection();
        }

        public void PopulateRecipeList()
        {
            foreach (Transform child in recipeListRoot) Destroy(child.gameObject);

            foreach (var recipe in allRecipes)
            {
                var go = Instantiate(recipeButtonPrefab, recipeListRoot);
                go.GetComponentInChildren<TMP_Text>().text = recipe.recipeName;
                var btn = go.GetComponent<Button>();
                btn.onClick.AddListener(() => SelectRecipe(recipe));
            }
        }

        public void SelectRecipe(VoidForgeRecipeSO recipe)
        {
            _selectedRecipe = recipe;
            recipeNameText.text = recipe.recipeName;
            smithingLevelText.text = $"Requires Smithing: {recipe.smithingLevelRequired}";
            resultIcon.sprite = recipe.resultItem.icon;

            // Update Ingredients
            foreach (Transform child in ingredientListRoot) Destroy(child.gameObject);
            foreach (var ing in recipe.ingredients)
            {
                var row = Instantiate(ingredientRowPrefab, ingredientListRoot);
                var texts = row.GetComponentsInChildren<TMP_Text>();
                // Layout: [Icon] [Name] [Count/Owned]
                texts[0].text = ing.item.displayName;
                texts[1].text = $"x{ing.count}";
            }

            // Check if player can craft (Logic placeholder)
            craftButton.interactable = true; 
        }

        private void OnCraftClicked()
        {
            if (_selectedRecipe == null) return;
            
            Debug.Log($"[Void Forge] Crafting {_selectedRecipe.resultItem.displayName}...");
            // Logic: Remove ingredients from PlayerInventory, Grant resultItem
        }

        private void ClearSelection()
        {
            recipeNameText.text = "Select a Recipe";
            smithingLevelText.text = "";
            resultIcon.sprite = null;
            craftButton.interactable = false;
        }
    }
}
