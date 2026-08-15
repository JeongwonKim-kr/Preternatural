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
}
