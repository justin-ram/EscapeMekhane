using UnityEngine;

public class camerController : MonoBehaviour
{
    [SerializeField] int camSens;
    [SerializeField] int lockVertMin, lockVertMax;

    [Header("WallRun Lean")]
    [SerializeField] float leanAngle;
    float leanAngleStart;
    [SerializeField] float leanSpeed;
    float currentLeanAngle;
    float camRotX;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Update is called once per frame
    void Update()
    {
        if(gameManager.instance != null)
        {
            if (gameManager.instance.isPaused == false)
            {
                rotateCamera();
                LeanCamera();

                transform.localRotation = Quaternion.Euler(camRotX, 0, currentLeanAngle);
            }
        }
    }

    void rotateCamera()
    {
        float mouseX = Input.GetAxisRaw("Mouse X") * camSens;
        float mouseY = Input.GetAxisRaw("Mouse Y") * camSens;

        camRotX -= mouseY;
        camRotX = Mathf.Clamp(camRotX, lockVertMin, lockVertMax);
        transform.parent.Rotate(Vector3.up * mouseX);
    }

    void LeanCamera()
    {
        if(gameManager.instance.playerScript.isWallRunRight)
        {
            leanAngleStart = leanAngle;
           
        }
        else if(gameManager.instance.playerScript.isWallRunLeft)
        {
            leanAngleStart = -leanAngle;
          
        }
        else
        {
            leanAngleStart = 0;
        }

        currentLeanAngle = Mathf.LerpAngle(transform.localEulerAngles.z, leanAngleStart, leanSpeed * Time.deltaTime);

    }
}
