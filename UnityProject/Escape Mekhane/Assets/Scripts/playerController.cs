using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;

public class playerController : MonoBehaviour, IDamage, IPickup
{
    [SerializeField] CharacterController controller;
    [SerializeField] LayerMask ignoreLayer;

    [Header("Stats")]
    [Range(1, 100)][SerializeField] int HP;
    [Range(5, 10)][SerializeField] int speed;
    [Range(5, 10)][SerializeField] int jumpSpeed;
    [SerializeField] int maxJumps;
    [SerializeField] int sprintMult;
    [SerializeField] float gravity;
    float gravityOrig;
    //how long before you can press dash again
    [SerializeField] float dashCoolDownTime;
    [SerializeField] int dashSpeed;
    int HPOriginal;
    int jumpCount;
    //timer for dash cool down
    float dashTimer;
    float shootTimer;
    [SerializeField] float timeDashLastsTimer;
    //time before velocity is set to 0.
    float timeDashLasts;
    [SerializeField] int interactDist;
    float healCoolDown;
    [SerializeField] int healAmount;
    [SerializeField] float healCoolDownTimer;
    float healDuration;
    [SerializeField] float healDurationTimer;

    [Header("Gun Stuff")]
    [SerializeField] List<gunStats> gunInv = new List<gunStats>();
    [SerializeField] GameObject gunModel;
    [SerializeField] Transform gunEndpoint;

    [Header("Audio")]
    public AudioClip[] audJumpSound;
    [Range(0, 1)][SerializeField] float audJumpVol;
    public AudioClip[] audStepsSound;
    [Range(0, 1)][SerializeField] float audStepsVol;
    public AudioClip[] audDashSound;
    [Range(0, 1)][SerializeField] float audDashVol;
    public AudioClip[] audHurtSound;
    [Range(0, 1)][SerializeField] float audHurtVol;
    int gunInvPos;
    bool isSprinting;
    bool isPlayingSteps;

    [Header("Grapple Things")]
    [SerializeField] int grappleDistance;
    [SerializeField] int grappleSpeed;

    Vector3 hitPosition;
    bool isGrappling;
    Vector3 grappleDirection;

    Vector3 moveDirection;
    Vector3 playerVelocity;
    Vector3 dashDirection;

    Vector3 cameraDirection;
    Vector3 damageDir;

    Vector3 wallJumpDirection;
    int pushBackSpeed;
    float pushBackDuration;


    /*Make a bool of iswallrunning
      use movedirection because it wount be being used currently for the vector to go along
      make a int wallRundist
    */

    bool isWallRunning;
    public bool isWallRunLeft;
    public bool isWallRunRight;
    [SerializeField] float wallRunDist;
    [SerializeField] Transform wallRunTransform;
    [SerializeField] float wallRunGrav;
    [SerializeField] float wallJumpSpeed;

    [SerializeField] float wallJumpDurationTime;
    float wallJumpDuration;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        HPOriginal = HP;
        gravityOrig = gravity;
        spawnPlayer();
    }

    // Update is called once per frame
    void Update()
    {
        movement();
        sprint();
        dash();
        grapple();
        HealHp();
        weaponPushBack();
        interactUpdateUi();
        damagePushBack();
    }

    void movement()
    {
        //Debug.DrawRay(Camera.main.transform.position, Camera.main.transform.forward * gunInv[gunInvPos].shootDistance, Color.red);
        //Debug.DrawRay(Camera.main.transform.position, Camera.main.transform.forward * interactDist, Color.blue);
        shootTimer += Time.deltaTime;
        if (controller.isGrounded)
        {
            jumpCount = 0;
            playerVelocity.y = 0;

            if (moveDirection.magnitude > 0.3 && !isPlayingSteps)
            {
                StartCoroutine(playSteps());
            }
        }

        wallRun();
        //change this portion to check if wall running or grappling dont do if either
        if (!isGrappling && isWallRunning != true)
        {
            moveDirection = Input.GetAxis("Horizontal") * transform.right + Input.GetAxis("Vertical") * transform.forward;
            controller.Move(moveDirection.normalized * speed * Time.deltaTime);
        }
        /* if is walling = true then engage wall running
           
        */
        if (!isGrappling && !isWallRunning)
        {
            jump();
            controller.Move(playerVelocity * Time.deltaTime);
            playerVelocity.y -= gravity * Time.deltaTime;
        }


      //  wallJump();



        if (Input.GetButton("Fire1") && gunInv.Count > 0 && shootTimer > gunInv[gunInvPos].shootFireRate)
        {
            shoot();
        }

        if (Input.GetButtonDown("Fire2"))
        {
            interactWith();
        }

        selectGun();
    }

    /*
        void wallRun()
        {
            
            either set gravity to 0 or close to zero.
            after that calculate direction of wall and player so that it aligns across the wall
            use vector3.projectonplane
        }
    */
    void wallRun()
    {
        // create a raycast to see if you can wall run. if true then set isWallrun to true;
        //Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, gunInv[gunInvPos].shootDistance, ~ignoreLayer)
        RaycastHit hit;
        RaycastHit hit2;
        Debug.DrawLine(wallRunTransform.position, wallRunTransform.position + wallRunTransform.right * wallRunDist, Color.blue);
        Debug.DrawLine(wallRunTransform.position, wallRunTransform.position + -wallRunTransform.right * wallRunDist, Color.red);
        if (Physics.Raycast(wallRunTransform.position, wallRunTransform.right, out hit, wallRunDist, ~ignoreLayer))
        {
            // Debug.Log(hit.collider.name);
            IWallRun wallRun = hit.collider.GetComponent<IWallRun>();

            if (wallRun != null)
            {
                // Debug.Log("WallRunningRight");
                isWallRunRight = true;
                gravity = wallRunGrav;
                wallJumpDirection = hit.normal;
                jumpCount = 0;
                //Camera.main.transform.rotation = Quaternion.Euler(4.28847361f, 0.699134409f, 18.5101738f);
                moveDirection = Input.GetAxis("Horizontal") * transform.right + Input.GetAxis("Vertical") * transform.forward;
                //gets the direction of the plane
                Vector3 wallDir = Vector3.ProjectOnPlane(moveDirection.normalized, hit.normal).normalized;
                controller.Move(wallDir * speed * Time.deltaTime);
            }

        }
        else if (Physics.Raycast(wallRunTransform.position, -wallRunTransform.right, out hit2, wallRunDist, ~ignoreLayer))
        {
            // Debug.Log(hit2.collider.name);
            IWallRun wallRun = hit2.collider.GetComponent<IWallRun>();

            if (wallRun != null)
            {
                // Debug.Log("WallRunningleft");
                isWallRunLeft = true;
                gravity = wallRunGrav;
                jumpCount = 0;
              //  Camera.main.transform.rotation = Quaternion.Euler(-4.28847361f, -0.699134409f, -18.5101738f);
                wallJumpDirection = hit.normal;
                moveDirection = Input.GetAxis("Horizontal") * transform.right + Input.GetAxis("Vertical") * transform.forward;
                //gets the direction of the plane
                Vector3 wallDir = Vector3.ProjectOnPlane(moveDirection.normalized, hit.normal).normalized;
                controller.Move(wallDir * speed * Time.deltaTime);
            }

        }
        else
        {
            //Debug.Log("Gravity Returning to normal");
            //remove is wall running here and use iswallrunningleft. make a check after both the if statements to see
            //if either one of them is true and then set this one to true if one of them is true.
            isWallRunLeft = false;
            isWallRunRight = false;
            gravity = gravityOrig;
        }
        if (isWallRunLeft)
        {
            isWallRunning = true;
        }
        else if (isWallRunRight)
        {
            isWallRunning = true;
        }
        else
        {
            isWallRunning = false;
        }

    }

    void sprint()
    {
        if (Input.GetButtonDown("Sprint"))
        {
            speed *= sprintMult;
            isSprinting = true;
        }
        else if (Input.GetButtonUp("Sprint"))
        {
            speed /= sprintMult;
            isSprinting = false;
        }
    }

    IEnumerator playSteps()
    {
        isPlayingSteps = true;
        audioManager.instance.audPlayer.PlayOneShot(audStepsSound[Random.Range(0, audStepsSound.Length)], audStepsVol);
        if (isSprinting)
        {
            yield return new WaitForSeconds(0.3f);
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
        }
        isPlayingSteps = false;
    }
    void jump()
    {
        if (Input.GetButtonDown("Jump") && jumpCount < maxJumps)
        {
            jumpCount++;
            playerVelocity.y = jumpSpeed;
            audioManager.instance.audPlayer.PlayOneShot(audJumpSound[Random.Range(0, audJumpSound.Length)], audJumpVol);
        }
    }

    void wallJump()
    {
        if (wallJumpDuration > 0)
        {
            wallJumpDuration -= Time.deltaTime;
            controller.Move(wallJumpDirection * wallJumpSpeed * Time.deltaTime);
        }
        
        wallJumpDuration = wallJumpDurationTime;
    }

    public void jumpPowerUp(int amount)
    {
        maxJumps += amount;
    }

    void dash()
    {
        if (dashTimer > 0)
        {
            dashTimer -= Time.deltaTime;
            updatePlayerUI();
        }
        if (timeDashLasts > 0)
        {
            timeDashLasts -= Time.deltaTime;
            controller.Move(dashDirection.normalized * dashSpeed * Time.deltaTime);
        }
        if (timeDashLasts <= 0)
        {
            playerVelocity.x = 0;
        }
        if (Input.GetButtonDown("Dash") && dashTimer <= 0 && isGrappling == false)
        {
            dashDirection = transform.forward;
            dashTimer = dashCoolDownTime;
            timeDashLasts = timeDashLastsTimer;
            updatePlayerUI();
            StartCoroutine(invincibilityWindow());
            audioManager.instance.audPlayer.PlayOneShot(audDashSound[Random.Range(0, audDashSound.Length)], audDashVol);
        }
    }
    void weaponPushBack()
    {
        cameraDirection = Camera.main.transform.forward;

        if (gunInv.Count > 0)
        {
            if (gunInv[gunInvPos].weaponPushLasts > 0)
            {
                gunInv[gunInvPos].weaponPushLasts -= Time.deltaTime;
                controller.Move(-cameraDirection.normalized * gunInv[gunInvPos].weaponPushSpeed * Time.deltaTime);
            }
        }
    }
    IEnumerator invincibilityWindow()
    {
        gameObject.layer = LayerMask.NameToLayer("Invincible");
        yield return new WaitForSeconds(timeDashLastsTimer);
        gameObject.layer = LayerMask.NameToLayer("Player");
    }
    void shoot()
    {
        shootTimer = 0;
        audioManager.instance.audPlayer.PlayOneShot(gunInv[gunInvPos].shootSound[Random.Range(0, gunInv[gunInvPos].shootSound.Length)], gunInv[gunInvPos].shootSoundVol);

        RaycastHit hit;

        if (gunInv[gunInvPos].isProjectile == false)
        {
            if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, gunInv[gunInvPos].shootDistance, ~ignoreLayer))
            {
                // Debug.Log(hit.collider.name);

                Instantiate(gunInv[gunInvPos].hitEffect, hit.point, Quaternion.identity);
                IDamage dmg = hit.collider.GetComponent<IDamage>();
                if (dmg != null)
                {
                    dmg.takeDamage(gunInv[gunInvPos].shootDamage, gunInv[gunInvPos].damageDir, gunInv[gunInvPos].pushBackSpeed, gunInv[gunInvPos].pushBackDuration);
                }
            }
        }
        else
        {
            Instantiate(gunInv[gunInvPos].projectile, gunEndpoint.position, Camera.main.transform.rotation);
            gunInv[gunInvPos].weaponPushLasts = gunInv[gunInvPos].weaponPushLastsTimer;
        }
    }

    public void takeDamage(int amount, Vector3 damageDirection, int damageSpeed, float pushDurationTimer)
    {
        HP -= amount;
        pushBackDuration = pushDurationTimer;
        damageDir = damageDirection;
        pushBackSpeed = damageSpeed;
        audioManager.instance.audPlayer.PlayOneShot(audHurtSound[Random.Range(0, audHurtSound.Length)], audHurtVol);
        healCoolDown = healCoolDownTimer;
        updatePlayerUI();
        StartCoroutine(flashDamage());
        if (HP <= 0)
        {
            //You lose. Pauses game and put loss screen.
            gameManager.instance.youLose();
        }
    }

    void HealHp()
    {
        if (healCoolDown > 0)
        {
            healCoolDown -= Time.deltaTime;
        }
        if (healDuration > 0)
        {
            healDuration -= Time.deltaTime;
        }
        if (healCoolDown <= 0 && HP < HPOriginal)
        {
            if (healDuration <= 0)
            {
                HP += healAmount;
                healDuration = healCoolDownTimer;
                updatePlayerUI();
            }
        }
    }

    void updatePlayerUI()
    {
        gameManager.instance.playerHPBar.fillAmount = (float)HP / HPOriginal;
        gameManager.instance.playerDashBar.fillAmount = (float)dashTimer / dashCoolDownTime;
    }

    public void getGunStats(gunStats gun)
    {
        gunInv.Add(gun);
        gunInvPos = gunInv.Count - 1;
        changeGunModel();
    }

    void changeGunModel()
    {
        gunModel.GetComponent<MeshFilter>().sharedMesh = gunInv[gunInvPos].gunModel.GetComponent<MeshFilter>().sharedMesh;
        gunModel.GetComponent<MeshRenderer>().sharedMaterial = gunInv[gunInvPos].gunModel.GetComponent<MeshRenderer>().sharedMaterial;
    }
    void selectGun()
    {
        if (Input.GetAxis("Mouse ScrollWheel") > 0 && gunInvPos < gunInv.Count - 1)
        {
            gunInvPos++;
            changeGunModel();
        }
        else if (Input.GetAxis("Mouse ScrollWheel") < 0 && gunInvPos > 0)
        {
            gunInvPos--;
            changeGunModel();
        }
    }
    IEnumerator flashDamage()
    {
        gameManager.instance.damageFlash.SetActive(true);
        yield return new WaitForSeconds(0.3f);
        gameManager.instance.damageFlash.SetActive(false);
    }

    void interactUpdateUi()
    {
        RaycastHit hit;

        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, interactDist, ~ignoreLayer))
        {
            // Debug.Log(hit.collider.name);
            IInteract interact = hit.collider.GetComponent<IInteract>();
            if (interact != null)
            {
                gameManager.instance.showInteract(interact);
            }

        }
        else
        {
            gameManager.instance.disableInteract();
        }
    }
    void interactWith()
    {
        RaycastHit hit;

        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, interactDist, ~ignoreLayer))
        {
            //  Debug.Log(hit.collider.name);
            IInteract interact = hit.collider.GetComponent<IInteract>();
            if (interact != null)
            {
                interact.Interact();
            }
        }
    }

    void grapple()
    {
        if (Input.GetButtonDown("Grapple"))
        {
            RaycastHit hit;

            if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hit, grappleDistance, ~ignoreLayer))
            {
                Debug.Log(hit.collider.name);
                IGrapple grapple = hit.collider.GetComponent<IGrapple>();
                if (grapple != null)
                {
                    hitPosition = hit.point;
                    grappleDirection = hit.point - transform.position;
                    isGrappling = true;
                    gravity = 0;
                }
            }
        }
        if (isGrappling)
        {
            controller.Move(grappleDirection.normalized * grappleSpeed * Time.deltaTime);
            if (Vector3.Distance(hitPosition, transform.position) < 0.9f)
            {
                isGrappling = false;
                gravity = gravityOrig;
            }
        }

    }

    public void spawnPlayer()
    {
        if (controller == null)
        {
            Debug.LogError("Spawn failed: controller is not assigned.");
            return;
        }

        if (gameManager.instance == null)
        {
            Debug.LogError("Spawn failed: Game Manager instance is missing.");
            return;
        }

        if (gameManager.instance.playerSpawnPos == null)
        {
            Debug.LogError("Spawn failed: Player Spawn Position is not assigned.");
            return;
        }

        controller.transform.position =
            gameManager.instance.playerSpawnPos.transform.position;

        Physics.SyncTransforms();
        HP = HPOriginal;
        updatePlayerUI();
    }

    public void teleportPlayer(Vector3 teleportPoint)
    {
        controller.transform.position = teleportPoint;
        Physics.SyncTransforms();
    }

    void damagePushBack()
    {
        if (pushBackDuration > 0)
        {
            pushBackDuration -= Time.deltaTime;
            controller.Move(damageDir * pushBackSpeed * Time.deltaTime);
        }
    }


}
