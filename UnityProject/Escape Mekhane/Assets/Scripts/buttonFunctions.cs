using UnityEngine;
using UnityEngine.SceneManagement;

public class buttonFunctions : MonoBehaviour
{
    public void resume()
    {
        gameManager.instance.stateUnpause();
    }

    public void restart()
    {
        //Debug.Log("RESTARTING");
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        gameManager.instance.stateUnpause();
    }

    public void quit()
    {
#if UNITY_EDITOR
        //Debug.Log("QUITING");
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void credits()
    {
        gameManager.instance.openMenu("Credits");
    }

    public void back()
    {
        gameManager.instance.openMenu("Main Menu");
    }
    public void settings()
    {
        gameManager.instance.openMenu("Settings");
    }
    public void StartGame()
    {
        SceneManager.LoadScene("Level1_Blockout");
    }

    public void respawnPlayer()
    {
        gameManager.instance.playerScript.spawnPlayer();
        gameManager.instance.stateUnpause();
    }
}
