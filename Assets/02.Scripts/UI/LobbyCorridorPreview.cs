using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    /// Owns the homescreen preview's lifetime without coupling the menu to GameScene.
    public sealed class LobbyCorridorPreview : MonoBehaviour
    {
        const string HomescreenSceneName = "Homescreen";

        static LobbyCorridorPreview s_instance;

        bool _previewLoaded;
        bool _previewLoading;

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
            // The preview content is introduced by the following slice. This shell
            // owns the deterministic scene/lifetime decision points only.
            string activeSceneName = SceneManager.GetActiveScene().name;
            if (ShouldReleasePreview(activeSceneName, _previewLoaded))
                _ = ReleasePreviewAsync();

            if (ShouldLoadPreview(activeSceneName, _previewLoaded, _previewLoading))
                _previewLoading = false;
        }

        Task ReleasePreviewAsync()
        {
            _previewLoaded = false;
            _previewLoading = false;
            return Task.CompletedTask;
        }
    }
}
