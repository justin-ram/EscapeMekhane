using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class Laser : MonoBehaviour
{
    [Header("Scale Things")]
    [SerializeField] float scaleValue;
    [SerializeField] Transform scalePivot;
    Transform scalePivotOriginal;

    float scaleY;
    float scaleYOrig;
    [SerializeField] float delayTimer;
    bool scaleShrunk;
    [SerializeField] bool shouldShrink;

    [Header("Rotate Things")]
    [SerializeField] Transform rotationPivot;
    [SerializeField] bool shouldRotate;
    [SerializeField] float rotationX;
  


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        scalePivotOriginal = scalePivot;
        scaleY = scalePivot.localScale.y;
        scaleYOrig = scalePivot.localScale.y;
   
    }

    // Update is called once per frame
    void Update()
    {
        if (gameManager.instance.isPaused == false)
        {
            StartCoroutine(scaleLasers());
            rotateLasers();
        }

    }

    IEnumerator scaleLasers()
    {
        if (shouldShrink)
        {
            if (scaleY > 0.1 && !scaleShrunk)
            {
                scaleY -= scaleValue;
                scalePivot.localScale = new Vector3(scalePivot.localScale.x, scaleY, scalePivot.localScale.z);
            }
            else
            {
                scaleShrunk = true;
            }
            yield return new WaitForSeconds(delayTimer);

            if (scaleShrunk)
            {
                scaleY += scaleValue;
                scalePivot.localScale = new Vector3(scalePivot.localScale.x, scaleY, scalePivot.localScale.z);
                if (scaleY > scaleYOrig)
                    scaleShrunk = false;
            }
        }
    }

    void rotateLasers()
    {
        if (shouldRotate)
        {

            rotationPivot.Rotate(rotationX, 0, 0);

        }
    }

}
