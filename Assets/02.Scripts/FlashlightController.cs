using UnityEngine;

public class FlashlightController : MonoBehaviour
{
    [Header("References")]
    public Transform flashlightHandle;
    public Light flashlightLight;
    public CharacterController controller;
    public PlayerMovement playerMovement;


    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip flashlightClickSound;


    [Header("Controls")]
    public KeyCode toggleKey = KeyCode.F;
    public KeyCode sprintKey = KeyCode.LeftShift;



    [Header("Idle Rotation")]
    public Vector3 flashlightRotation = new Vector3(-90f, -90f, -90f);



    [Header("Walk Shake")]
    public float walkShakeSpeed = 5f;
    public float walkPositionAmount = 0.006f;
    public float walkRotationAmount = 0.8f;



    [Header("Sprint Shake")]
    public float sprintShakeSpeed = 7.5f;
    public float sprintPositionAmount = 0.025f;
    public float sprintRotationAmount = 3.5f;



    [Header("Mouse Sway")]
    public float mouseSwayAmount = 20f;
    public float mouseSwaySmooth = 8f;



    [Header("Smoothing")]
    public float smoothSpeed = 6f;



    private bool flashlightOn = true;


    private Vector3 defaultLocalPosition;
    private Quaternion defaultLocalRotation;


    private Vector3 currentPos;
    private Quaternion currentRot;


    private float shakeTimer;

    private Quaternion swayRotation = Quaternion.identity;



    void Start()
    {
        defaultLocalPosition = flashlightHandle.localPosition;

        defaultLocalRotation =
            Quaternion.Euler(flashlightRotation);


        currentPos = defaultLocalPosition;
        currentRot = defaultLocalRotation;


        flashlightHandle.localPosition = currentPos;
        flashlightHandle.localRotation = currentRot;


        flashlightLight.enabled = flashlightOn;
    }



    void Update()
    {
        HandleInput();
        HandleShake();
        HandleMouseSway();
    }



    void HandleInput()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            flashlightOn = !flashlightOn;

            flashlightLight.enabled = flashlightOn;


            if (audioSource != null &&
                flashlightClickSound != null)
            {
                audioSource.PlayOneShot(
                    flashlightClickSound
                );
            }
        }
    }



    void HandleMouseSway()
    {
        float mouseX =
            Input.GetAxis("Mouse X");


        float mouseY =
            Input.GetAxis("Mouse Y");



        float swayX =
            -mouseY *
            mouseSwayAmount;


        float swayY =
            mouseX *
            mouseSwayAmount;



        float swayZ =
            -mouseX *
            mouseSwayAmount *
            0.5f;



        Quaternion targetSway =
            Quaternion.Euler(
                swayX,
                swayY,
                swayZ
            );



        swayRotation =
            Quaternion.Slerp(
                swayRotation,
                targetSway,
                Time.deltaTime *
                mouseSwaySmooth
            );
    }



    void HandleShake()
    {
        float horizontal =
            Input.GetAxisRaw("Horizontal");


        float vertical =
            Input.GetAxisRaw("Vertical");



        bool moving =
            horizontal != 0 ||
            vertical != 0;



        bool sprinting =
            moving &&
            Input.GetKey(sprintKey) &&
            vertical > 0 &&
            playerMovement != null &&
            playerMovement.StaminaPercent > 0.01f;



        float speed;
        float posAmount;
        float rotAmount;



        if (sprinting)
        {
            speed = sprintShakeSpeed;
            posAmount = sprintPositionAmount;
            rotAmount = sprintRotationAmount;
        }
        else
        {
            speed = walkShakeSpeed;
            posAmount = walkPositionAmount;
            rotAmount = walkRotationAmount;
        }



        Vector3 targetPos =
            defaultLocalPosition;


        Quaternion targetRot =
            defaultLocalRotation;



        if (moving)
        {
            shakeTimer +=
                Time.deltaTime *
                speed;



            // Smooth left and right hand movement
            targetPos.x +=
                Mathf.Sin(shakeTimer) *
                posAmount;



            // Small up and down movement
            targetPos.y +=
                Mathf.Abs(
                    Mathf.Sin(shakeTimer * 0.5f)
                ) *
                posAmount *
                0.6f;



            // Slight forward movement
            targetPos.z +=
                Mathf.Cos(shakeTimer * 0.5f) *
                posAmount *
                0.5f;



            float pitch =
                Mathf.Sin(shakeTimer * 0.5f) *
                rotAmount;



            float yaw =
                Mathf.Cos(shakeTimer) *
                rotAmount *
                0.3f;



            float roll =
                Mathf.Sin(shakeTimer) *
                rotAmount *
                0.5f;



            targetRot =
                defaultLocalRotation *
                Quaternion.Euler(
                    pitch,
                    yaw,
                    roll
                );
        }
        else
        {
            shakeTimer = 0f;
        }



        // Add mouse sway on top of movement shake
        targetRot *= swayRotation;



        currentPos =
            Vector3.Lerp(
                currentPos,
                targetPos,
                Time.deltaTime *
                smoothSpeed
            );



        currentRot =
            Quaternion.Slerp(
                currentRot,
                targetRot,
                Time.deltaTime *
                smoothSpeed
            );



        flashlightHandle.localPosition =
            currentPos;


        flashlightHandle.localRotation =
            currentRot;
    }
}