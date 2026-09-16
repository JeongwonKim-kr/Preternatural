using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class SettingsMenu : MonoBehaviour
{
    [Header("Settings Panel")]
    public GameObject settingsPanel;
    public GameObject menuRoot;

    [Header("Settings Lock")]
    private bool settingsLocked = false;
    private bool blockSettingsInput = false;

    [Header("Objects To Toggle")]
    public GameObject[] objectsToToggle;

    [Header("Objects To Toggle 1")]
    public GameObject[] objectsToToggle1;

    [Header("RawImages - READ ONLY")]
    [Tooltip("Assign RawImages here. This list ONLY reads their values. It never changes visibility, enabled state, or alpha.")]
    public RawImage[] rawImages;

    [Header("Objects To Disable On END TAPE")]
    public GameObject[] objectsToDisableOnExit;

    [Header("Input Delay")]
    public float inputCooldown = 0.1f;

    [Header("Menu Text")]
    public TMP_Text soundText;
    public TMP_Text sensitivityText;
    public TMP_Text exitText;
    public TMP_Text selectorText;
    public TMP_Text playStopText;

    [Header("Crosshair")]
    public GameObject crosshair;

    [Header("Player")]
    public PlayerMovement playerMovement;

    [Header("Black RawImage Controller")]
    public BlackRawImageSettingsController blackRawImageSettingsController;

    [Header("Menu Audio")]
    public AudioSource menuAudioSource;
    public AudioClip moveSound;
    public AudioClip changeSound;
    public AudioClip selectSound;
    public AudioClip openCloseSound;

    [Header("Sound")]
    [Range(0f, 1f)]
    public float soundValue = 1f;

    [Header("Sensitivity")]
    [Range(0.1f, 2f)]
    public float sensitivityValue = 1f;

    [Header("Selector")]
    public float selectorSpacing = 25f;

    [Header("Mouse")]
    public bool lockMouseWhenClosed = true;

    [Header("Exit")]
    public string homeScreenSceneName = "HomeScreen";
    public RawImage fadeImage;
    public float fadeDuration = 0.5f;
    public float waitBeforeHomeScreen = 4f;

    [Header("Text")]
    public string playText = "PLAY";
    public string stopText = "STOP";

    [Header("PlayerPrefs")]
    public string soundPlayerPrefsKey = "Settings_Sound";
    public string sensitivityPlayerPrefsKey = "Settings_Sensitivity";

    private bool settingsOpen = false;
    private bool editing = false;
    private bool exiting = false;

    private int selectedOption = 0;

    private float nextSelectionTime = 0f;
    private float nextValueChangeTime = 0f;
    private float nextOpenCloseTime = 0f;

    private float previousTimeScale = 1f;
    private bool previousAudioPause = false;

    private Coroutine blinkCoroutine;

    private readonly string[] optionNames =
    {
        "Sound",
        "Sensitivity",
        "END TAPE"
    };

    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        if (menuRoot == null)
        {
            menuRoot = transform.root.gameObject;
        }

        if (menuAudioSource != null)
        {
            menuAudioSource.ignoreListenerPause = true;
        }

        LoadSettings();

        settingsOpen = false;
        editing = false;
        exiting = false;
        blockSettingsInput = false;

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        if (crosshair != null)
        {
            crosshair.SetActive(true);
        }

        SetToggleObjects(true);
        SetToggleObjects1(false);

        if (playerMovement != null)
        {
            playerMovement.enabled = true;
        }

        if (playStopText != null)
        {
            playStopText.text = playText;
        }

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(false);

            Color color = fadeImage.color;
            color.a = 0f;
            fadeImage.color = color;
        }

        ShowMenuTexts();

        UpdateSoundText();
        UpdateSensitivityText();
        UpdateSelector();

        LockMouse();
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (exiting || blockSettingsInput)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        // If the black RawImage is visible,
        // Settings cannot OPEN.
        //
        // The SettingsMenu itself remains enabled.
        if (!settingsOpen && settingsLocked)
        {
            return;
        }

        // =====================================================
        // ESC
        // =====================================================

        if (keyboard.escapeKey.wasPressedThisFrame &&
            Time.unscaledTime >= nextOpenCloseTime)
        {
            nextOpenCloseTime =
                Time.unscaledTime + inputCooldown;

            if (!settingsOpen)
            {
                OpenSettings();
            }
            else if (!editing)
            {
                CloseSettings();
            }

            return;
        }

        // =====================================================
        // SETTINGS CLOSED
        // =====================================================

        if (!settingsOpen)
        {
            return;
        }

        // =====================================================
        // EDITING
        // =====================================================

        if (editing)
        {
            HandleEditing(keyboard);
            return;
        }

        // =====================================================
        // UP
        // =====================================================

        if (Time.unscaledTime >= nextSelectionTime)
        {
            if (keyboard.upArrowKey.wasPressedThisFrame)
            {
                MoveSelection(-1);

                nextSelectionTime =
                    Time.unscaledTime + inputCooldown;

                return;
            }

            // =================================================
            // DOWN
            // =================================================

            if (keyboard.downArrowKey.wasPressedThisFrame)
            {
                MoveSelection(1);

                nextSelectionTime =
                    Time.unscaledTime + inputCooldown;

                return;
            }
        }

        // =====================================================
        // ENTER
        // =====================================================

        if (keyboard.enterKey.wasPressedThisFrame ||
            keyboard.numpadEnterKey.wasPressedThisFrame)
        {
            SelectCurrentOption();
        }
    }

    // =========================================================
    // SETTINGS LOCK
    // =========================================================

    public void SetSettingsLocked(bool locked)
    {
        settingsLocked = locked;
    }

    public bool IsSettingsLocked()
    {
        return settingsLocked;
    }

    // =========================================================
    // OPEN
    // =========================================================

    private void OpenSettings()
    {
        if (exiting || settingsLocked)
        {
            return;
        }

        settingsOpen = true;
        editing = false;
        selectedOption = 0;

        previousTimeScale = Time.timeScale;
        previousAudioPause = AudioListener.pause;

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }

        if (menuRoot != null)
        {
            menuRoot.SetActive(true);
        }

        ShowMenuTexts();

        if (crosshair != null)
        {
            crosshair.SetActive(false);
        }

        SetToggleObjects(false);
        SetToggleObjects1(true);

        if (playStopText != null)
        {
            playStopText.text = stopText;
        }

        UpdateSoundText();
        UpdateSensitivityText();
        UpdateSelector();

        Time.timeScale = 0f;
        AudioListener.pause = true;

        PlayMenuSound(openCloseSound);

        UnlockMouse();
    }

    // =========================================================
    // CLOSE
    // =========================================================

    private void CloseSettings()
    {
        if (exiting || !settingsOpen)
        {
            return;
        }

        settingsOpen = false;
        editing = false;

        StopBlink();

        PlayMenuSound(openCloseSound);

        Time.timeScale = previousTimeScale;
        AudioListener.pause = previousAudioPause;

        if (playerMovement != null)
        {
            playerMovement.enabled = true;
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        if (crosshair != null)
        {
            crosshair.SetActive(true);
        }

        SetToggleObjects(true);
        SetToggleObjects1(false);

        if (playStopText != null)
        {
            playStopText.text = playText;
        }

        LockMouse();
    }

    // =========================================================
    // RAWIMAGE READ ONLY
    // =========================================================

    public Color GetRawImageColor(int index)
    {
        if (rawImages == null)
        {
            return Color.clear;
        }

        if (index < 0 || index >= rawImages.Length)
        {
            return Color.clear;
        }

        if (rawImages[index] == null)
        {
            return Color.clear;
        }

        return rawImages[index].color;
    }

    public float GetRawImageAlpha(int index)
    {
        if (rawImages == null)
        {
            return 0f;
        }

        if (index < 0 || index >= rawImages.Length)
        {
            return 0f;
        }

        if (rawImages[index] == null)
        {
            return 0f;
        }

        return rawImages[index].color.a;
    }

    public bool IsRawImageEnabled(int index)
    {
        if (rawImages == null)
        {
            return false;
        }

        if (index < 0 || index >= rawImages.Length)
        {
            return false;
        }

        if (rawImages[index] == null)
        {
            return false;
        }

        return rawImages[index].enabled;
    }

    public bool IsRawImageGameObjectActive(int index)
    {
        if (rawImages == null)
        {
            return false;
        }

        if (index < 0 || index >= rawImages.Length)
        {
            return false;
        }

        if (rawImages[index] == null)
        {
            return false;
        }

        return rawImages[index].gameObject.activeSelf;
    }

    // =========================================================
    // OBJECT TOGGLE
    // =========================================================

    private void SetToggleObjects(bool active)
    {
        if (objectsToToggle == null)
        {
            return;
        }

        foreach (GameObject obj in objectsToToggle)
        {
            if (obj == null)
            {
                continue;
            }

            if (obj == gameObject)
            {
                continue;
            }

            if (settingsPanel != null &&
                obj == settingsPanel)
            {
                continue;
            }

            obj.SetActive(active);
        }
    }

    // =========================================================
    // OBJECT TOGGLE 1
    // =========================================================

    private void SetToggleObjects1(bool active)
    {
        if (objectsToToggle1 == null)
        {
            return;
        }

        foreach (GameObject obj in objectsToToggle1)
        {
            if (obj == null)
            {
                continue;
            }

            if (obj == gameObject)
            {
                continue;
            }

            if (settingsPanel != null &&
                obj == settingsPanel)
            {
                continue;
            }

            obj.SetActive(active);
        }
    }

    // =========================================================
    // SELECTION
    // =========================================================

    private void MoveSelection(int direction)
    {
        selectedOption += direction;

        if (selectedOption < 0)
        {
            selectedOption =
                optionNames.Length - 1;
        }

        if (selectedOption >= optionNames.Length)
        {
            selectedOption = 0;
        }

        UpdateSelector();

        PlayMenuSound(moveSound);
    }

    // =========================================================
    // SELECT
    // =========================================================

    private void SelectCurrentOption()
    {
        PlayMenuSound(selectSound);

        if (selectedOption == 0)
        {
            StartEditing();
        }
        else if (selectedOption == 1)
        {
            StartEditing();
        }
        else if (selectedOption == 2)
        {
            if (!exiting)
            {
                StartCoroutine(
                    ExitGameRoutine());
            }
        }
    }

    // =========================================================
    // EDIT
    // =========================================================

    private void StartEditing()
    {
        editing = true;

        nextValueChangeTime =
            Time.unscaledTime + inputCooldown;

        StartBlink();
    }

    private void HandleEditing(Keyboard keyboard)
    {
        if (keyboard.enterKey.wasPressedThisFrame ||
            keyboard.numpadEnterKey.wasPressedThisFrame)
        {
            editing = false;

            StopBlink();

            UpdateSoundText();
            UpdateSensitivityText();
            UpdateSelector();

            SaveSettings();

            PlayMenuSound(selectSound);

            return;
        }

        if (Time.unscaledTime <
            nextValueChangeTime)
        {
            return;
        }

        if (keyboard.leftArrowKey.wasPressedThisFrame)
        {
            ChangeCurrentValue(-0.1f);

            nextValueChangeTime =
                Time.unscaledTime + inputCooldown;

            return;
        }

        if (keyboard.rightArrowKey.wasPressedThisFrame)
        {
            ChangeCurrentValue(0.1f);

            nextValueChangeTime =
                Time.unscaledTime + inputCooldown;

            return;
        }
    }

    // =========================================================
    // VALUE
    // =========================================================

    private void ChangeCurrentValue(float amount)
    {
        if (selectedOption == 0)
        {
            soundValue += amount;

            soundValue =
                Mathf.Clamp01(soundValue);

            ApplySound();
            UpdateSoundText();
        }
        else if (selectedOption == 1)
        {
            sensitivityValue += amount;

            sensitivityValue =
                Mathf.Clamp(
                    sensitivityValue,
                    0.1f,
                    2f);

            ApplySensitivity();
            UpdateSensitivityText();
        }

        PlayMenuSound(changeSound);

        UpdateSelector();

        SaveSettings();
    }

    // =========================================================
    // SOUND
    // =========================================================

    private void ApplySound()
    {
        AudioListener.volume =
            soundValue;
    }

    private void UpdateSoundText()
    {
        if (soundText == null)
        {
            return;
        }

        soundText.text =
            "Volume: " +
            soundValue.ToString("0.0");
    }

    // =========================================================
    // SENSITIVITY
    // =========================================================

    private void ApplySensitivity()
    {
        if (playerMovement == null)
        {
            return;
        }

        playerMovement.mouseSensitivity =
            700f * sensitivityValue;
    }

    private void UpdateSensitivityText()
    {
        if (sensitivityText == null)
        {
            return;
        }

        sensitivityText.text =
            "Sensitivity: " +
            sensitivityValue.ToString("0.0");
    }

    // =========================================================
    // SELECTOR
    // =========================================================

    private void UpdateSelector()
    {
        if (selectorText == null)
        {
            return;
        }

        RectTransform selectedRect = null;

        if (selectedOption == 0 &&
            soundText != null)
        {
            selectedRect =
                soundText.rectTransform;
        }
        else if (selectedOption == 1 &&
                 sensitivityText != null)
        {
            selectedRect =
                sensitivityText.rectTransform;
        }
        else if (selectedOption == 2 &&
                 exitText != null)
        {
            selectedRect =
                exitText.rectTransform;
        }

        if (selectedRect == null)
        {
            return;
        }

        RectTransform selectorRect =
            selectorText.rectTransform;

        if (selectorRect == null)
        {
            return;
        }

        Vector3[] corners =
            new Vector3[4];

        selectedRect.GetWorldCorners(corners);

        Vector3 leftMiddle =
            (corners[0] + corners[1]) * 0.5f;

        leftMiddle.x -= selectorSpacing;

        Transform parent =
            selectorRect.parent;

        if (parent != null)
        {
            selectorRect.localPosition =
                parent.InverseTransformPoint(
                    leftMiddle);
        }
        else
        {
            selectorRect.position =
                leftMiddle;
        }

        selectorRect.pivot =
            new Vector2(1f, 0.5f);

        selectorText.text = ">";
    }

    // =========================================================
    // BLINK
    // =========================================================

    private void StartBlink()
    {
        StopBlink();

        blinkCoroutine =
            StartCoroutine(
                BlinkRoutine());
    }

    private void StopBlink()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);

            blinkCoroutine = null;
        }

        SetSelectedTextEnabled(true);
    }

    private IEnumerator BlinkRoutine()
    {
        while (editing)
        {
            SetSelectedTextEnabled(false);

            yield return
                new WaitForSecondsRealtime(0.25f);

            if (!editing)
            {
                break;
            }

            SetSelectedTextEnabled(true);

            yield return
                new WaitForSecondsRealtime(0.25f);
        }

        SetSelectedTextEnabled(true);
    }

    private void SetSelectedTextEnabled(bool enabledState)
    {
        if (selectedOption == 0)
        {
            if (soundText != null)
            {
                soundText.enabled =
                    enabledState;
            }
        }
        else if (selectedOption == 1)
        {
            if (sensitivityText != null)
            {
                sensitivityText.enabled =
                    enabledState;
            }
        }
        else if (selectedOption == 2)
        {
            if (exitText != null)
            {
                exitText.enabled =
                    enabledState;
            }
        }
    }

    // =========================================================
    // MENU TEXT
    // =========================================================

    private void ShowMenuTexts()
    {
        if (soundText != null)
        {
            soundText.gameObject.SetActive(true);
            soundText.enabled = true;
        }

        if (sensitivityText != null)
        {
            sensitivityText.gameObject.SetActive(true);
            sensitivityText.enabled = true;
        }

        if (exitText != null)
        {
            exitText.gameObject.SetActive(true);
            exitText.enabled = true;
        }

        if (selectorText != null)
        {
            selectorText.gameObject.SetActive(true);
            selectorText.enabled = true;
        }

        if (playStopText != null)
        {
            playStopText.gameObject.SetActive(true);
            playStopText.enabled = true;
        }
    }

    // =========================================================
    // END TAPE
    // =========================================================

    private IEnumerator ExitGameRoutine()
    {
        exiting = true;
        settingsOpen = false;
        editing = false;
        blockSettingsInput = true;

        StopBlink();

        SaveSettings();

        Time.timeScale = 0f;

        if (playerMovement != null)
        {
            playerMovement.enabled = false;
        }

        DisableExitObjects();

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        if (crosshair != null)
        {
            crosshair.SetActive(false);
        }

        if (playStopText != null)
        {
            playStopText.gameObject.SetActive(true);
            playStopText.text = stopText;
            playStopText.enabled = true;
        }

        // IMPORTANT:
        // No RawImage visibility/alpha is changed here.

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);

            Color color =
                fadeImage.color;

            color.a = 0f;
            fadeImage.color = color;

            float timer = 0f;

            while (timer < fadeDuration)
            {
                timer +=
                    Time.unscaledDeltaTime;

                float alpha =
                    Mathf.Clamp01(
                        timer / fadeDuration);

                color.a = alpha;
                fadeImage.color = color;

                Time.timeScale = 0f;

                yield return null;
            }

            color.a = 1f;
            fadeImage.color = color;
        }

        MuteAudioForExit();

        float waitTimer = 0f;

        while (waitTimer <
               waitBeforeHomeScreen)
        {
            waitTimer +=
                Time.unscaledDeltaTime;

            Time.timeScale = 0f;

            yield return null;
        }

        HideMenuRootAfterExit();

        Time.timeScale = 0f;

        AudioListener.pause = true;
        AudioListener.volume = 0f;

        SceneManager.LoadScene(
            homeScreenSceneName);
    }

    // =========================================================
    // EXIT OBJECTS
    // =========================================================

    private void DisableExitObjects()
    {
        if (objectsToDisableOnExit == null)
        {
            return;
        }

        foreach (GameObject obj in objectsToDisableOnExit)
        {
            if (obj == null)
            {
                continue;
            }

            if (obj == gameObject)
            {
                continue;
            }

            obj.SetActive(false);
        }
    }

    // =========================================================
    // MENU ROOT
    // =========================================================

    private void HideMenuRootAfterExit()
    {
        if (menuRoot != null)
        {
            menuRoot.SetActive(false);
        }
    }

    // =========================================================
    // AUDIO
    // =========================================================

    private void PlayMenuSound(AudioClip clip)
    {
        if (menuAudioSource == null ||
            clip == null)
        {
            return;
        }

        menuAudioSource.ignoreListenerPause = true;

        menuAudioSource.PlayOneShot(clip);
    }

    private void MuteAudioForExit()
    {
        AudioListener.pause = true;
        AudioListener.volume = 0f;
    }

    // =========================================================
    // MOUSE
    // =========================================================

    private void LockMouse()
    {
        if (!lockMouseWhenClosed)
        {
            return;
        }

        Cursor.lockState =
            CursorLockMode.Locked;

        Cursor.visible = false;
    }

    private void UnlockMouse()
    {
        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible = true;
    }

    // =========================================================
    // SAVE
    // =========================================================

    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(
            soundPlayerPrefsKey,
            soundValue);

        PlayerPrefs.SetFloat(
            sensitivityPlayerPrefsKey,
            sensitivityValue);

        PlayerPrefs.Save();
    }

    // =========================================================
    // LOAD
    // =========================================================

    private void LoadSettings()
    {
        if (PlayerPrefs.HasKey(soundPlayerPrefsKey))
        {
            soundValue =
                PlayerPrefs.GetFloat(
                    soundPlayerPrefsKey);
        }

        if (PlayerPrefs.HasKey(
                sensitivityPlayerPrefsKey))
        {
            sensitivityValue =
                PlayerPrefs.GetFloat(
                    sensitivityPlayerPrefsKey);
        }

        soundValue =
            Mathf.Clamp01(soundValue);

        sensitivityValue =
            Mathf.Clamp(
                sensitivityValue,
                0.1f,
                2f);

        ApplySound();
        ApplySensitivity();
    }

    // =========================================================
    // BLACK RAWIMAGE
    // =========================================================

    public bool BlackRawImageLocksSettings()
    {
        if (blackRawImageSettingsController == null)
        {
            return false;
        }

        return blackRawImageSettingsController
            .SettingsAreLocked();
    }
}