using System.Collections;
using UnityEngine;

public class WoodBoard : MonoBehaviour, IInteractable
{
    [Header("Tool")]
    public ToolType requiredTool = ToolType.Crowbar;

    [Header("References")]
    public ToolHolder toolHolder;
    public BoardedDoor boardedDoor;
    public Camera playerCamera;

    [Header("Interaction")]
    public float interactDistance = 3f;
    public KeyCode interactKey = KeyCode.E;

    [Header("Animation")]
    public float moveDistance = 0.4f;
    public float rotateAmount = 25f;
    public float animationTime = 0.35f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip removeSound;

    private bool removed;

    Game.Net.NetToggleSync _sync;

    void Awake()
    {
        _sync = GetComponent<Game.Net.NetToggleSync>();
        if (_sync != null) _sync.OnStateChanged += _ => StartCoroutine(RemoveBoard());
    }

    // 리뷰 반영: 씬에 정적으로 배선된 toolHolder는 멀티에서 비활성화된 씬 Player를 가리킨다.
    // 로컬 NetPlayer가 있으면 그쪽 ToolHolder를 우선 사용하고, 없으면(오프라인) 기존 필드로 폴백.
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
    // 카메라를 가리킨다. 로컬 NetPlayer가 있으면 그쪽 HeadCamera를 우선 사용, 없으면(오프라인) 폴백.
    Camera ResolveCamera()
    {
        var local = Game.Net.NetPlayer.Local;
        if (local != null && local.HeadCamera != null) return local.HeadCamera;
        return playerCamera; // 오프라인/로비 기존 경로
    }

    void Update()
    {
        if (removed)
            return;

        if (!Input.GetKeyDown(interactKey))
            return;

        var holder = ResolveHolder();

        if (holder == null || !holder.HasTool())
            return;

        if (holder.CurrentTool() != requiredTool)
            return;

        var cam = ResolveCamera();
        if (cam == null)
            return;

        Ray ray = cam.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));

        RaycastHit hit;


        if (!Physics.Raycast(ray, out hit, interactDistance))
            return;

        WoodBoard board =
            hit.collider.GetComponentInParent<WoodBoard>();

        if (board != this)
            return;

        // 도구 확인은 요청자만(위에서 이미 완료) — 실행은 전 클라이언트에서 코루틴으로.
        if (_sync != null) _sync.RequestSet(true);
        else StartCoroutine(RemoveBoard()); // 어댑터 없으면 기존 경로
    }

    IEnumerator RemoveBoard()
    {
        removed = true;

        if (audioSource != null &&
            removeSound != null)
        {
            audioSource.PlayOneShot(removeSound);
        }

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        Vector3 endPos =
            startPos +
            transform.forward * moveDistance;

        Quaternion endRot =
            startRot *
            Quaternion.Euler(
                rotateAmount,
                0f,
                0f);

        float timer = 0f;

        while (timer < 1f)
        {
            timer += Time.deltaTime / animationTime;

            float t =
                Mathf.SmoothStep(
                    0f,
                    1f,
                    timer);

            transform.position =
                Vector3.Lerp(
                    startPos,
                    endPos,
                    t);

            transform.rotation =
                Quaternion.Slerp(
                    startRot,
                    endRot,
                    t);

            yield return null;
        }

        boardedDoor.RemoveBoard();

        Destroy(gameObject);
    }
}