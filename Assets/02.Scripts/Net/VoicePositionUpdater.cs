using Unity.Netcode;
using Unity.Services.Vivox;
using UnityEngine;

namespace Game.Voice
{
    /// 소유자 카메라의 위치·방향을 0.3초 주기로 Vivox에 전달한다 (매 프레임 금지 — 공식 권장).
    /// 플레이어 프리팹의 카메라 GameObject에 부착.
    public class VoicePositionUpdater : MonoBehaviour
    {
        const float Interval = 0.3f;
        float _next;
        NetworkObject _netObj;

        void Awake() => _netObj = GetComponentInParent<NetworkObject>();

        void Update()
        {
            if (_netObj == null || !_netObj.IsOwner) return;
            var voice = VoiceManager.Instance;
            if (voice == null || !voice.VoiceReady) return;

            if (Time.unscaledTime >= _next)
            {
                _next = Time.unscaledTime + Interval;
                VivoxService.Instance.Set3DPosition(gameObject, voice.ActiveChannel);
            }
        }
    }
}
