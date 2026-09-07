using UnityEngine;

public class PlayerScanner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private ScanDialogueUI dialogUI;

    [Header("Scan Settings")]
    [SerializeField] private float scanRange = 8f;
    [SerializeField] private KeyCode scanKey = KeyCode.E;

    void Start()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(scanKey))
        {
            if (dialogUI.IsShowing())
            {
                dialogUI.CloseDialogue();
                return;
            }

            TryScan();

        }

        void TryScan()
        {
            Ray ray = playerCamera.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f)
            );

            Debug.DrawRay(ray.origin, ray.direction * scanRange, Color.green, 2f);

            if (Physics.Raycast(ray, out RaycastHit hit, scanRange))
            {
                Debug.Log("Scanner hit: " + hit.collider.name);

                ScannableObject scannedObject =
                    hit.collider.GetComponentInParent<ScannableObject>();

                if (scannedObject != null)
                {
                    scannedObject.Scan(dialogUI);
                    return;
                }

                Debug.Log("Hit an object, but it has no ScannableObject script.");
                return;
            }

            Debug.Log("Scanner ray hit nothing.");
        }
    }
}
