using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 3f;
    public float sprintSpeed = 5f;
    public KeyCode sprintKey = KeyCode.LeftShift;


    [Header("Gravity")]
    public float gravity = -20f;


    [Header("Stamina")]
    public Transform staminaBar;
    public float maxStamina = 100f;
    public float staminaDrain = 5f;
    public float staminaRegen = 8f;
    public float regenDelay = 1.5f;


    [Header("3D Bar Fade")]
    public float fadeSpeed = 5f;


    [Header("Footsteps")]
    public AudioSource audioSource;
    public AudioClip[] footstepSounds;
    

    public float walkStepDistance = 2.3f;
    public float sprintStepDistance = 1.8f;


    [Header("Metal Footsteps")]
    public AudioClip[] metalFootstepSounds;


    [Header("Metal Detection")]
    public string metalTag = "Metal";
    public float groundCheckDistance = 1.5f;
    


    private CharacterController controller;
    private Vector3 velocity;

    private float stamina;
    private float regenTimer;

    // Distance traveled since last footstep
    private float distanceSinceFootstep;

    // Position used to calculate movement distance
    private Vector3 lastFootstepPosition;


    private Vector3 originalBarScale;

    private Renderer staminaRenderer;
    private Material staminaMaterial;

    private bool staminaUsed;


    [Header("isHiding")]
    public bool isHiding = false;


    void Start()
    {
        controller = GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        stamina = maxStamina;


        if (staminaBar != null)
        {
            originalBarScale =
                staminaBar.localScale;

            staminaRenderer =
                staminaBar.GetComponent<Renderer>();

            if (staminaRenderer != null)
            {
                staminaMaterial =
                    staminaRenderer.material;
            }
        }


        // Start measuring footsteps from player's starting position
        lastFootstepPosition = transform.position;

        distanceSinceFootstep = 0f;


        SetBarAlpha(0);
    }


    void Update()
    {
        MovePlayer();

        ApplyGravity();

        UpdateStamina();

        HandleFootsteps();

        FadeStamina();
    }


    // =========================================================
    // MOVEMENT
    // =========================================================

    void MovePlayer()
    {
        float horizontal =
            Input.GetAxisRaw("Horizontal");

        float vertical =
            Input.GetAxisRaw("Vertical");


        Vector3 move =
            transform.right * horizontal +
            transform.forward * vertical;


        if (move.sqrMagnitude > 1f)
            move.Normalize();


        bool wantsSprint =
            Input.GetKey(sprintKey) &&
            vertical > 0;


        bool sprinting =
            wantsSprint &&
            stamina > 0;


        float currentSpeed =
            sprinting ?
            sprintSpeed :
            walkSpeed;


        controller.Move(
            move *
            currentSpeed *
            Time.deltaTime
        );


        if (sprinting)
        {
            stamina -=
                staminaDrain *
                Time.deltaTime;

            staminaUsed = true;

            regenTimer = 0;


            if (stamina < 0.1f)
                stamina = 0.1f;
        }
    }


    // =========================================================
    // GRAVITY
    // =========================================================

    void ApplyGravity()
    {
        if (controller.isGrounded)
        {
            velocity.y = -2f;
        }
        else
        {
            velocity.y +=
                gravity *
                Time.deltaTime;
        }


        controller.Move(
            velocity *
            Time.deltaTime
        );
    }


    // =========================================================
    // STAMINA
    // =========================================================

    void UpdateStamina()
    {
        if (stamina < maxStamina)
        {
            regenTimer +=
                Time.deltaTime;


            if (regenTimer >= regenDelay)
            {
                stamina +=
                    staminaRegen *
                    Time.deltaTime;


                if (stamina > maxStamina)
                    stamina = maxStamina;
            }
        }


        if (staminaBar != null)
        {
            float percent =
                stamina /
                maxStamina;


            staminaBar.localScale =
                new Vector3(
                    originalBarScale.x * percent,
                    originalBarScale.y,
                    originalBarScale.z
                );
        }


        if (stamina >= maxStamina)
        {
            staminaUsed = false;
        }
    }


    // =========================================================
    // STAMINA FADE
    // =========================================================

    void FadeStamina()
    {
        if (staminaMaterial == null)
            return;


        bool visible =
            staminaUsed ||
            stamina < maxStamina;


        float targetAlpha =
            visible ? 1f : 0f;


        Color color =
            staminaMaterial.color;


        color.a = Mathf.Lerp(
            color.a,
            targetAlpha,
            Time.deltaTime *
            fadeSpeed
        );


        staminaMaterial.color =
            color;
    }


    void SetBarAlpha(float alpha)
    {
        if (staminaMaterial == null)
            return;


        Color color =
            staminaMaterial.color;

        color.a = alpha;

        staminaMaterial.color =
            color;
    }


    // =========================================================
    // FOOTSTEPS
    // =========================================================

    void HandleFootsteps()
    {
        bool moving =
            Input.GetAxisRaw("Horizontal") != 0 ||
            Input.GetAxisRaw("Vertical") != 0;


        // Don't count movement while in the air
        if (!moving ||
            !controller.isGrounded)
        {
            // Update position so falling/teleporting
            // doesn't create a huge distance jump.
            lastFootstepPosition =
                transform.position;

            return;
        }


        // ==========================================
        // CALCULATE ACTUAL DISTANCE MOVED
        // ==========================================

        float movedDistance =
            Vector3.Distance(
                transform.position,
                lastFootstepPosition);


        distanceSinceFootstep +=
            movedDistance;


        lastFootstepPosition =
            transform.position;


        // ==========================================
        // CHECK SPRINTING
        // ==========================================

        bool sprinting =
            Input.GetKey(sprintKey) &&
            Input.GetAxisRaw("Vertical") > 0 &&
            stamina > 0;


        float requiredDistance =
            sprinting ?
            sprintStepDistance :
            walkStepDistance;


        // ==========================================
        // PLAY FOOTSTEP AFTER DISTANCE
        // ==========================================

        if (distanceSinceFootstep >= requiredDistance)
        {
            PlayFootstep();

            distanceSinceFootstep = 0f;
        }
    }


    // =========================================================
    // PLAY FOOTSTEP
    // =========================================================

    void PlayFootstep()
    {
        if (audioSource == null)
            return;


        RaycastHit hit;


        // Check what GameObject is directly underneath
        bool hitGround =
            Physics.Raycast(
                transform.position,
                Vector3.down,
                out hit,
                groundCheckDistance,
                ~0,
                QueryTriggerInteraction.Ignore
            );


        // =====================================================
        // METAL
        // =====================================================

        if (hitGround)
        {
            GameObject groundObject =
                hit.collider.gameObject;


            if (groundObject.CompareTag(metalTag))
            {
                if (metalFootstepSounds != null &&
                    metalFootstepSounds.Length > 0)
                {
                    int index =
                        Random.Range(
                            0,
                            metalFootstepSounds.Length
                        );


                    AudioClip metalSound =
                        metalFootstepSounds[index];


                    if (metalSound != null)
                    {
audioSource.PlayOneShot(
    metalSound,
    1.3f
);

                        return;
                    }
                }
            }
        }


        // =====================================================
        // NORMAL FOOTSTEP
        // =====================================================

        if (footstepSounds == null ||
            footstepSounds.Length == 0)
            return;


        int normalIndex =
            Random.Range(
                0,
                footstepSounds.Length
            );


        AudioClip normalSound =
            footstepSounds[normalIndex];


        if (normalSound != null)
        {
            audioSource.PlayOneShot(
                normalSound
            );
        }
    }


    // =========================================================
    // STAMINA PERCENT
    // =========================================================

    public float StaminaPercent
    {
        get
        {
            return stamina / maxStamina;
        }
    }


    // =========================================================
    // IS SPRINTING
    // =========================================================

    public bool IsSprinting
    {
        get
        {
            return Input.GetKey(sprintKey)
                && stamina > 0.1f;
        }
    }
}