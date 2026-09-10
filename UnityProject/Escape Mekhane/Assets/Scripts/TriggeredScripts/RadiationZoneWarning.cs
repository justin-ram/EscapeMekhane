using UnityEngine;
//Untested
public class RadiationZoneWarning : MonoBehaviour
{
    [SerializeField] private GameObject warningPanel;
    [SerializeField] private string warningMessage = "Warning: High radiation detected.";

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        warningPanel.SetActive(false);
        Debug.Log(warningMessage);
    }

    void OnTriggerExits(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }
        warningPanel.SetActive(true);
        Debug.Log("Exited Radiation Field");
    }
}
