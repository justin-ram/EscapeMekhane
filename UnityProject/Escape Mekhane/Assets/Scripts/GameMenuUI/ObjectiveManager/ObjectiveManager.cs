using UnityEngine;

public class ObjectiveManager : MonoBehaviour
{
    public static ObjectiveManager instance;
    [Header("Objective")]
    [TextArea(2, 4)]
    [SerializeField] private string[] objectives;
    [SerializeField] private int currentObjectiveIndex;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    public string GetCurrentObjective()
    {
        if (currentObjectiveIndex >= objectives.Length)
        {
            return "All Objectives Complete";
        }
        return objectives[currentObjectiveIndex];
    }

    public void CompleteCurrentObjective()
    {
        if (currentObjectiveIndex >= objectives.Length)
        {
            return;
        }

        Debug.Log("Objective Completed " + objectives[currentObjectiveIndex]);
        currentObjectiveIndex++;
        Debug.Log("New Objectives " + currentObjectiveIndex.ToString());
    }
}
