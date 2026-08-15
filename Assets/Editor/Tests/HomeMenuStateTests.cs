using Game.UI;
using NUnit.Framework;

public class HomeMenuStateTests
{
    [TestCase(HomeMenuView.Main)]
    [TestCase(HomeMenuView.CreateRoom)]
    [TestCase(HomeMenuView.JoinRoom)]
    public void Select_WithoutSession_KeepsRequestedMainMenuView(HomeMenuView requested)
        => Assert.That(HomeMenuState.Select(false, requested), Is.EqualTo(requested));

    [Test]
    public void Select_WithSession_AlwaysShowsLobby()
        => Assert.That(HomeMenuState.Select(true, HomeMenuView.JoinRoom), Is.EqualTo(HomeMenuView.Lobby));
}
