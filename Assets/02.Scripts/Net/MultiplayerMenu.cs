using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Core;
using Game.Net;

namespace Game.UI
{
    /// Homescreen 멀티플레이 패널. 씬 직렬화에 의존하지 않고 Awake에서 UGUI 전부를 코드로 생성·배선한다
    /// (jungwon 함정 ③: 에디터에서 미리 걸어둔 AddListener는 씬 저장에 포함되지 않는다).
    /// 기존 Homescreen UI는 건드리지 않고 별도 Canvas(sortingOrder=100) 위에 독립적으로 그린다.
    ///
    /// 이 컴포넌트가 붙는 NetBootstrap GO는 SessionManager.Awake가 DontDestroyOnLoad로 유지하므로
    /// Awake는 앱 수명 동안 단 한 번만 실행된다(Homescreen 재로드로 다시 생성되지 않음). 따라서 씬
    /// 전환/재로드에 따른 표시 상태(패널 활성, 커서, 상태 문구)는 이벤트 구독이 아니라 Update에서
    /// 매 프레임 현재 씬/세션 상태를 다시 읽어 반영한다 — 해제할 구독이 없어 OnDestroy도 불필요하다.
    public class MultiplayerMenu : MonoBehaviour
    {
        const string HomescreenSceneName = "Homescreen";
        const string GameSceneName = "GameScene";
        const int NicknameCharLimit = 20; // NicknameUtil의 61바이트 한도 내 안전 여유(한글 3바이트 x 20 = 60)

        static readonly Color BoneTextColor = new(0.84f, 0.82f, 0.76f, 1f);
        static readonly Color SecondaryTextColor = new(0.62f, 0.60f, 0.56f, 1f);
        static readonly Color ButtonNormalColor = new(0.07f, 0.065f, 0.06f, 0.94f);
        static readonly Color ButtonHighlightColor = new(0.30f, 0.09f, 0.10f, 0.97f);
        static readonly Color ButtonPressedColor = new(0.17f, 0.045f, 0.05f, 1f);
        static readonly Color ButtonBorderColor = new(0.62f, 0.59f, 0.52f, 0.68f);

        static TMP_FontAsset s_koreanFont;
        static TMP_FontAsset s_horrorFont;

        /// TMP 기본 폰트(LiberationSans SDF)는 한글 글리프가 없어 모든 라벨이 □로 깨진다(Play 스모크에서
        /// 실측: "The character with Unicode value ... was not found" 경고 다수). macOS 시스템 폰트(Apple
        /// SD Gothic Neo)로 Dynamic-OS SDF 폰트를 즉석 생성해 대체한다 — 별도 폰트 에셋을 임포트/베이크할
        /// 필요가 없고, 빌드 타깃이 macOS(P12)라 안전하다. 실패 시(다른 OS 등) 기본 폰트로 조용히 폴백.
        /// MicStatusHud(게임 중 마이크 HUD)도 같은 캐시를 재사용한다 — 동적 폰트 생성이 가볍지 않아
        /// 인스턴스마다 새로 만들지 않는다.
        public static TMP_FontAsset KoreanFont
        {
            get
            {
                if (s_koreanFont != null) return s_koreanFont;
                s_koreanFont = TMP_FontAsset.CreateFontAsset("Apple SD Gothic Neo", "Regular", 90);
                if (s_koreanFont == null)
                {
                    Debug.LogWarning("[MultiplayerMenu] 한글 시스템 폰트를 찾지 못해 기본 폰트로 대체 — 한글이 깨질 수 있음.");
                    s_koreanFont = TMP_Settings.defaultFontAsset;
                }
                return s_koreanFont;
            }
        }

        public static TMP_FontAsset HorrorFont
        {
            get
            {
                if (s_horrorFont != null) return s_horrorFont;
                var sourceFont = Resources.Load<Font>("Fonts/LibreBaskerville-Regular");
                if (sourceFont != null)
                {
                    s_horrorFont = TMP_FontAsset.CreateFontAsset(sourceFont);
                    if (s_horrorFont != null)
                        s_horrorFont.name = $"{sourceFont.name} SDF";
                }

                if (s_horrorFont == null)
                {
                    Debug.LogWarning("[MultiplayerMenu] Libre Baskerville was not found; using the system fallback.");
                    return KoreanFont;
                }

                var fallback = KoreanFont;
                if (fallback != null && s_horrorFont.fallbackFontAssetTable != null &&
                    !s_horrorFont.fallbackFontAssetTable.Contains(fallback))
                    s_horrorFont.fallbackFontAssetTable.Add(fallback);
                return s_horrorFont;
            }
        }

        GameObject _canvasGo;
        GameObject _panel;
        GameObject _mainActionsGroup;
        GameObject _createRoomGroup;
        GameObject _joinRoomGroup;
        GameObject _sessionGroup;
        GameObject _eventSystemGo;

        TMP_InputField _createNicknameInput;
        TMP_InputField _joinNicknameInput;
        TMP_InputField _codeInput;
        TMP_Text _status;
        TMP_Text _roomCodeText;
        TMP_Text _playerCountText;
        TMP_Text _playerListText;
        TMP_Text _waitingText;
        TMP_Text _micStatusText;

        Button _showCreateBtn, _showJoinBtn, _quitBtn, _createBtn, _joinBtn, _createBackBtn, _joinBackBtn;
        Button _copyCodeBtn, _startBtn, _leaveBtn;

        bool _busy;
        bool _showedNotReadyMsg;
        HomeMenuView _requestedView = HomeMenuView.Main;

        // ---------- 로비 표시 상태 ----------
        // LobbyChanged 구독 + 1초 폴링 안전망(값이 실제로 바뀐 경우에만 다시 그린다).
        // MultiplayerMenu는 앱 수명 동안 파괴되지 않으므로(클래스 상단 주석 참고) 구독 해제가 불필요하다.
        bool _lobbySubscribed;
        bool _lobbyDirty;
        float _nextLobbyPoll;
        string _lastJoinCode;
        int _lastPlayerCount = -1;
        bool _lastIsHost;
        string _lastPlayerNamesJoined;

        void Awake()
        {
            if (SceneManager.GetActiveScene().name != HomescreenSceneName) return;
            BuildUi();
        }

        void Update()
        {
            if (_canvasGo == null) return; // Awake에서 스킵된 경우(Homescreen 밖에서 생성) — 할 일 없음

            bool onHome = SceneManager.GetActiveScene().name == HomescreenSceneName;
            if (_canvasGo.activeSelf != onHome)
            {
                _canvasGo.SetActive(onHome);
                if (onHome) UnlockCursor(); // 게임 씬(커서 잠금)에서 돌아온 직후 — 메인 메뉴를 즉시 조작할 수 있게 한다.
                else _busy = false; // 씬을 벗어나는 시점에 리셋 — OnStartClicked 성공 경로가 busy를 풀지 않으므로
                                     // 여기서 안 풀면 이후 세션 종료→Homescreen 복귀 시 패널이 영구 먹통이 된다.
                                     // 성공 직후 곧바로 풀면 씬 전환 전 이중 클릭 창이 생기므로, 씬 이탈 시점이 안전하다.
            }
            if (!onHome) return;

            EnsureEventSystem();
            EnsureLobbySubscription();

            bool ready = GameBootstrap.IsReady;
            bool inSession = SessionManager.Instance != null && SessionManager.Instance.InSession;
            bool isHost = SessionManager.Instance != null && SessionManager.Instance.IsHost;

            var view = HomeMenuState.Select(inSession, _requestedView);
            _mainActionsGroup.SetActive(view == HomeMenuView.Main);
            _createRoomGroup.SetActive(view == HomeMenuView.CreateRoom);
            _joinRoomGroup.SetActive(view == HomeMenuView.JoinRoom);
            _sessionGroup.SetActive(view == HomeMenuView.Lobby);
            _quitBtn.gameObject.SetActive(HomeMenuState.ShowsQuitAction(view));
            _copyCodeBtn.gameObject.SetActive(inSession && !string.IsNullOrEmpty(SessionManager.Instance?.JoinCode));
            _startBtn.gameObject.SetActive(isHost);
            _waitingText.gameObject.SetActive(!isHost);

            if (!_busy)
            {
                _showCreateBtn.interactable = ready;
                _showJoinBtn.interactable = ready;
                _quitBtn.interactable = true;
                _createBtn.interactable = ready;
                _joinBtn.interactable = ready;
                _createBackBtn.interactable = ready;
                _joinBackBtn.interactable = ready;
                _leaveBtn.interactable = ready;
                _startBtn.interactable = ready;

                if (!ready)
                {
                    _status.text = GameBootstrap.StatusMessage;
                    _showedNotReadyMsg = true;
                }
                else if (_showedNotReadyMsg)
                {
                    // Play 스모크로 실측한 버그: ready로 바뀐 뒤에도 아무도 상태 문구를 지우지 않아
                    // "서비스 초기화 중..."이 영구히 남아 있었다(버튼은 이미 활성인데 문구만 낡음).
                    // 딱 이 전환 프레임에서 한 번만 안내 문구로 교체하고, 이후엔 다시 건드리지 않는다.
                    _status.text = "Enter a nickname, then create or join a room.";
                    _showedNotReadyMsg = false;
                }

                if (ready && !string.IsNullOrEmpty(SessionManager.LastEndReason))
                {
                    _requestedView = HomeMenuView.Main;
                    _status.text = SessionManager.LastEndReason;
                    SessionManager.LastEndReason = null;
                }
            }

            if (inSession) { UpdateLobbyDisplay(); UpdateMicStatusText(); }
            else ResetLobbySnapshot(); // 세션 밖에서는 다음 진입 시 즉시 다시 그리도록 캐시를 무효화
        }

        /// 로비 마이크 상태 한 줄 안내. VoiceReady가 아니면(연결 중/끊김/사용 불가) 조용히 무시하지 않고
        /// VoiceManager.StatusMessage로 이유를 그대로 보여준다 — M키를 눌러도 반응이 없을 때 원인을
        /// 알 수 있게 하기 위함(ToggleMute는 VoiceReady==false면 아무것도 하지 않고 무시한다).
        void UpdateMicStatusText()
        {
            if (_micStatusText == null) return;
            var voice = Game.Voice.VoiceManager.Instance;
            if (voice == null)
            {
                _micStatusText.text = "Voice unavailable";
                _micStatusText.color = SecondaryTextColor;
                return;
            }
            if (!voice.VoiceReady)
            {
                _micStatusText.text = voice.StatusMessage;
                _micStatusText.color = SecondaryTextColor;
                return;
            }
            if (voice.IsMuted)
            {
                _micStatusText.text = "Mic muted  [M: unmute]";
                _micStatusText.color = new Color(0.66f, 0.32f, 0.31f, 1f);
            }
            else
            {
                _micStatusText.text = "Mic live  [M: mute]";
                _micStatusText.color = BoneTextColor;
            }
        }

        // ---------- 로비 표시 ----------

        void EnsureLobbySubscription()
        {
            if (_lobbySubscribed || SessionManager.Instance == null) return;
            SessionManager.Instance.LobbyChanged += OnLobbyChanged;
            _lobbySubscribed = true;
        }

        void OnLobbyChanged() => _lobbyDirty = true;

        void ResetLobbySnapshot()
        {
            _lastJoinCode = null;
            _lastPlayerCount = -1;
        }

        void UpdateLobbyDisplay()
        {
            // 이벤트 유실에 대비한 안전망 — 1초마다 실제 값이 바뀌었는지 확인하고, 바뀐 경우에만 다시 그린다.
            if (Time.unscaledTime >= _nextLobbyPoll)
            {
                _nextLobbyPoll = Time.unscaledTime + 1f;
                if (LobbySnapshotChanged()) _lobbyDirty = true;
            }

            if (!_lobbyDirty) return;
            _lobbyDirty = false;
            RedrawLobby();
        }

        bool LobbySnapshotChanged()
        {
            var sm = SessionManager.Instance;
            if (sm == null) return false;

            string code = sm.JoinCode;
            int count = sm.PlayerCount;
            bool host = sm.IsHost;
            string namesJoined = string.Join("|", sm.PlayerNames);

            bool changed = code != _lastJoinCode || count != _lastPlayerCount ||
                           host != _lastIsHost || namesJoined != _lastPlayerNamesJoined;

            _lastJoinCode = code;
            _lastPlayerCount = count;
            _lastIsHost = host;
            _lastPlayerNamesJoined = namesJoined;
            return changed;
        }

        void RedrawLobby()
        {
            var sm = SessionManager.Instance;
            if (sm == null) return;

            _roomCodeText.text = $"Room Code: {sm.JoinCode}";
            _playerCountText.text = $"Players {sm.PlayerCount}/{SessionManager.MaxPlayers}";

            var names = sm.PlayerNames;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(names[i]);
                if (i == 0) sb.Append("  [Host]"); // PlayerNames는 항상 호스트를 맨 앞에 둔다
            }
            _playerListText.text = sb.ToString();
        }

        // ---------- 버튼 핸들러 ----------

        public static HomeMenuView VisibleViewForTests(bool inSession, HomeMenuView requested)
            => HomeMenuState.Select(inSession, requested);

        void ShowCreateRoom() => _requestedView = HomeMenuView.CreateRoom;
        void ShowJoinRoom() => _requestedView = HomeMenuView.JoinRoom;
        void ShowMainActions() => _requestedView = HomeMenuView.Main;

        void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void CopyJoinCodeForTests()
        {
            var code = SessionManager.Instance?.JoinCode;
            if (string.IsNullOrEmpty(code)) return;
            GUIUtility.systemCopyBuffer = code;
            SetIdle("Room code copied.");
        }

        public async void OnCreateClicked()
        {
            if (_busy) return;
            SetBusy("Creating room...");
            try
            {
                await SessionManager.Instance.CreateRoomAsync(_createNicknameInput.text);
                // 성공: 씬 전환 없이 로비에서 대기한다(Homescreen 유지). InSession=true가 되면
                // 다음 Update에서 세션 패널(방 코드/인원/참가자 목록/게임 시작)이 열린다.
                if (this) _busy = false;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this) SetIdle(SessionManager.MessageFor(e));
            }
        }

        public async void OnJoinClicked()
        {
            if (_busy) return;
            var code = NormalizeCode(_codeInput.text);
            if (string.IsNullOrEmpty(code))
            {
                _status.text = "Enter a room code.";
                return;
            }

            SetBusy("Joining room...");
            try
            {
                await SessionManager.Instance.JoinRoomAsync(code, _joinNicknameInput.text);
                if (this) _busy = false; // 씬 동기화는 NGO가 자동 처리 — 완료 전까지는 세션 패널이 대기 상태를 보여준다
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this) SetIdle(SessionManager.MessageFor(e));
            }
        }

        public async void OnStartClicked()
        {
            if (_busy)
            {
                Debug.LogWarning("[MultiplayerMenu] OnStartClicked 무시 — 이미 다른 작업 처리 중(_busy=true)");
                return;
            }
            if (SessionManager.Instance == null || !SessionManager.Instance.IsHost)
            {
                _status.text = "Only the host can start the game.";
                Debug.LogWarning("[MultiplayerMenu] OnStartClicked 무시 — Session host가 아님");
                return;
            }

            _busy = true;
            _status.text = "Starting game...";
            try
            {
                GameStartTransition.Begin();
                await LobbyCorridorPreview.ReleaseForGameStartAsync();
                await SessionManager.Instance.StartGameNetworkAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                GameStartTransition.Cancel();
                if (this) SetIdle(SessionManager.MessageFor(e));
                return;
            }

            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
            {
                GameStartTransition.Cancel();
                if (this) SetIdle("Game network failed to start.");
                return;
            }

            var status = NetworkManager.Singleton.SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                GameStartTransition.Cancel();
                _busy = false;
                _status.text = $"Game start failed. ({status})";
                Debug.LogWarning($"[MultiplayerMenu] 게임 씬 로드 실패: {status}");
            }
            // 성공 시: 곧 씬이 바뀌며 다음 Update가 캔버스를 스스로 비활성화한다 — busy를 따로 풀 필요 없음.
        }

        public async void OnLeaveClicked()
        {
            if (_busy) return;
            SetBusy("Leaving room...");
            try
            {
                await SessionManager.Instance.LeaveRoomAsync();
                if (this) SetIdle("Left the room.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this) SetIdle(SessionManager.MessageFor(e));
            }
        }

        static string NormalizeCode(string raw) => (raw ?? string.Empty).Trim().ToUpperInvariant();

        void SetBusy(string msg)
        {
            _busy = true;
            _showCreateBtn.interactable = false;
            _showJoinBtn.interactable = false;
            _createBtn.interactable = false;
            _joinBtn.interactable = false;
            _createBackBtn.interactable = false;
            _joinBackBtn.interactable = false;
            _leaveBtn.interactable = false;
            _startBtn.interactable = false;
            _status.text = msg;
        }

        void SetIdle(string msg)
        {
            _busy = false;
            if (_status) _status.text = msg;
        }

        static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// Homescreen에 EventSystem이 없으면 하나 만든다. 일부러 DontDestroyOnLoad 하지 않는다 —
        /// GameScene에는 이미 자체 EventSystem이 있으므로, 씬 로컬로 둬야 Homescreen 언로드 시
        /// Unity가 자동으로 정리해 "2개의 EventSystem" 중복/경고를 피할 수 있다. Homescreen이
        /// 재로드되면(세션 종료 복귀) 이 참조도 함께 파괴되므로 다음 Update에서 다시 만든다.
        void EnsureEventSystem()
        {
            if (_eventSystemGo != null)
            {
                // GameScene을 로비 배경으로 additive 로드하면 그 씬의 EventSystem이 잠시 current가 된 뒤
                // LobbyCorridorPreview에 의해 비활성화될 수 있다. 이때 메뉴용 EventSystem 오브젝트가 이미
                // 있다는 이유로 바로 반환하면 current는 비활성 시스템을 계속 가리켜 UI가 보이기만 하고
                // 클릭되지 않는다. Homescreen에서 Update가 호출될 때 메뉴 시스템을 다시 복구한다.
                if (!_eventSystemGo.activeSelf) _eventSystemGo.SetActive(true);

                var menuSystem = _eventSystemGo.GetComponent<EventSystem>();
                var menuInput = _eventSystemGo.GetComponent<BaseInputModule>();
                if (menuSystem != null) menuSystem.enabled = true;
                if (menuInput != null) menuInput.enabled = true;
                if (Application.isPlaying && menuSystem != null && EventSystem.current != menuSystem)
                    EventSystem.current = menuSystem;
                return;
            }

            if (EventSystem.current != null && EventSystem.current.isActiveAndEnabled)
                return; // 다른 주체가 이미 정상 동작 중인 하나를 갖고 있음 — 중복 생성 금지

            _eventSystemGo = new GameObject("MultiplayerMenu_EventSystem",
                typeof(EventSystem), typeof(StandaloneInputModule));
            if (Application.isPlaying)
                EventSystem.current = _eventSystemGo.GetComponent<EventSystem>();
        }

        // ---------- UI 빌드 ----------

        void BuildUi()
        {
            _canvasGo = new GameObject("MultiplayerMenuCanvas", typeof(RectTransform));
            _canvasGo.transform.SetParent(transform, false); // NetBootstrap(DDOL) 자식 — 씬 전환에도 유지, Update가 표시만 토글

            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            // 4K(3840x2160)와 1080p 양쪽에서 패널이 비슷한 비율로 보이도록 ScaleWithScreenSize +
            // referenceResolution 1920x1080 + matchWidthOrHeight 0.5(폭/높이 절반씩 반영)를 유지한다.
            // 아래 패널 크기(600 등)는 전부 이 기준 해상도 단위다.
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            _canvasGo.AddComponent<GraphicRaycaster>();

            // 패널은 배경이나 외곽선 없이 현재 메뉴 상태의 레이아웃만 소유한다.
            _panel = new GameObject("Panel", typeof(RectTransform));
            _panel.transform.SetParent(_canvasGo.transform, false);
            var panelRt = _panel.GetComponent<RectTransform>();
            SetAnchor(panelRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            panelRt.sizeDelta = new Vector2(600, 0);
            panelRt.anchoredPosition = new Vector2(0f, -150f);

            var panelLayout = _panel.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(32, 32, 28, 28);
            panelLayout.spacing = 10;
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            var panelFitter = _panel.AddComponent<ContentSizeFitter>();
            panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // --- 시작 화면: 제목 아래의 두 직접 진입 버튼 ---
            _mainActionsGroup = CreateGroup(_panel.transform, "MainActions");
            _showCreateBtn = CreateButton(_mainActionsGroup.transform, "ShowCreateButton", "Create Room");
            AddHeight(_showCreateBtn.gameObject, 52);
            _showCreateBtn.onClick.AddListener(ShowCreateRoom);
            _showJoinBtn = CreateButton(_mainActionsGroup.transform, "ShowJoinButton", "Join Room");
            AddHeight(_showJoinBtn.gameObject, 52);
            _showJoinBtn.onClick.AddListener(ShowJoinRoom);
            _quitBtn = CreateButton(_mainActionsGroup.transform, "QuitButton", "Quit");
            AddHeight(_quitBtn.gameObject, 46);
            _quitBtn.onClick.AddListener(QuitGame);

            // --- 방 만들기 카드 ---
            _createRoomGroup = CreateGroup(_panel.transform, "CreateRoomCard");
            _createRoomGroup.SetActive(false);
            CreateLabel(_createRoomGroup.transform, "CreateTitle", "Create a New Room", 24, FontStyles.Bold, 34);
            _createNicknameInput = CreateInputField(_createRoomGroup.transform, "CreateNicknameInput", "Nickname", 44);
            _createNicknameInput.characterLimit = NicknameCharLimit;
            _createBtn = CreateButton(_createRoomGroup.transform, "CreateButton", "Create Room");
            AddHeight(_createBtn.gameObject, 44);
            _createBtn.onClick.AddListener(OnCreateClicked);
            _createBackBtn = CreateButton(_createRoomGroup.transform, "CreateBackButton", "Back");
            AddHeight(_createBackBtn.gameObject, 40);
            _createBackBtn.onClick.AddListener(ShowMainActions);

            // --- 방 참여 카드 ---
            _joinRoomGroup = CreateGroup(_panel.transform, "JoinRoomCard");
            _joinRoomGroup.SetActive(false);
            CreateLabel(_joinRoomGroup.transform, "JoinTitle", "Join a Room", 24, FontStyles.Bold, 34);
            _joinNicknameInput = CreateInputField(_joinRoomGroup.transform, "JoinNicknameInput", "Nickname", 44);
            _joinNicknameInput.characterLimit = NicknameCharLimit;
            _codeInput = CreateInputField(_joinRoomGroup.transform, "CodeInput", "Room Code", 44);
            _joinBtn = CreateButton(_joinRoomGroup.transform, "JoinButton", "Join Room");
            AddHeight(_joinBtn.gameObject, 44);
            _joinBtn.onClick.AddListener(OnJoinClicked);
            _joinBackBtn = CreateButton(_joinRoomGroup.transform, "JoinBackButton", "Back");
            AddHeight(_joinBackBtn.gameObject, 40);
            _joinBackBtn.onClick.AddListener(ShowMainActions);

            // --- 세션 중 그룹 (방 만들기/입장 전엔 숨김) ---
            _sessionGroup = CreateGroup(_panel.transform, "SessionGroup");
            _sessionGroup.SetActive(false);

            // 방 코드 — 다른 참가자에게 불러줘야 하는 값이라 패널에서 가장 크고 눈에 띄게 표시한다.
            _roomCodeText = CreateLabel(_sessionGroup.transform, "RoomCodeText", "Room Code: ------", 44, FontStyles.Bold, 60);
            _roomCodeText.color = BoneTextColor;
            if (_roomCodeText is TextMeshProUGUI roomCodeTmp)
            {
                roomCodeTmp.characterSpacing = 3f;
                roomCodeTmp.textWrappingMode = TextWrappingModes.NoWrap;
                roomCodeTmp.enableAutoSizing = true;
                roomCodeTmp.fontSizeMin = 30f;
                roomCodeTmp.fontSizeMax = 44f;
                roomCodeTmp.overflowMode = TextOverflowModes.Overflow;
            }

            _copyCodeBtn = CreateButton(_sessionGroup.transform, "CopyCodeButton", "Copy Room Code");
            AddHeight(_copyCodeBtn.gameObject, 44);
            _copyCodeBtn.onClick.AddListener(CopyJoinCodeForTests);

            // 인원/참가자 목록은 방 코드 바로 아래, 더 작은 크기로 명확히 구분해 표시한다.
            _playerCountText = CreateLabel(_sessionGroup.transform, "PlayerCountText", $"Players 0/{SessionManager.MaxPlayers}", 20, FontStyles.Normal, 28);

            _playerListText = CreateLabel(_sessionGroup.transform, "PlayerListText", "", 18, FontStyles.Normal, 100);
            _playerListText.alignment = TextAlignmentOptions.TopLeft;

            // 마이크 상태 + M키 안내 한 줄 — 보이스 채널은 세션 참가 시점에 조인되므로 세션 중 그룹에 둔다.
            _micStatusText = CreateLabel(_sessionGroup.transform, "MicStatusText", "Checking microphone...", 15, FontStyles.Normal, 22);
            _micStatusText.alignment = TextAlignmentOptions.Left;

            // 참가자에게는 시작 버튼 대신 이 안내를 보여준다(호스트 재량으로 언제든 시작 가능).
            _waitingText = CreateLabel(_sessionGroup.transform, "WaitingText", "Waiting for host", 16, FontStyles.Italic, 40);
            _waitingText.color = SecondaryTextColor;

            // 세로 공간을 줄이고 주요 동작을 한눈에 비교할 수 있도록 한 줄에 둔다.
            // 참가자에게 StartButton이 숨겨지면 HorizontalLayoutGroup이 LeaveButton을 전체 폭으로 확장한다.
            var sessionActions = CreateHorizontalGroup(_sessionGroup.transform, "SessionActions");
            _leaveBtn = CreateButton(sessionActions.transform, "LeaveButton", "Leave Room");
            AddHeight(_leaveBtn.gameObject, 44);
            AddFlexibleWidth(_leaveBtn.gameObject, 1f);
            _leaveBtn.onClick.AddListener(OnLeaveClicked);

            _startBtn = CreateButton(sessionActions.transform, "StartButton", "Start Game");
            AddHeight(_startBtn.gameObject, 44);
            AddFlexibleWidth(_startBtn.gameObject, 1f);
            _startBtn.onClick.AddListener(OnStartClicked);

            // --- 상태 라벨(항상 표시) ---
            _status = CreateLabel(_panel.transform, "StatusText", "Enter a nickname, then create or join a room.", 18, FontStyles.Normal, 48);
            _status.color = SecondaryTextColor;

            var keyboardShortcuts = _canvasGo.AddComponent<MenuKeyboardShortcuts>();
            keyboardShortcuts.Configure(
                _showCreateBtn, _showJoinBtn, _quitBtn,
                _createNicknameInput, _createBtn, _createBackBtn,
                _joinNicknameInput, _codeInput, _joinBtn, _joinBackBtn,
                _copyCodeBtn, _leaveBtn, _startBtn);

            _panel.SetActive(true);
        }

        static GameObject CreateGroup(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return go;
        }

        static GameObject CreateHorizontalGroup(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return go;
        }

        static void AddHeight(GameObject go, float h)
        {
            var le = go.GetComponent<LayoutElement>();
            if (!le) le = go.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            le.minHeight = h;
        }

        static void AddFlexibleWidth(GameObject go, float width)
        {
            var le = go.GetComponent<LayoutElement>();
            if (!le) le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = width;
        }

        static void SetAnchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = pivot;
        }

        static Button CreateButton(Transform parent, string name, string label)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = Color.white;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = ButtonNormalColor;
            colors.highlightedColor = ButtonHighlightColor;
            colors.selectedColor = ButtonHighlightColor;
            colors.pressedColor = ButtonPressedColor;
            colors.disabledColor = new Color(0.08f, 0.075f, 0.07f, 0.42f);
            colors.fadeDuration = 0.10f;
            btn.colors = colors;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = ButtonBorderColor;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.font = HorrorFont;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 21;
            text.color = BoneTextColor;
            text.raycastTarget = false;

            return btn;
        }

        static TMP_InputField CreateInputField(Transform parent, string name, string placeholder, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.045f, 0.042f, 0.038f, 0.94f);

            var input = go.AddComponent<TMP_InputField>();
            input.lineType = TMP_InputField.LineType.SingleLine;

            var viewportGo = new GameObject("Text Area", typeof(RectTransform));
            var viewportRt = (RectTransform)viewportGo.transform;
            viewportRt.SetParent(go.transform, false);
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = new Vector2(10, 6);
            viewportRt.offsetMax = new Vector2(-10, -6);
            viewportGo.AddComponent<RectMask2D>();

            var textGo = new GameObject("Text", typeof(RectTransform));
            var textRt = (RectTransform)textGo.transform;
            textRt.SetParent(viewportRt, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var textComp = textGo.AddComponent<TextMeshProUGUI>();
            textComp.font = HorrorFont;
            textComp.fontSize = 20;
            textComp.color = BoneTextColor;
            textComp.alignment = TextAlignmentOptions.MidlineLeft;
            textComp.textWrappingMode = TextWrappingModes.NoWrap;

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
            var placeholderRt = (RectTransform)placeholderGo.transform;
            placeholderRt.SetParent(viewportRt, false);
            placeholderRt.anchorMin = Vector2.zero;
            placeholderRt.anchorMax = Vector2.one;
            placeholderRt.offsetMin = Vector2.zero;
            placeholderRt.offsetMax = Vector2.zero;
            var placeholderComp = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholderComp.text = placeholder;
            placeholderComp.font = HorrorFont;
            placeholderComp.fontSize = 20;
            placeholderComp.fontStyle = FontStyles.Italic;
            placeholderComp.color = new Color(0.62f, 0.60f, 0.56f, 0.60f);
            placeholderComp.alignment = TextAlignmentOptions.MidlineLeft;
            placeholderComp.textWrappingMode = TextWrappingModes.NoWrap;

            input.textViewport = viewportRt;
            input.textComponent = textComp;
            input.placeholder = placeholderComp;

            AddHeight(go, height);
            return input;
        }

        static TMP_Text CreateLabel(Transform parent, string name, string initial, int fontSize, FontStyles style, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = initial;
            text.font = HorrorFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = BoneTextColor;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            AddHeight(go, height);
            return text;
        }
    }
}
