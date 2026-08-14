using System;
using System.Threading.Tasks;
using Game.Net;
using Unity.Services.Vivox;
using UnityEngine;

namespace Game.Voice
{
    /// Vivox 근접 보이스. 실패해도 게임 진행에는 영향이 없도록 전부 격리한다.
    /// 채널명 = 세션 Id → 같은 방 사람들끼리만 들린다.
    /// 감쇠: 2m 안 원음량, 15m 밖 무음 (Channel3DProperties는 위치 인자로만 생성 —
    /// 세 번째 파라미터명이 SDK 오타라 named argument 금지)
    public class VoiceManager : MonoBehaviour
    {
        public static VoiceManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // 에디터 'Enter Play Mode without Domain Reload' 대응
            Instance = null;
        }

        public bool VoiceReady { get; private set; }
        public string ActiveChannel { get; private set; }
        public string StatusMessage { get; private set; } = "보이스 대기 중";
        public bool IsMuted => VoiceReady && VivoxService.Instance.IsInputDeviceMuted;

        const int AudibleDistance = 15;
        const int ConversationalDistance = 2;

        string _loggedInName;
        bool _joinInFlight;
        string _pendingChannel; // 조인 진행 중 새 조인 요청이 오면 1칸 대기열로 보관

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (Instance != this) return; // 중복 인스턴스가 Destroy 전에 Start를 받는 경우 방어
            if (SessionManager.Instance == null)
            {
                StatusMessage = "보이스 초기화 실패 (세션 매니저 없음)";
                Debug.LogWarning("[Voice] SessionManager.Instance가 없어 보이스를 비활성화합니다.");
                return;
            }
            SessionManager.Instance.SessionStarted += OnSessionStarted;
            SessionManager.Instance.SessionEnded += OnSessionEnded;
        }

        void OnSessionStarted(string sessionId) => _ = JoinVoiceAsync("s-" + sessionId);
        void OnSessionEnded() => _ = LeaveVoiceAsync();

        async Task JoinVoiceAsync(string channelName)
        {
            if (_joinInFlight) { _pendingChannel = channelName; return; }
            try
            {
                _joinInFlight = true;
                StatusMessage = "보이스 연결 중...";
                await VivoxService.Instance.InitializeAsync();

                var nickname = SessionManager.LocalNickname;
                if (VivoxService.Instance.IsLoggedIn && _loggedInName != nickname)
                {
                    // 닉네임이 바뀐 채 재입장 — DisplayName 갱신을 위해 재로그인
                    await VivoxService.Instance.LogoutAsync();
                }
                if (!VivoxService.Instance.IsLoggedIn)
                {
                    await VivoxService.Instance.LoginAsync(new LoginOptions
                    {
                        DisplayName = nickname,
                    });
                    _loggedInName = nickname;
                }

                var props = new Channel3DProperties(
                    AudibleDistance, ConversationalDistance, 1.0f, AudioFadeModel.InverseByDistance);
                await VivoxService.Instance.JoinPositionalChannelAsync(
                    channelName, ChatCapability.AudioOnly, props);

                var currentId = SessionManager.Instance?.ActiveSession?.Id;
                if (currentId == null || "s-" + currentId != channelName)
                {
                    // 조인 진행 중 세션이 끝났거나 다른 세션으로 바뀜 — 잘못된 채널 즉시 퇴장
                    await VivoxService.Instance.LeaveChannelAsync(channelName);
                    StatusMessage = "보이스 대기 중";
                    return;
                }

                ActiveChannel = channelName;
                VoiceReady = true;
                StatusMessage = "보이스 켜짐 (M: 음소거)";
                SetMuted(!LocalPlayerAlive()); // 늦은 조인: 관전 상태로 시작했으면 보이스 준비 시점에 뮤트 재적용
            }
            catch (Exception e)
            {
                VoiceReady = false;
                StatusMessage = "보이스 사용 불가 (대시보드 Vivox 활성화/마이크 권한 확인)";
                Debug.LogWarning($"[Voice] 초기화 실패: {e.Message}");
            }
            finally
            {
                _joinInFlight = false;
                if (_pendingChannel != null)
                {
                    var next = _pendingChannel;
                    _pendingChannel = null;
                    if (next != ActiveChannel) _ = JoinVoiceAsync(next); // 드롭됐던 조인 복구
                }
            }
        }

        async Task LeaveVoiceAsync()
        {
            _pendingChannel = null; // 세션 종료 — 대기 중인 조인도 무효
            var channel = ActiveChannel;
            ActiveChannel = null;
            VoiceReady = false;
            StatusMessage = "보이스 대기 중";
            if (channel == null) return;
            try { await VivoxService.Instance.LeaveChannelAsync(channel); }
            catch (Exception e) { Debug.LogWarning($"[Voice] 채널 퇴장 실패: {e.Message}"); }
        }

        public void ToggleMute()
        {
            if (!VoiceReady || !LocalPlayerAlive()) return;
            if (VivoxService.Instance.IsInputDeviceMuted) VivoxService.Instance.UnmuteInputDevice();
            else VivoxService.Instance.MuteInputDevice();
        }

        /// 로컬 플레이어가 생존 상태인가 (플레이어 없으면 제한 없음 — 메뉴 등)
        /// [REMOVED] 외부 의존성: Game.Player.NetworkPlayer, Game.Gameplay.PlayerLifeState/LifeState
        static bool LocalPlayerAlive()
        {
            // TODO: Restore when Game.Player.NetworkPlayer and Game.Gameplay.PlayerLifeState are available
            // foreach (var p in Game.Player.NetworkPlayer.All)
            // {
            //     if (!p.IsOwner) continue;
            //     var life = p.GetComponent<Game.Gameplay.PlayerLifeState>();
            //     return life == null || life.State.Value == Game.Gameplay.LifeState.Alive;
            // }
            return true;
        }

        /// 관전 등에서 마이크를 강제로 켜고 끈다 (VoiceReady 전엔 무시)
        public void SetMuted(bool muted)
        {
            if (!VoiceReady) return;
            if (muted && !VivoxService.Instance.IsInputDeviceMuted)
                VivoxService.Instance.MuteInputDevice();
            else if (!muted && VivoxService.Instance.IsInputDeviceMuted)
                VivoxService.Instance.UnmuteInputDevice();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.M)) ToggleMute();
        }
    }
}
