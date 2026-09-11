using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
[RequireComponent(typeof(LineRenderer))]
public class FabricatorAI : MonoBehaviour, IDamage
{
    [Header("Required References")]
    [SerializeField] Renderer _model;
    [SerializeField] Transform _tetherOrigin;
    [SerializeField] Material _tetherMaterial;

    [Header("Health")]
    [Min(1)][SerializeField] int _maxHealth = 35;
    [Min(0f)][SerializeField] float _damageFlashTime = 0.08f;

    [Header("Healing")]
    [Min(0.1f)][SerializeField] float _healingRange = 12f;
    [Min(1)][SerializeField] int _healAmount = 2;
    [Min(0.05f)][SerializeField] float _healInterval = 0.5f;
    [Min(0.05f)][SerializeField] float _targetSearchInterval = 0.5f;
    [SerializeField] LayerMask _healableMask = ~0;
    [SerializeField] LayerMask _tetherObstructionMask = ~0;

    [Header("Tether Appearance")]
    [SerializeField] Color _tetherColor = new Color(0.1f, 1f, 0.75f, 1f);
    [Min(0.01f)][SerializeField] float _tetherWidth = 0.08f;

    [Header("Flight")]
    [Min(0.1f)][SerializeField] float _hoverHeight = 4f;
    [Min(0.1f)][SerializeField] float _flightSpeed = 3f;
    [Min(0.1f)][SerializeField] float _verticalSpeed = 4f;
    [Min(0.1f)][SerializeField] float _hoverCorrectionSpeed = 2f;
    [Min(0.1f)][SerializeField] float _patrolRadius = 6f;
    [Min(0.05f)][SerializeField] float _patrolArrivalDistance = 0.5f;
    [Min(0f)][SerializeField] float _patrolPause = 1f;
    [Min(0.1f)][SerializeField] float _turnSpeed = 5f;

    [Header("Geometry Avoidance")]
    [SerializeField] LayerMask _geometryMask = ~0;
    [Min(0f)][SerializeField] float _groundProbeStartHeight = 1.5f;
    [Min(0.1f)][SerializeField] float _groundProbeDistance = 25f;
    [Min(0.1f)][SerializeField] float _obstacleCheckDistance = 2.5f;
    [Min(0.05f)][SerializeField] float _avoidanceRadius = 0.55f;
    [Min(0f)][SerializeField] float _avoidanceStrength = 1.5f;

    readonly Collider[] _healableHits = new Collider[32];
    readonly RaycastHit[] _groundHits = new RaycastHit[12];
    readonly RaycastHit[] _obstacleHits = new RaycastHit[12];
    readonly RaycastHit[] _tetherHits = new RaycastHit[12];

    Rigidbody _rigidbody;
    LineRenderer _lineRenderer;
    Material _material;
    Color _originalColor;

    IHealable _healingTarget;
    MonoBehaviour _healingTargetBehaviour;
    Renderer _healingTargetRenderer;

    Vector3 _spawnPosition;
    Vector3 _patrolDestination;

    int _currentHealth;
    float _fallbackHoverY;
    float _patrolTimer;
    float _targetSearchTimer;
    float _healTimer;

    bool _hasPatrolDestination;
    bool _isDead;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _lineRenderer = GetComponent<LineRenderer>();

        _rigidbody.useGravity = false;					// Flight code maintains altitude without allowing gravity to pull the Fabricator down.
        _rigidbody.isKinematic = true;
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        PrepareTether();					// Configure the required LineRenderer before a healing target can be selected.
    }

    void Start()
    {
        _currentHealth = _maxHealth;

        PrepareModel();
        PlaceAtHoverHeight();

        _spawnPosition = _rigidbody.position;					// The spawn point becomes the center of the Fabricator's assigned patrol area.
        SelectPatrolDestination();
        _targetSearchTimer = 0f;					// Search immediately so an injured nearby enemy can be supported after spawning.
    }

    void Update()
    {
        if (_isDead)
        {
            return;
        }

        UpdateHealingTarget();
    }

    void FixedUpdate()
    {
        if (_isDead)
        {
            return;
        }

        MoveFabricator();
    }

    void LateUpdate()
    {
        UpdateTetherPositions();					// LateUpdate keeps the line attached after both enemies have moved for the frame.
    }

    void PrepareModel()
    {
        if (_model == null)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                if (renderers[rendererIndex] != _lineRenderer)
                {
                    _model = renderers[rendererIndex];
                    break;					// Skip the tether renderer when locating the Fabricator's visible body automatically.
                }
            }
        }

        if (_model != null)
        {
            _material = _model.material;
            _originalColor = _material.color;
        }
    }

    void PrepareTether()
    {
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.positionCount = 2;
        _lineRenderer.startWidth = _tetherWidth;
        _lineRenderer.endWidth = _tetherWidth;
        _lineRenderer.startColor = _tetherColor;
        _lineRenderer.endColor = _tetherColor;

        if (_tetherMaterial != null)
        {
            _lineRenderer.sharedMaterial = _tetherMaterial;
        }

        _lineRenderer.enabled = false;					// Hide the tether until a valid injured enemy has been selected.
    }

    void PlaceAtHoverHeight()
    {
        Vector3 startPosition = _rigidbody.position;
        _fallbackHoverY = startPosition.y + _hoverHeight;

        if (TryGetGroundHeight(out float groundHeight))
        {
            startPosition.y = groundHeight + _hoverHeight;
            _rigidbody.position = startPosition;					// A spawned Fabricator rises from its ground spawn point before beginning patrol.
            _fallbackHoverY = startPosition.y;
        }
    }

    void MoveFabricator()
    {
        Vector3 horizontalDirection = GetPatrolDirection();
        float targetHoverY = _fallbackHoverY;

        if (TryGetGroundHeight(out float groundHeight))
        {
            targetHoverY = groundHeight + _hoverHeight;
            _fallbackHoverY = targetHoverY;					// Maintain a consistent height above the floor beneath the Fabricator.
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

        FaceMovementDirection(desiredVelocity);
    }

    Vector3 GetPatrolDirection()
    {
        Vector3 fromCenter = _rigidbody.position - _spawnPosition;
        fromCenter.y = 0f;

        if (fromCenter.magnitude > _patrolRadius)
        {
            _hasPatrolDestination = false;
            return -fromCenter.normalized;					// Return toward the assigned area if avoidance movement pushes the Fabricator outside it.
        }

        if (!_hasPatrolDestination)
        {
            _patrolTimer += Time.fixedDeltaTime;

            if (_patrolTimer >= _patrolPause)
            {
                SelectPatrolDestination();
            }

            return Vector3.zero;
        }

        Vector3 direction = _patrolDestination - _rigidbody.position;
        direction.y = 0f;

        if (direction.magnitude <= _patrolArrivalDistance)
        {
            _hasPatrolDestination = false;
            _patrolTimer = 0f;
            return Vector3.zero;					// Pause after reaching each patrol destination before selecting another.
        }

        return direction.normalized;
    }

    void SelectPatrolDestination()
    {
        Vector2 randomPoint = Random.insideUnitCircle * _patrolRadius;

        _patrolDestination = _spawnPosition +
            new Vector3(randomPoint.x, 0f, randomPoint.y);

        _hasPatrolDestination = true;
        _patrolTimer = 0f;
    }

    void FaceMovementDirection(Vector3 velocity)
    {
        velocity.y = 0f;

        if (velocity.sqrMagnitude <= 0.01f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(velocity.normalized);
        Quaternion smoothRotation = Quaternion.Slerp(
            _rigidbody.rotation,
            targetRotation,
            _turnSpeed * Time.fixedDeltaTime);

        _rigidbody.MoveRotation(smoothRotation);
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
                hit.collider.transform.root == transform.root)
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

        return avoidanceDirection * desiredVelocity.magnitude;					// Slide away before the Fabricator can touch nearby geometry.
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

    void UpdateHealingTarget()
    {
        if (_healingTarget != null && !IsHealingTargetValid())
        {
            DisconnectHealingTarget();
        }

        _targetSearchTimer -= Time.deltaTime;

        if (_healingTarget == null && _targetSearchTimer <= 0f)
        {
            FindHealingTarget();
            _targetSearchTimer = _targetSearchInterval;					// Limit physics searches while still responding quickly to injured enemies.
        }

        if (_healingTarget == null)
        {
            return;
        }

        _healTimer += Time.deltaTime;

        if (_healTimer < _healInterval)
        {
            return;
        }

        _healTimer -= _healInterval;
        _healingTarget.Heal(_healAmount);					// Healing occurs in configurable pulses rather than being tied to frame rate.

        if (!IsHealingTargetValid())
        {
            DisconnectHealingTarget();
        }
    }

    void FindHealingTarget()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            _healingRange,
            _healableHits,
            _healableMask,
            QueryTriggerInteraction.Ignore);

        IHealable closestTarget = null;
        MonoBehaviour closestBehaviour = null;
        Renderer closestRenderer = null;
        float closestSqrDistance = float.PositiveInfinity;

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Collider hit = _healableHits[hitIndex];

            if (hit == null || hit.transform.root == transform.root)
            {
                continue;
            }

            IHealable candidate = hit.GetComponentInParent<IHealable>();
            MonoBehaviour candidateBehaviour = candidate as MonoBehaviour;

            if (candidate == null ||
                candidateBehaviour == null ||
                !candidateBehaviour.isActiveAndEnabled ||
                !candidate.CanReceiveHealing)
            {
                continue;
            }

            Renderer candidateRenderer =
                candidateBehaviour.GetComponentInChildren<Renderer>();

            if (!HasClearTetherPath(candidateBehaviour.transform, candidateRenderer))
            {
                continue;
            }

            float sqrDistance =
                (candidateBehaviour.transform.position - transform.position).sqrMagnitude;

            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closestTarget = candidate;
                closestBehaviour = candidateBehaviour;
                closestRenderer = candidateRenderer;
            }
        }

        if (closestTarget == null)
        {
            return;
        }

        _healingTarget = closestTarget;
        _healingTargetBehaviour = closestBehaviour;
        _healingTargetRenderer = closestRenderer;
        _healTimer = 0f;
        _lineRenderer.enabled = true;					// Show the tether as soon as an injured target has been acquired.
        UpdateTetherPositions();
    }

    bool IsHealingTargetValid()
    {
        if (_healingTargetBehaviour == null ||
            !_healingTargetBehaviour.isActiveAndEnabled ||
            !_healingTarget.CanReceiveHealing)
        {
            return false;
        }

        float sqrDistance =
            (_healingTargetBehaviour.transform.position - transform.position).sqrMagnitude;

        if (sqrDistance > _healingRange * _healingRange)
        {
            return false;					// Moving the supported enemy out of range breaks the healing connection.
        }

        return HasClearTetherPath(
            _healingTargetBehaviour.transform,
            _healingTargetRenderer);
    }

    bool HasClearTetherPath(Transform targetTransform, Renderer targetRenderer)
    {
        Vector3 origin = GetTetherOriginPosition();
        Vector3 targetPosition = GetTargetPosition(targetTransform, targetRenderer);
        Vector3 direction = targetPosition - origin;
        float distance = direction.magnitude;

        if (distance <= 0.01f)
        {
            return true;
        }

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction.normalized,
            _tetherHits,
            distance,
            _tetherObstructionMask,
            QueryTriggerInteraction.Ignore);

        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            RaycastHit hit = _tetherHits[hitIndex];

            if (hit.collider == null)
            {
                continue;
            }

            Transform hitRoot = hit.collider.transform.root;

            if (hitRoot == transform.root || hitRoot == targetTransform.root)
            {
                continue;
            }

            return false;					// Any configured obstruction between both enemies breaks the tether.
        }

        return true;
    }

    void UpdateTetherPositions()
    {
        if (_lineRenderer == null || !_lineRenderer.enabled)
        {
            return;
        }

        if (_healingTargetBehaviour == null)
        {
            DisconnectHealingTarget();
            return;
        }

        _lineRenderer.SetPosition(0, GetTetherOriginPosition());
        _lineRenderer.SetPosition(
            1,
            GetTargetPosition(
                _healingTargetBehaviour.transform,
                _healingTargetRenderer));
    }

    Vector3 GetTetherOriginPosition()
    {
        return _tetherOrigin != null
            ? _tetherOrigin.position
            : transform.position;
    }

    Vector3 GetTargetPosition(Transform targetTransform, Renderer targetRenderer)
    {
        return targetRenderer != null
            ? targetRenderer.bounds.center
            : targetTransform.position;
    }

    void DisconnectHealingTarget()
    {
        _healingTarget = null;
        _healingTargetBehaviour = null;
        _healingTargetRenderer = null;
        _healTimer = 0f;

        if (_lineRenderer != null)
        {
            _lineRenderer.enabled = false;					// Removing the visible tether also clearly communicates that healing has stopped.
        }
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
            Die();
        }
        else
        {
            StartCoroutine(FlashDamage());
        }
    }

    void Die()
    {
        _isDead = true;
        DisconnectHealingTarget();
        StopAllCoroutines();
        Destroy(gameObject);					// Destroying the root immediately removes the Fabricator and its healing effect.
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
            _material.color = _originalColor;
        }
    }

    void OnDisable()
    {
        DisconnectHealingTarget();					// Defensive cleanup also removes the tether when the object is disabled without dying.
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _healingRange);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, _patrolRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _avoidanceRadius);
    }
}
