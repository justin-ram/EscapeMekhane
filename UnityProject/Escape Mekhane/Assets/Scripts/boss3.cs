using System.Collections;
using UnityEngine;


public class boss3 : MonoBehaviour, IDamage
{
    

    [SerializeField] Renderer model;
    [Header("Stats")]
    [Range(1, 100)][SerializeField] int HP;
    
    [Header("Weapons")]
    [SerializeField] GameObject bullet;
    [SerializeField] GameObject shipItem;
    [SerializeField] bossSpawner spawner1;
    [SerializeField] bossSpawner spanwer2;
    [SerializeField] bossSpawner spanwer3;
    [SerializeField] bossSpawner spanwer4;
    [SerializeField] float spawnRate;
    int spawnLevel;
    int HPOrig;



    Color colorOrig;
    
    
    float spawnTimer;
    bool playerInTrigger;
    
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

        colorOrig = model.material.color;
        spawnLevel = 1;
        HPOrig = HP;
        
        




    }

    // Update is called once per frame
    void Update()
    {
        if (playerInTrigger)
        {

            

            spawnTimer = spawnTimer + Time.deltaTime;
            
           
            





            if (spawnTimer >= spawnRate)
            {
                callSpawn(spawnLevel);
                spawnTimer = 0;
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
        if (HP <= HPOrig / 3 * 2 && HP > HPOrig / 3)
        {
            if (spawnLevel != 2)
            {
                spawnLevel = 2;
                spawnRate = spawnRate / 2;

            }
        }
        else if (HP <= HPOrig / 3)
        {
            if (spawnLevel != 3)
            {
                spawnLevel = 3;
            }
        }
        
    }
    IEnumerator flashRed()
    {
        model.material.color = Color.red;
        yield return new WaitForSeconds(0.01f);
        model.material.color = colorOrig;
    }
    void callSpawn(int spawnLevel)
    {
       
        if (spawnLevel == 0)
        {

            spawner1.spawn(1);
            spanwer2.spawn(1);

        }
        else if (spawnLevel == 1)
        {

            spawner1.spawn2 = true;
            spawner1.spawn(1);


            spanwer2.spawn2 = true;
            spanwer2.spawn(1);



        }
        else if (spawnLevel == 2)
        {

            spawner1.spawn2 = true;
            spawner1.spawn(1);


            spanwer2.spawn2 = true;

            spanwer2.spawn(1);
            
        }
        else if (spawnLevel == 3)
        {

            spawner1.spawn2 = true;
            spawner1.spawn(1);

            spanwer2.spawn2 = true;
            spanwer2.spawn(1);



            spanwer3.spawn(1);




            spanwer4.spawn(1);
            
        }
        


    }

    
}

    

