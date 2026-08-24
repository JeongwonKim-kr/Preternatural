using System.Reflection;
using Game.UI;
using NUnit.Framework;
using UnityEngine;

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

    [TestCase("PSXShaderKit.PSXPostProcessEffect", "Player/Camera", true)]
    [TestCase("UnityEngine.Rendering.PostProcessing.PostProcessLayer", "Player/Camera", true)]
    [TestCase("UnityEngine.Rendering.PostProcessing.PostProcessVolume", "PostProcessing", true)]
    [TestCase("UnityEngine.Canvas", "Canvas", true)]
    [TestCase("UnityEngine.UI.CanvasScaler", "Canvas", true)]
    [TestCase("UnityEngine.UI.GraphicRaycaster", "Canvas", false)]
    [TestCase("UnityEngine.UI.RawImage", "Canvas/glitch effect", true)]
    [TestCase("UnityEngine.Video.VideoPlayer", "Canvas/glitch effect", true)]
    [TestCase("UnityEngine.UI.RawImage", "Canvas/LeftPanel", true)]
    [TestCase("UnityEngine.UI.RawImage", "Canvas/RightPanel", true)]
    [TestCase("UnityEngine.UI.RawImage", "Canvas/JumpscareFadeOut", false)]
    [TestCase("UnityEngine.Video.VideoPlayer", "Canvas/DeadScreen", false)]
    [TestCase("TMPro.TextMeshPro", "Player/Camera/Crosshair/PLAY", true)]
    [TestCase("TMPro.TextMeshPro", "Player/Camera/Crosshair/Text", true)]
    [TestCase("TMPro.TextMeshPro", "Player/Camera/Crosshair/Text (1)", true)]
    [TestCase("TMPro.TextMeshPro", "Player/Camera/Crosshair/IfInteractable", false)]
    [TestCase("FirstPersonCamera", "Player/Camera", false)]
    [TestCase("PlayerMovement", "Player", false)]
    [TestCase("Game.MonsterAI", "Monster", false)]
    public void KeepsPreviewBehaviour_PreservesOnlyTheCameraAndVhsPresentation(
        string typeName,
        string hierarchyPath,
        bool expected)
        => Assert.That(
            LobbyCorridorPreview.KeepsPreviewBehaviour(typeName, hierarchyPath),
            Is.EqualTo(expected));

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

    [TestCase("GameScene", CursorLockMode.Locked, false, true)]
    [TestCase("Homescreen", CursorLockMode.None, true, true)]
    [TestCase("", CursorLockMode.None, true, false)]
    public void PreviewRelease_AppliesTheCursorStateForTheNextScene(
        string activeSceneName,
        CursorLockMode expectedLockMode,
        bool expectedVisible,
        bool expectedHandled)
    {
        var method = typeof(LobbyCorridorPreview).GetMethod(
            "ApplyCursorAfterPreviewRelease",
            BindingFlags.Public | BindingFlags.Static);

        Assert.That(method, Is.Not.Null,
            "Preview cleanup must apply the next scene's real cursor state.");

        CursorLockMode originalLockMode = Cursor.lockState;
        bool originalVisible = Cursor.visible;
        try
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            bool handled = (bool)method.Invoke(null, new object[] { activeSceneName });

            Assert.That(handled, Is.EqualTo(expectedHandled));
            Assert.That(Cursor.lockState, Is.EqualTo(expectedLockMode));
            Assert.That(Cursor.visible, Is.EqualTo(expectedVisible));
        }
        finally
        {
            Cursor.lockState = originalLockMode;
            Cursor.visible = originalVisible;
        }
    }
}
