using UnityEngine;

public class FallRespawn : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Only respond when the player enters the fall zone.

        if (!other.CompareTag("Player"))
        {
            return;
        }

        playerController player =
            other.GetComponentInParent<playerController>();

        if (player != null)
        {
            player.spawnPlayer();
        }
    }
}