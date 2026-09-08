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
        dialoguePanel.SetActive(true);
        dialogueText.text = message;
    }

    public bool IsShowing()
    {
        return dialoguePanel.activeSelf;
    }

    public void CloseDialogue()
    {
        dialoguePanel.SetActive(false);
    }
}
