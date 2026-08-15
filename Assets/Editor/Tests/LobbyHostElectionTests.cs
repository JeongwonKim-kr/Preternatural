using System;
using NUnit.Framework;
using Game.Net;

public class LobbyHostElectionTests
{
    [Test] public void SelectSuccessor_ReturnsFirstNonHostMember()
        => Assert.That(LobbyHostElection.SelectSuccessor("host", new[] { "host", "guest-1", "guest-2" }), Is.EqualTo("guest-1"));

    [Test] public void SelectSuccessor_SkipsHostWhenHostIsNotFirst()
        => Assert.That(LobbyHostElection.SelectSuccessor("host", new[] { "guest-1", "host", "guest-2" }), Is.EqualTo("guest-1"));

    [Test] public void SelectSuccessor_WithoutAnotherMember_ReturnsNull()
        => Assert.That(LobbyHostElection.SelectSuccessor("host", new[] { "host" }), Is.Null);

    [Test]
    public void LobbyNetworkLifecycle_OnlyPermitsPreGameTransfer()
    {
        var type = typeof(LobbyHostElection).Assembly.GetType("Game.Net.LobbyNetworkLifecycle");
        Assert.That(type, Is.Not.Null, "로비와 게임 네트워크를 분리하는 정책이 필요합니다.");

        var method = type.GetMethod("CanTransferPreGameHost");
        Assert.That(method, Is.Not.Null);
        Assert.That((bool)method.Invoke(null, new object[] { "Homescreen", false }), Is.True);
        Assert.That((bool)method.Invoke(null, new object[] { "Homescreen", true }), Is.False);
        Assert.That((bool)method.Invoke(null, new object[] { "GameScene", false }), Is.False);
    }
}
