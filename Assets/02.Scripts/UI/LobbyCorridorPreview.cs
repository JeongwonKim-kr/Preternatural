using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    /// Owns the homescreen preview's lifetime without coupling the menu to GameScene.
    public sealed class LobbyCorridorPreview : MonoBehaviour
    {
        const string HomescreenSceneName = "Homescreen";
        const string GameSceneName = "GameScene";
        const string PlayerRootName = "Player";
        const string CanvasRootName = "Canvas";

        static LobbyCorridorPreview s_instance;

        bool _previewLoaded;
        bool _previewLoading;
        bool _releaseRequested;
        Scene _previewScene;
        Camera _previewCamera;
        AudioListener _previewListener;
        Camera _homescreenCamera;
        AudioListener _homescreenListener;
        bool _homescreenCameraWasEnabled;
        bool _homescreenListenerWasEnabled;
        CursorLockMode _homescreenCursorLockMode;
        bool _homescreenCursorVisible;
        bool _cursorStateCaptured;
        Task _loadTask;
        Task _releaseTask;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureInstance()
        {
            if (s_instance != null) return;

            var existing = FindAnyObjectByType<LobbyCorridorPreview>(FindObjectsInactive.Include);
            if (existing != null)
            {
                s_instance = existing;
                return;
            }

            new GameObject(nameof(LobbyCorridorPreview)).AddComponent<LobbyCorridorPreview>();
        }

        public static bool IsHomescreen(string sceneName)
            => sceneName == HomescreenSceneName;

        public static bool ShouldLoadPreview(string activeSceneName, bool previewLoaded, bool previewLoading)
            => IsHomescreen(activeSceneName) && !previewLoaded && !previewLoading;

        public static bool ShouldReleasePreview(string activeSceneName, bool previewLoaded)
            => previewLoaded && !IsHomescreen(activeSceneName);

        public static bool KeepsPreviewBehaviour(string typeName)
            => typeName == "PSXShaderKit.PSXPostProcessEffect" ||
               typeName == "UnityEngine.Rendering.PostProcessing.PostProcessLayer";

        public static bool ShouldConfigurePreview(string activeSceneName, bool releaseRequested)
            => IsHomescreen(activeSceneName) && !releaseRequested;

        public static Task ReleaseForGameStartAsync()
            => s_instance == null ? Task.CompletedTask : s_instance.ReleasePreviewAsync();

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
            ReconcilePreview();
        }

        void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            if (s_instance == this) s_instance = null;
        }

        void OnActiveSceneChanged(Scene _, Scene __) => ReconcilePreview();

        void ReconcilePreview()
        {
            string activeSceneName = SceneManager.GetActiveScene().name;
            if (ShouldReleasePreview(activeSceneName, _previewLoaded) ||
                (_previewLoading && !IsHomescreen(activeSceneName)))
                _ = ReleasePreviewAsync();

            if (ShouldLoadPreview(activeSceneName, _previewLoaded, _previewLoading))
                _ = BeginLoadPreviewAsync();
        }

        Task BeginLoadPreviewAsync()
        {
            if (_loadTask != null) return _loadTask;
            _releaseRequested = false;
            CaptureHomescreenCursor();
            _loadTask = LoadPreviewAsync();
            return _loadTask;
        }

        async Task LoadPreviewAsync()
        {
            _previewLoading = true;

            try
            {
                var loadOperation = SceneManager.LoadSceneAsync(GameSceneName, LoadSceneMode.Additive);
                if (loadOperation == null)
                {
                    _releaseRequested = true;
                    WarnAndRestore("GameScene preview load could not be started.");
                    return;
                }

                while (!loadOperation.isDone)
                    await Task.Yield();

                _previewScene = SceneManager.GetSceneByName(GameSceneName);
                if (!_previewScene.IsValid() || !_previewScene.isLoaded)
                {
                    _releaseRequested = true;
                    WarnAndRestore("GameScene preview did not load.");
                    return;
                }

                if (!ShouldConfigurePreview(SceneManager.GetActiveScene().name, _releaseRequested)) return;

                if (!TryConfigurePreview(_previewScene))
                {
                    _releaseRequested = true;
                    WarnAndRestore("GameScene preview is missing Player, its Camera, or its AudioListener.");
                    return;
                }

                _previewLoaded = true;
            }
            catch (System.Exception exception)
            {
                _releaseRequested = true;
                WarnAndRestore($"GameScene preview failed: {exception.Message}");
            }
            finally
            {
                _previewLoading = false;
                _loadTask = null;
                if (_releaseRequested) _ = ReleasePreviewAsync();
            }
        }

        bool TryConfigurePreview(Scene previewScene)
        {
            GameObject playerRoot = FindRoot(previewScene, PlayerRootName);
            Camera previewCamera = playerRoot == null ? null : playerRoot.GetComponentInChildren<Camera>(true);
            AudioListener previewListener = previewCamera == null ? null : previewCamera.GetComponent<AudioListener>();
            Camera homescreenCamera = FindHomescreenCamera();
            AudioListener homescreenListener = homescreenCamera == null
                ? null
                : homescreenCamera.GetComponent<AudioListener>();

            if (previewCamera == null || previewListener == null || homescreenCamera == null)
                return false;

            _previewCamera = previewCamera;
            _previewListener = previewListener;
            _homescreenCamera = homescreenCamera;
            _homescreenListener = homescreenListener;
            _homescreenCameraWasEnabled = homescreenCamera.enabled;
            _homescreenListenerWasEnabled = homescreenListener != null && homescreenListener.enabled;

            DisablePreviewBehaviours(previewScene);
            DeactivateRootCanvas(previewScene);
            DisableAllCamerasAndListeners(previewScene);

            _previewCamera.enabled = true;
            _previewListener.enabled = true;
            _homescreenCamera.enabled = false;
            if (_homescreenListener != null) _homescreenListener.enabled = false;
            UnlockHomescreenCursor();
            return true;
        }

        static GameObject FindRoot(Scene scene, string rootName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == rootName) return root;
            return null;
        }

        static Camera FindHomescreenCamera()
        {
            Scene homescreen = SceneManager.GetSceneByName(HomescreenSceneName);
            if (!homescreen.IsValid() || !homescreen.isLoaded) return null;

            foreach (GameObject root in homescreen.GetRootGameObjects())
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                if (camera.CompareTag("MainCamera")) return camera;
            return null;
        }

        static void DisablePreviewBehaviours(Scene previewScene)
        {
            foreach (GameObject root in previewScene.GetRootGameObjects())
            foreach (Behaviour behaviour in root.GetComponentsInChildren<Behaviour>(true))
                if (!KeepsPreviewBehaviour(behaviour.GetType().FullName))
                {
                    if (behaviour is MonoBehaviour monoBehaviour)
                        monoBehaviour.StopAllCoroutines();
                    behaviour.enabled = false;
                }
        }

        static void DeactivateRootCanvas(Scene previewScene)
        {
            GameObject canvas = FindRoot(previewScene, CanvasRootName);
            if (canvas != null) canvas.SetActive(false);
        }

        static void DisableAllCamerasAndListeners(Scene previewScene)
        {
            foreach (GameObject root in previewScene.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                    camera.enabled = false;
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true))
                    listener.enabled = false;
            }
        }

        void WarnAndRestore(string warning)
        {
            Debug.LogWarning($"[LobbyCorridorPreview] {warning}");
            RestoreHomescreenPresentation();
        }

        Task ReleasePreviewAsync()
        {
            _releaseRequested = true;
            if (_releaseTask != null) return _releaseTask;
            _releaseTask = ReleasePreviewInternalAsync();
            return _releaseTask;
        }

        async Task ReleasePreviewInternalAsync()
        {
            Task loadTask = _loadTask;
            if (loadTask != null) await loadTask;

            _previewLoaded = false;
            _previewLoading = false;

            Scene previewScene = _previewScene;
            _previewScene = default;
            RestoreHomescreenPresentation();
            ClearPreviewReferences();

            try
            {
                if (previewScene.IsValid() && previewScene.isLoaded)
                {
                    var unloadOperation = SceneManager.UnloadSceneAsync(previewScene);
                    if (unloadOperation != null)
                        while (!unloadOperation.isDone) await Task.Yield();
                }
            }
            finally
            {
                _releaseTask = null;
            }
        }

        void RestoreHomescreenCamera()
        {
            if (_homescreenCamera != null) _homescreenCamera.enabled = _homescreenCameraWasEnabled;
            if (_homescreenListener != null) _homescreenListener.enabled = _homescreenListenerWasEnabled;
        }

        void CaptureHomescreenCursor()
        {
            if (_cursorStateCaptured) return;
            _homescreenCursorLockMode = Cursor.lockState;
            _homescreenCursorVisible = Cursor.visible;
            _cursorStateCaptured = true;
        }

        void RestoreHomescreenPresentation()
        {
            RestoreHomescreenCamera();
            if (IsHomescreen(SceneManager.GetActiveScene().name))
                UnlockHomescreenCursor();
            else
                RestoreCapturedCursorState();
        }

        void UnlockHomescreenCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void RestoreCapturedCursorState()
        {
            if (!_cursorStateCaptured) return;
            Cursor.lockState = _homescreenCursorLockMode;
            Cursor.visible = _homescreenCursorVisible;
        }

        void ClearPreviewReferences()
        {
            _previewCamera = null;
            _previewListener = null;
            _homescreenCamera = null;
            _homescreenListener = null;
            _homescreenCameraWasEnabled = false;
            _homescreenListenerWasEnabled = false;
            _homescreenCursorLockMode = CursorLockMode.None;
            _homescreenCursorVisible = true;
            _cursorStateCaptured = false;
        }
    }
}
