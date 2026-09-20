using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelTransition : MonoBehaviour
{
    [SerializeField] private string destinationScene;
    private bool isLoading;

    private void OnTriggerEnter(Collider other)
    {
        if (isLoading || !other.CompareTag("Player"))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(destinationScene))
        {
            Debug.LogError("No destination scene has been entered.");
            return;
        }

        isLoading = true;

        Debug.Log("Loading scene: " + destinationScene);

        SceneManager.LoadSceneAsync(
            destinationScene,
            LoadSceneMode.Single
            );
    }
}
