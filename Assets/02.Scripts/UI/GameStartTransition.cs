using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// Keeps the last lobby-preview frame on screen and degrades it like a broken TV signal
    /// until both the minimum presentation window and the real game scene are ready.
    public sealed class GameStartTransition : MonoBehaviour
    {
        const string HomescreenSceneName = "Homescreen";
        const string GameSceneName = "GameScene";
        const string SignalShaderResourcePath = "Shaders/TVSignalGlitch";
        const float HandoffFadeSeconds = 0.3f;
        const int SortingOrder = 1000;
        const int MaxCaptureWidth = 1920;
        const int MaxCaptureHeight = 1080;

        static readonly int SignalTimeId = Shader.PropertyToID("_SignalTime");
        static readonly int TearStrengthId = Shader.PropertyToID("_TearStrength");
        static readonly int ChromaticOffsetId = Shader.PropertyToID("_ChromaticOffset");
        static readonly int StaticAlphaId = Shader.PropertyToID("_StaticAlpha");
        static readonly int DropoutAlphaId = Shader.PropertyToID("_DropoutAlpha");

        public const float MinimumSignalSeconds = 2.6f;

        public readonly struct SignalPresentation
        {
            public SignalPresentation(float interference, float horizontalTear,
                float chromaticOffset, float staticAlpha, float dropoutAlpha)
            {
                Interference = interference;
                HorizontalTear = horizontalTear;
                ChromaticOffset = chromaticOffset;
                StaticAlpha = staticAlpha;
                DropoutAlpha = dropoutAlpha;
            }

            public float Interference { get; }
            public float HorizontalTear { get; }
            public float ChromaticOffset { get; }
            public float StaticAlpha { get; }
            public float DropoutAlpha { get; }
        }

        static GameStartTransition s_instance;

        NetworkSceneManager _subscribedNetworkSceneManager;

        public static bool IsActiveForTests => s_instance != null && s_instance._running;

        GameObject _canvasObject;
        RawImage _capturedImage;
        Image _fallbackBlack;
        Texture2D _capturedFrame;
        Material _signalMaterial;
        float _elapsed;
        float _fadeElapsed;
        bool _running;
        bool _fading;
        bool _gameSceneReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallForInitialLobby()
        {
            if (SceneManager.GetActiveScene().name == HomescreenSceneName)
                EnsureInstalled();
        }

        public static SignalPresentation SignalPresentationAt(float seconds)
        {
            float elapsed = Mathf.Max(0f, seconds);
            float settling = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(elapsed / MinimumSignalSeconds));
            float interference = Mathf.Lerp(1f, 0.1f, settling);
            float frame = Mathf.Floor(elapsed * 24f);
            float tearNoise = Hash(frame + 7.31f) * 2f - 1f;
            float horizontalTear = tearNoise * Mathf.Lerp(0.055f, 0.004f, settling);
            float chromaticOffset = Mathf.Lerp(0.016f, 0.0015f, settling);
            float staticAlpha = Mathf.Lerp(0.42f, 0.06f, settling);
            float dropoutNoise = Hash(frame + 43.17f);
            float dropoutAlpha = dropoutNoise > 0.76f
                ? interference * Mathf.Lerp(0.18f, 0.48f, dropoutNoise)
                : 0f;
            return new SignalPresentation(interference, horizontalTear,
                chromaticOffset, staticAlpha, dropoutAlpha);
        }

        public static bool CanRevealGame(float elapsed, bool gameSceneReady)
            => gameSceneReady && elapsed >= MinimumSignalSeconds;

        public static bool ShouldBeginFromNetworkLoad(
            string activeSceneName,
            string loadingSceneName,
            bool isLoadEvent,
            bool transitionRunning)
            => ShouldBeginFromNetworkSceneEvent(
                activeSceneName,
                loadingSceneName,
                isLoadEvent,
                false,
                transitionRunning);

        public static bool ShouldBeginFromNetworkSceneEvent(
            string activeSceneName,
            string loadingSceneName,
            bool isLoadEvent,
            bool isSynchronizeEvent,
            bool transitionRunning)
            => !transitionRunning &&
               activeSceneName == HomescreenSceneName &&
               ((isLoadEvent && loadingSceneName == GameSceneName) || isSynchronizeEvent);

        static float Hash(float value)
            => Mathf.Repeat(Mathf.Sin(value * 12.9898f) * 43758.5453f, 1f);

        public static void Begin()
        {
            EnsureInstalled().BeginTransition();
        }

        static GameStartTransition EnsureInstalled()
        {
            var transition = FindFirstObjectByType<GameStartTransition>(FindObjectsInactive.Include);
            if (transition == null)
                transition = new GameObject(nameof(GameStartTransition)).AddComponent<GameStartTransition>();
            return transition;
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
            SubscribeToNetworkSceneManager(null);
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            DestroyOverlay();
            if (s_instance == this) s_instance = null;
        }

        void Update()
        {
            RefreshNetworkSceneSubscription();
            if (!_running || _canvasObject == null) return;

            _elapsed += Time.unscaledDeltaTime;

            if (_fading)
            {
                _fadeElapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Clamp01(1f - _fadeElapsed / HandoffFadeSeconds);
                _capturedImage.color = new Color(1f, 1f, 1f, alpha);
                _fallbackBlack.color = new Color(0f, 0f, 0f, alpha);
                if (_fadeElapsed >= HandoffFadeSeconds) DestroyOverlay();
                return;
            }

            ApplySignalPresentation(SignalPresentationAt(_elapsed));
            if (CanRevealGame(_elapsed, _gameSceneReady))
            {
                _fading = true;
                _fadeElapsed = 0f;
            }
        }

        void RefreshNetworkSceneSubscription()
        {
            NetworkSceneManager current = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.SceneManager
                : null;
            if (ReferenceEquals(current, _subscribedNetworkSceneManager)) return;
            SubscribeToNetworkSceneManager(current);
        }

        void SubscribeToNetworkSceneManager(NetworkSceneManager sceneManager)
        {
            if (_subscribedNetworkSceneManager != null)
                _subscribedNetworkSceneManager.OnSceneEvent -= OnNetworkSceneEvent;

            _subscribedNetworkSceneManager = sceneManager;
            if (_subscribedNetworkSceneManager != null)
                _subscribedNetworkSceneManager.OnSceneEvent += OnNetworkSceneEvent;
        }

        void OnNetworkSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent == null) return;
            if (!ShouldBeginFromNetworkSceneEvent(
                    SceneManager.GetActiveScene().name,
                    sceneEvent.SceneName,
                    sceneEvent.SceneEventType == SceneEventType.Load,
                    sceneEvent.SceneEventType == SceneEventType.Synchronize,
                    _running))
                return;

            BeginTransition();
        }

        void OnActiveSceneChanged(Scene _, Scene current)
        {
            if (!_running) return;
            if (current.name == HomescreenSceneName)
            {
                CancelTransition();
                return;
            }

            if (current.name == GameSceneName) _gameSceneReady = true;
        }

        void BeginTransition()
        {
            CapturePreviewFrame();
            EnsureOverlay();
            _elapsed = 0f;
            _fadeElapsed = 0f;
            _running = true;
            _fading = false;
            _gameSceneReady = SceneManager.GetActiveScene().name == GameSceneName;
            _capturedImage.color = Color.white;
            _fallbackBlack.color = Color.black;
            ApplySignalPresentation(SignalPresentationAt(0f));
        }

        void CancelTransition()
        {
            _running = false;
            _fading = false;
            DestroyOverlay();
        }

        void CapturePreviewFrame()
        {
            ReleaseCaptureResources();
            Camera camera = LobbyCorridorPreview.TransitionCamera;
            if (camera == null)
            {
                Debug.LogWarning("[GameStartTransition] Preview camera unavailable; using black fallback.");
                return;
            }

            int sourceWidth = Mathf.Max(1, Screen.width);
            int sourceHeight = Mathf.Max(1, Screen.height);
            float scale = Mathf.Min(1f,
                Mathf.Min((float)MaxCaptureWidth / sourceWidth, (float)MaxCaptureHeight / sourceHeight));
            int width = Mathf.Max(1, Mathf.RoundToInt(sourceWidth * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(sourceHeight * scale));
            var temporary = new RenderTexture(width, height, 24,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
            {
                name = "GameStartCaptureBuffer",
                memorylessMode = RenderTextureMemoryless.None,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            temporary.Create();
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;

            try
            {
                camera.targetTexture = temporary;
                camera.Render();
                RenderTexture.active = temporary;
                _capturedFrame = new Texture2D(width, height, TextureFormat.RGB24, false);
                _capturedFrame.name = "GameStartCapturedFrame";
                _capturedFrame.wrapMode = TextureWrapMode.Clamp;
                _capturedFrame.filterMode = FilterMode.Bilinear;
                _capturedFrame.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                _capturedFrame.Apply(false, false);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[GameStartTransition] Could not capture preview frame: {exception.Message}");
                if (_capturedFrame != null) Destroy(_capturedFrame);
                _capturedFrame = null;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                temporary.Release();
                Destroy(temporary);
            }
        }

        void EnsureOverlay()
        {
            if (_canvasObject != null)
            {
                _capturedImage.texture = _capturedFrame;
                return;
            }

            _canvasObject = new GameObject("GameStartTransitionCanvas", typeof(RectTransform));
            _canvasObject.transform.SetParent(transform, false);
            var canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = _canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasObject.AddComponent<GraphicRaycaster>();

            _fallbackBlack = CreateFullscreenImage("BlackFallback", Color.black);

            var capturedObject = new GameObject("CapturedGameFrame", typeof(RectTransform));
            capturedObject.transform.SetParent(_canvasObject.transform, false);
            Stretch((RectTransform)capturedObject.transform);
            _capturedImage = capturedObject.AddComponent<RawImage>();
            _capturedImage.texture = _capturedFrame;
            _capturedImage.color = Color.white;
            _capturedImage.raycastTarget = true;

            Shader shader = Resources.Load<Shader>(SignalShaderResourcePath);
            if (shader == null)
            {
                Debug.LogWarning("[GameStartTransition] TV signal shader missing; showing captured frame without distortion.");
                return;
            }

            _signalMaterial = new Material(shader) { name = "Runtime TV Signal Glitch" };
            _capturedImage.material = _signalMaterial;
        }

        Image CreateFullscreenImage(string name, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform));
            imageObject.transform.SetParent(_canvasObject.transform, false);
            Stretch((RectTransform)imageObject.transform);
            var image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return image;
        }

        void ApplySignalPresentation(SignalPresentation presentation)
        {
            if (_signalMaterial == null) return;
            _signalMaterial.SetFloat(SignalTimeId, _elapsed);
            _signalMaterial.SetFloat(TearStrengthId, Mathf.Abs(presentation.HorizontalTear));
            _signalMaterial.SetFloat(ChromaticOffsetId, presentation.ChromaticOffset);
            _signalMaterial.SetFloat(StaticAlphaId, presentation.StaticAlpha);
            _signalMaterial.SetFloat(DropoutAlphaId, presentation.DropoutAlpha);
        }

        void DestroyOverlay()
        {
            _running = false;
            _fading = false;
            if (_canvasObject != null) Destroy(_canvasObject);
            _canvasObject = null;
            _capturedImage = null;
            _fallbackBlack = null;
            ReleaseCaptureResources();
        }

        void ReleaseCaptureResources()
        {
            if (_signalMaterial != null) Destroy(_signalMaterial);
            if (_capturedFrame != null) Destroy(_capturedFrame);
            _signalMaterial = null;
            _capturedFrame = null;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
