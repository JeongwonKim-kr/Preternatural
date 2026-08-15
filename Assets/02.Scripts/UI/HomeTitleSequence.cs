using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// Homescreen 진입마다 잠시 표시되는, 입력을 가로채지 않는 타이틀 오버레이다.
    public sealed class HomeTitleSequence : MonoBehaviour
    {
        const string HomescreenSceneName = "Homescreen";
        const float FadeStartSeconds = 2f;
        const int MultiplayerMenuSortingOrder = 100;

        public const float DurationSeconds = 2.5f;

        static HomeTitleSequence s_instance;

        GameObject _canvasObject;
        Image _backdrop;
        TextMeshProUGUI _title;
        float _elapsed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureInstance()
        {
            if (s_instance != null) return;

            var existing = FindFirstObjectByType<HomeTitleSequence>(FindObjectsInactive.Include);
            if (existing != null)
            {
                s_instance = existing;
                return;
            }

            new GameObject(nameof(HomeTitleSequence)).AddComponent<HomeTitleSequence>();
        }

        void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            RebuildForScene(SceneManager.GetActiveScene());
        }

        void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (s_instance == this) s_instance = null;
        }

        void Update()
        {
            if (_canvasObject == null) return;

            if (WasSkippedByInput()) _elapsed = Mathf.Max(_elapsed, FadeStartSeconds);

            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed >= DurationSeconds)
            {
                DestroyOverlay();
                return;
            }

            var titleColor = _title.color;
            titleColor.a = TitleAlphaAt(_elapsed);
            _title.color = titleColor;

            var backdropColor = _backdrop.color;
            backdropColor.a = 0.72f * FadeAlphaAt(_elapsed);
            _backdrop.color = backdropColor;
        }

        void OnActiveSceneChanged(Scene _, Scene current)
        {
            RebuildForScene(current);
        }

        void RebuildForScene(Scene scene)
        {
            DestroyOverlay();
            if (scene.name == HomescreenSceneName) BuildOverlay();
        }

        void BuildOverlay()
        {
            _elapsed = 0f;
            _canvasObject = new GameObject("HomeTitleSequenceCanvas", typeof(RectTransform));
            var canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = MultiplayerMenuSortingOrder - 1;

            var scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var backdropObject = new GameObject("Backdrop", typeof(RectTransform));
            backdropObject.transform.SetParent(_canvasObject.transform, false);
            var backdropRect = (RectTransform)backdropObject.transform;
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;
            _backdrop = backdropObject.AddComponent<Image>();
            _backdrop.color = new Color(0f, 0f, 0f, 0.72f);
            _backdrop.raycastTarget = false;

            var titleObject = new GameObject("Title", typeof(RectTransform));
            titleObject.transform.SetParent(_canvasObject.transform, false);
            var titleRect = (RectTransform)titleObject.transform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(1500f, 240f);

            _title = titleObject.AddComponent<TextMeshProUGUI>();
            _title.text = "Preternatural";
            _title.font = MultiplayerMenu.KoreanFont;
            _title.fontSize = 116f;
            _title.fontStyle = FontStyles.Bold;
            _title.characterSpacing = 7f;
            _title.alignment = TextAlignmentOptions.Center;
            _title.color = new Color(1f, 0.78f, 0.78f, 1f);
            _title.raycastTarget = false;
        }

        void DestroyOverlay()
        {
            if (_canvasObject == null) return;
            Destroy(_canvasObject);
            _canvasObject = null;
            _backdrop = null;
            _title = null;
        }

        static bool WasSkippedByInput()
        {
            return Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
        }

        static float FadeAlphaAt(float seconds)
        {
            return seconds < FadeStartSeconds ? 1f : 1f - Mathf.InverseLerp(FadeStartSeconds, DurationSeconds, seconds);
        }

        public static float TitleAlphaAt(float seconds)
        {
            if (seconds <= 0f) return 1f;
            if (seconds >= DurationSeconds) return 0f;

            if (seconds <= 0.20f) return 0.92f;
            if (seconds <= 0.32f) return 0.18f;
            if (seconds < 0.40f) return 1f;
            if (seconds < 0.52f) return 0.32f;
            if (seconds < 0.68f) return 0.88f;
            if (seconds < 0.84f) return 0.14f;
            if (seconds < 1.00f) return 1f;
            if (seconds < 1.22f) return 0.42f;
            if (seconds < 1.38f) return 0.92f;
            if (seconds < 1.56f) return 0.24f;
            if (seconds < 1.74f) return 0.82f;
            if (seconds < FadeStartSeconds) return 0.48f;

            return FadeAlphaAt(seconds);
        }
    }
}
