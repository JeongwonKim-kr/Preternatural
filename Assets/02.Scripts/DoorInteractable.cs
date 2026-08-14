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


    void Update()
    {
        if (controller == null)
            return;

        if (!Input.GetKeyDown(interactKey))
            return;

        if (playerCamera == null)
            return;


        Ray ray =
            playerCamera.ViewportPointToRay(
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