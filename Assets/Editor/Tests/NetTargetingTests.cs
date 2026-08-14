using NUnit.Framework;
using UnityEngine;
using Game.Net;

public class NetTargetingTests
{
    static NetTargeting.Candidate C(float x, bool alive = true, bool hidden = false)
        => new NetTargeting.Candidate { Position = new Vector3(x, 0, 0), Alive = alive, Hidden = hidden };

    [Test] public void Nearest_PicksClosestAliveVisible()
    {
        var list = new[] { C(10f), C(3f), C(1f, alive: false), C(2f, hidden: true) };
        Assert.AreEqual(1, NetTargeting.Nearest(Vector3.zero, list));
    }

    [Test] public void Nearest_ReturnsMinusOneWhenNoneEligible()
    {
        var list = new[] { C(1f, alive: false), C(2f, hidden: true) };
        Assert.AreEqual(-1, NetTargeting.Nearest(Vector3.zero, list));
    }

    [Test] public void Nearest_EmptyList_ReturnsMinusOne()
        => Assert.AreEqual(-1, NetTargeting.Nearest(Vector3.zero, new NetTargeting.Candidate[0]));
}
