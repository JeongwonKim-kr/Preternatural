using UnityEngine;

public class DoorInteractable : MonoBehaviour, IInteractable
{
    [Header("Controller")]
    public LeverDoorController controller;

    [Header("Interaction")]
    public Camera playerCamera;
    public float interactDistance = 3f;
    public KeyCode interactKey = KeyCode.E;

    Game.Net.NetToggleSync _sync;

    void Awake()
    {
        _sync = GetComponent<Game.Net.NetToggleSync>();
        if (_sync != null) _sync.OnStateChanged += _ => controller.InteractWithDoor();
    }

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
    }

    // 최종 리뷰 Critical 2: Start()의 Camera.main 폴백은 멀티에서 임의의(원격일 수도 있는) MainCamera
    // 태그 카메라를 잡을 수 있어 동일 문제다. 로컬 NetPlayer가 있으면 그쪽 HeadCamera를 우선 사용.
    Camera ResolveCamera()
    {
        var local = Game.Net.NetPlayer.Local;
        if (local != null && local.HeadCamera != null) return local.HeadCamera;
        return playerCamera; // 오프라인/로비 기존 경로
    }


    void Update()
    {
        if (controller == null)
            return;

        if (!Input.GetKeyDown(interactKey))
            return;

        var cam = ResolveCamera();
        if (cam == null)
            return;


        Ray ray =
            cam.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f));


        RaycastHit hit;


        if (!Physics.Raycast(
            ray,
            out hit,
            interactDistance))
            return;


        DoorInteractable doorHit =
            hit.collider.GetComponentInParent<DoorInteractable>();


        if (doorHit != this)
            return;


        OnInteract();
    }


    public void OnInteract()
    {
        if (controller == null)
            return;

        // 잠김 상태(레버 3개 미완)면 로컬 흔들림 연출 그대로 — 동기화 불필요.
        // 열림 가능 상태면 어댑터 경유(최초 요청자만 실제로 문을 연다).
        if (_sync != null && controller.IsUnlocked)
        {
            _sync.RequestSet(true);
            return;
        }

        controller.InteractWithDoor();
    }
}