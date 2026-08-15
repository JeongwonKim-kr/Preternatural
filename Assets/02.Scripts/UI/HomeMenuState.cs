namespace Game.UI
{
    public enum HomeMenuView
    {
        Main,
        CreateRoom,
        JoinRoom,
        Lobby
    }

    /// Homescreen 메뉴가 세션 상태에 맞는 하나의 화면만 보이도록 선택한다.
    public static class HomeMenuState
    {
        public static HomeMenuView Select(bool inSession, HomeMenuView requested)
        {
            if (inSession) return HomeMenuView.Lobby;
            return requested == HomeMenuView.Lobby ? HomeMenuView.Main : requested;
        }
    }
}
