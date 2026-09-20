using UnityEngine;

public class DoorPivot : MonoBehaviour
{
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float openSpeed = 120f;

    private Quaternion openRotation;
    private bool isOpening;

    void Awake()
    {
        openRotation = transform.localRotation * Quaternion.Euler(0f, openAngle, 0f);
    }
    void Update()
    {
        if (!isOpening)
        {
            return;
        }
        transform.localRotation = Quaternion.RotateTowards(
            transform.localRotation,
            openRotation,
            openSpeed * Time.deltaTime
            );
        if (Quaternion.Angle(transform.localRotation, openRotation) < 0.1f)
        {
            transform.localRotation = openRotation;
            isOpening = false;
        }
    }
    
    public void OpenDoor()
    {
        isOpening = true;
    }
}
