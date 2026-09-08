using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class bossAI : MonoBehaviour, IDamage
{
    [SerializeField] NavMeshAgent agent;

    [SerializeField] Renderer model;
    [Header("Stats")]
    [Range(1, 100)][SerializeField] int HP;
    [SerializeField] int faceTargetSpeed;
    [Header("Weapons")]
    [SerializeField] GameObject bullet;
    [SerializeField] GameObject shipItem;
    
    [SerializeField] Transform gunPivot;
    [SerializeField] Transform shootPos;
    [SerializeField] float shootRate;
    [SerializeField] int gunRotateSpeed;
    
    Color colorOrig;
    
    Vector3 playerDir;
    float shootTimer;
    bool playerInTrigger;
    float randomX;
    float randomZ;
    int doesTelleport;
    float telleportTime;
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       
        colorOrig = model.material.color;
        
        doesTelleport = 0;
        
        


    }

    // Update is called once per frame
    void Update()
    {
        if (playerInTrigger)
        {

            agent.SetDestination(gameManager.instance.player.transform.position);

            shootTimer = shootTimer + Time.deltaTime;
            telleportTime += Time.deltaTime;
            playerDir = gameManager.instance.player.transform.position - transform.position;
            faceTarget();
            rotateGun();
            




            if (shootTimer >= shootRate)
            {
                shoot();
            }
            if (telleportTime >= 1)
            {
                
                
                
                int random = Random.Range(doesTelleport, 7);
                if (random >= 6)
                {
                    telleport();
                    doesTelleport = 0;
                }
                else
                {
                    telleportTime = 0;
                    doesTelleport += 1;

                }
            }

        }



    }
    
    public void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInTrigger = true;
        }
    }
    public void OnTriggerExit(Collider other)
    {
        playerInTrigger = false;
    }
    void faceTarget()
    {
        Quaternion rot = Quaternion.LookRotation(new Vector3(playerDir.x, 0, playerDir.z));
        transform.rotation = Quaternion.Lerp(transform.rotation, rot, faceTargetSpeed * Time.deltaTime);
    }
    void rotateGun()
    {
        Quaternion rot = Quaternion.LookRotation(playerDir);
        gunPivot.rotation = Quaternion.Lerp(gunPivot.rotation, rot, gunRotateSpeed * Time.deltaTime);
    }
    public void takeDamage(int amount, Vector3 damageDirection, int damageSpeed, float pushDurationTimer)
    {
        HP -= amount;
        if (HP <= 0)
        {
            
            Instantiate(shipItem, transform.position, transform.rotation);
            Destroy(gameObject);
        }
        else
        {
            StartCoroutine(flashRed());
        }
    }
    IEnumerator flashRed()
    {
        model.material.color = Color.red;
        yield return new WaitForSeconds(0.01f);
        model.material.color = colorOrig;
    }
    void shoot()
    {
        shootTimer = 0;

        Instantiate(bullet, shootPos.position, transform.rotation);



    }
    
    void telleport()
    {

        
        float rangeX;
        float rangeZ;
        float xPoint1 = gameManager.instance.player.transform.position.x;
        float xPoint2 = transform.position.x;
        float zPoint1 = gameManager.instance.player.transform.position.z;
        float zPoint2 = transform.position.z;
        rangeX = xPoint1 - xPoint2;
        rangeZ = zPoint1 - zPoint2;
        

        randomX = Random.Range(rangeX, rangeX * -1);
        randomZ = Random.Range(rangeZ, rangeZ * -1);
        transform.position = new Vector3(gameManager.instance.player.transform.position.x + randomX, transform.position.y, gameManager.instance.player.transform.position.z + randomZ);
       
        telleportTime = 0;
        

    }
}
