using UnityEngine;

public class LeverInteractable : MonoBehaviour, IInteractable
{
    [Header("Controller")]
    public LeverDoorController controller;

    [Header("Lever")]
    public int leverNumber;

    [Header("Interaction")]
    public Camera playerCamera;
    public float interactDistance = 3f;
    public KeyCode interactKey = KeyCode.E;

    Game.Net.NetToggleSync _sync;

    void Awake()
    {
        _sync = GetComponent<Game.Net.NetToggleSync>();
        if (_sync != null) _sync.OnStateChanged += _ => controller.PullLever(leverNumber);
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


        LeverInteractable leverHit =
            hit.collider.GetComponentInParent<LeverInteractable>();


        if (leverHit != this)
            return;


        OnInteract();
    }


    public void OnInteract()
    {
        if (controller == null)
            return;

        if (_sync != null)
        {
            if (_sync.State) return; // 이미 당긴 레버 — 중복 호출 방지
            _sync.RequestSet(true);
            return;
        }

        controller.PullLever(
            leverNumber);
    }
}