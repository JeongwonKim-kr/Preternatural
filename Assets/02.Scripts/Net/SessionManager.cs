using System;
using System.Collections.Generic;
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
        const string MenuScene = "Homescreen";
        const string NamePropertyKey = "name";

        public ISession ActiveSession { get; private set; }
        public string JoinCode => ActiveSession?.Code;
        public bool InSession => ActiveSession != null;

        /// 로비 대기실 상태 — UI가 Sessions API를 직접 다루지 않도록 하는 경계.
        public bool IsHost => ActiveSession?.IsHost ?? false;
        public int PlayerCount => ActiveSession?.PlayerCount ?? 0;

        /// 참가자 이름 목록, 호스트가 맨 앞. 이름 프로퍼티가 없거나 비어 있으면 "플레이어 N"(N=1부터,
        /// Players 순회 순번)으로 대체한다.
        public IReadOnlyList<string> PlayerNames
        {
            get
            {
                var session = ActiveSession;
                if (session?.Players == null) return Array.Empty<string>();

                string hostId = session.Host;
                string hostName = null;
                var others = new List<string>(session.Players.Count);
                int n = 0;
                foreach (var player in session.Players)
                {
                    n++;
                    string name = null;
                    if (player.Properties != null &&
                        player.Properties.TryGetValue(NamePropertyKey, out var prop))
                        name = prop.Value;
                    if (string.IsNullOrWhiteSpace(name)) name = $"플레이어 {n}";

                    if (player.Id == hostId) hostName = name;
                    else others.Add(name);
                }
                if (hostName != null) others.Insert(0, hostName);
                return others;
            }
        }

        /// 세션 확립 직후 발생. 인자 = 보이스 채널명으로 쓸 세션 Id.
        public event Action<string> SessionStarted;
        public event Action SessionEnded;

        /// 로비 인원/이름 변동 통지 — PlayerJoined/PlayerHasLeft/PlayerPropertiesChanged/Changed를 묶는다.
        public event Action LobbyChanged;

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
            var options = new SessionOptions
            {
                MaxPlayers = MaxPlayers,
                PlayerProperties = new Dictionary<string, PlayerProperty>
                {
                    [NamePropertyKey] = new PlayerProperty(LocalNickname, VisibilityPropertyOptions.Public)
                }
            }.WithRelayNetwork();
            var session = await MultiplayerService.Instance.CreateSessionAsync(options);
            Hook(session);
            // 씬 전환 없음 — 방 생성 직후엔 로비에서 대기한다(Homescreen 유지).
            // 호스트가 [게임 시작]을 눌러야 NetworkManager.SceneManager.LoadScene이 호출된다.
            return session.Code;
        }

        public async Task JoinRoomAsync(string normalizedCode, string nickname)
        {
            LocalNickname = string.IsNullOrWhiteSpace(nickname) ? "무명" : nickname.Trim();
            var options = new JoinSessionOptions
            {
                PlayerProperties = new Dictionary<string, PlayerProperty>
                {
                    [NamePropertyKey] = new PlayerProperty(LocalNickname, VisibilityPropertyOptions.Public)
                }
            };
            var session = await MultiplayerService.Instance.JoinSessionByCodeAsync(normalizedCode, options);
            Hook(session);
            // 씬 이동은 NGO 씬 동기화가 자동 처리 (호스트가 게임 시작을 누른 뒤)
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
            session.PlayerJoined += OnLobbyPlayerChanged;
            session.PlayerHasLeft += OnLobbyPlayerChanged;
            session.PlayerPropertiesChanged += OnLobbyChanged;
            session.Changed += OnLobbyChanged;
            NetworkManager.Singleton.OnClientStopped += OnClientStopped;
            SessionStarted?.Invoke(session.Id);
        }

        void OnLobbyPlayerChanged(string playerId) => LobbyChanged?.Invoke();
        void OnLobbyChanged() => LobbyChanged?.Invoke();

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
                session.PlayerJoined -= OnLobbyPlayerChanged;
                session.PlayerHasLeft -= OnLobbyPlayerChanged;
                session.PlayerPropertiesChanged -= OnLobbyChanged;
                session.Changed -= OnLobbyChanged;
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
            // "같은 기기 두 번 실행" 버그 리포트의 실제 예외 메시지가 "player is already a member of the
            // lobby"였다 — se.Error가 LobbyAlreadyExists로 정확히 오는 경우는 아래 기본 경로가 이미
            // 처리하지만, SDK가 이 케이스도 Unknown으로 뭉갤 가능성에 대비해 메시지 문자열로도 식별한다.
            if (se.Message != null && se.Message.IndexOf("already a member", StringComparison.OrdinalIgnoreCase) >= 0)
                return SessionErrorMessages.For("LobbyAlreadyExists");
            return SessionErrorMessages.For(se.Error.ToString());
        }
    }
}
