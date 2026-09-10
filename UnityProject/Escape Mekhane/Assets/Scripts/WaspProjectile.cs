using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class WaspProjectile : MonoBehaviour
{
    [Header("Projectile")]
    [SerializeField] float _speed = 14f;
    [SerializeField] float _lifetime = 5f;
    [Min(1)][SerializeField] int _damage = 10;
    [SerializeField] int _pushbackSpeed = 4;
    [SerializeField] float _pushbackDuration = 0.15f;

    Rigidbody _rigidbody;
    SphereCollider _triggerCollider;
    Transform _ownerRoot;
    Vector3 _travelDirection;

    bool _isInitialized;
    bool _hasHit;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _triggerCollider = GetComponent<SphereCollider>();

        _rigidbody.useGravity = false;
        _rigidbody.isKinematic = false;
        _rigidbody.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;
        _triggerCollider.isTrigger = true;					// A trigger projectile reports contact without physically pushing the player.
    }

    void Start()
    {
        if (!_isInitialized)
        {
            _travelDirection = transform.forward.normalized;					// Allow the prefab to work during a simple manual projectile test.
            ApplyVelocity();
        }

        Destroy(gameObject, _lifetime);
    }

    public void Initialize(Vector3 travelDirection, Transform owner)
    {
        _travelDirection = travelDirection.sqrMagnitude > 0.01f
            ? travelDirection.normalized
            : transform.forward.normalized;

        _ownerRoot = owner != null ? owner.root : null;
        _isInitialized = true;

        transform.rotation = Quaternion.LookRotation(_travelDirection);
        IgnoreOwnerCollisions(owner);
        ApplyVelocity();					// Velocity is set once, making the projectile non-tracking.
    }

    void ApplyVelocity()
    {
        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity =
                _travelDirection * _speed;
        }
    }

    void IgnoreOwnerCollisions(Transform owner)
    {
        if (owner == null || _triggerCollider == null)
        {
            return;
        }

        Collider[] ownerColliders =
            owner.GetComponentsInChildren<Collider>();

        foreach (Collider ownerCollider in ownerColliders)
        {
            Physics.IgnoreCollision(
                _triggerCollider,
                ownerCollider,
                true);					// Prevent the shot from immediately colliding with the Wasp that fired it.
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (_hasHit || other.isTrigger)
        {
            return;
        }

        if (_ownerRoot != null &&
            other.transform.root == _ownerRoot)
        {
            return;
        }

        Transform hitRoot = other.transform.root;

        if (hitRoot.CompareTag("Player"))
        {
            IDamage damageable =
                other.GetComponentInParent<IDamage>();

            if (damageable != null)
            {
                damageable.takeDamage(
                    _damage,
                    _travelDirection,
                    _pushbackSpeed,
                    _pushbackDuration);					// Use the team interface already implemented by playerController.
            }
        }

        _hasHit = true;
        Destroy(gameObject);					// The projectile ends on the first solid surface or player hit.
    }
}

