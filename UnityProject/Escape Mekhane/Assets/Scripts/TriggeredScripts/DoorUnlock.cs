using UnityEngine;
public class DoorUnlock : MonoBehaviour
{
    [SerializeField] private string requiredItemID = "emergency_power_cell";
    [SerializeField] private DoorPivot doorToBeOpened;

    private bool isUnlocked;

    void OnTriggerEnter(Collider other)
    {
        if(isUnlocked || !other.CompareTag("Player"))
        {
            return;
        }

        if(InventoryManager.instance == null)
        {
            Debug.LogError("InventoryManager instance was not found");
            return;
        }

        if (InventoryManager.instance.HasItem(requiredItemID))
        {
            isUnlocked = true;
            doorToBeOpened.OpenDoor();
            Debug.LogError("DoorPivot has not been assigned.");
            Debug.Log("Door Unlocked");
        }
        else 
        { 
            Debug.Log("Emergency power cell needed");
        
        }
    }
}

