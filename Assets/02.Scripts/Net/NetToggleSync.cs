using System;
using Unity.Netcode;
using UnityEngine;

namespace Game.Net
{
    /// 상호작용 상태 1개(열림/제거됨 등) 동기화 어댑터.
    /// 오프라인(싱글)에서는 RequestSet이 즉시 OnStateChanged를 로컬 호출 — 기존 동작 보존.
    public class NetToggleSync : NetworkBehaviour
    {
        readonly NetworkVariable<bool> _state = new(); // 서버 쓰기 기본
        bool _localState;

        public event Action<bool> OnStateChanged;
        public static bool Online => NetLink.Online;

        public bool State => Online && IsSpawned ? _state.Value : _localState;

        public override void OnNetworkSpawn()
        {
            _state.OnValueChanged += HandleChanged;
            if (_state.Value) OnStateChanged?.Invoke(true); // 늦은 입장 동기화
        }

        public override void OnNetworkDespawn() => _state.OnValueChanged -= HandleChanged;
        void HandleChanged(bool _, bool now) => OnStateChanged?.Invoke(now);

        public void RequestSet(bool value)
        {
            if (NetLink.Online && !IsSpawned)
            {
                Debug.LogWarning($"[{GetType().Name}] 네트워크 가동 중 미스폰 상태 요청 무시: {name}");
                return;
            }
            if (!NetLink.Online || !IsSpawned)
            {
                if (_localState == value) return;
                _localState = value;
                OnStateChanged?.Invoke(value);
                return;
            }
            SetRpc(value);
        }

        [Rpc(SendTo.Server)]
        void SetRpc(bool value) { _state.Value = value; }
    }
}
