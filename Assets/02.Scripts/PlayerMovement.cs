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

    public float walkStepDelay = 0.7f;
    public float sprintStepDelay = 0.4f;



    private CharacterController controller;
    private Vector3 velocity;


    private float stamina;
    private float regenTimer;

    private float stepTimer;


    private Vector3 originalBarScale;


    private Renderer staminaRenderer;
    private Material staminaMaterial;


    private bool staminaUsed;
public bool isHiding = false;



    void Start()
    {
        controller = GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;


        stamina = maxStamina;


        if (staminaBar != null)
        {
            originalBarScale = staminaBar.localScale;

            staminaRenderer = staminaBar.GetComponent<Renderer>();

            if (staminaRenderer != null)
            {
                staminaMaterial = staminaRenderer.material;
            }
        }


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



    void MovePlayer()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");


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
            sprinting ? sprintSpeed : walkSpeed;


        controller.Move(
            move *
            currentSpeed *
            Time.deltaTime
        );



        if (sprinting)
        {
            stamina -= staminaDrain * Time.deltaTime;

            staminaUsed = true;

            regenTimer = 0;


            if (stamina < 0)
                stamina = 0;
        }
    }



    void ApplyGravity()
    {
        if (controller.isGrounded)
        {
            velocity.y = -2f;
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }


        controller.Move(
            velocity *
            Time.deltaTime
        );
    }



    void UpdateStamina()
    {
        if (stamina < maxStamina)
        {
            regenTimer += Time.deltaTime;


            if (regenTimer >= regenDelay)
            {
                stamina += staminaRegen * Time.deltaTime;


                if (stamina > maxStamina)
                    stamina = maxStamina;
            }
        }



        if (staminaBar != null)
        {
            float percent = stamina / maxStamina;


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



    void FadeStamina()
    {
        if (staminaMaterial == null)
            return;


        bool visible =
            staminaUsed ||
            stamina < maxStamina;


        float targetAlpha =
            visible ? 1f : 0f;


        Color color = staminaMaterial.color;


        color.a = Mathf.Lerp(
            color.a,
            targetAlpha,
            Time.deltaTime * fadeSpeed
        );


        staminaMaterial.color = color;
    }



    void SetBarAlpha(float alpha)
    {
        if (staminaMaterial == null)
            return;


        Color color = staminaMaterial.color;
        color.a = alpha;
        staminaMaterial.color = color;
    }



    void HandleFootsteps()
    {
        bool moving =
            Input.GetAxisRaw("Horizontal") != 0 ||
            Input.GetAxisRaw("Vertical") != 0;


        if (!moving || !controller.isGrounded)
        {
            stepTimer = 0;
            return;
        }



        bool sprinting =
            Input.GetKey(sprintKey) &&
            Input.GetAxisRaw("Vertical") > 0 &&
            stamina > 0;



        float delay =
            sprinting ?
            sprintStepDelay :
            walkStepDelay;



        stepTimer -= Time.deltaTime;



        if (stepTimer <= 0)
        {
            PlayFootstep();
            stepTimer = delay;
        }
    }



    void PlayFootstep()
    {
        if (footstepSounds.Length == 0)
            return;


        int index =
            Random.Range(
                0,
                footstepSounds.Length
            );


        audioSource.PlayOneShot(
            footstepSounds[index]
        );
    }



    public float StaminaPercent
    {
        get
        {
            return stamina / maxStamina;
        }
    }



    public bool IsSprinting
    {
        get
        {
            return Input.GetKey(sprintKey)
            && stamina > 0f;
        }
    }

}