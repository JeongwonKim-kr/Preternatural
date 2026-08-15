using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Net;
using Game.Voice;

namespace Game.UI
{
    /// 게임 중 화면 좌하단에 항상 보이는 마이크 상태 표시. 씬 직렬화에 의존하지 않고 Awake에서 UGUI를
    /// 코드로 생성한다(MultiplayerMenu와 동일 패턴). NetPlayer.OnNetworkSpawn의 owner 분기에서
    /// gameObject.AddComponent로 부착되므로 로컬 플레이어가 스폰돼 있는 동안만 존재하고, 플레이어가
    /// 디스폰되면(세션 종료 등) 자식 Canvas도 함께 파괴돼 별도 정리 코드가 필요 없다.
    ///
    /// 배치: GameScene의 기존 UI(크로스헤어, 페이드, DeadScreen 등, 전부 화면 중앙 anchor 0.5,0.5
    /// 기준)와 겹치지 않도록 좌하단 코너 anchor(0,0)를 쓴다.
    public class MicStatusHud : MonoBehaviour
    {
        static readonly Color ReadyColor = new(0.75f, 0.75f, 0.75f); // 차분한 회색 — 마이크 켜짐
        static readonly Color MutedColor = new(1f, 0.35f, 0.35f);    // 눈에 띄는 빨강 — 음소거
        static readonly Color WarnColor = new(1f, 0.65f, 0.25f);     // 주황 — 보이스 미준비/연결 끊김

        TMP_Text _label;
        NetPlayer _player;

        void Awake()
        {
            _player = GetComponent<NetPlayer>();
            BuildUi();
        }

        void Update()
        {
            if (_label == null) return;

            var voice = VoiceManager.Instance;
            if (voice == null)
            {
                Set("보이스 매니저 없음", WarnColor);
                return;
            }

            if (!voice.VoiceReady)
            {
                // ToggleMute()는 VoiceReady==false면 조용히 무시하므로, M키가 안 먹는 것처럼 보일 때
                // 이유(연결 중/끊김/사용 불가)를 그대로 보여준다.
                Set(voice.StatusMessage, WarnColor);
                return;
            }

            bool alive = _player == null || _player.IsAlive.Value;
            if (!alive)
            {
                Set("음소거 (사망)", MutedColor);
                return;
            }

            if (voice.IsMuted) Set("음소거 중 (M: 해제)", MutedColor);
            else Set("마이크 켜짐 (M: 음소거)", ReadyColor);
        }

        void Set(string text, Color color)
        {
            if (_label.text != text) _label.text = text;
            _label.color = color;
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("MicStatusCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var labelGo = new GameObject("MicStatusLabel", typeof(RectTransform));
            labelGo.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)labelGo.transform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(24, 24);
            rt.sizeDelta = new Vector2(460, 34);

            _label = labelGo.AddComponent<TextMeshProUGUI>();
            _label.font = MultiplayerMenu.KoreanFont;
            _label.fontSize = 20;
            _label.fontStyle = FontStyles.Bold;
            _label.alignment = TextAlignmentOptions.BottomLeft;
            _label.raycastTarget = false;
            _label.text = "마이크 상태 확인 중...";
        }
    }
}
