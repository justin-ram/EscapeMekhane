using UnityEngine;

public class groundPound : MonoBehaviour
{
    [SerializeField] SphereCollider col;
    public bool dealDamage;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        col.enabled = false;
    }

    // Update is called once per frame
    void Update()
    {
        if(dealDamage == true)
        {
            col.enabled = true;
        }
        else
        {
            col.enabled = false;
        }
        
       
    }
}
