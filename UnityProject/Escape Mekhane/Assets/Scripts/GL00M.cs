using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class GL00M : MonoBehaviour
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

    Color colorOrig;

    Vector3 playerDir;
    float shootTimer;
    bool playerInTrigger;
    float randomX;
    float randomZ;
    int doesTelleport;
    float jumpTime;
    bool isJumping;


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
           
            playerDir = gameManager.instance.player.transform.position - transform.position;
            faceTarget();
            rotateGun();





            if (shootTimer >= shootRate)
            {
                shoot();
            }
            if (jumpTime >= jumpRate && isJumping ==false)
            {

                StartCoroutine(jump());

                
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
    public void takeDamage(int amount)
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
    }
}
