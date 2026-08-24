using NUnit.Framework;
using UnityEditor;

public class StartupSceneSettingsTests
{
    [Test]
    public void EnabledBuildScenes_StartAtHomescreenBeforeGameScene()
    {
        var scenes = EditorBuildSettings.scenes;

        Assert.That(scenes, Has.Length.GreaterThanOrEqualTo(2));
        Assert.That(scenes[0].enabled, Is.True);
        Assert.That(scenes[0].path, Is.EqualTo("Assets/01.Scenes/Homescreen.unity"));
        Assert.That(scenes[1].enabled, Is.True);
        Assert.That(scenes[1].path, Is.EqualTo("Assets/01.Scenes/GameScene.unity"));
    }
}
