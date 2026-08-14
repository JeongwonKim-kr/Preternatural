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

        // 리뷰 반영: PickupItem 스폰 포인트를 서버가 1회 결정해 전 클라이언트에 배포 —
        // 각 클라이언트가 따로 Random을 굴리면 스폰 위치가 서로 어긋난다.
        readonly NetworkVariable<int> _spawnIndex = new(-1);
        public event System.Action<int> OnSpawnIndexChanged;

        public override void OnNetworkSpawn()
        {
            _taken.OnValueChanged += (_, t) => OnTakenChanged?.Invoke(t, _holder.Value);
            if (_taken.Value) OnTakenChanged?.Invoke(true, _holder.Value);

            _spawnIndex.OnValueChanged += (_, idx) => OnSpawnIndexChanged?.Invoke(idx);

            if (IsServer && _spawnIndex.Value < 0)
            {
                var pickup = GetComponent<PickupItem>();
                int count = pickup != null ? pickup.ValidSpawnPointCount : 0;
                if (count > 0) _spawnIndex.Value = Random.Range(0, count);
            }

            // 이미 정해진 값이면(서버 자신의 방금 대입 포함/늦은 입장) 다음 틱을 기다리지 않고 즉시 반영.
            if (_spawnIndex.Value >= 0) OnSpawnIndexChanged?.Invoke(_spawnIndex.Value);
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
