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

        static TMP_FontAsset s_koreanFont;

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

        GameObject _canvasGo;
        GameObject _panel;
        GameObject _joinCreateGroup;
        GameObject _sessionGroup;
        GameObject _eventSystemGo;

        TMP_InputField _nickInput;
        TMP_InputField _codeInput;
        TMP_Text _status;
        TMP_Text _roomCodeText;
        TMP_Text _playerCountText;
        TMP_Text _playerListText;
        TMP_Text _waitingText;
        TMP_Text _micStatusText;

        Button _openBtn, _createBtn, _joinBtn, _startBtn, _leaveBtn;

        bool _busy;
        bool _panelOpen;
        bool _showedNotReadyMsg;

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
                if (onHome) UnlockCursor(); // 게임 씬(커서 잠금)에서 돌아온 직후 — 안 풀면 토글 버튼조차 못 누른다(jungwon 함정 ⑧)
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

            _joinCreateGroup.SetActive(!inSession);
            _sessionGroup.SetActive(inSession);
            _startBtn.gameObject.SetActive(isHost);
            _waitingText.gameObject.SetActive(!isHost);

            if (!_busy)
            {
                _createBtn.interactable = ready;
                _joinBtn.interactable = ready;
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
                    _status.text = "닉네임 입력 후 방을 만들거나 코드로 입장하세요.";
                    _showedNotReadyMsg = false;
                }

                if (ready && !string.IsNullOrEmpty(SessionManager.LastEndReason))
                {
                    if (!_panelOpen) SetPanelOpen(true); // 접혀 있어도 종료 사유는 놓치지 않도록 자동으로 펼침
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
                _micStatusText.text = "보이스 매니저 없음";
                _micStatusText.color = new Color(1f, 0.6f, 0.3f);
                return;
            }
            if (!voice.VoiceReady)
            {
                _micStatusText.text = voice.StatusMessage;
                _micStatusText.color = new Color(1f, 0.6f, 0.3f);
                return;
            }
            if (voice.IsMuted)
            {
                _micStatusText.text = "마이크 음소거 중 (M: 해제)";
                _micStatusText.color = new Color(1f, 0.4f, 0.4f);
            }
            else
            {
                _micStatusText.text = "마이크 켜짐 (M: 음소거)";
                _micStatusText.color = new Color(0.7f, 0.85f, 0.7f);
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

            _roomCodeText.text = $"방 코드: {sm.JoinCode}";
            _playerCountText.text = $"인원 {sm.PlayerCount}/{SessionManager.MaxPlayers}";

            var names = sm.PlayerNames;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(names[i]);
                if (i == 0) sb.Append(" (호스트)"); // PlayerNames는 항상 호스트를 맨 앞에 둔다
            }
            _playerListText.text = sb.ToString();
        }

        // ---------- 버튼 핸들러 ----------

        void OnOpenToggleClicked() => SetPanelOpen(!_panelOpen);

        void SetPanelOpen(bool open)
        {
            _panelOpen = open;
            _panel.SetActive(open);
            if (open) UnlockCursor();
        }

        public async void OnCreateClicked()
        {
            if (_busy) return;
            SetBusy("방 생성 중...");
            try
            {
                await SessionManager.Instance.CreateRoomAsync(_nickInput.text);
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
                _status.text = "코드를 입력해 주세요.";
                return;
            }

            SetBusy("참가 중...");
            try
            {
                await SessionManager.Instance.JoinRoomAsync(code, _nickInput.text);
                if (this) _busy = false; // 씬 동기화는 NGO가 자동 처리 — 완료 전까지는 세션 패널이 대기 상태를 보여준다
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this) SetIdle(SessionManager.MessageFor(e));
            }
        }

        public void OnStartClicked()
        {
            if (_busy)
            {
                Debug.LogWarning("[MultiplayerMenu] OnStartClicked 무시 — 이미 다른 작업 처리 중(_busy=true)");
                return;
            }
            if (NetworkManager.Singleton == null)
            {
                _status.text = "네트워크가 초기화되지 않았습니다.";
                Debug.LogWarning("[MultiplayerMenu] OnStartClicked 무시 — NetworkManager.Singleton == null");
                return;
            }
            if (!NetworkManager.Singleton.IsHost)
            {
                _status.text = "호스트만 게임을 시작할 수 있습니다.";
                Debug.LogWarning("[MultiplayerMenu] OnStartClicked 무시 — 호스트가 아님(IsHost=false)");
                return;
            }

            _busy = true;
            _status.text = "게임 시작 중...";
            var status = NetworkManager.Singleton.SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                _busy = false;
                _status.text = $"게임 시작에 실패했습니다. ({status})";
                Debug.LogWarning($"[MultiplayerMenu] 게임 씬 로드 실패: {status}");
            }
            // 성공 시: 곧 씬이 바뀌며 다음 Update가 캔버스를 스스로 비활성화한다 — busy를 따로 풀 필요 없음.
        }

        public async void OnLeaveClicked()
        {
            if (_busy) return;
            SetBusy("나가는 중...");
            try
            {
                await SessionManager.Instance.LeaveRoomAsync();
                if (this) SetIdle("나갔습니다.");
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
            _createBtn.interactable = false;
            _joinBtn.interactable = false;
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
            if (_eventSystemGo != null) return;
            if (EventSystem.current != null) return; // 다른 주체가 이미 하나 갖고 있음 — 중복 생성 금지
            _eventSystemGo = new GameObject("MultiplayerMenu_EventSystem",
                typeof(EventSystem), typeof(StandaloneInputModule));
        }

        // ---------- UI 빌드 ----------

        void BuildUi()
        {
            _canvasGo = new GameObject("MultiplayerMenuCanvas", typeof(RectTransform));
            _canvasGo.transform.SetParent(transform, false); // NetBootstrap(DDOL) 자식 — 씬 전환에도 유지, Update가 표시만 토글

            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            _canvasGo.AddComponent<GraphicRaycaster>();

            // 우하단 토글 버튼 — 레이아웃 그룹에 속하지 않고 화면에 고정 배치된다.
            _openBtn = CreateButton(_canvasGo.transform, "OpenButton", "멀티플레이", new Color(0.15f, 0.55f, 0.95f, 0.95f));
            var openRt = _openBtn.GetComponent<RectTransform>();
            SetAnchor(openRt, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
            openRt.sizeDelta = new Vector2(150, 48);
            openRt.anchoredPosition = new Vector2(-20, 20);
            _openBtn.onClick.AddListener(OnOpenToggleClicked);

            // 패널(토글 대상) — 버튼 바로 위에 붙고, 내용에 따라 위로 자란다(피벗 하단 고정).
            _panel = new GameObject("Panel", typeof(RectTransform));
            _panel.transform.SetParent(_canvasGo.transform, false);
            var panelRt = _panel.GetComponent<RectTransform>();
            SetAnchor(panelRt, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0));
            panelRt.sizeDelta = new Vector2(340, 0);
            panelRt.anchoredPosition = new Vector2(-20, 84);

            var panelImg = _panel.AddComponent<Image>();
            panelImg.color = new Color(0.05f, 0.06f, 0.08f, 0.92f);

            var panelLayout = _panel.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(16, 16, 16, 16);
            panelLayout.spacing = 8;
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            var panelFitter = _panel.AddComponent<ContentSizeFitter>();
            panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateLabel(_panel.transform, "Title", "멀티플레이", 24, FontStyles.Bold, 30);

            // --- 방 만들기 / 코드 입장 그룹 (세션 중엔 숨김) ---
            _joinCreateGroup = CreateGroup(_panel.transform, "JoinCreateGroup");

            _nickInput = CreateInputField(_joinCreateGroup.transform, "NickInput", "닉네임", 44);
            _nickInput.characterLimit = NicknameCharLimit;

            _createBtn = CreateButton(_joinCreateGroup.transform, "CreateButton", "방 만들기", new Color(0.2f, 0.6f, 0.3f, 0.95f));
            AddHeight(_createBtn.gameObject, 44);
            _createBtn.onClick.AddListener(OnCreateClicked);

            _codeInput = CreateInputField(_joinCreateGroup.transform, "CodeInput", "코드 입력", 44);

            _joinBtn = CreateButton(_joinCreateGroup.transform, "JoinButton", "코드 입장", new Color(0.2f, 0.45f, 0.75f, 0.95f));
            AddHeight(_joinBtn.gameObject, 44);
            _joinBtn.onClick.AddListener(OnJoinClicked);

            // --- 세션 중 그룹 (방 만들기/입장 전엔 숨김) ---
            _sessionGroup = CreateGroup(_panel.transform, "SessionGroup");
            _sessionGroup.SetActive(false);

            // 방 코드 — 참가자에게 불러줄 값이라 크게 표시.
            _roomCodeText = CreateLabel(_sessionGroup.transform, "RoomCodeText", "방 코드: ------", 28, FontStyles.Bold, 38);

            _playerCountText = CreateLabel(_sessionGroup.transform, "PlayerCountText", $"인원 0/{SessionManager.MaxPlayers}", 18, FontStyles.Normal, 26);

            _playerListText = CreateLabel(_sessionGroup.transform, "PlayerListText", "", 16, FontStyles.Normal, 100);
            _playerListText.alignment = TextAlignmentOptions.TopLeft;

            // 마이크 상태 + M키 안내 한 줄 — 보이스 채널은 세션 참가 시점에 조인되므로 세션 중 그룹에 둔다.
            _micStatusText = CreateLabel(_sessionGroup.transform, "MicStatusText", "마이크 상태 확인 중...", 15, FontStyles.Normal, 22);
            _micStatusText.alignment = TextAlignmentOptions.Left;

            _startBtn = CreateButton(_sessionGroup.transform, "StartButton", "게임 시작", new Color(0.75f, 0.55f, 0.15f, 0.95f));
            AddHeight(_startBtn.gameObject, 44);
            _startBtn.onClick.AddListener(OnStartClicked);

            // 참가자에게는 시작 버튼 대신 이 안내를 보여준다(호스트 재량으로 언제든 시작 가능).
            _waitingText = CreateLabel(_sessionGroup.transform, "WaitingText", "호스트가 시작하기를 기다리는 중", 16, FontStyles.Italic, 40);
            _waitingText.color = new Color(0.8f, 0.8f, 0.8f);

            _leaveBtn = CreateButton(_sessionGroup.transform, "LeaveButton", "나가기", new Color(0.7f, 0.2f, 0.2f, 0.95f));
            AddHeight(_leaveBtn.gameObject, 44);
            _leaveBtn.onClick.AddListener(OnLeaveClicked);

            // --- 상태 라벨(항상 표시) ---
            _status = CreateLabel(_panel.transform, "StatusText", "닉네임 입력 후 방을 만들거나 코드로 입장하세요.", 18, FontStyles.Normal, 48);
            _status.color = new Color(1f, 0.85f, 0.4f);

            _panel.SetActive(false); // 기본은 접힘
            _panelOpen = false;
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

        static void AddHeight(GameObject go, float h)
        {
            var le = go.GetComponent<LayoutElement>();
            if (!le) le = go.AddComponent<LayoutElement>();
            le.preferredHeight = h;
            le.minHeight = h;
        }

        static void SetAnchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = pivot;
        }

        static Button CreateButton(Transform parent, string name, string label, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = color;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.6f);
            btn.colors = colors;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.font = KoreanFont;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 22;
            text.color = Color.white;
            text.raycastTarget = false;

            return btn;
        }

        static TMP_InputField CreateInputField(Transform parent, string name, string placeholder, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.12f);

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
            textComp.font = KoreanFont;
            textComp.fontSize = 20;
            textComp.color = Color.white;
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
            placeholderComp.font = KoreanFont;
            placeholderComp.fontSize = 20;
            placeholderComp.fontStyle = FontStyles.Italic;
            placeholderComp.color = new Color(1f, 1f, 1f, 0.45f);
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
            text.font = KoreanFont;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            AddHeight(go, height);
            return text;
        }
    }
}
