namespace Game.UI
{
    public static class InGameMenuState
    {
        public static bool IsGameScene(string sceneName) => sceneName == "GameScene";

        public static bool ShouldPauseWorld(bool multiplayerListening) => !multiplayerListening;
    }
}
