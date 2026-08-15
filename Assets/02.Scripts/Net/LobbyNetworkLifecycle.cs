namespace Game.Net
{
    /// <summary>
    /// 로비는 UGS Session만 유지하고, NGO/Relay는 게임 시작 때만 만든다.
    /// 따라서 로비의 방장 변경은 기존 게임 네트워크를 이전하지 않는다.
    /// </summary>
    public static class LobbyNetworkLifecycle
    {
        public const string HomescreenSceneName = "Homescreen";

        public static bool CanTransferPreGameHost(string sceneName, bool networkListening)
            => sceneName == HomescreenSceneName && !networkListening;

        public static bool CanStartGameNetwork(string sceneName, bool isSessionHost, bool networkListening)
            => sceneName == HomescreenSceneName && isSessionHost && !networkListening;
    }
}
