using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class HomeScreenController : MonoBehaviour
{
    [Header("Player")]
    public MonoBehaviour playerMovement;
    public FirstPersonCamera firstPersonCamera;

    [Header("Camera")]
    public Camera gameplayCamera;
    public Transform homeScreenCameraPosition;
    public Transform originalCameraPosition;

    [Header("Gameplay Objects")]
    public GameObject[] objectsToDisable;

    [Header("Home Screen UI")]
    public GameObject homeScreenUI;
    public TMP_Text startText;
    public TMP_Text exitText;
    public TMP_Text selectorText;

    [Header("Selector")]
    public float selectorSpacing = 25f;

    [Header("Input")]
    public float inputCooldown = 0.1f;

    [Header("Screen Fade")]
    public RawImage fadeImage;
    public float fadeDuration = 0.5f;

    [Header("Background Music")]
    public AudioSource backgroundAudioSource;
    public float musicFadeDuration = 0.5f;

    [Header("Menu Sounds")]
    public AudioSource menuAudioSource;
    public AudioClip moveSound;
    public AudioClip selectSound;

    private int selectedOption = 0;
    private float nextSelectionTime = 0f;

    private bool starting = false;
    private bool started = false;

    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        // Home screen is paused.
        Time.timeScale = 0f;

        AudioListener.pause = false;

        // -----------------------------------------------------
        // BACKGROUND MUSIC
        // -----------------------------------------------------

        if (backgroundAudioSource != null)
        {
            backgroundAudioSource.ignoreListenerPause = true;
            backgroundAudioSource.loop = true;

            if (!backgroundAudioSource.isPlaying)
                backgroundAudioSource.Play();
        }

        if (menuAudioSource != null)
            menuAudioSource.ignoreListenerPause = true;

        // -----------------------------------------------------
        // DISABLE PLAYER
        // -----------------------------------------------------

        if (playerMovement != null)
            playerMovement.enabled = false;

        // -----------------------------------------------------
        // DISABLE FIRST PERSON CAMERA SCRIPT
        // -----------------------------------------------------

        if (firstPersonCamera != null)
            firstPersonCamera.enabled = false;

        // -----------------------------------------------------
        // DISABLE GAMEPLAY OBJECTS
        // -----------------------------------------------------

        SetObjectsActive(false);

        // -----------------------------------------------------
        // SHOW HOME SCREEN
        // -----------------------------------------------------

        if (homeScreenUI != null)
            homeScreenUI.SetActive(true);

        // -----------------------------------------------------
        // MOVE CAMERA TO HOME SCREEN POSITION
        // -----------------------------------------------------

        if (gameplayCamera != null &&
            homeScreenCameraPosition != null)
        {
            gameplayCamera.transform.position =
                homeScreenCameraPosition.position;

            gameplayCamera.transform.rotation =
                homeScreenCameraPosition.rotation;

            // Camera itself stays enabled.
            gameplayCamera.enabled = true;
        }

        // -----------------------------------------------------
        // UNLOCK MOUSE
        // -----------------------------------------------------

        UnlockMouse();

        // -----------------------------------------------------
        // DEFAULT SELECTION
        // -----------------------------------------------------

        selectedOption = 0;
        UpdateSelector();

        // -----------------------------------------------------
        // START BLACK AND FADE INTO HOME SCREEN
        // -----------------------------------------------------

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);

            Color color = fadeImage.color;
            color.a = 1f;
            fadeImage.color = color;

            StartCoroutine(FadeFromBlack());
        }
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (starting || started)
            return;

        if (Keyboard.current == null)
            return;

        if (Time.unscaledTime < nextSelectionTime)
            return;

        bool moved = false;

        // -----------------------------------------------------
        // UP
        // -----------------------------------------------------

        if (Keyboard.current.wKey.wasPressedThisFrame ||
            Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            selectedOption--;

            if (selectedOption < 0)
                selectedOption = 1;

            moved = true;
        }

        // -----------------------------------------------------
        // DOWN
        // -----------------------------------------------------

        if (Keyboard.current.sKey.wasPressedThisFrame ||
            Keyboard.current.downArrowKey.wasPressedThisFrame)
        {
            selectedOption++;

            if (selectedOption > 1)
                selectedOption = 0;

            moved = true;
        }

        // -----------------------------------------------------
        // MOVEMENT SOUND
        // -----------------------------------------------------

        if (moved)
        {
            nextSelectionTime =
                Time.unscaledTime + inputCooldown;

            UpdateSelector();
            PlayMenuSound(moveSound);
        }

        // -----------------------------------------------------
        // ENTER
        // -----------------------------------------------------

        if (Keyboard.current.enterKey.wasPressedThisFrame ||
            Keyboard.current.numpadEnterKey.wasPressedThisFrame)
        {
            nextSelectionTime =
                Time.unscaledTime + inputCooldown;

            PlayMenuSound(selectSound);

            if (selectedOption == 0)
                StartGame();
            else
                ExitGame();
        }
    }

    // =========================================================
    // SELECTOR
    // =========================================================

    private void UpdateSelector()
    {
        if (selectorText == null)
            return;

        RectTransform selectorRect =
            selectorText.rectTransform;

        if (selectedOption == 0 &&
            startText != null)
        {
            selectorRect.position =
                startText.rectTransform.position;

            selectorRect.anchoredPosition +=
                new Vector2(
                    -selectorSpacing,
                    0f
                );
        }
        else if (selectedOption == 1 &&
                 exitText != null)
        {
            selectorRect.position =
                exitText.rectTransform.position;

            selectorRect.anchoredPosition +=
                new Vector2(
                    -selectorSpacing,
                    0f
                );
        }
    }

    // =========================================================
    // START GAME
    // =========================================================

    public void StartGame()
    {
        if (starting || started)
            return;

        StartCoroutine(StartGameRoutine());
    }

    private IEnumerator StartGameRoutine()
    {
        starting = true;

        // Keep gameplay paused during transition.
        Time.timeScale = 0f;

        // -----------------------------------------------------
        // FADE HOME SCREEN MUSIC OUT
        // -----------------------------------------------------

        yield return StartCoroutine(
            FadeMusicOut()
        );

        // -----------------------------------------------------
        // FADE HOME SCREEN TO BLACK
        // -----------------------------------------------------

        yield return StartCoroutine(
            FadeToBlack()
        );

        // -----------------------------------------------------
        // HIDE HOME SCREEN UI
        // -----------------------------------------------------

        if (homeScreenUI != null)
            homeScreenUI.SetActive(false);

        // -----------------------------------------------------
        // ENABLE GAMEPLAY OBJECTS
        // -----------------------------------------------------

        SetObjectsActive(true);

        // -----------------------------------------------------
        // MOVE ONLY THE CAMERA
        // -----------------------------------------------------

        if (firstPersonCamera != null &&
            originalCameraPosition != null)
        {
            firstPersonCamera.SetGameplayCameraPosition(
                originalCameraPosition
            );
        }
        else if (gameplayCamera != null &&
                 originalCameraPosition != null)
        {
            gameplayCamera.transform.position =
                originalCameraPosition.position;

            gameplayCamera.transform.rotation =
                originalCameraPosition.rotation;
        }

        // -----------------------------------------------------
        // KEEP SCREEN BLACK
        // -----------------------------------------------------

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);

            Color color = fadeImage.color;
            color.a = 1f;
            fadeImage.color = color;
        }

        // -----------------------------------------------------
        // CAMERA REMAINS ENABLED
        // -----------------------------------------------------

        if (gameplayCamera != null)
            gameplayCamera.enabled = true;

        // -----------------------------------------------------
        // PLAYER STILL DISABLED
        // -----------------------------------------------------

        if (playerMovement != null)
            playerMovement.enabled = false;

        // -----------------------------------------------------
        // START GAME TIME
        // -----------------------------------------------------

        Time.timeScale = 1f;

        LockMouse();

        // -----------------------------------------------------
        // ENABLE FIRST PERSON CAMERA
        // -----------------------------------------------------

        if (firstPersonCamera != null)
        {
            firstPersonCamera.enabled = true;

            // This is the ONLY place the wake-up intro starts.
            firstPersonCamera.BeginWakeUpIntro();
        }

        started = true;
        starting = false;
    }

    // =========================================================
    // EXIT GAME
    // =========================================================

    public void ExitGame()
    {
        if (starting)
            return;

        StartCoroutine(ExitGameRoutine());
    }

    private IEnumerator ExitGameRoutine()
    {
        starting = true;

        Time.timeScale = 0f;

        // -----------------------------------------------------
        // FADE MUSIC
        // -----------------------------------------------------

        yield return StartCoroutine(
            FadeMusicOut()
        );

        // -----------------------------------------------------
        // FADE TO BLACK
        // -----------------------------------------------------

        yield return StartCoroutine(
            FadeToBlack()
        );

        yield return new WaitForSecondsRealtime(0.1f);

        // -----------------------------------------------------
        // QUIT
        // -----------------------------------------------------

#if UNITY_EDITOR

        UnityEditor.EditorApplication.isPlaying = false;

#else

        Application.Quit();

#endif
    }

    // =========================================================
    // GAMEPLAY OBJECTS
    // =========================================================

    private void SetObjectsActive(bool active)
    {
        if (objectsToDisable == null)
            return;

        foreach (GameObject obj in objectsToDisable)
        {
            if (obj != null)
                obj.SetActive(active);
        }
    }

    // =========================================================
    // MUSIC FADE OUT
    // =========================================================

    private IEnumerator FadeMusicOut()
    {
        if (backgroundAudioSource == null)
            yield break;

        float startingVolume =
            backgroundAudioSource.volume;

        float timer = 0f;

        while (timer < musicFadeDuration)
        {
            timer += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    timer / musicFadeDuration
                );

            backgroundAudioSource.volume =
                Mathf.Lerp(
                    startingVolume,
                    0f,
                    t
                );

            yield return null;
        }

        backgroundAudioSource.volume = 0f;
        backgroundAudioSource.Stop();
    }

    // =========================================================
    // FADE TO BLACK
    // =========================================================

    private IEnumerator FadeToBlack()
    {
        if (fadeImage == null)
            yield break;

        fadeImage.gameObject.SetActive(true);

        Color color = fadeImage.color;

        float startingAlpha = color.a;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    timer / fadeDuration
                );

            color.a =
                Mathf.Lerp(
                    startingAlpha,
                    1f,
                    t
                );

            fadeImage.color = color;

            yield return null;
        }

        color.a = 1f;
        fadeImage.color = color;
    }

    // =========================================================
    // FADE FROM BLACK
    // =========================================================

    private IEnumerator FadeFromBlack()
    {
        if (fadeImage == null)
            yield break;

        fadeImage.gameObject.SetActive(true);

        Color color = fadeImage.color;

        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    timer / fadeDuration
                );

            color.a =
                Mathf.Lerp(
                    1f,
                    0f,
                    t
                );

            fadeImage.color = color;

            yield return null;
        }

        color.a = 0f;
        fadeImage.color = color;

        // Remove it after the menu is visible.
        fadeImage.gameObject.SetActive(false);
    }

    // =========================================================
    // MENU SOUND
    // =========================================================

    private void PlayMenuSound(AudioClip clip)
    {
        if (menuAudioSource == null ||
            clip == null)
            return;

        menuAudioSource.PlayOneShot(clip);
    }

    // =========================================================
    // MOUSE LOCK
    // =========================================================

    private void LockMouse()
    {
        Cursor.lockState =
            CursorLockMode.Locked;

        Cursor.visible = false;
    }

    // =========================================================
    // MOUSE UNLOCK
    // =========================================================

    private void UnlockMouse()
    {
        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible = true;
    }
}