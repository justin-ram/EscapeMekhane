using UnityEngine;
using System.Collections;

public class sheild : MonoBehaviour, IDamage
{
    [SerializeField] Renderer model;
    [SerializeField] int HP;
    [SerializeField] int speed;
    [SerializeField] bool isBossShield;
    [SerializeField] bool isLayer1;
    [SerializeField] bool isLayer2;
    [SerializeField] bool isLayer3;

    int HPOrig;
    Color colorOrig;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       colorOrig = model.material.color;
        HPOrig = HP;
    }

    // Update is called once per frame
    void Update()
    {

        transform.RotateAround(transform.parent.position, Vector3.up, speed * Time.deltaTime);
        

        
    }
    public void takeDamage(int amount, Vector3 damageDirection, int damageSpeed, float pushDurationTimer)
    {
        HP -= amount;
       
        if (HP <= 0)
        {
            boss2 parentScript = transform.parent.GetComponent<boss2>();
            if (isBossShield == true)
            {
                if (isLayer1 == true && parentScript.layer1 == true)
                {
                    parentScript.layer1 = false;
                    parentScript.layer2 = true;
                }
                else if (isLayer2 == true && parentScript.layer2 == true)
                {
                    parentScript.layer2 = false;
                    parentScript.layer3 = true;
                }
                else if (isLayer3 == true&& parentScript.layer3 == true)
                {
                    parentScript.layer3 = false;
                    parentScript.layer4 = true;
                }
            }
            Destroy(gameObject);
        }
        else
        {
            StartCoroutine(flashRed());
            if(HPOrig*2/3 >= HP&& HPOrig/3<HP)
            {
                model.material.color = Color.gray;
                colorOrig = model.material.color;
            }
            else if(HPOrig/3>=HP)
            {
                model.material.color = Color.black;
                colorOrig = model.material.color;
            }
        }
    }
    IEnumerator flashRed()
    {
        model.material.color = Color.red;
        yield return new WaitForSeconds(0.01f);
        model.material.color = colorOrig;
    }
}
