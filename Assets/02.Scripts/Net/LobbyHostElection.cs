using System.Collections.Generic;

namespace Game.Net
{
    public static class LobbyHostElection
    {
        public static string SelectSuccessor(string currentHostId, IReadOnlyList<string> playerIds)
        {
            foreach (var id in playerIds)
                if (!string.IsNullOrEmpty(id) && id != currentHostId) return id;
            return null;
        }
    }
}
