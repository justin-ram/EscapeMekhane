using UnityEngine;

public class RadiationSuit : MonoBehaviour
{
    [SerializeField] int increaseAmount;
    private void OnTriggerEnter(Collider other)
    {
        IPickup pickup = other.GetComponent<IPickup>();
        if (pickup != null)
        {
            pickup.jumpPowerUp(increaseAmount);
            gameManager.instance.playerScript.canBeDamagedByRadiation = false;
            Destroy(gameObject);
        }
    }
}
