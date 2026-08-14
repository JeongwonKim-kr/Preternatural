using System;
using System.Threading.Tasks;
using Game.Core;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Net
{
    /// 방(세션) 수명 관리. Sessions SDK가 Relay 연결과 NGO 시작/종료를 소유하므로
    /// NetworkManager.StartHost/StartClient는 절대 직접 호출하지 않는다.
    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }
        public static string LocalNickname = "무명";

        /// 세션이 끝난 사유 — 메뉴 복귀 후 1회 표시하고 소거한다
        public static string LastEndReason;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // 에디터 'Enter Play Mode without Domain Reload' 대응
            LastEndReason = null;
        }

        public const int MaxPlayers = 4;
        const string GameScene = "GameScene";
        const string MenuScene = "Homescreen";

        public ISession ActiveSession { get; private set; }
        public string JoinCode => ActiveSession?.Code;
        public bool InSession => ActiveSession != null;

        /// 세션 확립 직후 발생. 인자 = 보이스 채널명으로 쓸 세션 Id.
        public event Action<string> SessionStarted;
        public event Action SessionEnded;

        bool _ending;

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public async Task<string> CreateRoomAsync(string nickname)
        {
            LocalNickname = string.IsNullOrWhiteSpace(nickname) ? "무명" : nickname.Trim();
            var options = new SessionOptions { MaxPlayers = MaxPlayers }.WithRelayNetwork();
            var session = await MultiplayerService.Instance.CreateSessionAsync(options);
            Hook(session);

            var status = NetworkManager.Singleton.SceneManager.LoadScene(GameScene, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
                Debug.LogError($"[Session] 게임 씬 로드 실패: {status}");
            return session.Code;
        }

        public async Task JoinRoomAsync(string normalizedCode, string nickname)
        {
            LocalNickname = string.IsNullOrWhiteSpace(nickname) ? "무명" : nickname.Trim();
            var session = await MultiplayerService.Instance.JoinSessionByCodeAsync(normalizedCode);
            Hook(session);
            // 씬 이동은 NGO 씬 동기화가 자동 처리
        }

        public async Task LeaveRoomAsync()
        {
            if (_ending || ActiveSession == null) return;
            await EndSessionAsync();
        }

        void Hook(ISession session)
        {
            ActiveSession = session;
            _ending = false;
            session.Deleted += OnRemoteEnded;            // 호스트가 방을 닫음
            session.RemovedFromSession += OnRemoteEnded; // 추방/강제 종료
            NetworkManager.Singleton.OnClientStopped += OnClientStopped;
            SessionStarted?.Invoke(session.Id);
        }

        void OnRemoteEnded()
        {
            if (!_ending) LastEndReason = "방이 닫혔습니다.";
            _ = EndSessionAsync();
        }

        void OnClientStopped(bool wasHost)
        {
            if (!_ending) LastEndReason = "연결이 끊어졌습니다.";
            _ = EndSessionAsync();
        }

        async Task EndSessionAsync()
        {
            if (_ending) return;
            _ending = true;

            var session = ActiveSession;
            ActiveSession = null;

            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientStopped -= OnClientStopped;

            if (session != null)
            {
                session.Deleted -= OnRemoteEnded;
                session.RemovedFromSession -= OnRemoteEnded;
                try { await session.LeaveAsync(); }
                catch (Exception e) { Debug.LogWarning($"[Session] LeaveAsync: {e.Message}"); }
            }

            SessionEnded?.Invoke();

            if (SceneManager.GetActiveScene().name != MenuScene)
                SceneManager.LoadScene(MenuScene);
        }

        public static string MessageFor(Exception e)
        {
            if (e is SessionException se) return MapSessionError(se);
            if (e is AggregateException ae && ae.InnerException is SessionException ise)
                return MapSessionError(ise);
            return "연결에 실패했습니다. 인터넷 상태를 확인해 주세요.";
        }

        static string MapSessionError(SessionException se)
        {
            // 정원 초과는 SDK가 SessionError.Unknown으로 뭉개므로 백엔드 메시지로 식별
            // (취약: 백엔드 문구가 바뀌면 기본 메시지로 떨어짐 — Task 9에서 실검증)
            if (se.Error == SessionError.Unknown && se.Message != null &&
                se.Message.IndexOf("full", StringComparison.OrdinalIgnoreCase) >= 0)
                return SessionErrorMessages.For("SessionFull");
            return SessionErrorMessages.For(se.Error.ToString());
        }
    }
}
