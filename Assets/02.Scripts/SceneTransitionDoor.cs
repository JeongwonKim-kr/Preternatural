
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BlackDoor : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    public Camera playerCamera;
    public float interactDistance = 3f;

    [Header("Player")]
    public MonoBehaviour playerCode;

    [Header("Camera")]
    public MonoBehaviour cameraCode;

    [Header("Flashlight")]
    public MonoBehaviour flashlightCode;

    [Header("Sounds")]
    public AudioSource audioSource;
    public AudioClip interactSound;
    public AudioClip secondSound;
    public float secondSoundDelay = 1f;

    [Header("Music")]
    public AudioSource backgroundMusic;
    public float musicFadeDuration = 1f;

    [Header("Black Screen")]
    public RawImage blackScreen;
    public float fadeDuration = 0.5f;

    [Header("Scene")]
    public string sceneToLoad = "GameScene";

    [Header("After Scene Loads")]
    public float delayAfterSceneLoaded = 3f;

    private bool interacted = false;

    private void Start()
    {
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
        if (interacted)
            return;

        if (playerCamera == null)
            return;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            TryInteract();
        }
    }

    private void TryInteract()
    {
        Ray ray = new Ray(
            playerCamera.transform.position,
            playerCamera.transform.forward
        );

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            interactDistance
        ))
        {
            if (hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                OnInteract();
            }
        }
    }

    public void OnInteract()
    {
        if (interacted)
            return;

        interacted = true;

        StartCoroutine(TransitionRoutine());
    }

    private IEnumerator TransitionRoutine()
    {
        // =====================================================
        // DISABLE ONLY THE SCRIPTS
        // GAMEOBJECTS STAY ACTIVE
        // =====================================================

        if (playerCode != null)
            playerCode.enabled = false;

        if (cameraCode != null)
            cameraCode.enabled = false;

        if (flashlightCode != null)
            flashlightCode.enabled = false;

        // =====================================================
        // KEEP DOOR ALIVE DURING SCENE TRANSITION
        // =====================================================

        DontDestroyOnLoad(gameObject);

        // =====================================================
        // START BLACK FADE + MUSIC FADE + FIRST SOUND
        // AT THE SAME TIME
        // =====================================================

        Coroutine fadeCoroutine = null;

        if (blackScreen != null)
        {
            fadeCoroutine = StartCoroutine(FadeToBlack());
        }

        if (backgroundMusic != null)
        {
            StartCoroutine(FadeMusicOut());
        }

        if (audioSource != null &&
            interactSound != null)
        {
            audioSource.PlayOneShot(interactSound);
        }

        // =====================================================
        // WAIT FOR BLACK FADE
        // =====================================================

        if (fadeCoroutine != null)
        {
            yield return fadeCoroutine;
        }

        // =====================================================
        // WAIT 0.5 SECONDS
        // =====================================================

        yield return new WaitForSecondsRealtime(
            secondSoundDelay
        );

        // =====================================================
        // SECOND SOUND
        // =====================================================

        if (audioSource != null &&
            secondSound != null)
        {
            audioSource.PlayOneShot(secondSound);
        }

        // =====================================================
        // LOAD GAME SCENE
        // =====================================================

        AsyncOperation loading =
            SceneManager.LoadSceneAsync(sceneToLoad);

        if (loading == null)
        {
            Debug.LogError(
                "BlackDoor: Could not load scene: " +
                sceneToLoad
            );

            yield break;
        }

        loading.allowSceneActivation = false;

        // Wait until the scene is fully prepared.
        while (loading.progress < 0.9f)
        {
            yield return null;
        }

        // =====================================================
        // SCENE IS READY
        // WAIT 4 SECONDS
        // =====================================================

        yield return new WaitForSecondsRealtime(
            delayAfterSceneLoaded
        );

        // =====================================================
        // ACTIVATE GAME SCENE
        // =====================================================

        Time.timeScale = 1f;

        loading.allowSceneActivation = true;

        while (!loading.isDone)
        {
            yield return null;
        }

        yield return null;

        // =====================================================
        // FADE BLACK SCREEN OUT
        // =====================================================

        yield return StartCoroutine(FadeFromBlack());

        // =====================================================
        // FINISHED
        // =====================================================

        Destroy(gameObject);
    }

    // =========================================================
    // FADE TO BLACK
    // =========================================================

    private IEnumerator FadeToBlack()
    {
        if (blackScreen == null)
            yield break;

        blackScreen.gameObject.SetActive(true);

        Color color = blackScreen.color;
        float startAlpha = color.a;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                timer / fadeDuration
            );

            color.a = Mathf.Lerp(
                startAlpha,
                1f,
                t
            );

            blackScreen.color = color;

            yield return null;
        }

        color.a = 1f;
        blackScreen.color = color;
    }

    // =========================================================
    // FADE FROM BLACK
    // =========================================================

    private IEnumerator FadeFromBlack()
    {
        if (blackScreen == null)
            yield break;

        blackScreen.gameObject.SetActive(true);

        Color color = blackScreen.color;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                timer / fadeDuration
            );

            color.a = Mathf.Lerp(
                1f,
                0f,
                t
            );

            blackScreen.color = color;

            yield return null;
        }

        color.a = 0f;
        blackScreen.color = color;

        blackScreen.gameObject.SetActive(false);
    }

    // =========================================================
    // MUSIC FADE
    // =========================================================

    private IEnumerator FadeMusicOut()
    {
        if (backgroundMusic == null)
            yield break;

        float startVolume = backgroundMusic.volume;
        float timer = 0f;

        while (timer < musicFadeDuration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                timer / musicFadeDuration
            );

            backgroundMusic.volume = Mathf.Lerp(
                startVolume,
                0f,
                t
            );

            yield return null;
        }

        backgroundMusic.volume = 0f;
        backgroundMusic.Stop();
    }

    // =========================================================
    // GIZMO
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        if (playerCamera == null)
            return;

        Gizmos.color = Color.yellow;

        Gizmos.DrawRay(
            playerCamera.transform.position,
            playerCamera.transform.forward * interactDistance
        );
    }
}

