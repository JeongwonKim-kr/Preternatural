using UnityEngine;

namespace Game.UI
{
    public enum DisplayModePreference
    {
        Windowed = 0,
        Fullscreen = 1,
    }

    public static class DisplayModeSettings
    {
        const string PreferenceKey = "display.mode";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplySavedPreference()
        {
            Apply(LoadPreference());
        }

        public static DisplayModePreference LoadPreference()
        {
            return Normalize(PlayerPrefs.GetInt(PreferenceKey, (int)DisplayModePreference.Windowed));
        }

        public static void SavePreference(DisplayModePreference preference)
        {
            PlayerPrefs.SetInt(PreferenceKey, (int)Normalize((int)preference));
            PlayerPrefs.Save();
        }

        public static void Apply(DisplayModePreference preference)
        {
            switch (Normalize((int)preference))
            {
                case DisplayModePreference.Fullscreen:
                    Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                    break;
                default:
                    Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
                    break;
            }
        }

        public static void DeleteSavedPreferenceForTests()
        {
            PlayerPrefs.DeleteKey(PreferenceKey);
        }

        public static void SaveRawValueForTests(int value)
        {
            PlayerPrefs.SetInt(PreferenceKey, value);
        }

        static DisplayModePreference Normalize(int value)
        {
            return value == (int)DisplayModePreference.Fullscreen
                ? DisplayModePreference.Fullscreen
                : DisplayModePreference.Windowed;
        }
    }
}
