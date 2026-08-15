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

    [TestCase(HomeMenuView.Main, true)]
    [TestCase(HomeMenuView.CreateRoom, false)]
    [TestCase(HomeMenuView.JoinRoom, false)]
    [TestCase(HomeMenuView.Lobby, false)]
    public void ShowsQuitAction_OnlyShowsOnMainView(HomeMenuView view, bool expected)
        => Assert.That(HomeMenuState.ShowsQuitAction(view), Is.EqualTo(expected));
}
