# Town Merchant & Workshop Placement Guide

1. In your town scene, create four empty GameObjects (one per building):
   - WeaponsMerchantNPC (add WeaponsGearMerchant)
   - ConsumablesMerchantNPC (add ConsumablesMerchant)
   - SkillingSuppliesMerchantNPC (add SkillingSuppliesMerchant)
   - WorkshopMerchantNPC (add WorkshopMerchant)

2. For the last building (workshop):
   - Add four more GameObjects as children or nearby:
     - Forge (add ForgeInteractable)
     - SmithingStand (add SmithingStandInteractable)
     - Workshop (add WorkshopInteractable)
     - Bonfire (add BonfireInteractable)

3. To make them interactable:
   - Add a collider (e.g., BoxCollider) to each merchant and interactable object.
   - Optionally, add a script to detect player proximity/click and call the `Interact()` method.

4. To show a UI popup:
   - Add a Canvas with a SimpleInteractPopup component somewhere in your scene.
   - Call `Show("Welcome to the Weapon Shop!")` or similar from the merchant/interactable's Interact() method.

5. You can turn any of these into prefabs by dragging them from the scene to your Prefabs folder.

All scripts are in Assets/Game/Town/Merchants and Assets/Game/Town/Interactables.
