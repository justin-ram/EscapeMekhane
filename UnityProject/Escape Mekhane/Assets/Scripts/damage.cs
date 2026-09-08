using UnityEngine;
using System.Collections;

public class damage : MonoBehaviour
{
    enum damageType {bullet, stationary, DOT, proximity, rocket , explosion}
    [SerializeField] damageType type;
    [SerializeField] Rigidbody rb;
    [SerializeField] GameObject explosion;
    [SerializeField] int damageAmount;
    [SerializeField] float damageRate;
    [SerializeField] int bulletSpeed;
    [SerializeField] int bulletDestroyTime;
    [SerializeField] ParticleSystem hitEffect;

    [SerializeField] LayerMask targetLayers;
    [SerializeField] float explosionRadius;
    [SerializeField] float explosionDestroyTime;
    bool isDamaging;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(type == damageType.rocket)
        {
            rb.linearVelocity = transform.forward * bulletSpeed;
            StartCoroutine(explosionTimer());
            Destroy(gameObject, bulletDestroyTime);
        }
        if(type == damageType.explosion)
        {
            Destroy(gameObject, explosionDestroyTime);
        }
        if(type == damageType.bullet)
        {
            rb.linearVelocity = transform.forward * bulletSpeed;
            Destroy(gameObject, bulletDestroyTime);
        }
    }
    public void OnTriggerEnter(Collider other)
    {
        if(other.isTrigger)
        {
            return;
        }
        IDamage dmg = other.GetComponent<IDamage>();
        if(dmg != null && type != damageType.DOT && type != damageType.explosion && type != damageType.rocket)
        {
            dmg.takeDamage(damageAmount);
        }
        if(type == damageType.bullet)
        {
            if( hitEffect != null)
            {
                Instantiate(hitEffect, transform.position, Quaternion.identity);
            }
            Destroy(gameObject);
        }
        if(type == damageType.proximity)
        {
            Destroy(transform.parent.gameObject);
        }
        if(type == damageType.explosion)
        {
            ExplosionRadius();
           // Destroy(gameObject);
        }
        if(type == damageType.rocket)
        {
            Instantiate(explosion, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }
    private void OnTriggerStay(Collider other)
    {
        if(other.isTrigger)
        {
            return;
        }
        IDamage dmg = other.GetComponent<IDamage>();
        if(dmg != null&& type == damageType.DOT&& !isDamaging)
        {
            StartCoroutine(damageOther(dmg));
        }
    }


    IEnumerator damageOther(IDamage d)
    {
        isDamaging = true;
        d.takeDamage(damageAmount);
        yield return new WaitForSeconds(damageRate);
        isDamaging = false;
    }

    IEnumerator explosionTimer()
    {
        yield return new WaitForSeconds(bulletDestroyTime);
        Instantiate(explosion);
    }

    void ExplosionRadius()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius, targetLayers);

        foreach (var collider in colliders)
        {
            if(collider.GetComponent<IDamage>() != null)
            {
                collider.GetComponent<IDamage>().takeDamage(damageAmount);
            }
        }
    }
}
