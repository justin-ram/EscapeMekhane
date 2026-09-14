using UnityEngine;

public class teleportDevice : MonoBehaviour, IInteract
{
    [SerializeField] GameObject pointB;
    [SerializeField] bool isWinTerminal;
    public void Interact()
    {
        if (isWinTerminal)
        {
            gameManager.instance.playerScript.teleportPlayer(pointB.transform.position);
        }

        gameManager.instance.playerScript.teleportPlayer(
            pointB.transform.position);
    }
}
