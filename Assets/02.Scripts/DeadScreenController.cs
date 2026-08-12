using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using TMPro;

public class DeadScreenController : MonoBehaviour
{
    [Header("Objects")]
    public GameObject deadScreen;
    public RawImage blackFade;

    [Header("Video")]
    public VideoPlayer videoPlayer;

    [Header("Texts")]
    public TMP_Text deadText;
    public TMP_Text rewindText;
    public TMP_Text homeText;
    public TMP_Text timerText;

    [Header("Buttons")]
    public Button rewindButton;
    public Button homeButton;

    [Header("Scenes")]
    public string rewindScene;
    public string menuScene;

    [Header("Audio")]
    public AudioSource musicSource;
    public AudioClip deathMusic;

    public AudioSource sfxSource;
    public AudioClip glitchSound;
    public AudioClip textAppearSound;
    public AudioClip buttonClickSound;

    [Header("Settings")]
    public float startDelay = 0f;
    public float uiDelay = 0.6f;
    public float timerLength = 50f;
    public float fadeSpeed = 0.75f;

    [Header("Camera")]
    public MonoBehaviour cameraController;

    private float timer;
    private bool selected = false;


    // =========================================================
    // ENABLE
    // =========================================================

    void OnEnable()
    {

        StartCoroutine(StartDeadSequence());
    }


    // =========================================================
    // START
    // =========================================================

    void Start()
    {
        SetupDeathScreen();
    }


    // =========================================================
    // SETUP
    // =========================================================

    void SetupDeathScreen()
    {
        if (deadScreen != null)
            deadScreen.SetActive(false);


        if (deadText != null)
            deadText.gameObject.SetActive(false);

        if (rewindText != null)
            rewindText.gameObject.SetActive(false);

        if (homeText != null)
            homeText.gameObject.SetActive(false);

        if (timerText != null)
            timerText.gameObject.SetActive(false);


        if (blackFade != null)
        {
            Color c = blackFade.color;
            c.a = 0f;
            blackFade.color = c;
        }


        if (rewindButton != null)
        {
            rewindButton.onClick.RemoveAllListeners();
            rewindButton.onClick.AddListener(Rewind);
        }

        if (homeButton != null)
        {
            homeButton.onClick.RemoveAllListeners();
            homeButton.onClick.AddListener(Menu);
        }


        timer = timerLength;
        selected = false;
    }


    // =========================================================
    // DEATH SEQUENCE
    // =========================================================

    IEnumerator StartDeadSequence()
    {


        yield return new WaitForSeconds(
            startDelay);




        if (deadScreen != null)
            deadScreen.SetActive(true);


        // Disable camera.
        if (cameraController != null)
            cameraController.enabled = false;


        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible = true;


        // =====================================================
        // GLITCH SOUND
        // =====================================================

  


        if (glitchSound != null &&
            sfxSource != null)
        {
            sfxSource.PlayOneShot(
                glitchSound);
        }


        // =====================================================
        // VIDEO
        // =====================================================

        if (videoPlayer != null)
        {
            videoPlayer.Play();
        }


        // =====================================================
        // MUSIC
        // =====================================================

        if (deathMusic != null &&
            musicSource != null)
        {
            musicSource.clip =
                deathMusic;

            musicSource.volume =
                0.15f;

            musicSource.loop =
                true;

            musicSource.Play();
        }


        // =====================================================
        // DEAD TEXT
        // =====================================================

        yield return new WaitForSeconds(
            uiDelay);




        if (deadText != null)
        {
            deadText.gameObject.SetActive(true);

            PlayTextSound();
        }


        // =====================================================
        // REWIND
        // =====================================================

        yield return new WaitForSeconds(
            uiDelay);




        if (rewindText != null)
        {
            rewindText.gameObject.SetActive(true);

            PlayTextSound();
        }


        // =====================================================
        // HOME
        // =====================================================

        yield return new WaitForSeconds(
            uiDelay);




        if (homeText != null)
        {
            homeText.gameObject.SetActive(true);

            PlayTextSound();
        }


        // =====================================================
        // TIMER
        // =====================================================

        yield return new WaitForSeconds(
            uiDelay);





        if (timerText != null)
        {
            timerText.gameObject.SetActive(true);

            timerText.text =
                Mathf.CeilToInt(
                    timer).ToString();

            PlayTextSound();
        }


        StartCoroutine(
            TimerRoutine());
    }


    // =========================================================
    // TIMER
    // =========================================================

    IEnumerator TimerRoutine()
    {
        while (timer > 0f &&
               !selected)
        {
            timer -= Time.deltaTime;


            if (timerText != null)
            {
                timerText.text =
                    Mathf.CeilToInt(
                        timer).ToString();
            }


            yield return null;
        }


        if (!selected)
        {
            Menu();
        }
    }


    // =========================================================
    // TEXT SOUND
    // =========================================================

    void PlayTextSound()
    {



        if (textAppearSound != null &&
            sfxSource != null)
        {
            sfxSource.PlayOneShot(
                textAppearSound);
        }
    }


    // =========================================================
    // BUTTON SOUND
    // =========================================================

    void PlayButtonSound()
    {
        if (buttonClickSound != null &&
            sfxSource != null)
        {
            sfxSource.PlayOneShot(
                buttonClickSound);
        }
    }


    // =========================================================
    // REWIND
    // =========================================================

    public void Rewind()
    {
        if (selected)
            return;


        selected = true;


        PlayButtonSound();


        StopAllCoroutines();


        StartCoroutine(
            FadeAndLoad(
                rewindScene));
    }


    // =========================================================
    // MENU
    // =========================================================

    public void Menu()
    {
        if (selected)
            return;


        selected = true;


        PlayButtonSound();


        StopAllCoroutines();


        StartCoroutine(
            FadeAndLoad(
                menuScene));
    }


    // =========================================================
    // FADE
    // =========================================================

    IEnumerator FadeAndLoad(
        string sceneName)
    {
        float alpha = 0f;


        while (alpha < 1f)
        {
            alpha +=
                Time.deltaTime *
                fadeSpeed;


            alpha =
                Mathf.Clamp01(alpha);


            if (blackFade != null)
            {
                Color c =
                    blackFade.color;

                c.a = alpha;

                blackFade.color = c;
            }


            if (musicSource != null &&
                musicSource.isPlaying)
            {
                musicSource.volume =
                    Mathf.Lerp(
                        0.15f,
                        0f,
                        alpha);
            }


            yield return null;
        }


        SceneManager.LoadScene(
            sceneName);
    }
}