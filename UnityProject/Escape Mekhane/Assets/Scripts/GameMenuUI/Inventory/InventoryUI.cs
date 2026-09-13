using System.Text;
using TMPro;
using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private TextMeshProUGUI inventoryText;

    [Header("Controls")]
    [SerializeField] private KeyCode toggleKey = KeyCode.I;

    void Start()
    {
        inventoryPanel.SetActive(false);
    }
    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleInventory();
        }
    }
    public void ToggleInventory()
    {
        bool showInventory = !inventoryPanel.activeSelf;

        inventoryPanel.SetActive(showInventory);

        if (showInventory)
        {
            RefreshInventory();
        }
    }

    public void RefreshInventory()
    {
        if (inventoryManager == null)
        {
            inventoryText.text = "Inventory Manager is not assigned.";
            return;
        }

        StringBuilder inventoryDisplay = new StringBuilder();
        inventoryDisplay.AppendLine("<b>INVENTORY</b>");
        inventoryDisplay.AppendLine();

        if (inventoryManager.Items.Count == 0)
        {
            inventoryDisplay.Append("No items collected.");
        }
        else
        {
            foreach (InventoryItem item in inventoryManager.Items)
            {
                inventoryDisplay.AppendLine(item.DisplayName
                    + " x" + item.amount);
            }
        }
        inventoryText.text = inventoryDisplay.ToString();
    }
}
