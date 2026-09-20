using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(AudioSource))]
public class WaspAI : MonoBehaviour, IDamage, IHealable
{
    [Header("Required References")]
    [SerializeField] Renderer _model;
    [SerializeField] Transform _shootOrigin;
    [SerializeField] GameObject _projectilePrefab;

    [Header("Health")]
    [Min(1)][SerializeField] int _maxHealth = 25;
    [SerializeField] float _damageFlashTime = 0.08f;

    [Header("Death VFX")]
    [SerializeField] GameObject _deathVFXPrefab;										// Explosion prefab created when the Wasp is defeated.
    [Min(0.1f)][SerializeField] float _deathVFXLifetime = 4f;							// Allows the particles to finish before removing their temporary object.

    [Header("Detection")]
    [SerializeField] float _sightRange = 22f;
    [SerializeField] float _attackRange = 16f;
    [SerializeField] LayerMask _lineOfSightMask = ~0;

    [Header("Flight")]
    [SerializeField] float _hoverHeight = 4f;
    [SerializeField] float _flightSpeed = 4f;
    [SerializeField] float _verticalSpeed = 4f;
    [SerializeField] float _hoverCorrectionSpeed = 2f;
    [SerializeField] float _preferredDistance = 10f;
    [SerializeField] float _distanceTolerance = 2f;
    [SerializeField] float _strafeChangeInterval = 2f;
    [SerializeField] float _turnSpeed = 6f;

    [Header("Geometry Avoidance")]
    [SerializeField] LayerMask _geometryMask = ~0;
    [SerializeField] float _groundProbeStartHeight = 1.5f;
    [SerializeField] float _groundProbeDistance = 25f;
    [SerializeField] float _obstacleCheckDistance = 2.5f;
    [SerializeField] float _avoidanceRadius = 0.45f;
    [SerializeField] float _avoidanceStrength = 1.5f;

    [Header("Attack")]
    [SerializeField] float _warningTime = 0.5f;
    [SerializeField] float _attackCooldown = 1.5f;
    [SerializeField] float _playerAimHeight = 1f;
    [SerializeField] Color _warningColor = new Color(1f, 0.45f, 0f);

    [Header("Audio")]
    [SerializeField] AudioSource _hoverAudioSource;
    [SerializeField] AudioSource _actionAudioSource;

    [SerializeField] AudioClip _hoverClip;
    [SerializeField] AudioClip _attackChargeClip;
    [SerializeField] AudioClip _fireClip;
    [SerializeField] AudioClip _damageClip;
    [SerializeField] AudioClip _destroyClip;

    [Range(0f, 1f)][SerializeField] float _hoverVolume = 0.4f;
    [Range(0f, 1f)][SerializeField] float _actionVolume = 1f;
    [SerializeField] float _audioMinDistance = 2f;
    [SerializeField] float _audioMaxDistance = 20f;

    readonly RaycastHit[] _groundHits = new RaycastHit[12];
    readonly RaycastHit[] _obstacleHits = new RaycastHit[12];
    readonly RaycastHit[] _lineOfSightHits = new RaycastHit[12];

    Rigidbody _rigidbody;
    FlyingEnemyVisuals _flyingVisuals;
    Transform _player;
    Material _material;
    Color _originalColor;

    int _currentHealth;
    int _strafeDirection = 1;

    float _fallbackHoverY;
    float _strafeTimer;
    float _attackTimer;
    float _playerSearchTimer;

    bool _isAttacking;
    bool _isDead;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _flyingVisuals = GetComponent<FlyingEnemyVisuals>();						// The Wasp still works safely when the optional visuals component is absent.
        _rigidbody.useGravity = false;					// Flight code controls the Wasp's height instead of gravity.
        _rigidbody.isKinematic = true;
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        PrepareAudio();
    }

    void Start()
    {
        _currentHealth = _maxHealth;
        _attackTimer = _attackCooldown;					// Allow the first warning to begin as soon as the player is detected.
        _strafeDirection = Random.value < 0.5f ? -1 : 1;

        FindPlayer();
        PrepareModel();
        PlaceAtHoverHeight();
        StartHoverAudio();					// Begin the Wasp's continuous flying sound.
    }

    void OnEnable()
    {
        if (_currentHealth > 0)
        {
            StartHoverAudio();                  // Restart hovering audio if the Wasp is re-enabled while alive.
        }
    }

    void OnDisable()
    {
        if (_hoverAudioSource != null)
        {
            _hoverAudioSource.Stop();                   // Prevent the hover loop from continuing while disabled.
        }
    }

    void PrepareAudio()
    {
        AudioSource[] existingSources = GetComponents<AudioSource>();                   // Find any Audio Sources already attached to the Wasp.

        if (_hoverAudioSource == null)
        {
            if (existingSources.Length > 0)
            {
                _hoverAudioSource = existingSources[0];                 // Reuse the first available source for the hover loop.
            }
            else
            {
                _hoverAudioSource = gameObject.AddComponent<AudioSource>();                 // Create a hover source when none exists.
            }
        }

        if (_actionAudioSource == null)
        {
            foreach (AudioSource source in existingSources)
            {
                if (source != _hoverAudioSource)
                {
                    _actionAudioSource = source;                    // Use a separate source for attacks, damage, and destruction.
                    break;
                }
            }

            if (_actionAudioSource == null)
            {
                _actionAudioSource = gameObject.AddComponent<AudioSource>();                    // Create the separate action source when needed.
            }
        }

        ConfigureAudioSource(_hoverAudioSource);                    // Apply the shared 3D audio settings.
        ConfigureAudioSource(_actionAudioSource);

        _hoverAudioSource.loop = true;                  // The flying sound should continue until the Wasp dies or is disabled.
        _hoverAudioSource.volume = _hoverVolume;

        _actionAudioSource.loop = false;                    // Action sounds should play only once per event.
        _actionAudioSource.volume = _actionVolume;
    }

    void ConfigureAudioSource(AudioSource source)
    {
        source.playOnAwake = false;                 // Audio is started deliberately by the AI.
        source.spatialBlend = 1f;                   // Make the sound fully positional in the 3D world.
        source.dopplerLevel = 0f;                   // Prevent movement from unnaturally changing the pitch.
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = _audioMinDistance;
        source.maxDistance = _audioMaxDistance;
    }

    void StartHoverAudio()
    {
        if (_hoverAudioSource == null ||
            _hoverClip == null ||
            _isDead ||
            _hoverAudioSource.isPlaying)
        {
            return;                 // Avoid missing references, dead Wasps, and duplicate hover loops.
        }

        _hoverAudioSource.clip = _hoverClip;
        _hoverAudioSource.loop = true;
        _hoverAudioSource.volume = _hoverVolume;
        _hoverAudioSource.Play();                   // Start the continuous flying sound.
    }

    void PlayActionClip(AudioClip clip)
    {
        if (_actionAudioSource == null || clip == null)
        {
            return;                 // Safely ignore audio events that do not have an assigned clip.
        }

        _actionAudioSource.PlayOneShot(clip, _actionVolume);                    // Play without interrupting another action sound.
    }

    void PlayDetachedClip(AudioClip clip)
    {
        if (clip == null)
        {
            return;                 // Do nothing when no destruction clip has been assigned.
        }

        GameObject audioObject = new GameObject("Wasp Detached Audio");                 // Create audio that survives after the Wasp is destroyed.
        audioObject.transform.position = transform.position;

        AudioSource detachedSource = audioObject.AddComponent<AudioSource>();
        ConfigureAudioSource(detachedSource);                   // Give the detached sound the same 3D distance settings.

        detachedSource.clip = clip;
        detachedSource.volume = _actionVolume;

        if (_actionAudioSource != null)
        {
            detachedSource.outputAudioMixerGroup =
                _actionAudioSource.outputAudioMixerGroup;                   // Preserve the Wasp's assigned mixer group.
        }

        detachedSource.Play();                  // Play the complete destruction sound after the Wasp disappears.
        Destroy(audioObject, clip.length + 0.1f);                   // Remove the temporary audio object after playback finishes.
    }

    void Update()
    {
        if (_isDead)
        {
            return;
        }

        if (_player == null)
        {
            _playerSearchTimer -= Time.deltaTime;

            if (_playerSearchTimer <= 0f)
            {
                FindPlayer();					// Retry occasionally so manually placed Wasps also work in test scenes.
                _playerSearchTimer = 1f;
            }

            return;
        }

        _attackTimer += Time.deltaTime;

        if (!_isAttacking &&
            _attackTimer >= _attackCooldown &&
            CanSeePlayer(out float distanceToPlayer) &&
            distanceToPlayer <= _attackRange)
        {
            StartCoroutine(AttackRoutine());
        }
    }

    void FixedUpdate()
    {
        if (_isDead)
        {
            return;
        }

        UpdateStrafeDirection();
        MoveWasp();
        FacePlayer();
    }

    void FindPlayer()
    {
        if (gameManager.instance != null && gameManager.instance.player != null)
        {
            _player = gameManager.instance.player.transform;					// Match the player lookup already used by SentinelAI.
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            _player = playerObject.transform;
        }
    }

    void PrepareModel()
    {
        if (_model == null)
        {
            _model = GetComponentInChildren<Renderer>();
        }

        if (_model != null)
        {
            _material = _model.material;
            _originalColor = _material.color;
        }
    }

    void PlaceAtHoverHeight()
    {
        Vector3 startPosition = _rigidbody.position;
        _fallbackHoverY = startPosition.y + _hoverHeight;

        if (TryGetGroundHeight(out float groundHeight))
        {
            startPosition.y = groundHeight + _hoverHeight;
            _rigidbody.position = startPosition;					// Prevent a newly spawned Wasp from touching the floor.
            _fallbackHoverY = startPosition.y;
        }
    }

    void UpdateStrafeDirection()
    {
        _strafeTimer += Time.fixedDeltaTime;

        if (_strafeTimer >= _strafeChangeInterval)
        {
            _strafeTimer = 0f;
            _strafeDirection *= -1;					// Periodically reverse the orbit so movement is less predictable.
        }
    }

    void MoveWasp()
    {
        Vector3 horizontalDirection = CalculateHorizontalDirection();
        float targetHoverY = _fallbackHoverY;

        if (TryGetGroundHeight(out float groundHeight))
        {
            targetHoverY = groundHeight + _hoverHeight;					// Maintain the configured height above the floor beneath the Wasp.
            _fallbackHoverY = targetHoverY;
        }

        float heightDifference = targetHoverY - _rigidbody.position.y;
        float verticalVelocity = Mathf.Clamp(
            heightDifference * _hoverCorrectionSpeed,
            -_verticalSpeed,
            _verticalSpeed);

        Vector3 desiredVelocity =
            horizontalDirection * _flightSpeed +
            Vector3.up * verticalVelocity;

        desiredVelocity = AvoidGeometry(desiredVelocity);
        _rigidbody.MovePosition(
            _rigidbody.position + desiredVelocity * Time.fixedDeltaTime);
    }

    Vector3 CalculateHorizontalDirection()
    {
        if (_player == null)
        {
            return Vector3.zero;
        }

        Vector3 toPlayer = _player.position - _rigidbody.position;
        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude <= 0.01f)
        {
            return Vector3.zero;
        }

        float distanceToPlayer = toPlayer.magnitude;
        Vector3 towardPlayer = toPlayer.normalized;
        Vector3 strafeDirection =
            Vector3.Cross(Vector3.up, towardPlayer) * _strafeDirection;

        float distanceAdjustment = 0f;

        if (distanceToPlayer > _preferredDistance + _distanceTolerance)
        {
            distanceAdjustment = 1f;
        }
        else if (distanceToPlayer < _preferredDistance - _distanceTolerance)
        {
            distanceAdjustment = -1f;
        }

        return (strafeDirection + towardPlayer * distanceAdjustment).normalized;					// Orbit while correcting distance when too close or too far away.
    }

    Vector3 AvoidGeometry(Vector3 desiredVelocity)
    {
        if (desiredVelocity.sqrMagnitude <= 0.01f)
        {
            return desiredVelocity;
        }

        Vector3 direction = desiredVelocity.normalized;
        int hitCount = Physics.SphereCastNonAlloc(
            _rigidbody.position,
            _avoidanceRadius,
            direction,
            _obstacleHits,
            _obstacleCheckDistance,
            _geometryMask,
            QueryTriggerInteraction.Ignore);

        RaycastHit closestHit = new RaycastHit();
        float closestDistance = float.PositiveInfinity;
        bool obstacleFound = false;

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            RaycastHit hit = _obstacleHits[hitIndex];

            if (hit.collider == null ||
                hit.collider.transform.root == transform.root ||
                (_player != null && hit.collider.transform.root == _player.root))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
                obstacleFound = true;
            }
        }

        if (!obstacleFound)
        {
            return desiredVelocity;
        }

        Vector3 slideDirection =
            Vector3.ProjectOnPlane(direction, closestHit.normal);
        Vector3 avoidanceDirection =
            (slideDirection + closestHit.normal * _avoidanceStrength).normalized;

        return avoidanceDirection * desiredVelocity.magnitude;					// Slide away before the body reaches a wall, ceiling, or obstacle.
    }

    bool TryGetGroundHeight(out float groundHeight)
    {
        Vector3 origin =
            _rigidbody.position + Vector3.up * _groundProbeStartHeight;

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            _groundHits,
            _groundProbeDistance + _groundProbeStartHeight,
            _geometryMask,
            QueryTriggerInteraction.Ignore);

        float closestDistance = float.PositiveInfinity;
        groundHeight = 0f;
        bool groundFound = false;

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            RaycastHit hit = _groundHits[hitIndex];

            if (hit.collider == null ||
                hit.collider.transform.root == transform.root)
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                groundHeight = hit.point.y;
                groundFound = true;
            }
        }

        return groundFound;
    }

    bool CanSeePlayer(out float distanceToPlayer)
    {
        Vector3 origin = _shootOrigin != null
            ? _shootOrigin.position
            : transform.position;

        Vector3 targetPosition =
            _player.position + Vector3.up * _playerAimHeight;
        Vector3 directionToPlayer = targetPosition - origin;
        distanceToPlayer = directionToPlayer.magnitude;

        if (distanceToPlayer <= 0.01f || distanceToPlayer > _sightRange)
        {
            return false;
        }

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            directionToPlayer.normalized,
            _lineOfSightHits,
            distanceToPlayer,
            _lineOfSightMask,
            QueryTriggerInteraction.Ignore);

        RaycastHit closestHit = new RaycastHit();
        float closestDistance = float.PositiveInfinity;
        bool objectFound = false;

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            RaycastHit hit = _lineOfSightHits[hitIndex];

            if (hit.collider == null ||
                hit.collider.transform.root == transform.root)
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
                objectFound = true;
            }
        }

        return objectFound &&
            closestHit.collider.transform.root == _player.root;					// Attack only when the player is the first object along the sight line.
    }

    void FacePlayer()
    {
        if (_player == null)
        {
            return;
        }

        Vector3 directionToPlayer =
            _player.position - _rigidbody.position;
        directionToPlayer.y = 0f;

        if (directionToPlayer.sqrMagnitude <= 0.01f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(directionToPlayer.normalized);
        Quaternion smoothRotation = Quaternion.Slerp(
            _rigidbody.rotation,
            targetRotation,
            _turnSpeed * Time.fixedDeltaTime);

        _rigidbody.MoveRotation(smoothRotation);
    }

    IEnumerator AttackRoutine()
    {
        _isAttacking = true;
        PlayActionClip(_attackChargeClip);					// Warn the player that the Wasp is preparing to fire.

        if (_material != null)
        {
            _material.color = _warningColor;					// Orange provides the required visible warning before the shot.
        }

        yield return new WaitForSeconds(_warningTime);

        if (_material != null)
        {
            _material.color = _originalColor;
        }

        if (!_isDead &&
            _player != null &&
            CanSeePlayer(out float distanceToPlayer) &&
            distanceToPlayer <= _attackRange)
        {
            FireProjectile();
        }

        _attackTimer = 0f;
        _isAttacking = false;
    }

    void FireProjectile()
    {
        if (_projectilePrefab == null ||
            _shootOrigin == null ||
            _player == null)
        {
            return;
        }

        Vector3 recordedTarget =
            _player.position + Vector3.up * _playerAimHeight;
        Vector3 travelDirection =
            (recordedTarget - _shootOrigin.position).normalized;
        Quaternion projectileRotation =
            Quaternion.LookRotation(travelDirection);

        GameObject projectileObject = Instantiate(
            _projectilePrefab,
            _shootOrigin.position,
            projectileRotation);
        PlayActionClip(_fireClip);                  // Play the shot at the moment the projectile is created.

        if (_flyingVisuals != null)
        {
            _flyingVisuals.PlayRecoil();												// Visually kicks the model backward without affecting flight physics.
        }

        WaspProjectile projectile =
            projectileObject.GetComponent<WaspProjectile>();

        if (projectile != null)
        {
            projectile.Initialize(travelDirection, transform);					// The recorded direction never updates, so the projectile cannot track.
        }
    }


    void SpawnDeathVFX()
    {
        if (_deathVFXPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition =
            _model != null
            ? _model.bounds.center
            : transform.position;

        GameObject deathVFX = Instantiate(
            _deathVFXPrefab,
            spawnPosition,
            Quaternion.identity);														// Keep the explosion separate so destroying the Wasp cannot remove it.

        Destroy(deathVFX, _deathVFXLifetime);											// Clean up the completed effect instead of leaving an empty object behind.
    }

    public void takeDamage(
        int amount,
        Vector3 damageDirection,
        int damageSpeed,
        float pushDurationTimer)
    {
        if (_isDead || amount <= 0)
        {
            return;
        }

        _currentHealth -= amount;

        if (_currentHealth <= 0)
        {
            _isDead = true;
            StopAllCoroutines();                    // Cancel warnings and damage flashes when defeated.

            if (_hoverAudioSource != null)
            {
                _hoverAudioSource.Stop();                   // Immediately stop the continuous flying sound.
            }

            PlayDetachedClip(_destroyClip);                 // Allow the destruction sound to finish after this object is removed.
            SpawnDeathVFX();
            Destroy(gameObject);                    // Remove the defeated Wasp and all of its child objects.
        }
        else
        {
            PlayActionClip(_damageClip);                    // Play feedback for a nonlethal hit.
            StartCoroutine(FlashDamage());
        }
    }

    public bool CanReceiveHealing =>
    !_isDead &&
    _currentHealth > 0 &&
    _currentHealth < _maxHealth;                    // Allows the Fabricator to select this enemy only when it is alive and missing health.

    public void Heal(int amount)
    {
        if (!CanReceiveHealing || amount <= 0)
        {
            return;                 // Prevents healing dead, full-health, or incorrectly configured enemies.
        }

        _currentHealth = Mathf.Min(
            _currentHealth + amount,
            _maxHealth);                    // Restores health without allowing it to exceed the enemy's maximum health.
    }

    IEnumerator FlashDamage()
    {
        if (_material == null)
        {
            yield break;
        }

        _material.color = Color.red;
        yield return new WaitForSeconds(_damageFlashTime);

        if (!_isDead)
        {
            _material.color =
                _isAttacking ? _warningColor : _originalColor;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _sightRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attackRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _avoidanceRadius);
    }
}

