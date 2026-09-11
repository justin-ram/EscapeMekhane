using UnityEngine;
//Untested
public class DoorUnlock : MonoBehaviour
{
    [SerializeField] private string requiredItemID = "emergency_power_cell";
    [SerializeField] private GameObject doorToBeOpened;

    void OnTriggerEnter(Collider other)
    {
        if(!other.CompareTag("Player"))
        {
            return;
        }
        if (InventoryManager.instance.HasItem(requiredItemID))
        {
            doorToBeOpened.SetActive(false);
            Debug.Log("Door Unlocked");
        }
        else 
        { 
            Debug.Log("Emergency power cell needed");
        
        }
    }
}

