
using TMPro;
using UnityEngine;

public class GameMenuUi : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private InventoryManager inventoryManager;
    [SerializeField] private ObjectiveManager objectiveManager;
    [Header("Menu Panels")]
    [SerializeField] private GameObject gameMenuPanel;
    [Header("UI Text Fields")]
    [SerializeField] private TextMeshProUGUI craftingText;
    [SerializeField] private TextMeshProUGUI objectiveText;
    private InventoryItem selectedItem;
    
    [Header("Dynamic Inventory Button Setup")]
    [SerializeField] private Transform itemContainer;
    [SerializeField] private GameObject buttonPrefab;
    [Header("Input Controls")]
    [SerializeField] private KeyCode getMenu = KeyCode.Tab;
     
    void Start()
    {
        gameMenuPanel.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(getMenu))
        {
            ToggleMenu();
        }
    }
    public void ToggleMenu()
    {
        bool showMenu = !gameMenuPanel.activeSelf;
        gameMenuPanel.SetActive(showMenu);
        if (showMenu)
        {
            RefreshMenu();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState= CursorLockMode.Locked;
            Cursor.visible = false;
        }
        
    }

    public void RefreshMenu()
    {
        objectiveText.text = "OBJECTIVE\n\n" + objectiveManager.GetCurrentObjective();
        craftingText.text = "CRAFTING\n\n Damaged Power Relay + Ammo = Emergency Power Cell";
        foreach(Transform child in itemContainer)
        {
            Destroy(child.gameObject);
        }


        if(inventoryManager.Items.Count == 0)
        {
            GameObject emptyNotice = Instantiate(buttonPrefab, itemContainer);
            TextMeshProUGUI noticeText = emptyNotice.GetComponentInChildren<TextMeshProUGUI>();
            if(noticeText != null)
            {
                noticeText.text = " No Items collected";
            }
            UnityEngine.UI.Button noticeBtn = emptyNotice.GetComponent<UnityEngine.UI.Button>();
            if (noticeBtn != null)
            {
                noticeBtn.interactable = false;
            }
        }
        else
        {
            foreach (var item in inventoryManager.Items)
            {
                GameObject newButton = Instantiate(buttonPrefab, itemContainer);
                TextMeshProUGUI buttonText = newButton.GetComponentInChildren<TextMeshProUGUI>();
                if(buttonText != null) 
                {
                    buttonText.text = item.DisplayName + "  x" + item.amount;
                }
                UnityEngine.UI.Button btnComponent = newButton.GetComponent<UnityEngine.UI.Button>();
                if (btnComponent != null)
                {
                    var currentItem = item;
                    btnComponent.onClick.AddListener(() => OnItemClicked(currentItem));
                }
            }
        }
    }

    private void OnItemClicked(InventoryItem item)
    {
        selectedItem = item;
        Debug.Log("Selected Items: "
            + selectedItem.DisplayName
            + "|ID: "
            + selectedItem.ItemID
            + "|Amount: "
            + selectedItem.amount
            );

    }
}

