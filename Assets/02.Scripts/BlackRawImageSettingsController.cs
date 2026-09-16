using UnityEngine;
using UnityEngine.UI;

public class BlackRawImageSettingsController : MonoBehaviour
{
    [Header("Black RawImage")]
    public RawImage blackRawImage;

    [Header("Settings Menu")]
    public SettingsMenu settingsMenu;

    [Header("Visibility Threshold")]
    [Tooltip("Alpha above this value prevents Settings from opening.")]
    [Range(0f, 1f)]
    public float visibleThreshold = 0.1f;

    private bool lastLockedState = false;

    private void Start()
    {
        UpdateSettingsLock();
    }

    private void Update()
    {
        UpdateSettingsLock();
    }

    public void UpdateSettingsLock()
    {
        if (blackRawImage == null)
            return;

        bool locked =
            blackRawImage.color.a > visibleThreshold;

        if (locked == lastLockedState)
            return;

        lastLockedState = locked;

        if (settingsMenu != null)
        {
            settingsMenu.SetSettingsLocked(locked);
        }
    }

    public bool SettingsAreLocked()
    {
        if (blackRawImage == null)
            return false;

        return blackRawImage.color.a > visibleThreshold;
    }
}