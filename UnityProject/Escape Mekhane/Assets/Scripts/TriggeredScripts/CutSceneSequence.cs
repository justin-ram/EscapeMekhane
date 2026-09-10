using UnityEngine;
using UnityEngine.Events;
//UnTested
public class CutSceneSequence : MonoBehaviour
{
    [SerializeField] private bool playOne = true;
    [SerializeField] private UnityEvent onCutsceneStarted;

    private bool hasPlayed;

    public void PlayCutscene()
    {
        if (playOne && hasPlayed)
        {
            return;
        }

        hasPlayed = true;
        Debug.Log("CutScene started " + gameObject.name);
        onCutsceneStarted?.Invoke();
    }
}
