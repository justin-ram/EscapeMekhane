using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

public class SHR0UD : MonoBehaviour, IDamage
{
    [SerializeField] NavMeshAgent agent;

    [SerializeField] Renderer model;
    [Header("Stats")]
    [Range(1, 100)][SerializeField] int HP;
    [SerializeField] int faceTargetSpeed;
    [SerializeField] int FOV;
    [SerializeField] int roamDist;
    [SerializeField] int roamPauseTime;
    [Header("Weapons")]
    [SerializeField] GameObject clone;
    [SerializeField] Transform gunPivot;
    [SerializeField] Transform shootPos;
    [SerializeField] float shootRate;
    [SerializeField] int gunRotateSpeed;
    [SerializeField] float sniperRate;
    [SerializeField] LayerMask ingnoreLayer;
    [SerializeField] float x1;
    [SerializeField] float y1;
    [SerializeField] float z1;
    [SerializeField] float x2;
    [SerializeField] float y2;
    [SerializeField] float z2;
    [SerializeField] float x3;
    [SerializeField] float y3;
    [SerializeField] float z3;
    [SerializeField] float x4;
    [SerializeField] float y4;
    [SerializeField] float z4;
    [SerializeField] GameObject shipItem;

    [SerializeField] float gravity;
    [SerializeField] float loadRate;
    bool reloading;
    bool isShown;
    float sniperTime;
    Color colorOrig;
    [SerializeField]LineRenderer render;
    Vector3 playerDir;
    Vector3 startingPos;
    bool isPhase1;
    int HPOrig;
    float shootTimer;
    float angleToPlayer;
    GameObject lineObject;
    float stoppingDistOrig;
    bool playerInTrigger;
    float reloadTimer;
    Vector3 pos1;
    Vector3 pos2;
    Vector3 pos3;
    Vector3 pos4;
    bool isPhase2;
    Vector3 startPos;
    [SerializeField]float attackRate;
    float attackTimer;
    bool shouldShoot;
    [SerializeField]int cloneDist;
    int randomNum;
    float baseAttackRate;
    bool isPhase3;
    



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        pos1 = new Vector3(x1, y1, z1);
        pos2 = new Vector3(x2, y2, z2);
        pos3 = new Vector3(x3, y3, z3);
        pos4 = new Vector3(x4, y4, z4);
        startPos = transform.position;

        HPOrig = HP;
        isPhase1 = true;
        colorOrig = model.material.color;

        startingPos = transform.position;
        stoppingDistOrig = agent.stoppingDistance;



        render.enabled = false;
        baseAttackRate = attackRate;


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
            
            shootTimer += Time.deltaTime;
            playerDir = gameManager.instance.player.transform.position - transform.position;
            faceTarget();
            rotateGun();
            if (reloading == true)
            {
                StartCoroutine(reload());
            }
            if (isPhase1 != true)
            {
                attackTimer += Time.deltaTime;
                if(attackTimer>= attackRate)
                {
                    randomNum = Random.Range(1, 3);
                    if(randomNum == 1)
                    {
                        sniperTime = 0;
                        telleport();
                        if(attackRate < baseAttackRate)
                        {
                            attackRate = baseAttackRate;
                        }
                        shouldShoot = true;
                    }
                    else
                    {
                        if(isPhase2 == true)
                        {
                            spawnClone(2);
                        }
                        else if(isPhase3 == true)
                        {
                            spawnClone(3);
                        }
                        
                        shouldShoot = false;
                    }
                    attackTimer = 0;
                    
                }
                if(shouldShoot == true)
                {
                    StartCoroutine(sniper());
                }
                
            }
           
            if(isPhase1 == true && reloading == false)
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

        if (HP+amount == HPOrig)
        {
            isPhase1 = false;
            isPhase2 = true;
            sniperRate = sniperRate / 2;
            telleport();

        }
        if(HP<= HPOrig/2 && isPhase2 == true)
        {
            isPhase2 = false;
            isPhase3 = true;
        }

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
        yield return new WaitForSeconds(0.1f);
        model.material.color = colorOrig;
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
        reloading = true;
        shouldShoot = false;




    }
    IEnumerator reload()
    {
        reloadTimer += Time.deltaTime;
        while (reloadTimer <= loadRate)
        {
            yield return null;
        }
        reloading = false;
        reloadTimer = 0;
        shouldShoot = true;

        
    }
    void telleport()
    {
        int rand = Random.Range(1, 3);
        
        if (transform.position == pos1)
        {
            if(rand ==1)
            {
                transform.position = pos2;
            }
            else if(rand == 2)
            {
                transform.position = pos3;
            }
            else if(rand == 3)
            {
                transform.position = pos4;
            }
            
        }
        else if (transform.position == pos2)
        {
            if (rand == 1)
            {
                transform.position = pos1;
            }
            else if (rand == 2)
            {
                transform.position = pos3;
            }
            else if (rand == 3)
            {
                transform.position = pos4;
            }

        }
        else if (transform.position == pos3)
        {
            if (rand == 1)
            {
                transform.position = pos1;
            }
            else if (rand == 2)
            {
                transform.position = pos2;
            }
            else if (rand == 3)
            {
                transform.position = pos4;
            }

        }
        if (transform.position == pos4)
        {
            if (rand == 1)
            {
                transform.position = pos1;
            }
            else if (rand == 2)
            {
                transform.position = pos2;
            }
            else if (rand == 3)
            {
                transform.position = pos3;
            }

        }
        else
        {
            if (rand == 1)
            {
                transform.position = pos1;
            }
            else if (rand == 2)
            {
                transform.position = pos2;
            }
            else if (rand == 3)
            {
                transform.position = pos3;
            }
        }


    }
    void spawnClone(int num)
    {


        Vector3 newLocation = new Vector3(transform.position.x, transform.position.y - 20, transform.position.z);
        transform.position = newLocation;
        int offset;
        Vector3 spawnPos = gameManager.instance.player.transform.position + (gameManager.instance.player.transform.forward * 15);


        for (int j = 0; j < num; j++)
        {
            offset = j * cloneDist;
            Instantiate(clone, spawnPos+ (gameManager.instance.player.transform.right*offset), transform.rotation);
            Instantiate(clone, spawnPos - (gameManager.instance.player.transform.right * offset), transform.rotation);
            
        }
        attackRate = 5;
        
        
       

    }
   
}


    
 
