using System.Collections.Generic;
using Game.Net;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    public sealed class InGameEscapeMenu : MonoBehaviour
    {
        const string HomescreenSceneName = "Homescreen";

        readonly List<SavedBehaviourState> _savedBehaviours = new();

        GameObject _canvasObject;
        float _savedTimeScale;
        CursorLockMode _savedCursorLockMode;
        bool _savedCursorVisible;
        bool _hasSavedState;
        bool _pausedWorld;

        public static InGameEscapeMenu Instance { get; private set; }
        public bool IsOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureInstance()
        {
            if (Instance != null) return;

            var existing = FindFirstObjectByType<InGameEscapeMenu>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Instance = existing;
                return;
            }

            new GameObject(nameof(InGameEscapeMenu)).AddComponent<InGameEscapeMenu>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            RebuildForScene(SceneManager.GetActiveScene());
        }

        void Update()
        {
            if (!InGameMenuState.IsGameScene(SceneManager.GetActiveScene().name)) return;
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            if (IsOpen) CloseMenu();
            else OpenMenu();
        }

        void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            RestoreSavedState();
            if (Instance == this) Instance = null;
        }

        public void OpenForTests()
        {
            OpenMenu();
        }

        public void CloseForTests()
        {
            CloseMenu();
        }

        void OnActiveSceneChanged(Scene _, Scene current)
        {
            CloseMenu();
            RebuildForScene(current);
        }

        void RebuildForScene(Scene scene)
        {
            if (_canvasObject != null)
            {
                Destroy(_canvasObject);
                _canvasObject = null;
            }

            if (InGameMenuState.IsGameScene(scene.name)) BuildUi();
        }

        void OpenMenu()
        {
            if (IsOpen || !InGameMenuState.IsGameScene(SceneManager.GetActiveScene().name)) return;
            if (_canvasObject == null) BuildUi();

            var networkManager = NetworkManager.Singleton;
            bool multiplayerListening = networkManager != null && networkManager.IsListening;

            _savedTimeScale = Time.timeScale;
            _savedCursorLockMode = Cursor.lockState;
            _savedCursorVisible = Cursor.visible;
            _hasSavedState = true;
            _pausedWorld = InGameMenuState.ShouldPauseWorld(multiplayerListening);

            CaptureLocalBehaviours(networkManager, multiplayerListening);
            if (multiplayerListening) DisableSavedBehaviours();
            if (_pausedWorld) Time.timeScale = 0f;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _canvasObject.SetActive(true);
            IsOpen = true;
        }

        void CloseMenu()
        {
            if (_canvasObject != null) _canvasObject.SetActive(false);
            RestoreSavedState();
            IsOpen = false;
        }

        void RestoreSavedState()
        {
            if (!_hasSavedState) return;

            foreach (var saved in _savedBehaviours)
            {
                if (saved.Behaviour != null) saved.Behaviour.enabled = saved.Enabled;
            }

            if (_pausedWorld) Time.timeScale = _savedTimeScale;
            Cursor.lockState = _savedCursorLockMode;
            Cursor.visible = _savedCursorVisible;

            _savedBehaviours.Clear();
            _hasSavedState = false;
            _pausedWorld = false;
        }

        void CaptureLocalBehaviours(NetworkManager networkManager, bool multiplayerListening)
        {
            _savedBehaviours.Clear();

            if (multiplayerListening)
            {
                var localPlayer = networkManager.LocalClient?.PlayerObject;
                if (localPlayer != null)
                {
                    Capture(localPlayer.GetComponentsInChildren<PlayerMovement>(true));
                    Capture(localPlayer.GetComponentsInChildren<FirstPersonCamera>(true));
                    return;
                }

                // NGO가 GameScene 진입 직후 아직 PlayerObject를 만들기 전이면, 네트워크 루트가 없는
                // 씬 기본 Player가 잠시 로컬 입력을 소유한다. 원격 NetworkObject는 절대 건드리지 않는다.
                CaptureScenePlayerWithoutNetworkObject<PlayerMovement>();
                CaptureScenePlayerWithoutNetworkObject<FirstPersonCamera>();
                return;
            }

            CaptureSceneBehaviours<PlayerMovement>();
            CaptureSceneBehaviours<FirstPersonCamera>();
        }

        void CaptureSceneBehaviours<T>() where T : Behaviour
        {
            var activeScene = SceneManager.GetActiveScene();
            foreach (var behaviour in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour.gameObject.scene == activeScene) Save(behaviour);
            }
        }

        void CaptureScenePlayerWithoutNetworkObject<T>() where T : Behaviour
        {
            var activeScene = SceneManager.GetActiveScene();
            foreach (var behaviour in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (behaviour.gameObject.scene == activeScene &&
                    behaviour.GetComponentInParent<NetworkObject>() == null)
                    Save(behaviour);
            }
        }

        void Capture<T>(T[] behaviours) where T : Behaviour
        {
            foreach (var behaviour in behaviours) Save(behaviour);
        }

        void Save(Behaviour behaviour)
        {
            if (behaviour != null)
                _savedBehaviours.Add(new SavedBehaviourState(behaviour, behaviour.enabled));
        }

        void DisableSavedBehaviours()
        {
            foreach (var saved in _savedBehaviours)
            {
                if (saved.Behaviour != null) saved.Behaviour.enabled = false;
            }
        }

        void SetDisplayMode(DisplayModePreference preference)
        {
            DisplayModeSettings.SavePreference(preference);
            DisplayModeSettings.Apply(preference);
        }

        void ReturnToHomescreen()
        {
            CloseMenu();

            var sessionManager = SessionManager.Instance;
            if (sessionManager != null && sessionManager.InSession)
                _ = sessionManager.LeaveRoomAsync();
            else
                SceneManager.LoadScene(HomescreenSceneName);
        }

        void QuitGame()
        {
            CloseMenu();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void BuildUi()
        {
            if (_canvasObject != null) return;

            _canvasObject = new GameObject("InGameEscapeMenuCanvas", typeof(RectTransform));
            _canvasObject.transform.SetParent(transform, false);

            var canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasObject.AddComponent<GraphicRaycaster>();

            var backdrop = new GameObject("Backdrop", typeof(RectTransform));
            backdrop.transform.SetParent(_canvasObject.transform, false);
            Stretch((RectTransform)backdrop.transform);
            var backdropImage = backdrop.AddComponent<Image>();
            backdropImage.color = new Color(0.01f, 0.015f, 0.025f, 0.82f);

            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(_canvasObject.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 0f);
            panelRect.anchoredPosition = Vector2.zero;

            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.055f, 0.06f, 0.075f, 0.98f);

            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(42, 42, 38, 38);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            CreateLabel(panel.transform, "Title", "일시 정지", 34, FontStyles.Bold, 54f);
            CreateButton(panel.transform, "ContinueButton", "계속하기", 54f, CloseMenu);
            CreateLabel(panel.transform, "DisplaySettingsLabel", "화면 설정", 24, FontStyles.Bold, 42f);
            CreateButton(panel.transform, "WindowedButton", "창 모드 (1920×1080)", 54f,
                () => SetDisplayMode(DisplayModePreference.Windowed));
            CreateButton(panel.transform, "FullscreenButton", "전체 화면", 54f,
                () => SetDisplayMode(DisplayModePreference.Fullscreen));
            CreateButton(panel.transform, "HomescreenButton", "처음 화면으로", 54f, ReturnToHomescreen);
            CreateButton(panel.transform, "QuitButton", "게임 종료", 54f, QuitGame,
                new Color(0.52f, 0.13f, 0.14f, 1f));

            _canvasObject.SetActive(false);
        }

        static Button CreateButton(Transform parent, string name, string label, float height,
            UnityEngine.Events.UnityAction onClick, Color? color = null)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = color ?? new Color(0.14f, 0.17f, 0.22f, 1f);

            var button = buttonObject.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            var colors = button.colors;
            colors.highlightedColor = new Color(0.28f, 0.34f, 0.44f, 1f);
            colors.pressedColor = new Color(0.09f, 0.11f, 0.15f, 1f);
            button.colors = colors;

            AddHeight(buttonObject, height);

            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(buttonObject.transform, false);
            Stretch((RectTransform)textObject.transform);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.font = MultiplayerMenu.KoreanFont;
            text.fontSize = 22f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return button;
        }

        static void CreateLabel(Transform parent, string name, string value, float fontSize,
            FontStyles fontStyle, float height)
        {
            var labelObject = new GameObject(name, typeof(RectTransform));
            labelObject.transform.SetParent(parent, false);
            var text = labelObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = MultiplayerMenu.KoreanFont;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            AddHeight(labelObject, height);
        }

        static void AddHeight(GameObject gameObject, float height)
        {
            var layout = gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
        }

        static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        readonly struct SavedBehaviourState
        {
            public SavedBehaviourState(Behaviour behaviour, bool enabled)
            {
                Behaviour = behaviour;
                Enabled = enabled;
            }

            public Behaviour Behaviour { get; }
            public bool Enabled { get; }
        }
    }
}
