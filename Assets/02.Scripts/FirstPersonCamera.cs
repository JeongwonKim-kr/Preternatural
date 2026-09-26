
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class FirstPersonCamera : MonoBehaviour
{
    [Header("Mouse Look")]
    public Transform playerBody;
    public float mouseSensitivity = 40f;

    [Header("References")]
    public CharacterController controller;
    public PlayerMovement playerMovement;
    public RawImage blackScreen;

    [Header("Walking Bob")]
    public float walkBobSpeed = 10f;
    public float walkBobAmount = 0.045f;

    [Header("Sprint Bob")]
    public float sprintBobSpeed = 16f;
    public float sprintBobAmount = 0.085f;

    [Header("Camera Movement")]
    public float positionSmooth = 8f;
    public float rotationSmooth = 8f;

    [Header("Camera Roll")]
    public float walkRoll = 2.5f;
    public float strafeTilt = 3f;
    public float stepPitch = 1f;

    [Header("Field of View")]
    public float normalFOV = 55f;
    public float sprintFOV = 55f;
    public float fovSmooth = 8f;

    [Header("Breathing")]
    public float breathingSpeed = 1.5f;
    public float breathingAmount = 0.008f;

    [Header("Wake Up Intro")]
    public float fadeDuration = 2f;
    public float wakeDuration = 4f;
    public float lieDownDuration = 2.1f;
    public float riseDuration = 1.2f;
    public float lookWallDuration = 0.9f;
    public float startPitch = 88f;
    public float endPitch = 0f;
    public float swayAmount = 1.2f;
    public float swaySpeed = 1.8f;
    public float shakeAmount = 0.08f;
    public float shakeFrequency = 18f;
    public float jitterAmount = 0.03f;
    public float delayBeforeControl = 0.5f;

    [Header("Flashlight")]
    public GameObject flashlightObject;
    public FlashlightController flashlightController;
    public AudioSource equipAudioSource;
    public AudioClip equipSound;
    public float flashlightDelay = 0.35f;

    private Camera cam;

    private float xRotation;
    private float bobTimer;

    private Vector3 defaultLocalPosition;
    private Quaternion defaultLocalRotation;

    private bool introFinished = false;
    private bool introStarted = false;

    private const float MIN_STAMINA = 0.001f;

    // =========================================================
    // SET X ROTATION
    // Used by CabinetHide.cs
    // =========================================================

    public void SetXRotation(float rotation)
    {
        xRotation = rotation;
    }

    // =========================================================
    // SET GAMEPLAY CAMERA POSITION
    // =========================================================

    public void SetGameplayCameraPosition(Transform target)
    {
        if (target == null)
            return;

        transform.position = target.position;
        transform.rotation = target.rotation;

        defaultLocalPosition = transform.localPosition;
        defaultLocalRotation = transform.localRotation;

        bobTimer = 0f;

        Vector3 angles = transform.localEulerAngles;

        xRotation = NormalizeAngle(angles.x);
    }

    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        cam = GetComponent<Camera>();

        if (cam != null)
            cam.fieldOfView = normalFOV;

        defaultLocalPosition = transform.localPosition;
        defaultLocalRotation = transform.localRotation;

        introFinished = false;
        introStarted = false;

        // -----------------------------------------------------
        // FLASHLIGHT START STATE
        // -----------------------------------------------------

        if (flashlightObject != null)
            flashlightObject.SetActive(false);

        if (flashlightController != null)
            flashlightController.enabled = false;

        // -----------------------------------------------------
        // CHECK CURRENT SCENE
        // -----------------------------------------------------

        string sceneName = SceneManager.GetActiveScene().name;

        // -----------------------------------------------------
        // GAME SCENE
        // Automatically start wake-up intro.
        // -----------------------------------------------------

        if (sceneName == "GameScene")
        {
            BeginWakeUpIntro();
        }

        // -----------------------------------------------------
        // HOMESCREEN
        // Do NOT start intro here.
        // HomeScreenController will start it after START.
        // -----------------------------------------------------

        else if (sceneName == "Homescreen")
        {
            // Nothing.
        }
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (!introFinished)
            return;

        MouseLook();
        HeadBob();
        UpdateFOV();
    }

    // =========================================================
    // BEGIN WAKE UP INTRO
    // =========================================================

    public void BeginWakeUpIntro()
    {
        if (introStarted)
            return;

        StopAllCoroutines();

        introStarted = true;
        introFinished = false;

        Time.timeScale = 1f;

        StartCoroutine(WakeUpIntroRoutine());
    }

    // =========================================================
    // WAKE UP INTRO
    // =========================================================

    private IEnumerator WakeUpIntroRoutine()
    {
        // -----------------------------------------------------
        // PLAYER CANNOT MOVE DURING INTRO
        // -----------------------------------------------------

        if (playerMovement != null)
            playerMovement.enabled = false;

        float pitch = startPitch;
        float timer = 0f;

        // -----------------------------------------------------
        // BLACK SCREEN
        // -----------------------------------------------------

        if (blackScreen != null)
        {
            blackScreen.gameObject.SetActive(true);

            Color color = blackScreen.color;
            color.a = 1f;
            blackScreen.color = color;
        }

        // =====================================================
        // INTRO
        // =====================================================

        while (timer < wakeDuration)
        {
            timer += Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    timer / wakeDuration
                );

            float smooth =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    t
                );

            float pitchTarget;
            float yawTarget = 0f;

            // -------------------------------------------------
            // LYING DOWN
            // -------------------------------------------------

            if (timer <= lieDownDuration)
            {
                pitchTarget = startPitch;
                yawTarget = 0f;
            }

            // -------------------------------------------------
            // RISING
            // -------------------------------------------------

            else if (
                timer <=
                lieDownDuration +
                riseDuration
            )
            {
                float riseT =
                    Mathf.Clamp01(
                        (timer - lieDownDuration) /
                        riseDuration
                    );

                pitchTarget =
                    Mathf.Lerp(
                        startPitch,
                        8f,
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            riseT
                        )
                    );

                yawTarget = 0f;
            }

            // -------------------------------------------------
            // LOOK FORWARD
            // -------------------------------------------------

            else
            {
                float frontT =
                    Mathf.Clamp01(
                        (
                            timer -
                            lieDownDuration -
                            riseDuration
                        ) /
                        lookWallDuration
                    );

                pitchTarget =
                    Mathf.Lerp(
                        8f,
                        endPitch,
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            frontT
                        )
                    );

                yawTarget = 0f;
            }

            // -------------------------------------------------
            // PITCH
            // -------------------------------------------------

            pitch =
                Mathf.Lerp(
                    pitch,
                    pitchTarget,
                    Time.deltaTime * 2.5f
                );

            // -------------------------------------------------
            // SWAY
            // -------------------------------------------------

            float baseSway =
                Mathf.Sin(
                    Time.time * swaySpeed
                ) *
                swayAmount *
                (1f - smooth);

            // -------------------------------------------------
            // SHAKE
            // -------------------------------------------------

            float shake =
                Mathf.Sin(
                    Time.time * shakeFrequency
                ) *
                shakeAmount *
                (1f - smooth);

            // -------------------------------------------------
            // JITTER
            // -------------------------------------------------

            float jitter =
                Mathf.Sin(
                    Time.time *
                    (shakeFrequency * 1.7f) +
                    0.5f
                ) *
                jitterAmount *
                (1f - smooth);

            float finalPitch =
                pitch +
                shake * 0.6f;

            float finalRoll =
                baseSway +
                jitter * 2f;

            float finalYaw =
                yawTarget +
                shake * 1.5f;

            transform.localRotation =
                Quaternion.Euler(
                    finalPitch,
                    finalYaw,
                    finalRoll
                );

            // -------------------------------------------------
            // FADE BLACK SCREEN
            // -------------------------------------------------

            if (
                blackScreen != null &&
                timer <= fadeDuration
            )
            {
                Color color =
                    blackScreen.color;

                color.a =
                    Mathf.Lerp(
                        1f,
                        0f,
                        timer / fadeDuration
                    );

                blackScreen.color = color;
            }

            yield return null;
        }

        // =====================================================
        // CLEAR BLACK SCREEN
        // =====================================================

        if (blackScreen != null)
        {
            Color color = blackScreen.color;
            color.a = 0f;
            blackScreen.color = color;
        }

        // =====================================================
        // SETTLE CAMERA
        // =====================================================

        float settleTimer = 0f;

        while (settleTimer < 0.8f)
        {
            settleTimer += Time.deltaTime;

            float settleT =
                Mathf.Clamp01(
                    settleTimer / 0.8f
                );

            float eased =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    settleT
                );

            Quaternion targetRotation =
                Quaternion.Euler(
                    endPitch,
                    0f,
                    0f
                );

            transform.localRotation =
                Quaternion.Slerp(
                    transform.localRotation,
                    targetRotation,
                    eased
                );

            yield return null;
        }

        // -----------------------------------------------------
        // FINAL CAMERA POSITION / ROTATION
        // -----------------------------------------------------

        transform.localRotation =
            Quaternion.Euler(
                endPitch,
                0f,
                0f
            );

        transform.localPosition =
            defaultLocalPosition;

        // =====================================================
        // WAIT
        // =====================================================

        yield return new WaitForSeconds(
            delayBeforeControl
        );

        // =====================================================
        // ENABLE PLAYER
        // =====================================================

        if (playerMovement != null)
            playerMovement.enabled = true;

        // =====================================================
        // FLASHLIGHT
        // =====================================================

        if (
            equipAudioSource != null &&
            equipSound != null
        )
        {
            equipAudioSource.PlayOneShot(
                equipSound
            );
        }

        if (flashlightObject != null)
            flashlightObject.SetActive(true);

        if (flashlightController != null)
        {
            flashlightController.enabled = true;

            flashlightController.gameObject.SetActive(
                true
            );
        }

        // =====================================================
        // FINISH
        // =====================================================

        if (blackScreen != null)
        {
            Color color =
                blackScreen.color;

            color.a = 0f;

            blackScreen.color =
                color;

            blackScreen.gameObject.SetActive(false);
        }

        introFinished = true;
        introStarted = false;
    }

    // =========================================================
    // MOUSE LOOK
    // =========================================================

    private void MouseLook()
    {
        if (Mouse.current == null)
            return;

        Vector2 mouseDelta =
            Mouse.current.delta.ReadValue();

        float mouseX =
            mouseDelta.x *
            mouseSensitivity *
            Time.deltaTime;

        float mouseY =
            mouseDelta.y *
            mouseSensitivity *
            Time.deltaTime;

        xRotation -= mouseY;

        xRotation =
            Mathf.Clamp(
                xRotation,
                -85f,
                85f
            );

        if (playerBody != null)
        {
            playerBody.Rotate(
                Vector3.up *
                mouseX
            );
        }
    }

    // =========================================================
    // HEAD BOB
    // =========================================================

    private void HeadBob()
    {
        if (controller == null)
            return;

        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed)
                horizontal -= 1f;

            if (Keyboard.current.dKey.isPressed)
                horizontal += 1f;

            if (Keyboard.current.sKey.isPressed)
                vertical -= 1f;

            if (Keyboard.current.wKey.isPressed)
                vertical += 1f;
        }

        bool moving =
            controller.isGrounded &&
            (
                horizontal != 0f ||
                vertical != 0f
            );

        bool sprinting =
            moving &&
            IsSprintPressed() &&
            vertical > 0f &&
            playerMovement != null &&
            playerMovement.StaminaPercent >
            MIN_STAMINA;

        float bobSpeed =
            sprinting
            ? sprintBobSpeed
            : walkBobSpeed;

        float bobAmount =
            sprinting
            ? sprintBobAmount
            : walkBobAmount;

        if (moving)
        {
            bobTimer +=
                Time.deltaTime *
                bobSpeed;
        }

        float sin =
            Mathf.Sin(
                bobTimer
            );

        float cos =
            Mathf.Cos(
                bobTimer * 0.5f
            );

        float breathing =
            Mathf.Sin(
                Time.time *
                breathingSpeed
            ) *
            breathingAmount;

        Vector3 targetPos =
            defaultLocalPosition;

        if (moving)
        {
            targetPos.y +=
                sin *
                bobAmount;

            targetPos.x +=
                cos *
                bobAmount *
                0.55f;

            targetPos.z +=
                Mathf.Cos(
                    bobTimer
                ) *
                bobAmount *
                0.25f;
        }

        targetPos.y += breathing;

        transform.localPosition =
            Vector3.Lerp(
                transform.localPosition,
                targetPos,
                Time.deltaTime *
                positionSmooth
            );

        float roll =
            moving
            ? cos * walkRoll
            : 0f;

        roll +=
            -horizontal *
            strafeTilt;

        float pitch =
            moving
            ? sin * stepPitch
            : 0f;

        Quaternion targetRotation =
            Quaternion.Euler(
                xRotation + pitch,
                0f,
                roll
            );

        transform.localRotation =
            Quaternion.Slerp(
                transform.localRotation,
                targetRotation,
                Time.deltaTime *
                rotationSmooth
            );
    }

    // =========================================================
    // FOV
    // =========================================================

    private void UpdateFOV()
    {
        if (
            cam == null ||
            controller == null
        )
            return;

        bool hasStamina =
            playerMovement == null ||
            playerMovement.StaminaPercent >
            MIN_STAMINA;

        float vertical = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed)
                vertical += 1f;

            if (Keyboard.current.sKey.isPressed)
                vertical -= 1f;
        }

        bool sprinting =
            IsSprintPressed() &&
            vertical > 0f &&
            controller.isGrounded &&
            hasStamina;

        float targetFOV =
            sprinting
            ? sprintFOV
            : normalFOV;

        cam.fieldOfView =
            Mathf.Lerp(
                cam.fieldOfView,
                targetFOV,
                Time.deltaTime *
                fovSmooth
            );
    }

    // =========================================================
    // SPRINT
    // =========================================================

    private bool IsSprintPressed()
    {
        if (Keyboard.current == null)
            return false;

        return Keyboard.current.leftShiftKey.isPressed;
    }

    // =========================================================
    // NORMALIZE ANGLE
    // =========================================================

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f)
            angle -= 360f;

        while (angle < -180f)
            angle += 360f;

        return angle;
    }
}