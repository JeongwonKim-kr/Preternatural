using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class DoorTeleport : MonoBehaviour, IInteractable
{
    [Header("References")]
    public Transform player;
    public Transform teleportPoint;
    public Camera playerCamera;

    [Header("Interaction")]
    public float interactDistance = 3f;
    public KeyCode interactKey = KeyCode.E;
    [Header("Enable After Video")]
public GameObject objectToEnable;


    [Header("Black Screen")]
    public RawImage blackScreen;
    public float fadeSpeed = 2f;


    [Header("Audio")]
    public AudioSource doorAudio;
    public AudioClip doorOpenSound;


    [Header("Background Music")]
    public AudioSource currentMusic;
    public AudioClip newMusicClip;
    public float musicFadeSpeed = 1.5f;


    [Header("Video")]
    public VideoPlayer videoPlayer;
    public RawImage videoRawImage;


    [Header("Disable During Cutscene")]
    public MonoBehaviour playerMovement;
    public MonoBehaviour playerCameraScript;
    public FlashlightController flashlightController;
    public GameObject flashlightObject;


    [Header("Options")]
    public bool disableAfterUse = true;


    bool isBusy;
    bool used;
    bool videoFinished;

    // 최종 리뷰 Critical 2: 씬에 정적으로 배선된 playerCamera는 멀티에서 비활성화된 씬 Player의
    // 카메라를 가리킨다. 로컬 NetPlayer가 있으면 그쪽 HeadCamera를 우선 사용, 없으면(오프라인) 폴백.
    Camera ResolveCamera()
    {
        var local = Game.Net.NetPlayer.Local;
        if (local != null && local.HeadCamera != null) return local.HeadCamera;
        return playerCamera; // 오프라인/로비 기존 경로
    }


    void Start()
    {
        if (blackScreen != null)
        {
            Color c = blackScreen.color;
            c.a = 0f;
            blackScreen.color = c;
        }
if (objectToEnable != null)
{
    // 최종 리뷰 Critical 1(실행 중 발견): 몬스터 GO(MonsterNetAdapter 부착)는 NGO 스폰 요건상
    // 항상 활성 상태를 유지해야 한다 — "잠듦"은 이제 GameObject 활성 여부가 아니라 ai/agent
    // enabled로 표현한다(NetSetupTool이 씬 저장 시 비활성으로 둔다). 여기서 GO 자체를 비활성화하면
    // 매 씬 로드마다 몬스터가 다시 미스폰 상태로 돌아가 버리므로, 몬스터 GO는 건너뛴다.
    // 몬스터가 아닌 다른 대상은 기존 그대로 시작 시 비활성화.
    if (objectToEnable.GetComponent<Game.Net.MonsterNetAdapter>() == null)
        objectToEnable.SetActive(false);
}

        if (currentMusic != null)
        {
            currentMusic.loop = true;
        }


        if (videoRawImage != null)
        {
            videoRawImage.gameObject.SetActive(false);
        }


        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += VideoFinished;
        }
    }



    void Update()
    {
        if (used || isBusy)
            return;


        if (!Input.GetKeyDown(interactKey))
            return;


        var cam = ResolveCamera();
        if (cam == null)
            return;

        Ray ray = cam.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0));


        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            DoorTeleport door =
                hit.collider.GetComponentInParent<DoorTeleport>();

            if (door == this)
            {
                StartCoroutine(TeleportRoutine());
            }
        }
    }



    IEnumerator TeleportRoutine()
    {
        isBusy = true;


        if (doorAudio != null && doorOpenSound != null)
        {
            doorAudio.PlayOneShot(doorOpenSound);
        }



        // Fade black in
        while (blackScreen.color.a < 1f)
        {
            Color c = blackScreen.color;

            c.a += fadeSpeed * Time.deltaTime;

            blackScreen.color = c;

            yield return null;
        }



        // Disable player
        if (playerMovement != null)
            playerMovement.enabled = false;


        if (playerCameraScript != null)
            playerCameraScript.enabled = false;


        if (flashlightController != null)
            flashlightController.enabled = false;


        if (flashlightObject != null)
            flashlightObject.SetActive(false);



        // Teleport player
        if (player != null && teleportPoint != null)
        {
            CharacterController cc =
                player.GetComponent<CharacterController>();

            if (cc != null)
                cc.enabled = false;


            player.position = teleportPoint.position;
            player.rotation = teleportPoint.rotation;


            if (cc != null)
                cc.enabled = true;
        }




        // Change music
        if (currentMusic != null && newMusicClip != null)
        {
            while (currentMusic.volume > 0)
            {
                currentMusic.volume = Mathf.MoveTowards(
                    currentMusic.volume,
                    0,
                    musicFadeSpeed * Time.deltaTime);

                yield return null;
            }


            currentMusic.Stop();

            currentMusic.clip = newMusicClip;
            currentMusic.loop = true;
            currentMusic.volume = 0;
            currentMusic.Play();
        }




        // Play video
        if (videoRawImage != null)
        {
            videoRawImage.gameObject.SetActive(true);
        }


        if (videoPlayer != null)
        {
            videoFinished = false;

            videoPlayer.Stop();
            videoPlayer.Play();


            while (!videoFinished)
            {
                yield return null;
            }
        }



        // Enable player before fade out
        if (playerMovement != null)
            playerMovement.enabled = true;


        if (playerCameraScript != null)
            playerCameraScript.enabled = true;


        if (flashlightController != null)
            flashlightController.enabled = true;


        if (flashlightObject != null)
            flashlightObject.SetActive(true);



        // Fade black out
        while (blackScreen.color.a > 0)
        {
            Color c = blackScreen.color;

            c.a -= fadeSpeed * Time.deltaTime;

            blackScreen.color = c;


            if (currentMusic != null)
            {
                currentMusic.volume = Mathf.MoveTowards(
                    currentMusic.volume,
                    1f,
                    musicFadeSpeed * Time.deltaTime);
            }


            yield return null;
        }


        Color finalColor = blackScreen.color;
        finalColor.a = 0;
        blackScreen.color = finalColor;



        if (disableAfterUse)
        {
            used = true;
            gameObject.SetActive(false);
        }


        isBusy = false;
    }



    void VideoFinished(VideoPlayer vp)
{
    videoFinished = true;


    if (videoRawImage != null)
    {
        videoRawImage.gameObject.SetActive(false);
    }


    // Enable object after video ends
    if (objectToEnable != null)
    {
        // 최종 리뷰 Critical 1: 몬스터 GO는 이제 항상 활성 상태로 씬에 저장돼 있으므로(NGO 스폰
        // 요건) SetActive(true)만으로는 더 이상 몬스터를 "깨우지" 못한다 — ai/agent 자체가
        // 비활성으로 시작하기 때문. 몬스터가 아닌 오브젝트를 위해 SetActive(true)는 그대로 두고,
        // MonsterNetAdapter가 있으면 온라인/오프라인에 맞는 경로로 추가로 깨운다.
        objectToEnable.SetActive(true);

        var adapter = objectToEnable.GetComponent<Game.Net.MonsterNetAdapter>();
        if (adapter != null)
        {
            if (Game.Net.NetLink.Online)
                adapter.RequestWakeRpc(); // 서버 권위 — 클라 호출도 서버로 라우팅됨(호스트는 즉시 실행)
            else
                adapter.LocalWake(); // 오프라인: 네트워크 스폰과 무관하게 즉시 깨움(기존 동작 보존)
        }
    }
}



    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        Gizmos.DrawWireSphere(
            transform.position,
            interactDistance);
    }
}