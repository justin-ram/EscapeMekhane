using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class InventoryItem
{
    public string ItemID;
    public string DisplayName;
    public int amount;
}
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager instance;

    [SerializeField] private List<InventoryItem> items =
        new List<InventoryItem>();

    public List<InventoryItem> Items
    {
        get { return items; }
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(this);
        }
    }

    public void AddItem(string itemID, string displayName, int amount = 1)
    {
        foreach (InventoryItem item in items)
        {
            if (item.ItemID == itemID)
            {
                item.amount += amount;
                Debug.Log("Inventory updated " +
                    item.DisplayName + " x" + item.amount);
                return;

            }
        }
        InventoryItem newItem = new InventoryItem();
        newItem.ItemID = itemID;
        newItem.DisplayName = displayName;
        newItem.amount = amount;

        items.Add(newItem);
        Debug.Log("Item added " +
            displayName + " x" + amount);
    }
    public bool HasItem(string itemID, int requiredAmount = 1)
    {
        foreach (InventoryItem item in items)
        {
            if (item.ItemID == itemID && item.amount >= requiredAmount)
            {
                return true;
            }
        }

        return false;
    }

    public bool RemoveItem(string itemID, int amount = 1)
    {
        for(int i = 0; i < items.Count; i++)
        {
            if (items[i].ItemID == itemID)
            {
                if (items[i].amount < amount)
                {
                    return false;
                }

                items[i].amount -= amount;

                Debug.Log("Item used: " + items[i].DisplayName + " x" + amount);

                if (items[i].amount <= 0)
                {
                    items.RemoveAt(i);
                }

                return true;
            }
        }

        return false;
    }
}
