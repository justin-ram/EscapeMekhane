using UnityEngine;
public class RadiationZoneWarning : MonoBehaviour
{
    [SerializeField] private GameObject warningPanel;
    [SerializeField] private string warningMessage = "Warning: High radiation detected.";

    void Start()
    {
        warningPanel.SetActive(false);
    }
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        warningPanel.SetActive(true);
        Debug.Log(warningMessage);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }
        warningPanel.SetActive(false);
        Debug.Log("Exited Radiation Field");
    }
}
