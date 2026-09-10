using UnityEngine.Events;
using UnityEngine;

//Untested
public class UniversalTrigger : MonoBehaviour
{
    [SerializeField] private string targetTag = "Player";
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private UnityEvent onTriggerEntered;

    private bool hasTriggered = false;
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(targetTag))
        {
            return;
        }

        if (triggerOnce && hasTriggered)
        {
            return;
        }

        hasTriggered = true;
        onTriggerEntered?.Invoke();
    }
}
