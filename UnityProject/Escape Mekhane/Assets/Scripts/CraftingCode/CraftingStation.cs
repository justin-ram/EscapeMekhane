using UnityEngine;

public class CraftingStation : MonoBehaviour
{
    [Header("Recipe Ingredients")]
    [SerializeField] private string ingredientOneID = "damaged_power_relay";
    [SerializeField] private int ingredientOneAmount = 1;
    [SerializeField] private string ingredientTwoID = "ammo_pack";
    [SerializeField] private int ingredientTwoAmount = 1;

    [Header("Crafted Item")]
    [SerializeField] private string craftedItemID = "emergency_power_cell";
    [SerializeField] private string craftedItemName = "Emergency Power Cell";
    [SerializeField] private int craftedItemAmount = 1;

    [Header("Controls")]
    [SerializeField] private KeyCode craftKey = KeyCode.C;

    private bool playerInRange;

    void Update()
    {
        if(playerInRange && Input.GetKeyDown(craftKey))
        {
            TryCraft();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            playerInRange = true;
            Debug.Log("Crafting station ready. Press C to craft.");

        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;

        }
    }

    void TryCraft()
    {
        if(InventoryManager.instance == null)
        {
            Debug.LogError("No InventoryManager found");
            return;
        }
        bool hasFirstIngredient = InventoryManager.instance.HasItem(
            ingredientOneID,
            ingredientOneAmount
            );

        bool hasSecondIngredient = InventoryManager.instance.HasItem(
            ingredientTwoID,
            ingredientTwoAmount
            );

        if(!hasFirstIngredient || !hasSecondIngredient)
        {
            Debug.Log("Missing crafting ingredients.");
            return;
        }

        InventoryManager.instance.RemoveItem(
            ingredientOneID,
            ingredientOneAmount
            );

        InventoryManager.instance.RemoveItem(
            ingredientTwoID,
            ingredientTwoAmount
            );
        InventoryManager.instance.AddItem(
            craftedItemID,
            craftedItemName,
            craftedItemAmount
            );
        Debug.Log("Crafted: " + craftedItemName);
    }
}
