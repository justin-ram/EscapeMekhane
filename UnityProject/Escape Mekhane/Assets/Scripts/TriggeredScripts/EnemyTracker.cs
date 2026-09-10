using System.Collections.Generic;
using UnityEngine;
//Untested
public class EnemyTracker : MonoBehaviour
{
    [SerializeField] private List<GameObject> enemies = new List<GameObject>();
    [SerializeField] private GameObject gate;

    private bool gateUnlocked = false;

    void CheckEnemies()
    {
        for (int i = enemies.Count -1; i >= 0; i--)
        {
            if (enemies[i] == null)
            {
                enemies.RemoveAt(i);
            }
        }
    }
    void Update()
    {
        CheckEnemies();
        if (enemies.Count == 0 && !gateUnlocked)
        {
            gate.SetActive(false);
            gateUnlocked = true;
            Debug.Log("All Enemies defeated.Gate unlocked");
        }
    }
}
