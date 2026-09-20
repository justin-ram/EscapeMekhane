using TMPro;
using UnityEngine;

public class ScanDialogueUI : MonoBehaviour
{
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TextMeshProUGUI dialogueText;

    void Start()
    {
        dialoguePanel.SetActive(false);
    }

    public void ShowDialogue(string message)
    {
        dialogueText.text =
            "<color=#55E7FF><b>SCAN REPORT</b></color>\n\n" +
            message +
            "\n\n<size=20><color=#55E7FF>[E] CLOSE</color></size>";

        dialoguePanel.SetActive(true);

        Time.timeScale = 0f;

        if (gameManager.instance != null)
            gameManager.instance.isPaused = true;
    }

    public bool IsShowing()
    {
        return dialoguePanel.activeSelf;
    }

    public void CloseDialogue()
    {
        dialoguePanel.SetActive(false);

        Time.timeScale = 1f;

        if (gameManager.instance != null)
            gameManager.instance.isPaused = false;
    }
}