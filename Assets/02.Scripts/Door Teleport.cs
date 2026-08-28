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

    [Header("Audio Sources To Fade Out")]
    public AudioSource[] audioSourcesToFadeOut;
    public float audioFadeSpeed = 1.5f;

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

        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0));

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            interactDistance))
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


        // ==========================================
        // DOOR SOUND
        // ==========================================

        if (doorAudio != null &&
            doorOpenSound != null)
        {
            doorAudio.PlayOneShot(doorOpenSound);
        }


        // ==========================================
        // FADE BLACK IN
        // ==========================================

        while (blackScreen.color.a < 1f)
        {
            Color c = blackScreen.color;

            c.a += fadeSpeed * Time.deltaTime;

            blackScreen.color = c;

            yield return null;
        }


        // ==========================================
        // FADE OTHER AUDIO SOURCES OUT
        // ==========================================

        bool audioStillPlaying = true;

        while (audioStillPlaying)
        {
            audioStillPlaying = false;

            if (audioSourcesToFadeOut != null)
            {
                foreach (AudioSource source in audioSourcesToFadeOut)
                {
                    if (source != null &&
                        source.isPlaying)
                    {
                        source.volume = Mathf.MoveTowards(
                            source.volume,
                            0f,
                            audioFadeSpeed * Time.deltaTime);

                        if (source.volume > 0.001f)
                            audioStillPlaying = true;
                    }
                }
            }

            yield return null;
        }


        // ==========================================
        // STOP AND DISABLE FADED AUDIO
        // ==========================================

        if (audioSourcesToFadeOut != null)
        {
            foreach (AudioSource source in audioSourcesToFadeOut)
            {
                if (source != null)
                {
                    source.Stop();
                    source.enabled = false;
                }
            }
        }


        // ==========================================
        // DISABLE PLAYER
        // ==========================================

        if (playerMovement != null)
            playerMovement.enabled = false;

        if (playerCameraScript != null)
            playerCameraScript.enabled = false;

        if (flashlightController != null)
            flashlightController.enabled = false;

        if (flashlightObject != null)
            flashlightObject.SetActive(false);


        // ==========================================
        // TELEPORT PLAYER
        // ==========================================

        if (player != null &&
            teleportPoint != null)
        {
            CharacterController cc =
                player.GetComponent<CharacterController>();

            if (cc != null)
                cc.enabled = false;

            player.position =
                teleportPoint.position;

            player.rotation =
                teleportPoint.rotation;

            if (cc != null)
                cc.enabled = true;
        }


        // ==========================================
        // CHANGE MUSIC
        // ==========================================

        if (currentMusic != null &&
            newMusicClip != null)
        {
            while (currentMusic.volume > 0)
            {
                currentMusic.volume =
                    Mathf.MoveTowards(
                        currentMusic.volume,
                        0f,
                        musicFadeSpeed *
                        Time.deltaTime);

                yield return null;
            }

            currentMusic.Stop();

            currentMusic.clip =
                newMusicClip;

            currentMusic.loop = true;
            currentMusic.volume = 0f;
            currentMusic.Play();
        }


        // ==========================================
        // PLAY VIDEO
        // ==========================================

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


        // ==========================================
        // ENABLE PLAYER
        // ==========================================

        if (playerMovement != null)
            playerMovement.enabled = true;

        if (playerCameraScript != null)
            playerCameraScript.enabled = true;

        if (flashlightController != null)
            flashlightController.enabled = true;

        if (flashlightObject != null)
            flashlightObject.SetActive(true);


        // ==========================================
        // FADE BLACK OUT
        // ==========================================

        while (blackScreen.color.a > 0)
        {
            Color c = blackScreen.color;

            c.a -= fadeSpeed *
                   Time.deltaTime;

            blackScreen.color = c;


            // Fade new music IN
            if (currentMusic != null)
            {
                currentMusic.volume =
                    Mathf.MoveTowards(
                        currentMusic.volume,
                        1f,
                        musicFadeSpeed *
                        Time.deltaTime);
            }

            yield return null;
        }


        Color finalColor =
            blackScreen.color;

        finalColor.a = 0f;

        blackScreen.color =
            finalColor;


        // ==========================================
        // DISABLE DOOR AFTER USE
        // ==========================================

        if (disableAfterUse)
        {
            used = true;
            gameObject.SetActive(false);
        }

        isBusy = false;
    }


    // ==========================================
    // VIDEO FINISHED
    // ==========================================

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
            objectToEnable.SetActive(true);
        }
    }


    // ==========================================
    // GIZMOS
    // ==========================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        Gizmos.DrawWireSphere(
            transform.position,
            interactDistance);
    }
}