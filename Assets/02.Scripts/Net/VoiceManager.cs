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
        public string StatusMessage { get; private set; } = "VOICE STANDBY";
        public bool IsMuted => VoiceReady && VivoxService.Instance.IsInputDeviceMuted;

        const int AudibleDistance = 15;
        const int ConversationalDistance = 2;
        const float LobbyPositionInterval = 0.5f;

        string _loggedInName;
        bool _joinInFlight;
        string _pendingChannel; // 조인 진행 중 새 조인 요청이 오면 1칸 대기열로 보관
        float _nextLobbyPositionUpdate;
        bool _channelLeftSubscribed;
        bool _loggedLobbyPositionFailure;

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
                StatusMessage = "VOICE INIT FAILED  [NO SESSION MANAGER]";
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
                StatusMessage = "CONNECTING VOICE...";
                await VivoxService.Instance.InitializeAsync();

                if (!_channelLeftSubscribed)
                {
                    // InitializeAsync 성공 이후에만 안전 — VivoxService.Instance는 UGS 초기화 전엔 비어 있을 수 있다.
                    VivoxService.Instance.ChannelLeft += OnChannelLeft;
                    _channelLeftSubscribed = true;
                }

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
                    StatusMessage = "VOICE STANDBY";
                    return;
                }

                ActiveChannel = channelName;
                VoiceReady = true;
                _loggedLobbyPositionFailure = false; // 새 채널 조인 성공 — 이전 실패 상태를 잊고 다시 시도할 수 있게 한다
                StatusMessage = "VOICE LIVE  [M: MUTE]";
                SetMuted(!LocalPlayerAlive()); // 늦은 조인: 관전 상태로 시작했으면 보이스 준비 시점에 뮤트 재적용
            }
            catch (Exception e)
            {
                VoiceReady = false;
                StatusMessage = "VOICE UNAVAILABLE  [CHECK VIVOX AND MICROPHONE ACCESS]";
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
            StatusMessage = "VOICE STANDBY";
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

        /// 로컬 플레이어가 생존 상태인가 (NetPlayer.Local이 없으면 제한 없음 — 로비 등)
        static bool LocalPlayerAlive()
        {
            var local = NetPlayer.Local;
            return local == null || local.IsAlive.Value;
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
            // 로비 등 NetPlayer.Local이 없는 동안만 이 경로가 M키를 처리한다.
            // 스폰된 뒤에는 NetPlayer.Update()가 IsAlive 게이트를 포함해 M키를 전담하므로
            // 여기서도 같이 처리하면 같은 프레임에 두 번 토글되어 상쇄(무반응)된다.
            if (NetPlayer.Local != null) return;
            if (Input.GetKeyDown(KeyCode.M)) ToggleMute();

            UpdateLobbyPosition();
        }

        /// 로비 동안(NetPlayer.Local이 없는 동안)엔 플레이어가 스폰되지 않아 3D 위치가 갱신되지
        /// 않는다. 채널을 따로 만들지 않고 전원을 원점/정면에 고정해 거리 감쇠 없이 들리게 한다.
        /// NetPlayer.Local이 생기면(게임 씬 진입) 이 갱신은 멈추고 VoicePositionUpdater가
        /// 실제 카메라 위치로 인계한다.
        void UpdateLobbyPosition()
        {
            if (!VoiceReady || ActiveChannel == null) return;
            if (Time.unscaledTime < _nextLobbyPositionUpdate) return;
            _nextLobbyPositionUpdate = Time.unscaledTime + LobbyPositionInterval;

            // Set3DPosition은 SDK 내부에서 "채널에 있지 않음" 상황을 예외를 던지지 않고 로그만 남기므로
            // (VivoxServiceInternal.EnsureIsInChannel), 미리 ActiveChannels를 확인해 그 로그 자체를 피한다 —
            // 채널이 실제로는 끊겼는데(RTP Timeout 등) 로컬 상태(VoiceReady/ActiveChannel)가 아직 못
            // 따라잡은 경우를 잡아낸다. ChannelLeft 구독은 이 상태를 더 빨리 반영하기 위한 보조 경로.
            if (VivoxService.Instance == null || !VivoxService.Instance.ActiveChannels.ContainsKey(ActiveChannel))
            {
                HandleLobbyPositionFailure("채널을 이미 이탈한 상태(ActiveChannels에 없음, 예: RTP Timeout)");
                return;
            }

            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            try
            {
                VivoxService.Instance.Set3DPosition(gameObject, ActiveChannel);
            }
            catch (Exception e)
            {
                HandleLobbyPositionFailure(e.Message);
            }
        }

        /// 로비 위치 갱신 실패 처리 — 최초 1회만 경고하고, 보이스 상태를 끊김으로 되돌려
        /// VoiceReady 가드가 다음 프레임부터 자동으로 갱신을 중단하게 한다(반복 시도 방지).
        /// 재조인(JoinVoiceAsync)에 성공하면 _loggedLobbyPositionFailure가 초기화돼 다시 경고할 수 있다.
        void HandleLobbyPositionFailure(string reason)
        {
            VoiceReady = false;
            ActiveChannel = null;
            StatusMessage = "VOICE DISCONNECTED";
            if (_loggedLobbyPositionFailure) return;
            _loggedLobbyPositionFailure = true;
            Debug.LogWarning($"[Voice] 로비 위치 갱신 실패 — 보이스 상태를 끊김으로 되돌리고 갱신 중단: {reason}");
        }

        /// Vivox SDK가 채널 이탈(정상 퇴장 포함)을 알릴 때 호출된다. RTP Timeout 등으로 우리 모르게
        /// 끊긴 경우를 반영해 VoiceReady를 false로 되돌린다 — 이러면 UpdateLobbyPosition뿐 아니라
        /// ToggleMute/SetMuted 등 다른 VoiceReady 소비자도 곧바로 정확한 상태를 보게 된다.
        void OnChannelLeft(string channelName)
        {
            if (channelName != ActiveChannel) return; // 우리가 이미 알고 있는 채널이 아님(정리된 이전 채널 등) — 무시
            VoiceReady = false;
            ActiveChannel = null;
            StatusMessage = "VOICE DISCONNECTED";
            Debug.LogWarning($"[Voice] 채널 연결 끊김 감지({channelName}) — VoiceReady 해제");
        }
    }
}
