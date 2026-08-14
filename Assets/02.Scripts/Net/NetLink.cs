using Unity.Netcode;

namespace Game.Net
{
    /// 네트워크 세션 가동 여부 — 어댑터 공용 판정.
    public static class NetLink
    {
        public static bool Online =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }
}
