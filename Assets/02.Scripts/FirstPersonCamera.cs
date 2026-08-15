using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FirstPersonCamera : MonoBehaviour
{
    [Header("Mouse Look")]
    public Transform playerBody;
    public float mouseSensitivity = 700f;

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


    [Header("Sprint")]
    public KeyCode sprintKey = KeyCode.LeftShift;


    [Header("Field of View")]
    public float normalFOV = 55f;
    public float sprintFOV = 55f;
    public float fovSmooth = 8f;


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
    public GameObject flashlightObject;
    public FlashlightController flashlightController;
    public AudioSource equipAudioSource;
    public AudioClip equipSound;
    public float flashlightDelay = 0.35f;


    private Camera cam;
    private float xRotation;
    private float bobTimer;
    private Vector3 defaultLocalPosition;
    private bool introFinished;
    // enabled는 인트로/은신/사망 쪽 권위 상태다. 메뉴는 별도 입력 억제 플래그만 소유한다.
    private bool menuInputSuppressed;

public void SetMenuInputSuppressed(bool suppressed)
{
    menuInputSuppressed = suppressed;
}

public void SetXRotation(float rotation)
{
    xRotation = rotation;
}

    /// 씬 Canvas의 인트로 페이드 오버레이 이름. NetPlayer 프리팹은 씬 오브젝트를 직렬화할 수
    /// 없어 blackScreen 참조가 끊기므로, 런타임에 이 이름으로 씬에서 다시 찾는다.
    /// 찾지 못하면 페이드 없이 진행한다(오버레이가 없는 씬).
    const string IntroOverlayName = "IntroFadeIn";

    void Start()
    {
        if (blackScreen == null)
        {
            blackScreen = FindIntroOverlay();
            // 멀티에서는 NetPlayer 스폰이 씬 활성화보다 빠를 수 있어 이 시점엔 아직 못 찾는다.
            // 그 경우 오버레이가 나타날 때까지 기다렸다가 직접 걷어낸다(안 그러면 단색 화면이 남는다).
            if (blackScreen == null) StartCoroutine(ClearIntroOverlayWhenAvailable());
        }

        if (!menuInputSuppressed)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }


        defaultLocalPosition = transform.localPosition;


        cam = GetComponent<Camera>();

        if (cam != null)
            cam.fieldOfView = normalFOV;

        if (blackScreen != null)
        {
            Color c = blackScreen.color;
            c.a = 1f;
            blackScreen.color = c;
        }

        if (flashlightObject != null)
            flashlightObject.SetActive(false);

        if (flashlightController != null)
            flashlightController.enabled = false;

        StartCoroutine(WakeUpIntroRoutine());
    }

    static RawImage FindIntroOverlay()
    {
        foreach (var overlay in FindObjectsByType<RawImage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (overlay.name == IntroOverlayName) return overlay;
        return null;
    }

    /// 오버레이가 씬에 나타날 때까지 기다렸다가 페이드로 걷어낸다.
    /// WakeUpIntroRoutine의 페이드 구간(timer <= fadeDuration)은 이미 지났을 수 있으므로
    /// 여기서 독립적으로 알파를 낮춘다 — 그쪽은 blackScreen이 null인 동안 아무것도 하지 않는다.
    IEnumerator ClearIntroOverlayWhenAvailable()
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while (blackScreen == null && Time.realtimeSinceStartup < deadline)
        {
            blackScreen = FindIntroOverlay();
            if (blackScreen == null) yield return null;
        }
        if (blackScreen == null)
        {
            Debug.LogWarning("[FirstPersonCamera] 인트로 오버레이를 찾지 못함 — 화면이 가려져 있으면 이 경로를 확인할 것");
            yield break;
        }

        float elapsed = 0f;
        Color color = blackScreen.color;
        float from = color.a;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(from, 0f, elapsed / fadeDuration);
            blackScreen.color = color;
            yield return null;
        }
        color.a = 0f;
        blackScreen.color = color;
    }



    void Update()
    {
        if (menuInputSuppressed)
            return;

        if (!introFinished)
            return;

        MouseLook();
        HeadBob();
        UpdateFOV();
    }



    IEnumerator WakeUpIntroRoutine()
    {
        if (playerMovement != null)
            playerMovement.enabled = false;

        float pitch = startPitch;
        float timer = 0f;

        while (timer < wakeDuration)
        {
            timer += Time.deltaTime;

            float t = Mathf.Clamp01(timer / wakeDuration);
            float smooth = Mathf.SmoothStep(0f, 1f, t);

            float pitchTarget = startPitch;
            float yawTarget = 0f;

            if (timer <= lieDownDuration)
            {
                pitchTarget = startPitch;
                yawTarget = 0f;
            }
            else if (timer <= lieDownDuration + riseDuration)
            {
                float riseT = Mathf.Clamp01((timer - lieDownDuration) / riseDuration);
                pitchTarget = Mathf.Lerp(startPitch, 8f, Mathf.SmoothStep(0f, 1f, riseT));
                yawTarget = 0f;
            }
            else
            {
                float frontT = Mathf.Clamp01((timer - lieDownDuration - riseDuration) / lookWallDuration);
                pitchTarget = Mathf.Lerp(8f, endPitch, Mathf.SmoothStep(0f, 1f, frontT));
                yawTarget = 0f;
            }

            pitch = Mathf.Lerp(pitch, pitchTarget, Time.deltaTime * 2.5f);

            float baseSway =
                Mathf.Sin(Time.time * swaySpeed) *
                swayAmount *
                (1f - smooth);

            float shake =
                Mathf.Sin(Time.time * shakeFrequency) *
                shakeAmount *
                (1f - smooth);

            float jitter =
                Mathf.Sin(Time.time * (shakeFrequency * 1.7f) + 0.5f) *
                jitterAmount *
                (1f - smooth);

            float finalPitch = pitch + shake * 0.6f;
            float finalRoll = baseSway + jitter * 2f;
            float finalYaw = yawTarget + shake * 1.5f;

            transform.localRotation =
                Quaternion.Euler(
                    finalPitch,
                    finalYaw,
                    finalRoll
                );

            if (blackScreen != null && timer <= fadeDuration)
            {
                Color color = blackScreen.color;
                color.a = Mathf.Lerp(1f, 0f, timer / fadeDuration);
                blackScreen.color = color;
            }

            yield return null;
        }

        float settleTimer = 0f;
        while (settleTimer < 0.8f)
        {
            settleTimer += Time.deltaTime;
            float settleT = Mathf.Clamp01(settleTimer / 0.8f);
            float eased = Mathf.SmoothStep(0f, 1f, settleT);

            Quaternion targetRotation = Quaternion.Euler(endPitch, 0f, 0f);
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                targetRotation,
                eased
            );

            yield return null;
        }

        transform.localRotation = Quaternion.Euler(endPitch, 0f, 0f);
        transform.localPosition = defaultLocalPosition;

        yield return new WaitForSeconds(delayBeforeControl);

        if (playerMovement != null)
            playerMovement.enabled = true;

        if (equipAudioSource != null && equipSound != null)
        {
            equipAudioSource.PlayOneShot(equipSound);
        }

        if (flashlightObject != null)
        {
            flashlightObject.SetActive(true);
        }

        if (flashlightController != null)
        {
            flashlightController.enabled = true;
            flashlightController.gameObject.SetActive(true);
        }

        if (blackScreen != null)
            blackScreen.gameObject.SetActive(false);

        introFinished = true;
    }



    void MouseLook()
    {
        float mouseX =
            Input.GetAxis("Mouse X") *
            mouseSensitivity *
            Time.deltaTime;


        float mouseY =
            Input.GetAxis("Mouse Y") *
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



    void HeadBob()
    {
        float horizontal =
            Input.GetAxisRaw("Horizontal");


        float vertical =
            Input.GetAxisRaw("Vertical");



        bool moving =
            controller.isGrounded &&
            (horizontal != 0 || vertical != 0);



        bool sprinting =
            moving &&
            Input.GetKey(sprintKey) &&
            playerMovement != null &&
            playerMovement.StaminaPercent > 0f;



        float bobSpeed =
            sprinting ?
            sprintBobSpeed :
            walkBobSpeed;


        float bobAmount =
            sprinting ?
            sprintBobAmount :
            walkBobAmount;



        if (moving)
        {
            bobTimer +=
                Time.deltaTime *
                bobSpeed;
        }



        float sin =
            Mathf.Sin(bobTimer);


        float cos =
            Mathf.Cos(
                bobTimer * 0.5f
            );



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
                Mathf.Cos(bobTimer) *
                bobAmount *
                0.25f;
        }



        transform.localPosition =
            Vector3.Lerp(
                transform.localPosition,
                targetPos,
                Time.deltaTime *
                positionSmooth
            );



        float roll =
            moving ?
            cos * walkRoll :
            0f;


        roll +=
            -horizontal *
            strafeTilt;



        float pitch =
            moving ?
            sin *
            stepPitch :
            0f;



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



    void UpdateFOV()
    {
        if (cam == null)
            return;



        bool hasStamina =
            playerMovement == null ||
            playerMovement.StaminaPercent > 0f;



        bool sprinting =
            Input.GetKey(sprintKey) &&
            Input.GetAxisRaw("Vertical") > 0 &&
            controller.isGrounded &&
            hasStamina;



        float targetFOV =
            sprinting ?
            sprintFOV :
            normalFOV;



        cam.fieldOfView =
            Mathf.Lerp(
                cam.fieldOfView,
                targetFOV,
                Time.deltaTime *
                fovSmooth
            );
    }
}
