using UnityEngine;
using System.Collections;
using Unity.VisualScripting;
public class backupenemy : MonoBehaviour, IDamage
{

    [SerializeField] Renderer model;
    Color colorOrig;

    [Header("Stats")]
    [Range(1, 10)][SerializeField] int HP;
    [SerializeField] int faceTargetSpeed;

    [Header("Weapon")]
    [SerializeField] GameObject bullet;
    [SerializeField] Transform gunPivot;
    [SerializeField] Transform gunEndPoint;
    [SerializeField] float shootRate;
    [SerializeField] int gunRotateSpeed;

    Vector3 playerDirection;

    float shootTimer;

    bool playerInTrigger;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        colorOrig = model.material.color;
    }

    void faceTarget()
    {
        Quaternion rot = Quaternion.LookRotation(new Vector3(playerDirection.x, 0, playerDirection.z));
        transform.rotation = Quaternion.Lerp(transform.rotation, rot, faceTargetSpeed * Time.deltaTime);
    }

    void rotateGun()
    {
        Quaternion rot = Quaternion.LookRotation(playerDirection);
        gunPivot.rotation = Quaternion.Lerp(gunPivot.rotation, rot, gunRotateSpeed * Time.deltaTime);
    }

    // Update is called once per frame
    void Update()
    {
        if (playerInTrigger == true)
        {
            shootTimer += Time.deltaTime;
            playerDirection = gameManager.instance.player.transform.position - transform.position;
            faceTarget();
            rotateGun();
            if (shootTimer >= shootRate)
            {
                shoot();
            }
        }
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
        }
    }

    void shoot()
    {
        shootTimer = 0;
        Instantiate(bullet, gunEndPoint.position, transform.rotation);
    }

    IEnumerator flashRed()
    {
        model.material.color = Color.red;
        yield return new WaitForSeconds(0.01f);
        model.material.color = colorOrig;
    }
}
