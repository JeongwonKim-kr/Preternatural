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

    [TestCase("PSXShaderKit.PSXPostProcessEffect", true)]
    [TestCase("UnityEngine.Rendering.PostProcessing.PostProcessLayer", true)]
    [TestCase("FirstPersonCamera", false)]
    [TestCase("PlayerMovement", false)]
    [TestCase("Game.MonsterAI", false)]
    public void KeepsPreviewBehaviour_OnlyKeepsCameraPostProcessing(string typeName, bool expected)
        => Assert.That(LobbyCorridorPreview.KeepsPreviewBehaviour(typeName), Is.EqualTo(expected));

    [TestCase("Homescreen", false, true)]
    [TestCase("Homescreen", true, false)]
    [TestCase("GameScene", false, false)]
    public void ShouldConfigurePreview_RequiresHomescreenWithoutReleaseRequest(
        string activeSceneName,
        bool releaseRequested,
        bool expected)
        => Assert.That(
            LobbyCorridorPreview.ShouldConfigurePreview(activeSceneName, releaseRequested),
            Is.EqualTo(expected));
}
