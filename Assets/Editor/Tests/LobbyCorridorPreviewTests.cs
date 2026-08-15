using Game.UI;
using NUnit.Framework;

public class LobbyCorridorPreviewTests
{
    [TestCase("Homescreen", true)]
    [TestCase("GameScene", false)]
    [TestCase("", false)]
    public void IsHomescreen_OnlyMatchesTheMenuScene(string sceneName, bool expected)
        => Assert.That(LobbyCorridorPreview.IsHomescreen(sceneName), Is.EqualTo(expected));

    [Test]
    public void ShouldLoadPreview_OnlyLoadsOnceOnHomescreen()
    {
        Assert.That(LobbyCorridorPreview.ShouldLoadPreview("Homescreen", false, false), Is.True);
        Assert.That(LobbyCorridorPreview.ShouldLoadPreview("Homescreen", true, false), Is.False);
        Assert.That(LobbyCorridorPreview.ShouldLoadPreview("Homescreen", false, true), Is.False);
        Assert.That(LobbyCorridorPreview.ShouldLoadPreview("GameScene", false, false), Is.False);
    }

    [Test]
    public void ShouldReleasePreview_ReleasesWhenLeavingHomescreen()
    {
        Assert.That(LobbyCorridorPreview.ShouldReleasePreview("GameScene", true), Is.True);
        Assert.That(LobbyCorridorPreview.ShouldReleasePreview("Homescreen", true), Is.False);
        Assert.That(LobbyCorridorPreview.ShouldReleasePreview("GameScene", false), Is.False);
    }
}
