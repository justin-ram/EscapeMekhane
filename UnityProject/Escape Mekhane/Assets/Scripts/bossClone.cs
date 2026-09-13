using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class bossClone : MonoBehaviour, IDamage
{
    [SerializeField] NavMeshAgent agent;

    [SerializeField] Renderer model;
    [Header("Stats")]
    [Range(1, 100)][SerializeField] int HP;
    [SerializeField] int faceTargetSpeed;
    
   
    [Header("Weapons")]
    [SerializeField] GameObject clone;
    [SerializeField] Transform gunPivot;
    [SerializeField] Transform shootPos;
    [SerializeField] float shootRate;
    [SerializeField] int gunRotateSpeed;
    [SerializeField] float sniperRate;
    [SerializeField] LayerMask ingnoreLayer;
   

    [SerializeField] float gravity;
    [SerializeField] float loadRate;
    
    float sniperTime;
    
    [SerializeField] LineRenderer render;
    Vector3 playerDir;
    
    bool playerInTrigger;
    
    [SerializeField] float attackRate;
    
    bool shouldShoot;
   



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {









        shouldShoot = true;
        render.enabled = false;



    }



    void faceTarget()
    {
        Quaternion rot = Quaternion.LookRotation(new Vector3(playerDir.x, 0, playerDir.z));
        transform.rotation = Quaternion.Lerp(transform.rotation, rot, faceTargetSpeed * Time.deltaTime);
    }

    void Update()
    {
        if (playerInTrigger)
        {

            
            playerDir = gameManager.instance.player.transform.position - transform.position;
            faceTarget();
            rotateGun();
            if (shouldShoot == true)
            {
                StartCoroutine(sniper());
            }
        }

       


    }







    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInTrigger = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInTrigger = false;
            agent.stoppingDistance = 0;
        }
    }

    public void rotateGun()
    {
        Quaternion rot = Quaternion.LookRotation(playerDir);
        gunPivot.rotation = Quaternion.Lerp(gunPivot.rotation, rot, gunRotateSpeed * Time.deltaTime);
    }
    public void takeDamage(int amount, Vector3 damageDirection, int damageSpeed, float pushDurationTimer)
    {

        HP -= amount;

        

        if (HP <= 0)
        {

            Destroy(gameObject);
        }
       
    }




    IEnumerator sniper()
    {
        render.enabled = true;
        sniperTime += Time.deltaTime;
        RaycastHit hit;

        while (sniperTime <= sniperRate)
        {




            render.SetPosition(0, shootPos.position);





            if (Physics.Raycast(shootPos.position, shootPos.forward, out hit, 100, ~ingnoreLayer))
            {
                Debug.Log(hit.collider.name);
                render.SetPosition(1, hit.point);




            }
            yield return null;

        }
        if (Physics.Raycast(shootPos.position, shootPos.forward, out hit, 100, ~ingnoreLayer))
        {
            if (hit.collider.CompareTag("Player"))
            {
                gameManager.instance.youLose();
            }
        }
        render.enabled = false;
        sniperTime = 0;
        Destroy(gameObject);




    }
}
   
    



    
 


