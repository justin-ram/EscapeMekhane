using UnityEngine;


public class CollectibleItem : MonoBehaviour
{
    [Header("Item Information")]
    [SerializeField] private string itemId = "ammo_pack";
    [SerializeField] private string displayName = "Ammo Pack";
    [SerializeField] private int amount = 1;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }
        if(InventoryManager.instance == null)
        {
            Debug.LogError("No InventoryManager Found");
        }

        InventoryManager.instance.AddItem(itemId, displayName, amount);

        Destroy(gameObject);
    }
}
