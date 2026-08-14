
using UnityEngine;
using Unity.Netcode;

public class PickupItem : MonoBehaviour, IInteractable
{
    [Header("Tool")]
    public ToolType toolType;

    [Header("Interaction")]
    public Camera playerCamera;
    public ToolHolder toolHolder;

    public float pickupDistance = 3f;
    public KeyCode pickupKey = KeyCode.E;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip pickupSound;
    public AudioClip dropSound;

    [Header("Drop Sound")]
    public float minImpactVelocity = 1f;

    [Header("Spawn Points")]
    public Transform spawnPoint1;
    public Transform spawnPoint2;
    public Transform spawnPoint3;

    private Rigidbody rb;

    private bool pickedUp;
    private bool canPlayDropSound;

    // The spawn point this item was placed at
    private Transform currentSpawnPoint;

    Game.Net.NetPickupSync _sync;

    void Awake()
    {
        _sync = GetComponent<Game.Net.NetPickupSync>();
        if (_sync != null)
        {
            _sync.OnTakenChanged += HandleTakenChanged;
            _sync.OnSpawnIndexChanged += ApplySpawnPoint;
        }
    }

    // 리뷰 반영: 씬에 정적으로 배선된 toolHolder는 멀티에서 비활성화된 씬 Player를 가리킨다
    // (NetPlayer.OnNetworkSpawn이 자신의 오너 스폰 시 씬 Player를 SetActive(false)). 로컬 NetPlayer가
    // 있으면 그쪽 ToolHolder를 우선 사용하고, 없으면(오프라인) 기존 toolHolder 필드로 폴백한다.
    ToolHolder ResolveHolder()
    {
        if (Game.Net.NetPlayer.Local != null)
        {
            var h = Game.Net.NetPlayer.Local.GetComponentInChildren<ToolHolder>(true);
            if (h != null) return h;
        }
        return toolHolder; // 오프라인 기존 경로
    }

    // 최종 리뷰 Critical 2: 씬에 정적으로 배선된 playerCamera는 멀티에서 비활성화된 씬 Player의
    // 카메라를 가리킨다. 로컬 NetPlayer가 있으면 그쪽 HeadCamera를 우선 사용하고, 없으면(오프라인)
    // 기존 필드로 폴백.
    Camera ResolveCamera()
    {
        var local = Game.Net.NetPlayer.Local;
        if (local != null && local.HeadCamera != null) return local.HeadCamera;
        return playerCamera; // 오프라인/로비 기존 경로
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // 리뷰 반영: 온라인 + 어댑터 있으면 각 클라이언트가 따로 Random을 굴리지 않고
        // NetPickupSync가 서버에서 정한 인덱스를 기다린다(Awake에서 구독한 ApplySpawnPoint).
        // 오프라인/어댑터 없으면 기존 Random 경로 그대로.
        if (_sync == null || !Game.Net.NetToggleSync.Online)
        {
            var validSpawnPoints = GetValidSpawnPoints();

            if (validSpawnPoints.Count > 0)
                ApplySpawnPoint(Random.Range(0, validSpawnPoints.Count));
        }

        // Rigidbody should not fight against the spawn point
        if (rb != null)
        {
            rb.isKinematic = true;
        }
    }

    // Remove empty spawn points
    System.Collections.Generic.List<Transform> GetValidSpawnPoints()
    {
        Transform[] spawnPoints = { spawnPoint1, spawnPoint2, spawnPoint3 };

        var validSpawnPoints = new System.Collections.Generic.List<Transform>();

        foreach (Transform point in spawnPoints)
        {
            if (point != null)
                validSpawnPoints.Add(point);
        }

        return validSpawnPoints;
    }

    /// 유효 스폰 포인트 개수 — NetPickupSync가 서버에서 인덱스 범위를 정할 때 참조.
    public int ValidSpawnPointCount => GetValidSpawnPoints().Count;

    /// index번째 유효 스폰 포인트로 이동+추종. 오프라인(Start)과 온라인(NetPickupSync.OnSpawnIndexChanged)
    /// 양쪽에서 공용으로 쓴다.
    public void ApplySpawnPoint(int index)
    {
        var validSpawnPoints = GetValidSpawnPoints();

        if (index < 0 || index >= validSpawnPoints.Count)
            return;

        currentSpawnPoint = validSpawnPoints[index];

        transform.position = currentSpawnPoint.position;
        transform.rotation = currentSpawnPoint.rotation;

        // Follow the spawn point
        transform.SetParent(currentSpawnPoint, true);
    }

    void Update()
    {
        if (pickedUp)
            return;

        var holder = ResolveHolder();

        if (holder == null)
            return;

        if (holder.HasTool())
            return;

        if (!Input.GetKeyDown(pickupKey))
            return;

        var cam = ResolveCamera();
        if (cam == null)
            return;

        Ray ray = cam.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        RaycastHit hit;

        if (!Physics.Raycast(ray, out hit, pickupDistance))
            return;

        PickupItem item =
            hit.collider.GetComponentInParent<PickupItem>();

        if (item != this)
            return;

        PickUp();
    }

    void PickUp()
    {
        pickedUp = true;

        if (_sync != null)
        {
            _sync.RequestPickup(); // 실제 장착은 OnTakenChanged 콜백에서(HandleTakenChanged)
            return;
        }

        DoLocalPickup(); // 어댑터 없으면 기존 경로
    }

    // Task 7: NetPickupSync.OnTakenChanged 콜백 — 서버 중재 결과 반영.
    void HandleTakenChanged(bool taken, ulong holderClientId)
    {
        if (!taken)
        {
            // 드롭됨 — 원격에서 숨겨뒀던 오브젝트를 재표시(위치는 SetDropPositionRpc가 이미 반영).
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            return;
        }

        // 오프라인/미스폰이면 NetPickupSync가 holderClientId=0으로 로컬 호출한 것 — 항상 로컬 획득.
        // (NetworkManager.Singleton이 아예 없을 수도 있음 — GameScene을 바로 Play한 싱글 경로.)
        bool offlineLocal = !Game.Net.NetToggleSync.Online || !_sync.IsSpawned;

        if (offlineLocal ||
            (NetworkManager.Singleton != null && holderClientId == NetworkManager.Singleton.LocalClientId))
        {
            DoLocalPickup();
        }
        else
        {
            // 다른 클라이언트가 먼저 획득 — 이 클라이언트에서는 월드에서 숨김.
            gameObject.SetActive(false);
        }
    }

    void DoLocalPickup()
    {
        canPlayDropSound = false;

        // Stop following the spawn point
        transform.SetParent(null, true);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (audioSource != null &&
            pickupSound != null)
        {
            audioSource.PlayOneShot(pickupSound);
        }

        ResolveHolder().PickupTool(
            toolType,
            gameObject
        );
    }

    public void SetPickedUp(bool value)
    {
        pickedUp = value;

        if (!value)
        {
            canPlayDropSound = true;

            if (rb != null)
            {
                rb.isKinematic = false;
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!canPlayDropSound)
            return;

        if (!collision.gameObject.CompareTag("Ground"))
            return;

        if (collision.relativeVelocity.magnitude < minImpactVelocity)
            return;

        if (audioSource != null &&
            dropSound != null)
        {
            audioSource.PlayOneShot(dropSound);
        }

        canPlayDropSound = false;
    }
}

