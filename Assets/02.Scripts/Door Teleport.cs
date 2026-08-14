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
        objectToEnable.SetActive(true);
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