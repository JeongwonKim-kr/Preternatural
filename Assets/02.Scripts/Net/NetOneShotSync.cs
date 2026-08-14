using System;
using Unity.Netcode;

namespace Game.Net
{
    /// 1회성 월드 이벤트(추격 트리거 등) 동기화. 최초 1회만 전파.
    public class NetOneShotSync : NetworkBehaviour
    {
        readonly NetworkVariable<bool> _fired = new();
        bool _localFired;

        public event Action OnFired;
        public bool Fired => NetToggleSync.Online && IsSpawned ? _fired.Value : _localFired;

        public override void OnNetworkSpawn()
        {
            _fired.OnValueChanged += HandleChanged;
            // 늦은 입장자에게는 재생하지 않음 — 이미 지나간 연출.
        }

        public override void OnNetworkDespawn() => _fired.OnValueChanged -= HandleChanged;
        void HandleChanged(bool was, bool now) { if (!was && now) OnFired?.Invoke(); }

        public void RequestFire()
        {
            if (!NetToggleSync.Online || !IsSpawned)
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
