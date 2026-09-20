using System.Collections;
using UnityEngine;

public class FlyingEnemyVisuals : MonoBehaviour
{
    [Header("Required Reference")]
    [SerializeField] Transform _visualRoot;											// Child containing the enemy's visible model.

    [Header("Hover")]
    [Min(0f)][SerializeField] float _hoverAmount = 0.12f;
    [Min(0.01f)][SerializeField] float _hoverSpeed = 2.5f;

    [Header("Flight Tilt")]
    [Min(0.01f)][SerializeField] float _movementSpeedForFullTilt = 4f;
    [Range(0f, 45f)][SerializeField] float _tiltAngle = 12f;
    [Min(0.01f)][SerializeField] float _tiltSmoothSpeed = 6f;

    [Header("Recoil")]
    [Min(0f)][SerializeField] float _recoilDistance = 0.2f;
    [Min(0.01f)][SerializeField] float _recoilDuration = 0.18f;

    Vector3 _startingLocalPosition;
    Quaternion _startingLocalRotation;

    Vector3 _lastWorldPosition;
    Vector3 _smoothedWorldVelocity;

    Coroutine _recoilCoroutine;

    float _hoverPhase;
    float _recoilOffset;

    bool _isReady;

    void Awake()
    {
        if (_visualRoot == null || _visualRoot == transform)
        {
            return;																	// The physics root should never receive the visual animation.
        }

        _startingLocalPosition = _visualRoot.localPosition;
        _startingLocalRotation = _visualRoot.localRotation;
        _hoverPhase = Random.Range(0f, Mathf.PI * 2f);								// Prevents multiple enemies from bobbing in synchronization.

        _isReady = true;
    }

    void OnEnable()
    {
        _lastWorldPosition = transform.position;
        _smoothedWorldVelocity = Vector3.zero;
        _recoilOffset = 0f;
    }

    void Update()
    {
        Vector3 currentWorldPosition = transform.position;

        if (!_isReady)
        {
            _lastWorldPosition = currentWorldPosition;
            return;
        }

        float frameTime = Time.deltaTime;

        if (frameTime <= 0f)
        {
            _lastWorldPosition = currentWorldPosition;
            return;
        }

        Vector3 measuredWorldVelocity =
            (currentWorldPosition - _lastWorldPosition) / frameTime;

        _lastWorldPosition = currentWorldPosition;

        _smoothedWorldVelocity = Vector3.Lerp(
            _smoothedWorldVelocity,
            measuredWorldVelocity,
            Mathf.Clamp01(10f * frameTime));										// Smooths movement readings from physics-driven enemies.

        UpdateHoverAndTilt(frameTime);
    }

    void UpdateHoverAndTilt(float frameTime)
    {
        float hoverOffset =
            Mathf.Sin(Time.time * _hoverSpeed + _hoverPhase) *
            _hoverAmount;

        Vector3 visualOffset =
            Vector3.up * hoverOffset +
            Vector3.back * _recoilOffset;

        _visualRoot.localPosition =
            _startingLocalPosition + visualOffset;									// Combines the hover and firing recoil without moving the collider.

        float safeMovementSpeed =
            Mathf.Max(_movementSpeedForFullTilt, 0.01f);

        Vector3 localVelocity =
            transform.InverseTransformDirection(_smoothedWorldVelocity);

        float forwardTilt =
            Mathf.Clamp(
                localVelocity.z / safeMovementSpeed,
                -1f,
                1f) *
            _tiltAngle;

        float sidewaysTilt =
            -Mathf.Clamp(
                localVelocity.x / safeMovementSpeed,
                -1f,
                1f) *
            _tiltAngle;

        Quaternion targetLocalRotation =
            _startingLocalRotation *
            Quaternion.Euler(forwardTilt, 0f, sidewaysTilt);

        _visualRoot.localRotation = Quaternion.Slerp(
            _visualRoot.localRotation,
            targetLocalRotation,
            _tiltSmoothSpeed * frameTime);											// Returns upright while stationary and leans while moving.
    }

    public void PlayRecoil()
    {
        if (!_isReady || !isActiveAndEnabled)
        {
            return;
        }

        if (_recoilCoroutine != null)
        {
            StopCoroutine(_recoilCoroutine);
        }

        _recoilCoroutine = StartCoroutine(RecoilRoutine());
    }

    IEnumerator RecoilRoutine()
    {
        float halfDuration =
            Mathf.Max(_recoilDuration * 0.5f, 0.01f);

        float elapsedTime = 0f;

        while (elapsedTime < halfDuration)
        {
            elapsedTime += Time.deltaTime;

            _recoilOffset = Mathf.Lerp(
                0f,
                _recoilDistance,
                Mathf.Clamp01(elapsedTime / halfDuration));							// Moves the model backward immediately after firing.

            yield return null;
        }

        elapsedTime = 0f;

        while (elapsedTime < halfDuration)
        {
            elapsedTime += Time.deltaTime;

            _recoilOffset = Mathf.Lerp(
                _recoilDistance,
                0f,
                Mathf.Clamp01(elapsedTime / halfDuration));							// Smoothly returns the model to its resting position.

            yield return null;
        }

        _recoilOffset = 0f;
        _recoilCoroutine = null;
    }

    void OnDisable()
    {
        if (_recoilCoroutine != null)
        {
            StopCoroutine(_recoilCoroutine);
            _recoilCoroutine = null;
        }

        _recoilOffset = 0f;
        _smoothedWorldVelocity = Vector3.zero;

        if (!_isReady)
        {
            return;
        }

        _visualRoot.localPosition = _startingLocalPosition;
        _visualRoot.localRotation = _startingLocalRotation;							// Leaves the model in its original prefab position when disabled.
    }
}