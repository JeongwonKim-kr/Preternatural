using System.Collections;
using UnityEngine;

public class CabinetHide : MonoBehaviour, IInteractable
{
    [Header("Player")]
    public Transform player;
    public Camera playerCamera;
    public PlayerMovement playerMovement;
    public FirstPersonCamera firstPersonCamera;

    [Header("Hide Positions")]
    public Transform hidePosition;
    public Transform exitPosition;

    [Header("Doors")]
    public Transform leftDoor;
    public Transform rightDoor;

    [Header("Door")]
    public float doorOpenAngle = 90f;
    public float doorSpeed = 2.5f;

    [Header("Camera")]
    public float cameraMoveSpeed = 2f;
    public float cameraRotateSpeed = 5f;

    [Header("Interaction")]
    public float interactDistance = 3f;
    public KeyCode interactKey = KeyCode.E;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip doorOpenSound;
    public AudioClip doorCloseSound;

    private bool isHidden;
    private bool isBusy;

    private Vector3 playerStartPosition;
    private Quaternion playerStartRotation;

    private Quaternion leftClosedRotation;
    private Quaternion rightClosedRotation;

    private Quaternion leftOpenRotation;
    private Quaternion rightOpenRotation;
    [Header("Animation")]
public float crouchAmount = 0.45f;
public float breathingSpeed = 1.2f;
public float breathingAmount = 0.015f;
public float shakeAmount = 0.004f;
public float shakeSpeed = 14f;
private Vector3 cameraStartLocalPos;
private bool breathing;
public bool IsHidden => isHidden;
public static bool AnyCabinetHidden;

    void Start()
    {
        leftClosedRotation = leftDoor.localRotation;
        rightClosedRotation = rightDoor.localRotation;

        leftOpenRotation =
            leftClosedRotation *
            Quaternion.Euler(0f, doorOpenAngle, 0f);

        rightOpenRotation =
            rightClosedRotation *
            Quaternion.Euler(0f, -doorOpenAngle, 0f);
        cameraStartLocalPos = playerCamera.transform.localPosition;
    }

void Update()
{
    if (isBusy)
        return;

    if (!Input.GetKeyDown(interactKey))
        return;

    // Exit immediately if already hiding
    if (isHidden)
    {
        StartCoroutine(ExitCabinet());
        return;
    }

    // Only raycast when trying to enter
    Ray ray = playerCamera.ViewportPointToRay(
        new Vector3(0.5f, 0.5f, 0f));

    RaycastHit hit;

    if (!Physics.Raycast(
        ray,
        out hit,
        interactDistance))
        return;

    CabinetHide cabinet =
        hit.collider.GetComponentInParent<CabinetHide>();

    if (cabinet != this)
        return;

    StartCoroutine(EnterCabinet());
}

    IEnumerator OpenDoors()
{
    float startPitch = firstPersonCamera != null
    ? playerCamera.transform.localEulerAngles.x
    : 0f;

if (startPitch > 180f)
    startPitch -= 360f;
    if (audioSource != null && doorOpenSound != null)
        audioSource.PlayOneShot(doorOpenSound);

    float timer = 0f;

    while (timer < 1f)
    {
        timer += Time.deltaTime * doorSpeed;

        // Heavy ease out
        float t = 1f - Mathf.Pow(1f - timer, 3f);
        if (firstPersonCamera != null)
{
    float pitch = Mathf.Lerp(
        startPitch,
        0f,
        t);

    firstPersonCamera.SetXRotation(pitch);
}

        leftDoor.localRotation =
            Quaternion.Slerp(
                leftClosedRotation,
                leftOpenRotation,
                t);

        rightDoor.localRotation =
            Quaternion.Slerp(
                rightClosedRotation,
                rightOpenRotation,
                t);

        yield return null;
    }
}

    IEnumerator CloseDoors()
    {
        if (audioSource != null && doorCloseSound != null)
            audioSource.PlayOneShot(doorCloseSound);

        float timer = 0f;

        while (timer < 1f)
        {
            timer += Time.deltaTime * doorSpeed;

            leftDoor.localRotation =
                Quaternion.Slerp(
                    leftOpenRotation,
                    leftClosedRotation,
                    timer);

            rightDoor.localRotation =
                Quaternion.Slerp(
                    rightOpenRotation,
                    rightClosedRotation,
                    timer);

            yield return null;
        }

        leftDoor.localRotation = leftClosedRotation;
        rightDoor.localRotation = rightClosedRotation;
    }
IEnumerator MovePlayer(Vector3 targetPosition, Quaternion targetRotation)
{
    Vector3 startPos = player.position;
    Quaternion startRot = player.rotation;
    float startPitch = firstPersonCamera != null
    ? playerCamera.transform.localEulerAngles.x
    : 0f;

if (startPitch > 180f)
    startPitch -= 360f;

    Vector3 camStart = playerCamera.transform.localPosition;

    float timer = 0f;
    float duration = .9f;

    while (timer < duration)
    {
        
        timer += Time.deltaTime;

        float t = timer / duration;

        t = 1f - Mathf.Pow(1f - t, 4f);

        player.position =
            Vector3.Lerp(
                startPos,
                targetPosition,
                t);

        player.rotation =
            Quaternion.Slerp(
                startRot,
                targetRotation,
                t);
                // Smoothly look forward
if (firstPersonCamera != null)
{
    float pitch = Mathf.Lerp(
        startPitch,
        0f,
        t);

    firstPersonCamera.SetXRotation(pitch);
}

        Vector3 cam = cameraStartLocalPos;

        // Crouch
        cam.y -= Mathf.Sin(t * Mathf.PI) * .18f;

        // Walking bob
        cam.y += Mathf.Sin(timer * 18f) * .012f;

        // Shoulder sway
        cam.x += Mathf.Sin(timer * 9f) * .01f;

        // Tiny forward dip
        cam.z += Mathf.Sin(t * Mathf.PI) * .03f;

        playerCamera.transform.localPosition =
            Vector3.Lerp(
                playerCamera.transform.localPosition,
                cam,
                Time.deltaTime * 10f);

        yield return null;
    }

    player.position = targetPosition;
    player.rotation = targetRotation;
}
IEnumerator EnterCabinet()
{
    AnyCabinetHidden = true;
    isBusy = true;

    // Save where the player was standing
    playerStartPosition = player.position;
    playerStartRotation = player.rotation;

    // Open cabinet
    yield return StartCoroutine(OpenDoors());

    // Disable movement
    if (playerMovement != null)
        playerMovement.enabled = false;

    // Disable mouse look and flashlight
    if (firstPersonCamera != null)
    {
        if (firstPersonCamera.flashlightController != null)
            firstPersonCamera.flashlightController.enabled = false;

        if (firstPersonCamera.flashlightObject != null)
            firstPersonCamera.flashlightObject.SetActive(false);

        firstPersonCamera.enabled = false;
    }

    // Smoothly move into the cabinet
    yield return StartCoroutine(
        MovePlayer(
            hidePosition.position,
            hidePosition.rotation
        )
    );

    yield return new WaitForSeconds(0.15f);

    // Close cabinet
    yield return StartCoroutine(CloseDoors());

    breathing = true;

isHidden = true;
isBusy = false;
}

    IEnumerator ExitCabinet()
{
    isBusy = true;
     

    // Open cabinet doors
    yield return StartCoroutine(OpenDoors());

    // Smoothly move player back outside
    yield return StartCoroutine(
MovePlayer(
    exitPosition.position,
    exitPosition.rotation
)
    );
 AnyCabinetHidden = false;
    // Enable movement again
    if (playerMovement != null)
        playerMovement.enabled = true;
    // Enable mouse look and flashlight
    if (firstPersonCamera != null)
    {
        firstPersonCamera.enabled = true;

        if (firstPersonCamera.flashlightObject != null)
            firstPersonCamera.flashlightObject.SetActive(true);

        if (firstPersonCamera.flashlightController != null)
            firstPersonCamera.flashlightController.enabled = true;
    }

    yield return new WaitForSeconds(0.15f);

    // Close cabinet doors
    yield return StartCoroutine(CloseDoors());

    isHidden = false;
    isBusy = false;
   
   
}
void LateUpdate()
{
    if (!breathing)
        return;

    float breathe =
        Mathf.Sin(Time.time * 1.2f);

    Vector3 pos = cameraStartLocalPos;

    // Slow inhale / exhale
    pos.y += breathe * .015f;

    // Slight body sway
    pos.x += Mathf.Sin(Time.time * .8f) * .005f;

    // Tiny random nervous shake
    pos += Random.insideUnitSphere * .0015f;

    playerCamera.transform.localPosition =
        Vector3.Lerp(
            playerCamera.transform.localPosition,
            pos,
            Time.deltaTime * 6f);
}
}