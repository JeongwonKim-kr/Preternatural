using UnityEngine;
using UnityEngine.Video;

public class VideoSettingsController : MonoBehaviour
{
    [Header("Video")]
    public VideoPlayer videoPlayer;

    [Header("Settings Object")]
    public GameObject settingsObject;
     public GameObject settingsObject1;

    private void Update()
    {
        if (videoPlayer == null || settingsObject == null)
            return;

        // Video is currently playing
        if (videoPlayer.isPlaying)
        {
            settingsObject1.SetActive(false);
        }
        // Video is not playing
        else
        {
            settingsObject.SetActive(true);
        }
    }
}