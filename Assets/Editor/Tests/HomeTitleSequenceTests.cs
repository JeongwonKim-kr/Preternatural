using Game.UI;
using NUnit.Framework;

public class HomeTitleSequenceTests
{
    [Test]
    public void TitleAlphaAt_StartsVisible()
        => Assert.That(HomeTitleSequence.TitleAlphaAt(0f), Is.EqualTo(1f));

    [Test]
    public void TitleAlphaAt_UsesFlickerBeforeCompletion()
        => Assert.That(HomeTitleSequence.TitleAlphaAt(0.28f),
            Is.LessThan(HomeTitleSequence.TitleAlphaAt(0.16f)));

    [Test]
    public void TitleAlphaAt_AfterDuration_IsZero()
        => Assert.That(HomeTitleSequence.TitleAlphaAt(HomeTitleSequence.DurationSeconds + 0.01f), Is.EqualTo(0f));
}
