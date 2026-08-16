using Game.UI;
using NUnit.Framework;

public class GameStartTransitionTests
{
    [TestCase(0f, 1f)]
    [TestCase(0.04f, 1f)]
    [TestCase(0.09f, 0.25f)]
    [TestCase(0.18f, 1f)]
    [TestCase(0.35f, 1f)]
    public void BlitzAlphaAt_UsesBlackBaseWithBriefPulses(float seconds, float expected)
        => Assert.That(GameStartTransition.BlitzAlphaAt(seconds), Is.EqualTo(expected));

    [TestCase(0f, true)]
    [TestCase(0.35f, true)]
    [TestCase(0.36f, false)]
    public void IsOpaqueBeforeSceneHandoff_EndsAfterBlitzWindow(float seconds, bool expected)
        => Assert.That(GameStartTransition.IsOpaqueBeforeSceneHandoff(seconds), Is.EqualTo(expected));
}
