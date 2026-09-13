using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using System.Collections;

public class UIButtonSound : MonoBehaviour, 
    IPointerEnterHandler, 
    IPointerClickHandler
{
    [Header("Sounds")]
    [SerializeField] private AudioSource uiAudioSource;
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;

    [Header("Optional Delayed Action")]
    [SerializeField] private bool delayButtonAction;
    [SerializeField] private float actionDelay = 0.15f;
    [SerializeField] private UnityEvent delayedAction;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if(uiAudioSource != null && hoverSound != null)
        {
            uiAudioSource.PlayOneShot(hoverSound);
        }
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (uiAudioSource != null && hoverSound != null)
        {
            uiAudioSource.PlayOneShot(clickSound);
        }

        if (delayButtonAction)
        {
            StartCoroutine(DelayThenRunAction());
        }
    }
    private IEnumerator DelayThenRunAction()
    {
        yield return new WaitForSecondsRealtime(actionDelay);
        delayedAction?.Invoke();
    }
}
