using Game.UI;
using NUnit.Framework;

public class HomeTitleSequenceTests
{
    [Test]
    public void TitlePresentationAt_StartsVisible()
        => Assert.That(HomeTitleSequence.TitlePresentationAt(0f).Alpha, Is.EqualTo(1f));

    [Test]
    public void TitlePresentationAt_UsesFlicker()
        => Assert.That(HomeTitleSequence.TitlePresentationAt(0.28f).Alpha,
            Is.LessThan(HomeTitleSequence.TitlePresentationAt(0.16f).Alpha));

    [Test]
    public void TitlePresentationAt_RepeatsAfterOneCycle()
    {
        var first = HomeTitleSequence.TitlePresentationAt(0.18f);
        var repeated = HomeTitleSequence.TitlePresentationAt(HomeTitleSequence.TitleCycleSeconds + 0.18f);

        Assert.That(repeated.Alpha, Is.EqualTo(first.Alpha));
        Assert.That(repeated.HorizontalOffset, Is.EqualTo(first.HorizontalOffset));
    }

    [Test]
    public void TitlePresentationAt_KeepsVisibleValuesInRange()
    {
        var presentation = HomeTitleSequence.TitlePresentationAt(0.42f);

        Assert.That(presentation.Alpha, Is.InRange(0.1f, 1f));
        Assert.That(presentation.Intensity, Is.InRange(0.5f, 1f));
    }
}
