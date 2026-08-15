using Game.UI;
using NUnit.Framework;

public class MultiplayerMenuViewTests
{
    [TestCase(HomeMenuView.Main)]
    [TestCase(HomeMenuView.CreateRoom)]
    [TestCase(HomeMenuView.JoinRoom)]
    public void VisibleViewForTests_WithoutSession_ShowsRequestedMainMenuView(HomeMenuView requested)
        => Assert.That(MultiplayerMenu.VisibleViewForTests(false, requested), Is.EqualTo(requested));

    [Test]
    public void VisibleViewForTests_SessionOverridesJoinCardWithLobby()
        => Assert.That(MultiplayerMenu.VisibleViewForTests(true, HomeMenuView.JoinRoom), Is.EqualTo(HomeMenuView.Lobby));
}
