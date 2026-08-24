using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// Homescreen의 입력을 막지 않는 영구 타이틀/배경 연출이다.
    public sealed class HomeTitleSequence : MonoBehaviour
    {
        const string HomescreenSceneName = "Homescreen";
        const int MultiplayerMenuSortingOrder = 100;
        const float TitleVerticalPosition = 150f;

        public readonly struct TitlePresentation
        {
            public TitlePresentation(float alpha, float intensity, float horizontalOffset)
            {
                Alpha = alpha;
                Intensity = intensity;
                HorizontalOffset = horizontalOffset;
            }

            public float Alpha { get; }
            public float Intensity { get; }
            public float HorizontalOffset { get; }
        }

        static HomeTitleSequence s_instance;

        GameObject _canvasObject;
        Image _backdrop;
        TextMeshProUGUI _title;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_instance = null;

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

        void OnActiveSceneChanged(Scene _, Scene current) => RebuildForScene(current);

        void RebuildForScene(Scene scene)
        {
            DestroyOverlay();
            if (scene.name == HomescreenSceneName) BuildOverlay();
        }

        void BuildOverlay()
        {
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
            _backdrop.color = new Color(0f, 0f, 0f, 0.58f);
            _backdrop.raycastTarget = false;

            var titleObject = new GameObject("Title", typeof(RectTransform));
            titleObject.transform.SetParent(_canvasObject.transform, false);
            var titleRect = (RectTransform)titleObject.transform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(1500f, 180f);
            titleRect.anchoredPosition = new Vector2(0f, TitleVerticalPosition);

            _title = titleObject.AddComponent<TextMeshProUGUI>();
            _title.text = "Preternatural";
            _title.font = MultiplayerMenu.HorrorFont;
            _title.fontSize = 104f;
            _title.fontStyle = FontStyles.Normal;
            _title.characterSpacing = 2.5f;
            _title.alignment = TextAlignmentOptions.Center;
            _title.color = TitleColorAt(0f);
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

        public static TitlePresentation TitlePresentationAt(float seconds)
            => new TitlePresentation(1f, 1f, 0f);

        public static Color TitleColorAt(float seconds)
            => new Color(0.88f, 0.85f, 0.79f, 1f);
    }
}
