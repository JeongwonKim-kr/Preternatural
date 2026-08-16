using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// Presents a deterministic blackout and brief white pulses while a game scene loads.
    public sealed class GameStartTransition : MonoBehaviour
    {
        const string HomescreenSceneName = "Homescreen";
        const string GameSceneName = "GameScene";
        const float BlitzWindowSeconds = 0.35f;
        const float HandoffFadeSeconds = 0.2f;
        const int SortingOrder = 1000;

        static GameStartTransition s_instance;

        GameObject _canvasObject;
        Image _black;
        Image _pulse;
        float _elapsed;
        float _fadeElapsed;
        bool _running;
        bool _fading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_instance = null;

        public static float BlitzAlphaAt(float seconds)
        {
            if (seconds < 0.06f) return 1f;
            if (seconds < 0.12f) return 0.25f;
            if (seconds < 0.18f) return 0.92f;
            if (seconds <= 0.18f) return 1f;
            if (seconds < 0.25f) return 0.15f;
            return 1f;
        }

        public static bool IsOpaqueBeforeSceneHandoff(float seconds) => seconds <= BlitzWindowSeconds;

        public static void Begin()
        {
            var transition = FindFirstObjectByType<GameStartTransition>(FindObjectsInactive.Include);
            if (transition == null)
                transition = new GameObject(nameof(GameStartTransition)).AddComponent<GameStartTransition>();
            transition.BeginTransition();
        }

        public static void Cancel()
        {
            if (s_instance != null) s_instance.CancelTransition();
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
        }

        void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (s_instance == this) s_instance = null;
        }

        void Update()
        {
            if (!_running || _canvasObject == null) return;

            _elapsed += Time.unscaledDeltaTime;
            _black.color = new Color(0f, 0f, 0f, _fading
                ? Mathf.Clamp01(1f - _fadeElapsed / HandoffFadeSeconds)
                : 1f);

            if (_fading)
            {
                _fadeElapsed += Time.unscaledDeltaTime;
                if (_fadeElapsed >= HandoffFadeSeconds)
                {
                    _black.color = Color.clear;
                    DestroyOverlay();
                }
                return;
            }

            float pulseAlpha = _elapsed <= BlitzWindowSeconds ? BlitzAlphaAt(_elapsed) : 0f;
            _pulse.color = new Color(1f, 1f, 1f, pulseAlpha);
        }

        void OnActiveSceneChanged(Scene _, Scene current)
        {
            if (!_running) return;
            if (current.name == HomescreenSceneName)
            {
                CancelTransition();
                return;
            }

            if (current.name == GameSceneName)
            {
                _fading = true;
                _fadeElapsed = 0f;
            }
        }

        void BeginTransition()
        {
            EnsureOverlay();
            _elapsed = 0f;
            _fadeElapsed = 0f;
            _running = true;
            _fading = false;
            _black.color = Color.black;
            _pulse.color = Color.white;
        }

        void CancelTransition()
        {
            _running = false;
            _fading = false;
            DestroyOverlay();
        }

        void EnsureOverlay()
        {
            if (_canvasObject != null) return;

            _canvasObject = new GameObject("GameStartTransitionCanvas", typeof(RectTransform));
            _canvasObject.transform.SetParent(transform, false);
            var canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _black = CreateFullscreenImage("Black", Color.black, true);
            _pulse = CreateFullscreenImage("Pulse", Color.white, false);
        }

        Image CreateFullscreenImage(string name, Color color, bool raycastTarget)
        {
            var imageObject = new GameObject(name, typeof(RectTransform));
            imageObject.transform.SetParent(_canvasObject.transform, false);
            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        void DestroyOverlay()
        {
            if (_canvasObject == null) return;
            Destroy(_canvasObject);
            _canvasObject = null;
            _black = null;
            _pulse = null;
        }
    }
}
