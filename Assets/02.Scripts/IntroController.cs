using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class IntroController : MonoBehaviour
{
    [Header("Video")]
    public VideoPlayer videoPlayer;

    [Header("Scene")]
    public string homeScreenSceneName = "Homescreen";

    private bool loadingScene = false;

    // =========================================================
    // START
    // =========================================================

    private void Start()
    {
        if (videoPlayer == null)
        {
            Debug.LogError("IntroController: VideoPlayer is not assigned.");
            return;
        }

        // Make sure the video does not loop.
        videoPlayer.isLooping = false;

        // When the video finishes, call OnVideoFinished.
        videoPlayer.loopPointReached += OnVideoFinished;

        // Start the video.
        videoPlayer.Play();
    }

    // =========================================================
    // VIDEO FINISHED
    // =========================================================

    private void OnVideoFinished(VideoPlayer source)
    {
        if (loadingScene)
            return;

        loadingScene = true;

        LoadHomeScreen();
    }

    // =========================================================
    // LOAD HOME SCREEN
    // =========================================================

    private void LoadHomeScreen()
    {
        SceneManager.LoadScene(homeScreenSceneName);
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }
}