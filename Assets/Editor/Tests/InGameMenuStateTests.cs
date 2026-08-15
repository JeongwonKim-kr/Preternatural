using NUnit.Framework;
using Game.UI;

public class InGameMenuStateTests
{
    [SetUp] public void SetUp() => DisplayModeSettings.DeleteSavedPreferenceForTests();
    [TearDown] public void TearDown() => DisplayModeSettings.DeleteSavedPreferenceForTests();

    [Test] public void LoadPreference_WithoutSavedValue_ReturnsWindowed()
        => Assert.That(DisplayModeSettings.LoadPreference(), Is.EqualTo(DisplayModePreference.Windowed));

    [Test] public void LoadPreference_InvalidSavedValue_FallsBackToWindowed()
    {
        DisplayModeSettings.SaveRawValueForTests(99);
        Assert.That(DisplayModeSettings.LoadPreference(), Is.EqualTo(DisplayModePreference.Windowed));
    }

    [Test] public void LoadPreference_SavedFullscreen_RoundTrips()
    {
        DisplayModeSettings.SavePreference(DisplayModePreference.Fullscreen);
        Assert.That(DisplayModeSettings.LoadPreference(), Is.EqualTo(DisplayModePreference.Fullscreen));
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public void ShouldPauseWorld_OnlyForOfflinePlay(bool listening, bool expected)
        => Assert.That(InGameMenuState.ShouldPauseWorld(listening), Is.EqualTo(expected));
}
