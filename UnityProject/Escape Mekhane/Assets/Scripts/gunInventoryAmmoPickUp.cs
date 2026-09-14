using UnityEngine;

public class gunInventoryAmmoPickUp : MonoBehaviour
{
    
    
    private void OnTriggerEnter(Collider other)
    {
        IPickup pickup = other.GetComponent<IPickup>();
        if (pickup != null)
        {
            if(gameManager.instance.playerScript.gunInv.Count > 0)
           { 
                gameManager.instance.playerScript.gunInv[gameManager.instance.playerScript.gunInvPos].ammoCur = gameManager.instance.playerScript.gunInv[gameManager.instance.playerScript.gunInvPos].ammoMax;
                Destroy(gameObject);
            }
        }

    }
}
