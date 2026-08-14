using Unity.Netcode;
using UnityEngine;

namespace Game.Net
{
    /// 아이템 획득 서버 권위 중재. 먼저 요청한 1명만 획득.
    /// 획득 시 전 클라이언트에서 월드 오브젝트 숨김, 드롭 시 지정 위치에 재표시.
    public class NetPickupSync : NetworkBehaviour
    {
        readonly NetworkVariable<bool> _taken = new();
        readonly NetworkVariable<ulong> _holder = new();
        public bool Taken => NetToggleSync.Online && IsSpawned ? _taken.Value : _localTaken;
        bool _localTaken;
        public event System.Action<bool, ulong> OnTakenChanged; // (taken, holderClientId)

        public override void OnNetworkSpawn()
        {
            _taken.OnValueChanged += (_, t) => OnTakenChanged?.Invoke(t, _holder.Value);
            if (_taken.Value) OnTakenChanged?.Invoke(true, _holder.Value);
        }

        public void RequestPickup()
        {
            if (!NetToggleSync.Online || !IsSpawned)
            { _localTaken = true; OnTakenChanged?.Invoke(true, 0); return; }
            PickupRpc();
        }

        public void RequestDrop(Vector3 position)
        {
            if (!NetToggleSync.Online || !IsSpawned)
            { _localTaken = false; OnTakenChanged?.Invoke(false, 0); return; }
            DropRpc(position);
        }

        [Rpc(SendTo.Server)]
        void PickupRpc(RpcParams p = default)
        {
            if (_taken.Value) return; // 이미 다른 사람이 가짐
            _holder.Value = p.Receive.SenderClientId;
            _taken.Value = true;
        }

        [Rpc(SendTo.Server)]
        void DropRpc(Vector3 position, RpcParams p = default)
        {
            if (!_taken.Value || _holder.Value != p.Receive.SenderClientId) return;
            SetDropPositionRpc(position);
            _taken.Value = false;
        }

        [Rpc(SendTo.Everyone)]
        void SetDropPositionRpc(Vector3 position) => transform.position = position;
    }
}
