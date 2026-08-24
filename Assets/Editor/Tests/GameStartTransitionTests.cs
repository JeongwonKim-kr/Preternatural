using Game.UI;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

public class GameStartTransitionTests
{
    [TestCase("Homescreen", "GameScene", true, false, true)]
    [TestCase("Homescreen", "GameScene", true, true, false)]
    [TestCase("Homescreen", "GameScene", false, false, false)]
    [TestCase("Homescreen", "Homescreen", true, false, false)]
    [TestCase("GameScene", "GameScene", true, false, false)]
    public void NetworkLoad_StartsTransitionForEveryClientLeavingTheLobby(
        string activeSceneName,
        string loadingSceneName,
        bool isLoadEvent,
        bool transitionRunning,
        bool expected)
    {
        MethodInfo method = typeof(GameStartTransition).GetMethod(
            "ShouldBeginFromNetworkLoad",
            BindingFlags.Public | BindingFlags.Static);

        Assert.That(method, Is.Not.Null,
            "All peers need a shared decision path for the NGO GameScene load event.");

        bool actual = (bool)method.Invoke(null, new object[]
        {
            activeSceneName,
            loadingSceneName,
            isLoadEvent,
            transitionRunning
        });
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void NetworkSynchronize_StartsTransitionForGuestConnectingAfterHostLoad()
    {
        MethodInfo method = typeof(GameStartTransition).GetMethod(
            "ShouldBeginFromNetworkSceneEvent",
            BindingFlags.Public | BindingFlags.Static);

        Assert.That(method, Is.Not.Null,
            "A guest can enter through NGO's initial Synchronize event after the host has started loading.");

        bool actual = (bool)method.Invoke(null, new object[]
        {
            "Homescreen",
            null,
            false,
            true,
            false
        });
        Assert.That(actual, Is.True);
    }

    [Test]
    public void SignalPresentationAt_StartsWithSevereBroadcastInterference()
    {
        var presentation = GameStartTransition.SignalPresentationAt(0.1f);

        Assert.That(presentation.Interference, Is.GreaterThan(0.8f));
        Assert.That(presentation.StaticAlpha, Is.GreaterThan(0.2f));
        Assert.That(Mathf.Abs(presentation.HorizontalTear), Is.GreaterThan(0.001f));
    }

    [Test]
    public void SignalPresentationAt_SettlesNearTheEndOfTheMinimumWindow()
    {
        var opening = GameStartTransition.SignalPresentationAt(0.1f);
        var settling = GameStartTransition.SignalPresentationAt(2.55f);

        Assert.That(settling.Interference, Is.LessThan(opening.Interference));
        Assert.That(settling.StaticAlpha, Is.LessThan(opening.StaticAlpha));
    }

    [Test]
    public void SignalPresentationAt_IsDeterministicForTheSameFrame()
    {
        var first = GameStartTransition.SignalPresentationAt(0.47f);
        var repeated = GameStartTransition.SignalPresentationAt(0.47f);

        Assert.That(repeated.HorizontalTear, Is.EqualTo(first.HorizontalTear));
        Assert.That(repeated.ChromaticOffset, Is.EqualTo(first.ChromaticOffset));
        Assert.That(repeated.StaticAlpha, Is.EqualTo(first.StaticAlpha));
    }

    [TestCase(0f, false, false)]
    [TestCase(2.59f, true, false)]
    [TestCase(2.6f, false, false)]
    [TestCase(2.6f, true, true)]
    [TestCase(8f, true, true)]
    public void CanRevealGame_RequiresBothMinimumDurationAndReadyScene(
        float seconds, bool gameSceneReady, bool expected)
        => Assert.That(GameStartTransition.CanRevealGame(seconds, gameSceneReady), Is.EqualTo(expected));
}
