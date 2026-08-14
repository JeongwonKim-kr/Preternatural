using System;
using Unity.Netcode;
using UnityEngine;

namespace Game.Net
{
    /// 1회성 월드 이벤트(추격 트리거 등) 동기화. 최초 1회만 전파.
    public class NetOneShotSync : NetworkBehaviour
    {
        readonly NetworkVariable<bool> _fired = new();
        bool _localFired;

        public event Action OnFired;
        public bool Fired => NetLink.Online && IsSpawned ? _fired.Value : _localFired;

        public override void OnNetworkSpawn()
        {
            _fired.OnValueChanged += HandleChanged;
            // 늦은 입장자에게는 재생하지 않음 — 이미 지나간 연출.
        }

        public override void OnNetworkDespawn() => _fired.OnValueChanged -= HandleChanged;
        void HandleChanged(bool was, bool now) { if (!was && now) OnFired?.Invoke(); }

        public void RequestFire()
        {
            if (NetLink.Online && !IsSpawned)
            {
                Debug.LogWarning($"[{GetType().Name}] 네트워크 가동 중 미스폰 상태 요청 무시: {name}");
                return;
            }
            if (!NetLink.Online || !IsSpawned)
            {
                if (_localFired) return;
                _localFired = true;
                OnFired?.Invoke();
                return;
            }
            FireRpc();
        }

        [Rpc(SendTo.Server)]
        void FireRpc() { if (!_fired.Value) _fired.Value = true; }
    }
}
