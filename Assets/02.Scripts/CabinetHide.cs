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

    // 최종 리뷰 Important 3: static은 도메인 리로드에도 살아남지 않지만 에디터에서 Play 세션을 걸쳐
    // 값이 새지 않도록(숨은 채 종료 → 다음 Play에서 몬스터 무력화) 재생 시작마다 명시적으로 리셋한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { AnyCabinetHidden = false; }

    // 리뷰 반영: 씬에 정적으로 배선된 playerCamera는 멀티에서 비활성화된 씬 Player의 카메라를 가리킨다.
    // 로컬 NetPlayer가 있으면 그쪽 HeadCamera를 우선 사용하고, 없으면(오프라인) 기존 필드로 폴백.
    Camera ResolveCamera()
    {
        var local = Game.Net.NetPlayer.Local;
        if (local != null && local.HeadCamera != null) return local.HeadCamera;
        return playerCamera; // 오프라인/로비 기존 경로
    }

    // 최종 리뷰 재지적(2차): 레이캐스트만 고쳐서 도달 가능해진 몸체 참조 — player/playerMovement/
    // firstPersonCamera는 여전히 비활성 씬 Player를 가리켜, 멀티에서 숨으면 로컬 시점은 그대로인데
    // isHidden/AnyCabinetHidden만 true가 돼(크로스헤어·도구 잠김) NetPlayer.IsHiding은 계속 false라
    // 몬스터에게 노출된 채로 남는다. ResolveCamera와 동형으로 런타임 해석.
    Transform ResolvePlayer()
    {
        var local = Game.Net.NetPlayer.Local;
        return local != null ? local.transform : player; // 오프라인/로비 기존 경로
    }

    PlayerMovement ResolvePlayerMovement()
    {
        var local = Game.Net.NetPlayer.Local;
        if (local != null)
        {
            var m = local.GetComponentInChildren<PlayerMovement>(true);
            if (m != null) return m;
        }
        return playerMovement; // 오프라인/로비 기존 경로
    }

    FirstPersonCamera ResolveFirstPersonCamera()
    {
        var local = Game.Net.NetPlayer.Local;
        if (local != null)
        {
            var c = local.GetComponentInChildren<FirstPersonCamera>(true);
            if (c != null) return c;
        }
        return firstPersonCamera; // 오프라인/로비 기존 경로
    }

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
    var cam = ResolveCamera();
    if (cam == null)
        return;

    Ray ray = cam.ViewportPointToRay(
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
    var fpc = ResolveFirstPersonCamera();
    float startPitch = fpc != null
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
        if (fpc != null)
{
    float pitch = Mathf.Lerp(
        startPitch,
        0f,
        t);

    fpc.SetXRotation(pitch);
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
    var target = ResolvePlayer();
    var fpc = ResolveFirstPersonCamera();

    Vector3 startPos = target.position;
    Quaternion startRot = target.rotation;
    float startPitch = fpc != null
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

        target.position =
            Vector3.Lerp(
                startPos,
                targetPosition,
                t);

        target.rotation =
            Quaternion.Slerp(
                startRot,
                targetRotation,
                t);
                // Smoothly look forward
if (fpc != null)
{
    float pitch = Mathf.Lerp(
        startPitch,
        0f,
        t);

    fpc.SetXRotation(pitch);
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

    target.position = targetPosition;
    target.rotation = targetRotation;
}
IEnumerator EnterCabinet()
{
    AnyCabinetHidden = true;
    isBusy = true;

    var target = ResolvePlayer();
    var movement = ResolvePlayerMovement();
    var fpc = ResolveFirstPersonCamera();

    // 최종 리뷰 재지적(2차): PlayerMovement.isHiding은 NetPlayer.IsHiding NetworkVariable의 유일한
    // 소스인데(NetPlayer.cs Update() 참고) 원래 아무도 이 필드를 설정하지 않았다 — 오프라인에서도
    // 잠재적으로 있던 갭이지만, 멀티에서는 몬스터의 은신 판정(MonsterAI.IsTargetHiding)이 이 값에
    // 직접 의존하므로 여기서 명시적으로 켠다.
    if (movement != null) movement.isHiding = true;

    // Save where the player was standing
    playerStartPosition = target.position;
    playerStartRotation = target.rotation;

    // Open cabinet
    yield return StartCoroutine(OpenDoors());

    // Disable movement
    if (movement != null)
        movement.enabled = false;

    // Disable mouse look and flashlight
    if (fpc != null)
    {
        if (fpc.flashlightController != null)
            fpc.flashlightController.enabled = false;

        if (fpc.flashlightObject != null)
            fpc.flashlightObject.SetActive(false);

        fpc.enabled = false;
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

    var movement = ResolvePlayerMovement();
    var fpc = ResolveFirstPersonCamera();

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
    if (movement != null) movement.isHiding = false; // NetPlayer.IsHiding 동기화(최종 리뷰 재지적 2차)
    // Enable movement again
    if (movement != null)
        movement.enabled = true;
    // Enable mouse look and flashlight
    if (fpc != null)
    {
        fpc.enabled = true;

        if (fpc.flashlightObject != null)
            fpc.flashlightObject.SetActive(true);

        if (fpc.flashlightController != null)
            fpc.flashlightController.enabled = true;
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