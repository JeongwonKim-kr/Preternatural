using Game.Net;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    public sealed class GameplayAspectFrame : MonoBehaviour
    {
        const string GameSceneName = "GameScene";

        Camera _framedCamera;
        Rect _savedRect;
        CameraClearFlags _savedClearFlags;
        Color _savedBackgroundColor;
        int _lastScreenWidth = -1;
        int _lastScreenHeight = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureInstance()
        {
            if (FindFirstObjectByType<GameplayAspectFrame>() != null) return;
            new GameObject(nameof(GameplayAspectFrame)).AddComponent<GameplayAspectFrame>();
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            NetPlayer.LocalPlayerAvailable += OnLocalPlayerAvailable;
            RefreshFrame(force: true);
        }

        void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            NetPlayer.LocalPlayerAvailable -= OnLocalPlayerAvailable;
            RestoreCamera();
        }

        void Update()
        {
            if (SceneManager.GetActiveScene().name != GameSceneName) return;
            RefreshFrame(force: false);
        }

        void OnActiveSceneChanged(Scene _, Scene current)
        {
            if (current.name != GameSceneName)
            {
                RestoreCamera();
                return;
            }

            RefreshFrame(force: true);
        }

        void OnLocalPlayerAvailable(NetPlayer _)
        {
            if (SceneManager.GetActiveScene().name == GameSceneName) RefreshFrame(force: true);
        }

        void RefreshFrame(bool force)
        {
            var camera = FindGameplayCamera();
            if (camera == null) return;

            if (camera != _framedCamera)
            {
                RestoreCamera();
                _framedCamera = camera;
                _savedRect = camera.rect;
                _savedClearFlags = camera.clearFlags;
                _savedBackgroundColor = camera.backgroundColor;
                force = true;
            }

            int width = Screen.width;
            int height = Screen.height;
            if (!force && width == _lastScreenWidth && height == _lastScreenHeight) return;

            _framedCamera.clearFlags = CameraClearFlags.SolidColor;
            _framedCamera.backgroundColor = Color.black;
            _framedCamera.rect = CentralViewportFor(width, height);
            _lastScreenWidth = width;
            _lastScreenHeight = height;
        }

        Camera FindGameplayCamera()
        {
            if (NetPlayer.Local != null && NetPlayer.Local.HeadCamera != null)
                return NetPlayer.Local.HeadCamera;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) return null;

            var scenePlayer = GameObject.Find("Player");
            return scenePlayer != null ? scenePlayer.GetComponentInChildren<Camera>(true) : null;
        }

        void RestoreCamera()
        {
            if (_framedCamera == null) return;

            _framedCamera.rect = _savedRect;
            _framedCamera.clearFlags = _savedClearFlags;
            _framedCamera.backgroundColor = _savedBackgroundColor;
            _framedCamera = null;
            _lastScreenWidth = -1;
            _lastScreenHeight = -1;
        }

        public static Rect CentralViewportFor(float width, float height)
        {
            const float target = 4f / 3f;
            float aspect = width / height;
            if (aspect >= target)
            {
                float viewportWidth = target / aspect;
                return new Rect((1f - viewportWidth) * .5f, 0f, viewportWidth, 1f);
            }

            float viewportHeight = aspect / target;
            return new Rect(0f, (1f - viewportHeight) * .5f, 1f, viewportHeight);
        }
    }
}
