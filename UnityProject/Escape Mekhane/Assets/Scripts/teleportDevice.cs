using UnityEngine;
using System.Collections;

public class teleportDevice : MonoBehaviour, IInteract
{
    [SerializeField] GameObject pointB;

    [Header("Teleport VFX")]
    [SerializeField] GameObject _teleportVFXPrefab;						// Effect shown around the player while teleporting.
    [Min(0f)][SerializeField] float _teleportDelay = 0.75f;				// Time the effect plays before moving the player.
    [Min(0.1f)][SerializeField] float _teleportVFXLifetime = 3f;			// Time before the spawned effect is removed.

    bool _isTeleporting;

    public void Interact()
    {
        if (_isTeleporting ||
            pointB == null ||
            gameManager.instance == null ||
            gameManager.instance.playerScript == null)
        {
            return;
        }

        StartCoroutine(TeleportSequence());
    }

    IEnumerator TeleportSequence()
    {
        _isTeleporting = true;

        playerController player =
            gameManager.instance.playerScript;

        GameObject teleportVFX = SpawnTeleportVFX(player.transform);				// Keeps the effect centered on the player.

        yield return new WaitForSeconds(_teleportDelay);							// Allows the buildup to play before teleporting.

        player.teleportPlayer(pointB.transform.position);

        if (teleportVFX != null)
        {
            Destroy(teleportVFX, _teleportVFXLifetime);							// Removes the temporary effect.
        }

        _isTeleporting = false;
    }

    GameObject SpawnTeleportVFX(Transform playerTransform)
    {
        if (_teleportVFXPrefab == null)
        {
            return null;
        }

        GameObject teleportVFX =
            Instantiate(_teleportVFXPrefab, playerTransform);

        teleportVFX.transform.localPosition = Vector3.zero;
        teleportVFX.transform.localRotation = Quaternion.identity;

        return teleportVFX;
    }
}
