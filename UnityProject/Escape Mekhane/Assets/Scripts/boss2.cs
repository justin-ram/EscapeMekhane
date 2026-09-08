using UnityEngine;

using System.Collections;

public class boss2 : MonoBehaviour, IDamage
{
    
    

    [SerializeField] Renderer model;
    [Header("Stats")]
    [Range(1, 100)][SerializeField] int HP;
    [SerializeField] int faceTargetSpeed;
    [Header("Weapons")]
    [SerializeField] GameObject bullet;
    [SerializeField] GameObject shipItem;
    [SerializeField] float dropPosX;
    [SerializeField] float dropPosY;
    [SerializeField] float dropPosZ;
    [SerializeField] Transform gunPivot;
    [SerializeField] Transform shootPos;
    [SerializeField] float shootRate;
    [SerializeField] int gunRotateSpeed;
    [SerializeField] float ballDistance;
    [SerializeField] int ballNumber;
    [SerializeField] float fireRate;

    Color colorOrig;

    Vector3 playerDir;
    float shootTimer;
    bool playerInTrigger;
    public bool layer1;
    public bool layer2;
    public bool layer3;
    public bool layer4;
    bool isFiring;
    int count;
    float fireTimer;
   


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        colorOrig = model.material.color;
        layer1 = true;
        count = 0;


    }

    // Update is called once per frame
    void Update()
    {
        if (playerInTrigger)
        {

           if(isFiring == false)
           {
                shootTimer = shootTimer + Time.deltaTime;
           }
           else
           {
                fireTimer += Time.deltaTime;
           }

            
           
            playerDir = gameManager.instance.player.transform.position - transform.position;
            faceTarget();
            rotateGun();





            if (layer1 == true && shootTimer >= shootRate && isFiring == false)
            {
               
                isFiring = true;

                shoot();
                fireTimer = fireTimer + Time.deltaTime;

            }
            else if (layer2 == true && shootTimer >= shootRate/2 && isFiring == false)
            {

                isFiring = true;

                shoot();
                fireTimer = fireTimer + Time.deltaTime;

            }
            else if (layer3 == true && shootTimer >= shootRate / 4 && isFiring == false)
            {

                isFiring = true;

                shoot();
                fireTimer = fireTimer + Time.deltaTime;

            }
            else if (layer4 == true && isFiring == false)
            {

                isFiring = true;

                shoot();
                fireTimer = fireTimer + Time.deltaTime;

            }
            if (fireTimer>=fireRate)
            {
                shoot();
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
        fireTimer = 0;
        
        float totalDis =0f;
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
        if (count == 3)
        {
            shootTimer = 0;
            isFiring = false;
            count = 0;
        }






    }

    
}