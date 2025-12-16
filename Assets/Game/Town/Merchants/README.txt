# Town Merchants and Workshop

This folder contains merchant scripts for the four main buildings in the town zone:

1. WeaponsGearMerchant: Weapons & armor shop (building 1)
2. ConsumablesMerchant: T1 potions, cooked fish, and other T1 consumables (building 2)
3. SkillingSuppliesMerchant: Skilling tools and supplies (building 3)
4. WorkshopMerchant: Forge, smithing stand, workshop, bonfire for cooking (building 4)

Each merchant has a placeholder inventory/services array and a simple Interact() method for future UI hookup.

To place these in the scene:
- Add an empty GameObject at each building, attach the appropriate merchant script, and set a unique name (e.g., "WeaponsMerchantNPC").
- For the workshop building, add interactable objects (forge, smithing stand, etc.) as children or separate GameObjects with WorkshopMerchant attached.
