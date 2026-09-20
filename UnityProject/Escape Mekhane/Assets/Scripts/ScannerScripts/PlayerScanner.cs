using System.Collections;
using TMPro;
using UnityEngine;

public class PlayerScanner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private ScanDialogueUI dialogUI;
    [SerializeField] private RectTransform scanSpinnerPivot;
    [SerializeField] private TextMeshProUGUI scanReadyText;

    [Header("Scan Settings")]
    [SerializeField] private float scanRange = 8f;
    [SerializeField] private KeyCode scanKey = KeyCode.E;
    [SerializeField] private float scanDuration = 2f;
    [SerializeField] private float spinnerSpeed = 540f;

    private bool isScanning;

    void Start()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (scanSpinnerPivot != null)
        {
            scanSpinnerPivot.gameObject.SetActive(false);
        }

        if (scanReadyText != null)
        {
            scanReadyText.text = "SCAN READY";
        }
    }

    void Update()
    {
        if (!Input.GetKeyDown(scanKey))
            return;

        // E closes an open scan report.
        if (dialogUI != null && dialogUI.IsShowing())
        {
            dialogUI.CloseDialogue();
            return;
        }

        // Ignore additional scan presses during the animation.
        if (isScanning)
            return;

        // Do not scan while another menu has paused the game.
        if (gameManager.instance != null &&
            gameManager.instance.isPaused)
        {
            return;
        }

        TryScan();
    }

    void TryScan()
    {
        if (playerCamera == null)
        {
            Debug.LogError("PlayerScanner has no camera assigned.");
            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        Debug.DrawRay(
            ray.origin,
            ray.direction * scanRange,
            Color.green,
            2f
        );

        if (Physics.Raycast(ray, out RaycastHit hit, scanRange))
        {
            Debug.Log("Scanner hit: " + hit.collider.name);

            ScannableObject scannedObject =
                hit.collider.GetComponentInParent<ScannableObject>();

            if (scannedObject != null)
            {
                StartCoroutine(ScanSequence(scannedObject));
                return;
            }

            Debug.Log(
                "Hit an object, but it has no ScannableObject script."
            );

            return;
        }

        Debug.Log("Scanner ray hit nothing.");
    }

    IEnumerator ScanSequence(ScannableObject scannedObject)
    {
        isScanning = true;

        float duration = Mathf.Max(0.1f, scanDuration);
        float elapsed = 0f;

        if (scanSpinnerPivot != null)
        {
            scanSpinnerPivot.localRotation = Quaternion.identity;
            scanSpinnerPivot.gameObject.SetActive(true);
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / duration);
            int percent = Mathf.RoundToInt(progress * 100f);

            if (scanSpinnerPivot != null)
            {
                scanSpinnerPivot.Rotate(
                    0f,
                    0f,
                    -spinnerSpeed * Time.deltaTime
                );
            }

            if (scanReadyText != null)
            {
                scanReadyText.text = "SCANNING " + percent + "%";
            }

            yield return null;
        }

        if (scanReadyText != null)
        {
            scanReadyText.text = "SCAN COMPLETE";
        }

        yield return new WaitForSeconds(0.25f);

        if (scanSpinnerPivot != null)
        {
            scanSpinnerPivot.gameObject.SetActive(false);
        }

        if (scanReadyText != null)
        {
            scanReadyText.text = "SCAN READY";
        }

        isScanning = false;

        if (scannedObject != null)
        {
            scannedObject.Scan(dialogUI);
        }
    }
}