using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 3f;
    public float sprintSpeed = 5f;

    [Header("Mouse Sensitivity")]
    public float mouseSensitivity = 700f;

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

    [Header("isHiding")]
    public bool isHiding = false;

    private CharacterController controller;
    private Vector3 velocity;

    private float stamina;
    private float regenTimer;

    private float distanceSinceFootstep;
    private Vector3 lastFootstepPosition;

    private Vector3 originalBarScale;

    private Renderer staminaRenderer;
    private Material staminaMaterial;

    private bool staminaUsed;

    // Sprint becomes unavailable at or below this value.
    private const float MIN_STAMINA = 0.001f;

    private void Start()
    {
        controller = GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        stamina = maxStamina;

        if (staminaBar != null)
        {
            originalBarScale = staminaBar.localScale;

            staminaRenderer =
                staminaBar.GetComponent<Renderer>();

            if (staminaRenderer != null)
            {
                staminaMaterial =
                    staminaRenderer.material;
            }
        }

        lastFootstepPosition =
            transform.position;

        distanceSinceFootstep = 0f;

        SetBarAlpha(0f);
    }

    private void Update()
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

    private void MovePlayer()
    {
        if (Keyboard.current == null)
            return;

        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current.aKey.isPressed)
            horizontal -= 1f;

        if (Keyboard.current.dKey.isPressed)
            horizontal += 1f;

        if (Keyboard.current.sKey.isPressed)
            vertical -= 1f;

        if (Keyboard.current.wKey.isPressed)
            vertical += 1f;

        Vector3 move =
            transform.right * horizontal +
            transform.forward * vertical;

        if (move.sqrMagnitude > 1f)
            move.Normalize();

        // LEFT SHIFT = SPRINT
        bool wantsSprint =
            Keyboard.current.leftShiftKey.isPressed &&
            vertical > 0f;

        bool sprinting =
            wantsSprint &&
            stamina > MIN_STAMINA;

        float currentSpeed =
            sprinting
                ? sprintSpeed
                : walkSpeed;

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

            regenTimer = 0f;

            if (stamina <= MIN_STAMINA)
            {
                stamina = MIN_STAMINA;
            }
        }
    }

    // =========================================================
    // GRAVITY
    // =========================================================

    private void ApplyGravity()
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

    private void UpdateStamina()
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
                stamina / maxStamina;

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

    private void FadeStamina()
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

    private void SetBarAlpha(float alpha)
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

    private void HandleFootsteps()
    {
        if (Keyboard.current == null)
            return;

        bool moving =
            Keyboard.current.wKey.isPressed ||
            Keyboard.current.aKey.isPressed ||
            Keyboard.current.sKey.isPressed ||
            Keyboard.current.dKey.isPressed;

        if (!moving ||
            !controller.isGrounded)
        {
            lastFootstepPosition =
                transform.position;

            return;
        }

        float movedDistance =
            Vector3.Distance(
                transform.position,
                lastFootstepPosition
            );

        distanceSinceFootstep +=
            movedDistance;

        lastFootstepPosition =
            transform.position;

        bool sprinting =
            Keyboard.current.leftShiftKey.isPressed &&
            Keyboard.current.wKey.isPressed &&
            stamina > MIN_STAMINA;

        float requiredDistance =
            sprinting
                ? sprintStepDistance
                : walkStepDistance;

        if (distanceSinceFootstep >=
            requiredDistance)
        {
            PlayFootstep();

            distanceSinceFootstep = 0f;
        }
    }

    // =========================================================
    // PLAY FOOTSTEP
    // =========================================================

    private void PlayFootstep()
    {
        if (audioSource == null)
            return;

        RaycastHit hit;

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
                            1.7f
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
            if (Keyboard.current == null)
                return false;

            return
                Keyboard.current.leftShiftKey.isPressed &&
                Keyboard.current.wKey.isPressed &&
                stamina > MIN_STAMINA;
        }
    }
}