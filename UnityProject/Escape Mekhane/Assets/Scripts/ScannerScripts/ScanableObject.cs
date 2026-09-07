using UnityEngine;

public class ScannableObject : MonoBehaviour
{
    [Header("Scan Info")]
    public string scanID = "Unknown_Object";
    public string objectName = "UnKnown Object";

    [TextArea(3, 6)]
    public string scanDialogue = " No useful information found.";

    [Header("behavior")]
    public bool canOnlyScanOnce = false;

    private bool hasBeenScanned;

    public void Scan(ScanDialogueUI dialogUI)
    {
        if (canOnlyScanOnce && hasBeenScanned)
        {
            dialogUI.ShowDialogue(objectName + "\n\nAlready Scanned");
            return;
        }
        hasBeenScanned = true;
        dialogUI.ShowDialogue("<b>" + objectName + "</b>\n\n " + scanDialogue);
        Debug.Log("scanned: " + scanID);
    }
}
