using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class BodyEndingController : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private float interactDistance = 3.3f;
    [SerializeField] private Transform playerCamera;

    [Header("Player")]
    [SerializeField] private GameObject playerObject;
    [SerializeField] private MonoBehaviour playerMovement;

    [Header("Stamina Bar")]
    [SerializeField] private GameObject staminaBar;

    [Header("Crosshair")]
    [SerializeField] private GameObject crosshair;

    [Header("Video")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage videoRawImage;

    [Header("Video Fade")]
    [SerializeField] private float videoFadeDuration = 0.1f;

    [Header("Background Music")]
    [SerializeField] private AudioSource backgroundMusic;

    [Header("Glitch Sound")]
    [SerializeField] private AudioSource glitchSource;
    [SerializeField] private AudioClip glitchSound;

    [Header("Tape Out Sound")]
    [SerializeField] private AudioSource tapeOutSource;
    [SerializeField] private AudioClip tapeOutSound;

    [Header("Black Screen")]
    [SerializeField] private RawImage blackScreen;

    [Header("Beep")]
    [SerializeField] private AudioSource beepSource;
    [SerializeField] private AudioClip beepSound;

    [Header("End Of Tape")]
    [SerializeField] private TMP_Text endOfTapeText;
    [SerializeField] private float endTextDelay = 1.5f;

    [Header("Final RawImage")]
    [SerializeField] private RawImage finalRawImage;

    [Header("Home Screen")]
    [SerializeField] private string homeScreenSceneName = "HomeScreen";
    [SerializeField] private float blackScreenWaitTime = 5f;

    private bool hasInteracted = false;
    private bool endingStarted = false;

    private void Start()
    {
        // Hide video
        if (videoRawImage != null)
        {
            videoRawImage.gameObject.SetActive(false);
        }

        // Stop video
        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }

        // Hide END OF TAPE
        if (endOfTapeText != null)
        {
            endOfTapeText.gameObject.SetActive(false);
        }

        // Hide final RawImage
        if (finalRawImage != null)
        {
            finalRawImage.gameObject.SetActive(false);
        }

        // Black screen starts invisible
        if (blackScreen != null)
        {
            Color color = blackScreen.color;
            color.a = 0f;
            blackScreen.color = color;
            blackScreen.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (endingStarted)
            return;

        if (playerCamera == null)
            return;

        float distance = Vector3.Distance(
            playerCamera.position,
            transform.position
        );

        if (distance <= interactDistance)
        {
            if (Keyboard.current != null &&
                Keyboard.current.eKey.wasPressedThisFrame)
            {
                Interact();
            }
        }
    }

    public void Interact()
    {
        if (endingStarted || hasInteracted)
            return;

        if (playerCamera == null)
            return;

        float distance = Vector3.Distance(
            playerCamera.position,
            transform.position
        );

        if (distance > interactDistance)
            return;

        hasInteracted = true;
        endingStarted = true;

        StartCoroutine(BodyEndingSequence());
    }

    private IEnumerator BodyEndingSequence()
    {
        // =====================================================
        // 1. REMOVE CROSSHAIR
        // =====================================================

        if (crosshair != null)
        {
            crosshair.SetActive(false);
        }

        // =====================================================
        // 2. REMOVE STAMINA BAR
        // =====================================================

        if (staminaBar != null)
        {
            staminaBar.SetActive(false);
        }

        // =====================================================
        // 3. STOP PLAYER MOVEMENT
        // =====================================================

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        // =====================================================
        // 4. STOP BACKGROUND MUSIC
        // =====================================================

        if (backgroundMusic != null)
        {
            backgroundMusic.Stop();
        }

        // =====================================================
        // 5. SHOW VIDEO
        // =====================================================

        if (videoRawImage != null)
        {
            videoRawImage.gameObject.SetActive(true);
        }

        // Make black screen invisible
        if (blackScreen != null)
        {
            Color blackColor = blackScreen.color;
            blackColor.a = 0f;
            blackScreen.color = blackColor;
            blackScreen.gameObject.SetActive(true);
        }

        // =====================================================
        // 6. PLAY VIDEO
        // =====================================================

        if (videoPlayer != null)
        {
            videoPlayer.Stop();

            videoPlayer.Prepare();

            // Wait for video to prepare
            while (!videoPlayer.isPrepared)
            {
                yield return null;
            }

            // =================================================
            // GLITCH WHEN VIDEO STARTS
            // =================================================

            if (glitchSource != null && glitchSound != null)
            {
                glitchSource.PlayOneShot(glitchSound);
            }

            // Start video
            videoPlayer.Play();

            // Wait for video to actually start
            float startTimer = 0f;

            while (!videoPlayer.isPlaying && startTimer < 5f)
            {
                startTimer += Time.unscaledDeltaTime;
                yield return null;
            }

            // =================================================
            // WAIT UNTIL FINAL MOMENT
            // =================================================

            while (videoPlayer.isPlaying &&
                   videoPlayer.length - videoPlayer.time > videoFadeDuration)
            {
                yield return null;
            }

            // =================================================
            // TAPE OUT SOUND
            // =================================================

            if (tapeOutSource != null && tapeOutSound != null)
            {
                tapeOutSource.PlayOneShot(tapeOutSound);
            }

            // =================================================
            // FADE VIDEO TO BLACK
            // =================================================

            if (blackScreen != null)
            {
                Color color = blackScreen.color;
                color.a = 0f;
                blackScreen.color = color;

                float fadeTimer = 0f;

                while (fadeTimer < videoFadeDuration)
                {
                    fadeTimer += Time.unscaledDeltaTime;

                    color.a = Mathf.Clamp01(
                        fadeTimer / videoFadeDuration
                    );

                    blackScreen.color = color;

                    yield return null;
                }

                color.a = 1f;
                blackScreen.color = color;
            }

            // Wait for video to completely finish
            while (videoPlayer.isPlaying)
            {
                yield return null;
            }
        }

        // =====================================================
        // 7. HIDE VIDEO
        // =====================================================

        if (videoRawImage != null)
        {
            videoRawImage.gameObject.SetActive(false);
        }

        // =====================================================
        // 8. WAIT 1.5 SECONDS
        // =====================================================

        yield return new WaitForSecondsRealtime(endTextDelay);

        // =====================================================
        // 9. BEEP
        // =====================================================

        if (beepSource != null && beepSound != null)
        {
            beepSource.PlayOneShot(beepSound);
        }

        // =====================================================
        // 10. SHOW END OF TAPE
        // =====================================================

        if (endOfTapeText != null)
        {
            endOfTapeText.enabled = true;
            endOfTapeText.gameObject.SetActive(true);
        }

        // =====================================================
        // 11. WAIT 1 SECOND
        // =====================================================

        yield return new WaitForSecondsRealtime(4f);

        // =====================================================
        // 12. HIDE END OF TAPE
        // =====================================================

        if (endOfTapeText != null)
        {
            endOfTapeText.gameObject.SetActive(false);
        }

        // =====================================================
        // 13. SHOW FINAL RAWIMAGE + BEEP
        // =====================================================
 yield return new WaitForSecondsRealtime(1.5f);
        if (finalRawImage != null)
        {
            finalRawImage.gameObject.SetActive(true);
        }

        if (beepSource != null && beepSound != null)
        {
            beepSource.PlayOneShot(beepSound);
        }

        // =====================================================
        // 14. KEEP RAWIMAGE FOR 1 SECOND
        // =====================================================

        yield return new WaitForSecondsRealtime(4f);

        // =====================================================
        // 15. HIDE FINAL RAWIMAGE
        // =====================================================

        if (finalRawImage != null)
        {
            finalRawImage.gameObject.SetActive(false);
        }

        // =====================================================
        // 16. BLACK SCREEN
        // =====================================================

        if (blackScreen != null)
        {
            Color color = blackScreen.color;
            color.a = 1f;
            blackScreen.color = color;
            blackScreen.gameObject.SetActive(true);
        }

        // =====================================================
        // 17. WAIT 5 SECONDS
        // =====================================================

        yield return new WaitForSecondsRealtime(blackScreenWaitTime);

        // =====================================================
        // 18. LOAD HOME SCREEN
        // =====================================================

        SceneManager.LoadScene(homeScreenSceneName);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            interactDistance
        );
    }
}