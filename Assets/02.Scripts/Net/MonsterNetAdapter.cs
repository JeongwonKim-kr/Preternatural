using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Game.Net
{
    /// 몬스터 호스트 권위. 클라이언트: 로컬 AI는 잠든 상태로 유지하고 NetworkTransform만 수신한다.
    /// 호스트: 매 프레임 가장 가까운 생존·비은신 플레이어를 MonsterLookAI.player/playerCamera에 주입하고,
    /// 애니메이션 상태를 NetworkVariable로 원격에 동기화한다. 공격 판정(호스트 전용)은 ServerAttack이 맡는다.
    ///
    /// 최종 리뷰 Critical 1: 몬스터 GO는 NGO in-scene 스폰 대상이 되려면 씬에 "활성 상태"로 저장돼 있어야
    /// 한다(PopulateScenePlacedObjects는 isActiveAndEnabled만 등록 — 비활성 시작은 영구 미스폰). 원래
    /// 싱글플레이는 Door Teleport.Start()가 objectToEnable(몬스터 GO)를 SetActive(false)로 꺼서 "아직
    /// 등장 안 함"을 표현했는데, 이게 NGO 스폰 판정보다 먼저 실행돼 영구 미스폰의 실제 원인이었다(확인:
    /// HEAD 상태로 재현 — Play 진입 직후 몬스터 GO.active=False). 그래서 이제 GO는 절대 SetActive로
    /// 끄지 않고(Door Teleport 쪽도 몬스터 대상이면 건너뛴다), "아직 깨어나지 않음"은 대신 _awake로
    /// 표현한다 — MonsterLookAI.SetAwake/SetVisible이 렌더러·콜라이더를 감추고 AI 이동을 멈춘다.
    /// MonsterLookAI.enabled/NavMeshAgent.enabled 자체는 건드리지 않는다. 컴포넌트를 꺼버리면
    /// MonsterLookAI.Start()까지 막혀 objectDisabledAtStart(DeathScreenController)가 초기화되지 않고,
    /// 원격 클라이언트에 CONNECTION LOST 화면이 남는 회귀가 발생한다.
    public class MonsterNetAdapter : NetworkBehaviour
    {
        [SerializeField] MonsterLookAI ai;
        [SerializeField] NavMeshAgent agent;

        readonly NetworkVariable<int> _animState = new(); // 0=Idle 1=Run 2=Jumpscare
        readonly NetworkVariable<bool> _awake = new(false); // 서버 쓰기 기본 — 몬스터 활동 시작 여부

        const float SpectatorDelay = 5f; // 점프스케어 연출과 겹치지 않도록 대기 후 관전 카메라 전환
        const float AttackCooldown = 5f; // 4인 밀집 시 매 프레임 재공격 방지(최종 리뷰 Important 6)
        float _nextAttackTime;

        void Awake()
        {
            if (ai == null) ai = GetComponent<MonsterLookAI>();
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            // Awake는 씬의 모든 Start()보다 먼저 실행이 보장되므로, MonsterLookAI.Start()가 돌기 전에
            // 반드시 "잠든" 상태로 되돌려 놓는다 — 온라인/오프라인 공통(오프라인은 OnNetworkSpawn이
            // 아예 안 불리므로 이게 유일한 초기화 지점이다). Door Teleport가 나중에 깨운다.
            if (ai != null) ai.SetAwake(false);
        }

        public override void OnNetworkSpawn()
        {
            if (ai == null) ai = GetComponent<MonsterLookAI>();
            if (agent == null) agent = GetComponent<NavMeshAgent>();

            // 원격 클라이언트에서도 MonsterLookAI.Start()는 실행돼야 시작 오버레이를 숨길 수 있다.
            // 로컬 AI 동작은 Awake()의 SetAwake(false)로 멈추며, 위치는 NetworkTransform이 수신한다.

            // 호스트/클라 공통: 현재 네트워크 권위 값으로 표시 상태를 맞춘다(호스트는 AI도 함께).
            ApplyAwakeState(_awake.Value);
            _awake.OnValueChanged += (_, awake) => ApplyAwakeState(awake);
            _animState.OnValueChanged += (_, s) => { if (!IsServer) PlayAnim(s); };
        }

        void ApplyAwakeState(bool awake)
        {
            if (ai == null) return;
            if (IsServer) ai.SetAwake(awake); // 호스트: AI 이동까지 함께 제어
            else ai.SetVisible(awake);         // 클라: 표시만(로컬 AI는 절대 돌리지 않음)
        }

        /// 서버 권위로 몬스터를 깨운다(표시+AI 활성화). 이미 깨어있으면 무해.
        public void ServerWake()
        {
            if (!IsServer) return;
            if (!_awake.Value) _awake.Value = true;
        }

        /// 클라이언트도 호출 가능 — 서버로 라우팅되어 ServerWake()를 실행한다(Door Teleport 등에서 사용).
        [Rpc(SendTo.Server)]
        public void RequestWakeRpc()
        {
            ServerWake();
        }

        /// 오프라인(미스폰) 전용 — 네트워크 변수를 거치지 않고 표시+AI를 직접 깨운다.
        public void LocalWake()
        {
            if (ai == null) ai = GetComponent<MonsterLookAI>();
            if (ai != null) ai.SetAwake(true);
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
            // 공격 쿨다운 중에는 타겟을 갈아끼우지 않는다 — 매 프레임 재타겟팅으로 인한 연속 공격 방지.
            if (Time.time < _nextAttackTime) return;

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
            if (Time.time < _nextAttackTime) return; // 쿨다운 중 재진입 무시(최종 리뷰 Important 6)
            _nextAttackTime = Time.time + AttackCooldown;

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
            if (IsServer) StartCoroutine(DelayedLeave());
            else _ = SessionManager.Instance.LeaveRoomAsync();
        }

        System.Collections.IEnumerator DelayedLeave()
        {
            yield return new WaitForSeconds(2f); // 클라이언트 RPC 처리·이탈 시간 확보
            var sm = SessionManager.Instance;
            if (sm != null && sm.InSession) _ = sm.LeaveRoomAsync();
        }
    }
}
