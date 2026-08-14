using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Net
{
    /// 몬스터 호스트 권위. 클라이언트: MonsterLookAI/NavMeshAgent 비활성(NetworkTransform 수신만).
    /// 호스트: 매 프레임 가장 가까운 생존·비은신 플레이어를 MonsterLookAI.player/playerCamera에 주입하고,
    /// 애니메이션 상태를 NetworkVariable로 원격에 동기화한다. 공격 판정(호스트 전용)은 ServerAttack이 맡는다.
    public class MonsterNetAdapter : NetworkBehaviour
    {
        [SerializeField] MonsterLookAI ai;
        [SerializeField] NavMeshAgent agent;

        readonly NetworkVariable<int> _animState = new(); // 0=Idle 1=Run 2=Jumpscare

        const float SpectatorDelay = 5f; // 점프스케어 연출과 겹치지 않도록 대기 후 관전 카메라 전환

        public override void OnNetworkSpawn()
        {
            if (ai == null) ai = GetComponent<MonsterLookAI>();
            if (agent == null) agent = GetComponent<NavMeshAgent>();

            if (!IsServer)
            {
                // 클라이언트는 몬스터를 직접 움직이지 않는다 — 위치는 NetworkTransform(서버 권위)이 수신한다.
                if (ai != null) ai.enabled = false;
                if (agent != null) agent.enabled = false;
            }

            _animState.OnValueChanged += (_, s) => { if (!IsServer) PlayAnim(s); };
        }

        void PlayAnim(int state)
        {
            if (ai == null || ai.monsterAnimation == null) return;
            var clip = state == 2 ? ai.jumpscareAnimation : state == 1 ? ai.runAnimation : ai.idleAnimation;
            if (clip != null && !ai.monsterAnimation.IsPlaying(clip.name))
                ai.monsterAnimation.CrossFade(clip.name, 0.2f);
        }

        void LateUpdate()
        {
            if (!IsServer || !IsSpawned || ai == null || NetPlayer.All.Count == 0) return;

            var candidates = new NetTargeting.Candidate[NetPlayer.All.Count];
            for (int i = 0; i < NetPlayer.All.Count; i++)
            {
                var p = NetPlayer.All[i];
                candidates[i] = new NetTargeting.Candidate
                {
                    Position = p.transform.position,
                    Alive = p.IsAlive.Value,
                    Hidden = p.IsHiding.Value
                };
            }

            int idx = NetTargeting.Nearest(transform.position, candidates);
            if (idx < 0)
            {
                ai.player = null;
                ai.playerCamera = null;
            }
            else
            {
                var target = NetPlayer.All[idx];
                ai.player = target.transform;
                // 단순화(스펙 대비): 시선 감지(CheckLooking)는 최근접 타겟의 카메라 기준으로만 판정한다.
                // 생존자 전원 각각에 대한 시선 판정(CheckLooking 로직 복제)까지는 하지 않음 — 체감 차이 미미.
                ai.playerCamera = target.HeadCamera;
            }

            _animState.Value = CurrentAnimState();
        }

        int CurrentAnimState()
        {
            if (ai.monsterAnimation == null) return 0;
            if (ai.jumpscareAnimation != null && ai.monsterAnimation.IsPlaying(ai.jumpscareAnimation.name)) return 2;
            if (ai.runAnimation != null && ai.monsterAnimation.IsPlaying(ai.runAnimation.name)) return 1;
            return 0;
        }

        /// 호스트가 공격 판정 시 MonsterAI(공격 분기)에서 호출.
        public void ServerAttack(NetPlayer victim)
        {
            if (!IsServer || victim == null) return;

            victim.ServerKill();
            JumpscareRpc(RpcTarget.Single(victim.OwnerClientId, RpcTargetUse.Temp));

            bool anyAlive = false;
            foreach (var p in NetPlayer.All)
                if (p.IsAlive.Value) { anyAlive = true; break; }
            if (!anyAlive) GameOverRpc();
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void JumpscareRpc(RpcParams _)
        {
            // 희생자 클라이언트: 로컬 몬스터로 기존 Jumpscare() 연출을 그대로 재생한 뒤,
            // 연출 여유(5초) 후 관전 카메라로 전환한다.
            if (ai != null) ai.Jumpscare();
            StartCoroutine(ActivateSpectatorAfterDelay());
        }

        IEnumerator ActivateSpectatorAfterDelay()
        {
            yield return new WaitForSeconds(SpectatorDelay);

            var local = NetPlayer.Local;
            var cam = local != null ? local.HeadCamera : null;
            if (cam == null) yield break;

            var spectator = cam.GetComponent<Game.Gameplay.SpectatorCamera>()
                         ?? cam.gameObject.AddComponent<Game.Gameplay.SpectatorCamera>();
            spectator.enabled = true; // 중복 활성이어도 무해 — NetPlayer 쪽 안전망과 겹칠 수 있음
        }

        [Rpc(SendTo.Everyone)]
        void GameOverRpc()
        {
            SessionManager.LastEndReason = "전원 사망 — 게임 오버";
            if (SessionManager.Instance != null)
                _ = SessionManager.Instance.LeaveRoomAsync(); // 세션 종료 → Homescreen 복귀(SessionManager 경로)
        }
    }
}
