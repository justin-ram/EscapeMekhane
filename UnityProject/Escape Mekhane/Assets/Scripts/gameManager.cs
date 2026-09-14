using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class gameManager : MonoBehaviour
{
    public static gameManager instance;

    [SerializeField] GameObject menuActive;
    [SerializeField] GameObject menuPause;
    [SerializeField] GameObject menuWin;
    [SerializeField] GameObject menuLose;
    [SerializeField] GameObject mainMenu;
    [SerializeField] GameObject menuCredit;
    [SerializeField] GameObject menuSettings;
    [SerializeField] GameObject cursor;
    [SerializeField] GameObject interactUI;
    [SerializeField] GameObject interactWarning;

    public bool isPaused;
    public GameObject player;
    public playerController playerScript;
    public Image playerHPBar;
    public Image playerDashBar;
    public GameObject damageFlash;
    public GameObject dashFlash;
    public TMP_Text shipPartsNeededTXT;
    public TMP_Text shipShipPartsCollectedTXT;

    public TMP_Text ammoCurrTxt;
    public TMP_Text ammoMaxTxt;

    public GameObject playerSpawnPos;
    public GameObject checkPointPopup;

    float timeScaleOrig;
    bool isPlayer;
    Dictionary<string, GameObject> menus;
    int shipItemGoalCount;
    [SerializeField] int winGameGoalCount;
    [SerializeField] float warningTimer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        instance = this;
        player = GameObject.FindWithTag("Player");
        playerSpawnPos = GameObject.FindWithTag("Player Spawn Pos");

        menus = new Dictionary<string, GameObject>();
        menus.Add("Credits", menuCredit);
        menus.Add("Main Menu", mainMenu);
        menus.Add("Settings", menuSettings);

        if (player != null)
        {
            playerScript = player.GetComponent<playerController>();
            isPlayer = true;
        }
        else
        {
            isPlayer = false;
            menuActive = mainMenu;
            menuActive.SetActive(true);
            playerHPBar.transform.parent.gameObject.SetActive(false);
            playerDashBar.transform.parent.gameObject.SetActive(false);
            cursor.SetActive(false);

        }
        timeScaleOrig = Time.timeScale;
        shipPartsNeededTXT.text = winGameGoalCount.ToString("F0");
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetButtonDown("Cancel") && isPlayer)
        {
            if (menuActive == null)
            {
                statePause(menuPause);
            }
            else if (menuActive == menuPause)
            {
                stateUnpause();
            }
        }
        if (player != null)
        {
            if (gameManager.instance.playerScript.gunInv.Count > 0)
            {
                ammoCurrTxt.text = gameManager.instance.playerScript.gunInv[gameManager.instance.playerScript.gunInvPos].ammoCur.ToString("F0");
                ammoMaxTxt.text = gameManager.instance.playerScript.gunInv[gameManager.instance.playerScript.gunInvPos].ammoMax.ToString("F0");
            }
        }
    }


    public void statePause(GameObject menu)
    {
        isPaused = true;
        Time.timeScale = 0;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        cursor.SetActive(false);
        menuActive = menu;
        menuActive.SetActive(true);
    }
    public void stateUnpause()
    {
        isPaused = false;
        Time.timeScale = timeScaleOrig;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        cursor.SetActive(true);
        menuActive.SetActive(false);
        menuActive = null;
    }
    public void openMenu(string menuName)
    {
        menuActive.SetActive(false);
        menuActive = null;
        if (menus.TryGetValue(menuName, out GameObject menu))
        {
            menuActive = menu;
        }
        menuActive.SetActive(true);
    }

    public void shipWin()
    {
        if (shipItemGoalCount >= winGameGoalCount)
        {
            statePause(menuWin);
        }
        else
        {
            StartCoroutine(warnInteract(interactWarning));
        }
    }
    public void updateShipItemCount(int amount)
    {
        shipItemGoalCount += amount;
        shipShipPartsCollectedTXT.text = shipItemGoalCount.ToString("F0");
    }

    public void youLose()
    {
        damageFlash.SetActive(false);
        statePause(menuLose);
    }

    public void showInteract(IInteract interact)
    {
        if (interact == null && interactUI.activeSelf || isPaused)
        {
            interactUI.SetActive(false);
        }
        else if (interact != null && !interactUI.activeSelf)
        {
            interactUI.SetActive(true);
        }
    }

    public void disableInteract()
    {
        interactUI.SetActive(false);
    }

    IEnumerator warnInteract(GameObject popUp)
    {
        popUp.SetActive(true);
        yield return new WaitForSeconds(warningTimer);
        popUp.SetActive(false);
    }
}
