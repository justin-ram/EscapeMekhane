using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]					// Guarantees that the Sentinel has the movement component used by this script.
public class SentinelAI : MonoBehaviour, IDamage					// Lets the player's existing weapons damage the Sentinel through IDamage.
{
    [Header("Required References")]
    [SerializeField] Renderer _model;					// Renderer used for the temporary red damage flash.
    [SerializeField] Transform _gunPivot;					// Rotates the weapon vertically toward the player.
    [SerializeField] Transform _shootOrigin;					// Marks where projectiles are created.
    [SerializeField] GameObject _projectilePrefab;					// Projectile assigned after its prefab is created.

    [Header("Health")]
    [Min(1)][SerializeField] int _maxHealth = 50;
    [SerializeField] float _damageFlashTime = 0.08f;

    [Header("Detection")]
    [SerializeField] float _sightRange = 18f;
    [SerializeField] float _attackRange = 14f;
    [Range(1f, 180f)][SerializeField] float _fieldOfView = 90f;
    [SerializeField] LayerMask _lineOfSightMask = ~0;					// Determines which layers can block the Sentinel's vision.

    [Header("Movement")]
    [SerializeField] float _patrolRadius = 6f;
    [SerializeField] float _patrolPause = 1.5f;
    [SerializeField] float _combatStoppingDistance = 7f;
    [SerializeField] float _bodyTurnSpeed = 6f;
    [SerializeField] float _gunTurnSpeed = 8f;

    [Header("Projectile Burst")]
    [Min(1)][SerializeField] int _burstSize = 3;
    [SerializeField] float _timeBetweenShots = 0.15f;
    [SerializeField] float _burstCooldown = 1.5f;

    NavMeshAgent _agent;
    Transform _player;
    Material _material;
    Color _originalColor;
    Vector3 _spawnPosition;					// Center point used when selecting nearby patrol destinations.

    int _currentHealth;
    float _patrolTimer;
    bool _hasPatrolDestination;					// Prevents a new patrol point from being chosen every frame.
    bool _isFiring;					// Prevents multiple burst coroutines from running together.
    bool _isDead;					// Stops all remaining behavior once health reaches zero.

    void Start()
    {
        _agent = GetComponent<NavMeshAgent>();					// Cache the component instead of repeatedly searching for it.
        _spawnPosition = transform.position;					// Save the original location as the patrol center.
        _currentHealth = _maxHealth;					// Begin every spawned Sentinel at full health.

        if (_model == null)
        {
            _model = GetComponentInChildren<Renderer>();					// Use the Body renderer when one was not assigned manually.
        }

        if (_model != null)
        {
            _material = _model.material;
            _originalColor = _material.color;
        }

        if (gameManager.instance != null && gameManager.instance.player != null)
        {
            _player = gameManager.instance.player.transform;					// Prefer the player reference already maintained by gameManager.
        }
        else
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
            {
                _player = playerObject.transform;					// Fallback allows the Sentinel to work in a simple test scene.
            }
        }
    }

    void Update()
    {
        if (_isDead)
        {
            return;
        }

        if (_player != null && CanSeePlayer(out float distanceToPlayer))					// Engage only when range, FOV, and line of sight all pass.
        {
            EngagePlayer(distanceToPlayer);
        }
        else
        {
            Patrol();
        }
    }

    bool CanSeePlayer(out float distanceToPlayer)
    {
        Vector3 origin = _shootOrigin != null
            ? _shootOrigin.position
            : transform.position + Vector3.up;					// Fall back to the Sentinel's upper body if no shoot origin is assigned.

        Vector3 target = _player.position + Vector3.up;
        Vector3 directionToPlayer = target - origin;
        distanceToPlayer = directionToPlayer.magnitude;					// Store the distance so Update can reuse it for movement and attacking.

        if (distanceToPlayer > _sightRange || distanceToPlayer <= 0.01f)
        {
            return false;					// Ignore players outside detection range or effectively at the same position.
        }

        Vector3 flatDirection = Vector3.ProjectOnPlane(directionToPlayer, Vector3.up);					// Check horizontal facing without height affecting the viewing angle.
        float angleToPlayer = Vector3.Angle(transform.forward, flatDirection);

        if (angleToPlayer > _fieldOfView * 0.5f)
        {
            return false;					// The field of view is split evenly to the Sentinel's left and right.
        }

        if (!Physics.Raycast(
            origin,
            directionToPlayer.normalized,
            out RaycastHit hit,
            distanceToPlayer,
            _lineOfSightMask,
            QueryTriggerInteraction.Ignore))
        {
            return false;					// No raycast hit means the player was not confirmed along this sight line.
        }

        return hit.transform.root == _player.root;					// Reject attacks when a wall or another object was hit first.
    }

    void EngagePlayer(float distanceToPlayer)
    {
        _hasPatrolDestination = false;					// Discard the patrol target when combat begins.
        _patrolTimer = 0f;
        FacePlayer();

        if (_agent.enabled && _agent.isOnNavMesh)
        {
            _agent.updateRotation = false;
            _agent.stoppingDistance = _combatStoppingDistance;

            if (distanceToPlayer > _combatStoppingDistance)
            {
                _agent.SetDestination(_player.position);					// Approach until the preferred combat distance is reached.
            }
            else
            {
                _agent.ResetPath();
            }
        }

        if (distanceToPlayer <= _attackRange && !_isFiring)
        {
            StartCoroutine(FireBurst());					// Fire one controlled burst instead of shooting every frame.
        }
    }

    void FacePlayer()
    {
        Vector3 bodyDirection = _player.position - transform.position;
        bodyDirection.y = 0f;					// Keep the Sentinel upright while its body turns toward the player.

        if (bodyDirection.sqrMagnitude > 0.01f)
        {
            Quaternion bodyRotation = Quaternion.LookRotation(bodyDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                bodyRotation,
                _bodyTurnSpeed * Time.deltaTime);					// Smooth the turn so the body does not snap instantly.
        }

        if (_gunPivot == null || _shootOrigin == null)
        {
            return;					// Body tracking can continue even if the weapon references are missing.
        }

        Vector3 gunDirection = (_player.position + Vector3.up) - _shootOrigin.position;					// Aim near the player's center instead of directly at their feet.
        Quaternion gunRotation = Quaternion.LookRotation(gunDirection);
        _gunPivot.rotation = Quaternion.Slerp(
            _gunPivot.rotation,
            gunRotation,
            _gunTurnSpeed * Time.deltaTime);					// Rotate the gun independently for vertical aiming.
    }

    void Patrol()
    {
        if (!_agent.enabled || !_agent.isOnNavMesh)
        {
            return;					// NavMesh movement cannot run until the agent is enabled and placed correctly.
        }

        _agent.updateRotation = true;					// Return turning control to the NavMeshAgent outside combat.
        _agent.stoppingDistance = 0f;

        if (_hasPatrolDestination &&
            (_agent.pathPending || _agent.remainingDistance > 0.2f))
        {
            return;					// Continue toward the current patrol destination before choosing another.
        }

        if (_agent.hasPath)
        {
            _agent.ResetPath();
        }

        _hasPatrolDestination = false;
        _patrolTimer += Time.deltaTime;					// Pause briefly at each destination before moving again.

        if (_patrolTimer < _patrolPause)
        {
            return;
        }

        for (int attempt = 0; attempt < 6; attempt++)					// Try several random points in case some fall outside the NavMesh.
        {
            Vector2 randomCircle = Random.insideUnitCircle * _patrolRadius;
            Vector3 point = _spawnPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);

            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas))					// Keep random patrol targets on walkable ground.
            {
                _agent.SetDestination(hit.position);
                _hasPatrolDestination = true;
                _patrolTimer = 0f;
                return;					// Stop searching after a valid patrol destination is found.
            }
        }

        _patrolTimer = 0f;					// Wait through another patrol pause before retrying failed samples.
    }

    IEnumerator FireBurst()
    {
        _isFiring = true;					// Lock burst creation so Update cannot start overlapping coroutines.

        for (int shot = 0; shot < _burstSize; shot++)					// Fire only the configured number of projectiles per burst.
        {
            if (_player == null ||
                !CanSeePlayer(out float distanceToPlayer) ||
                distanceToPlayer > _attackRange)
            {
                break;					// Cancel the remaining shots if the target is lost or leaves attack range.
            }

            FireProjectile();					// Create one projectile from the assigned shoot origin.

            if (shot < _burstSize - 1)
            {
                yield return new WaitForSeconds(_timeBetweenShots);					// Space the shots apart without delaying after the final shot.
            }
        }

        yield return new WaitForSeconds(_burstCooldown);					// Enforce recovery time even when a burst ends early.
        _isFiring = false;					// Allow Update to begin the next burst.
    }

    void FireProjectile()
    {
        if (_projectilePrefab == null || _shootOrigin == null)
        {
            return;
        }

        Vector3 direction = (_player.position + Vector3.up) - _shootOrigin.position;
        Quaternion rotation = Quaternion.LookRotation(direction);
        Instantiate(_projectilePrefab, _shootOrigin.position, rotation);					// Spawn the projectile already aimed toward the player.
    }


    public void takeDamage(int amount)
    {
        takeDamage(amount, Vector3.zero, 0, 0f);
    }
    public void takeDamage(int amount, Vector3 pushback, int damageSpeed, float pushDurationTimer)
    {
        if (_isDead || amount <= 0)
        {
            return;
        }

        _currentHealth -= amount;                   // Use the same damage amount supplied by the player's weapons.

        if (_currentHealth <= 0)
        {
            _isDead = true;
            StopAllCoroutines();
            Destroy(gameObject);                    // Removing the root also removes every visual and weapon child.
        }
        else
        {
            StartCoroutine(FlashDamage());
        }
    }

    IEnumerator FlashDamage()
    {
        if (_material == null)
        {
            yield break;
        }

        _material.color = Color.red;
        yield return new WaitForSeconds(_damageFlashTime);
        _material.color = _originalColor;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _sightRange);					// Yellow shows the maximum detection distance.

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attackRange);					// Red shows the distance at which bursts can begin.
    }
}
