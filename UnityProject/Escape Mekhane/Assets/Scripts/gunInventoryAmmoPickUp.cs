using UnityEngine;

public class gunInventoryAmmoPickUp : MonoBehaviour
{
    
    
    private void OnTriggerEnter(Collider other)
    {
        IPickup pickup = other.GetComponent<IPickup>();
        if (pickup != null)
        {
            gameManager.instance.playerScript.gunInv[gameManager.instance.playerScript.gunInvPos].ammoCur = gameManager.instance.playerScript.gunInv[gameManager.instance.playerScript.gunInvPos].ammoMax;
            Destroy(gameObject);
        }

    }
}
