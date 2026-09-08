
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class enemy2AI : MonoBehaviour, IDamage
{
    [SerializeField] NavMeshAgent agent;
    [SerializeField] Renderer model;
    [Header("Stats")]
    [Range(1, 10)][SerializeField] int HP;
    [SerializeField] int timeTillDestruction;
    [SerializeField] int faceTargetSpeed;
    [SerializeField] int FOV;



    Color colorOrig;
    
    Vector3 playerDir;
    float explosionTimer;
    bool playerInTrigger;
    float angleToPlayer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        colorOrig = model.material.color;
       
    }

    // Update is called once per frame
    void Update()
    {
        if (playerInTrigger && canSeePlayer())
        {
            

        }
    }
    bool canSeePlayer()
    {
        
        playerDir = gameManager.instance.player.transform.position - transform.position;
        
        angleToPlayer = Vector3.Angle(playerDir, transform.forward);
        Debug.DrawRay(transform.position, playerDir);
        RaycastHit hit;
        if (Physics.Raycast(transform.position, playerDir, out hit))
        {
            if (angleToPlayer <= FOV && hit.collider.CompareTag("Player"))
            {
                explosionTimer = explosionTimer + Time.deltaTime;
                agent.SetDestination(gameManager.instance.player.transform.position);
                faceTarget();
                if (explosionTimer >= timeTillDestruction)
                {
                    Destroy(gameObject);
                }
                else
                {
                    StartCoroutine(flashYellow());
                }
                
                return true;
            }
        }
        return false;

    }
    public void takeDamage(int amount, Vector3 damageDirection, int damageSpeed, float pushDurationTimer)
    {
        HP -= amount;
        if (HP <= 0)
        {
            Destroy(gameObject);
        }
        else
        {
            StartCoroutine(flashRed());
        }
    }
    void faceTarget()
    {
        Quaternion rot = Quaternion.LookRotation(playerDir);
        transform.rotation = Quaternion.Lerp(transform.rotation, rot, faceTargetSpeed * Time.deltaTime);
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
    IEnumerator flashRed()
    {
        model.material.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        model.material.color = colorOrig;
    }
    IEnumerator flashYellow()
    {

        while (true)
        {
            model.material.color = Color.yellow;
            yield return new WaitForSeconds(.1f);


            model.material.color = colorOrig;
            yield return new WaitForSeconds(.1f);


        }

    }


}
