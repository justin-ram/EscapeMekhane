using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using JetBrains.Annotations;


public class GL00M : MonoBehaviour, IDamage
{
    [SerializeField] NavMeshAgent agent;

    [SerializeField] Renderer model;
    [SerializeField] float jumpHeight;
    [SerializeField] float jumpDuration;
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
    [SerializeField] float jumpRate;
    [SerializeField] int ballNumber;
    [SerializeField] float ballDistance;
    [SerializeField] float fireRate;
    [SerializeField] int shockWaveDist;
    float fireTimer;
    int count;
    int patern; 

    Color colorOrig;

    Vector3 playerDir;
    float shootTimer;
    bool playerInTrigger;
   
   
    float jumpTime;
    bool isJumping;
    int rand;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        colorOrig = model.material.color;
        count = 0;
        isJumping = false;
        patern = 1;




    }

    // Update is called once per frame
    void Update()
    {
        if (playerInTrigger)
        {
            shootTimer += Time.deltaTime;
            agent.SetDestination(gameManager.instance.player.transform.position);

            shootTimer = shootTimer + Time.deltaTime;
            
            playerDir = gameManager.instance.player.transform.position - transform.position;
            faceTarget();
            rotateGun();





            if (shootTimer >= shootRate && isJumping == false)
            {
               
                if(patern == 1)
                {
                    StartCoroutine(jump());
                    
                }
                else if(patern ==2)
                {


                    fireTimer += Time.deltaTime;
                    if (fireTimer >= fireRate)
                    {
                        shoot3();
                        if (count == 3)
                        {
                            shootTimer = 0;
                            count = 0;
                            patern = 1;
                        }
                    }


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
    public void takeDamage(int amount, Vector3 gameDirection, int damageSpeed, float pushDurationTimer)
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
        

        float totalDis = 0f;
        Quaternion rotate;


        for (int j = 0; j < ballNumber; j++)
        {
            rotate = shootPos.rotation * Quaternion.Euler(0, -totalDis, 0);
            Instantiate(bullet, shootPos.position, rotate);
            totalDis += ballDistance;
        }
        totalDis = 0;
        for (int k = 0; k < ballNumber; k++)
        {
            rotate = shootPos.rotation * Quaternion.Euler(0, totalDis, 0);
            Instantiate(bullet, shootPos.position, rotate);
            totalDis += ballDistance;
        }
        


    }
    void shoot3()
    {
        fireTimer = 0;

        float totalDis = 0f;
        Quaternion rotate;


        for (int j = 0; j < ballNumber; j++)
        {
            rotate = shootPos.rotation * Quaternion.Euler(0, -totalDis, 0);
            Instantiate(bullet, shootPos.position, rotate);
            totalDis += ballDistance;
        }
        totalDis = 0;
        for (int k = 0; k < ballNumber; k++)
        {
            rotate = shootPos.rotation * Quaternion.Euler(0, totalDis, 0);
            Instantiate(bullet, shootPos.position, rotate);
            totalDis += ballDistance;
        }
        count++;
        

    }

   IEnumerator jump()
    {
        
        isJumping = true;
        Vector3 startPos = transform.position;
        Vector3 target = gameManager.instance.player.transform.position;
        float passedTime = 0.0f;
        
        while(passedTime< jumpDuration)
        {
            passedTime = passedTime + Time.deltaTime;
            float linearProgress = passedTime / jumpDuration;
            Vector3 currentPos = Vector3.Lerp(startPos, target, linearProgress);
            float arc = 4 * jumpHeight * linearProgress * (1 - linearProgress);
            currentPos.y += arc;
            transform.position = currentPos;
            yield return null;
            
        }
        transform.position = target;
        shoot();
        isJumping = false;
        RaycastHit hit;
        if(Physics.Raycast(shootPos.position, shootPos.forward, out hit, shockWaveDist))
        {
            if (hit.collider.CompareTag("Player"))
            {
                
            }
        }
        
       
        shootTimer = 0;
        patern++;
        
    }
}
