using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class spawner : MonoBehaviour
{
    [SerializeField] GameObject objectToSpawn;
    [SerializeField] int amountToSpawn;
    [SerializeField] int spawnRate;
    [SerializeField] int spawnDist;

    [Header("Spawn VFX")]
    [SerializeField] GameObject _spawnEffectPrefab;
    [SerializeField] float _spawnEffectDelay = 2.0f;
    [SerializeField] float _spawnEffectCleanupDelay = 2.0f;
    [SerializeField] float _spawnEffectHeight = 1.0f;

    int spawnCount;
    float spawnTimer;

    bool startSpawning;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (startSpawning)
        {

            spawnTimer += Time.deltaTime;
            if (spawnCount < amountToSpawn && spawnTimer >= spawnRate)
            {
                
                spawn();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            startSpawning = true;
        }
    }

    void spawn()
    {
        spawnTimer = 0;
        spawnCount++;
        Vector3 ranPos = Random.insideUnitSphere * spawnDist;
        ranPos += transform.position;
        NavMeshHit hit;
        NavMesh.SamplePosition(ranPos, out hit, spawnDist, 1);
        StartCoroutine(SpawnWithEffect(hit.position));
    }

    IEnumerator SpawnWithEffect(Vector3 spawnPosition)
    {
        Vector3 effectPosition = spawnPosition + Vector3.up * _spawnEffectHeight;
        Quaternion enemyRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);

        GameObject spawnEffect = Instantiate(_spawnEffectPrefab, effectPosition, Quaternion.identity);
        GameObject enemy = Instantiate(objectToSpawn, spawnPosition, enemyRotation);

        enemy.SetActive(false);                              // Keeps the enemy inactive while the spawn effect conceals it.

        yield return new WaitForSeconds(_spawnEffectDelay);

        yield return new WaitForSeconds(_spawnEffectCleanupDelay);				// Allows the remaining particles to dissipate.

        Destroy(spawnEffect);
        enemy.SetActive(true);													// Activates the enemy after the effect has cleared.
    }
}

