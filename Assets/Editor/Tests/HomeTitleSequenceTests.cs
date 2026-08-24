using Game.UI;
using NUnit.Framework;
using UnityEngine;

public class HomeTitleSequenceTests
{
    [Test]
    public void TitlePresentationAt_RemainsStaticOverTime()
    {
        var first = HomeTitleSequence.TitlePresentationAt(0f);
        var later = HomeTitleSequence.TitlePresentationAt(1.42f);

        Assert.That(first.Alpha, Is.EqualTo(1f));
        Assert.That(first.Intensity, Is.EqualTo(1f));
        Assert.That(first.HorizontalOffset, Is.Zero);
        Assert.That(later.Alpha, Is.EqualTo(first.Alpha));
        Assert.That(later.Intensity, Is.EqualTo(first.Intensity));
        Assert.That(later.HorizontalOffset, Is.EqualTo(first.HorizontalOffset));
    }

    [TestCase(0f)]
    [TestCase(0.28f)]
    [TestCase(1.42f)]
    public void TitleColorAt_UsesLowSaturationBoneWhite(float seconds)
    {
        var color = HomeTitleSequence.TitleColorAt(seconds);
        float channelSpread = Mathf.Max(color.r, Mathf.Max(color.g, color.b)) -
                              Mathf.Min(color.r, Mathf.Min(color.g, color.b));

        Assert.That(channelSpread, Is.LessThan(0.1f));
        Assert.That(color.r, Is.GreaterThanOrEqualTo(color.g));
        Assert.That(color.g, Is.GreaterThanOrEqualTo(color.b));
        Assert.That(color.a, Is.EqualTo(1f));
    }

    [Test]
    public void TitleColorAt_RemainsStaticOverTime()
        => Assert.That(
            HomeTitleSequence.TitleColorAt(1.42f),
            Is.EqualTo(HomeTitleSequence.TitleColorAt(0f)));
}
