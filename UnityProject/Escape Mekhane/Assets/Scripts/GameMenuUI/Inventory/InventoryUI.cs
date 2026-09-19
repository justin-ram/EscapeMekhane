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

        if (inventoryManager == null)
        {
            inventoryManager = InventoryManager.instance;
        }

        if (inventoryManager == null)
        {
            Debug.LogError("InventoryUI could not find InventoryManager.");
        }
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
        if (inventoryPanel.activeSelf)
        {
            CloseInventory();
        }
        else
        {
            OpenInventory();
        }
    }

    private void OpenInventory()
    {
        // Do not open over another paused menu.
        if (gameManager.instance != null &&
            gameManager.instance.isPaused)
        {
            return;
        }

        RefreshInventory();
        inventoryPanel.SetActive(true);

        Time.timeScale = 0f;

        if (gameManager.instance != null)
        {
            gameManager.instance.isPaused = true;
        }
    }

    private void CloseInventory()
    {
        inventoryPanel.SetActive(false);

        Time.timeScale = 1f;

        if (gameManager.instance != null)
        {
            gameManager.instance.isPaused = false;
        }
    }

    public void RefreshInventory()
    {
        if (inventoryManager == null)
        {
            inventoryManager = InventoryManager.instance;
        }

        if (inventoryManager == null)
        {
            inventoryText.text =
                "Inventory Manager is not assigned.";
            return;
        }

        StringBuilder inventoryDisplay = new StringBuilder();

        inventoryDisplay.AppendLine(
            "<color=#55E7FF><b>INVENTORY</b></color>"
        );

        inventoryDisplay.AppendLine();

        if (inventoryManager.Items.Count == 0)
        {
            inventoryDisplay.AppendLine("No items collected.");
        }
        else
        {
            foreach (InventoryItem item in inventoryManager.Items)
            {
                inventoryDisplay.AppendLine(
                    item.DisplayName + "  x" + item.amount
                );
            }
        }

        inventoryDisplay.AppendLine();
        inventoryDisplay.AppendLine(
            "<size=20><color=#55E7FF>[I] CLOSE</color></size>"
        );

        inventoryText.text = inventoryDisplay.ToString();
    }
}