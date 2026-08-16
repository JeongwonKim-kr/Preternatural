using Game.UI;
using NUnit.Framework;

public class GameplayAspectFrameTests
{
    [TestCase(1920f, 1080f, 0.125f, 0f, 0.75f, 1f)]
    [TestCase(1440f, 1080f, 0f, 0f, 1f, 1f)]
    [TestCase(1080f, 1920f, 0f, 0.2890625f, 1f, 0.421875f)]
    public void CentralViewportFor_UsesCenteredFourByThree(float width, float height,
        float x, float y, float viewportWidth, float viewportHeight)
    {
        var actual = GameplayAspectFrame.CentralViewportFor(width, height);

        Assert.That(actual.x, Is.EqualTo(x).Within(0.0001f));
        Assert.That(actual.y, Is.EqualTo(y).Within(0.0001f));
        Assert.That(actual.width, Is.EqualTo(viewportWidth).Within(0.0001f));
        Assert.That(actual.height, Is.EqualTo(viewportHeight).Within(0.0001f));
    }
}
