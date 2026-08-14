using UnityEngine;

namespace Game.Gameplay
{
    /// 자유 비행 관전 (로컬 소유자 전용, 기본 비활성 — PlayerLifeState가 켠다).
    /// CharacterController가 꺼진 상태에서 루트를 직접 이동한다.
    public class SpectatorCamera : MonoBehaviour
    {
        public Transform CameraPivot; // SceneBuilder가 와이어링

        [SerializeField] float speed = 8f;
        [SerializeField] float mouseSensitivity = 2f;
        float _pitch;

        void Awake()
        {
            enabled = false; // PlayerLifeState가 관전 시에만 켠다 — 프리팹 설정 실수 방어
        }

        void OnEnable()
        {
            SetCursorLocked(true);
            // 최종 리뷰 Important 4: 이 프로젝트에서는 CameraPivot을 아무도 배선하지 않아 항상 null이었다
            // — 폴백으로 자기 자신을 피벗으로 써서 상하 시점(피치)이 최소한 동작하게 한다.
            if (CameraPivot == null) CameraPivot = transform;
        }
        // OnDisable에서 해제하지 않는다 — 부활 시 FPC가 먼저 켜져 잠근 뒤 이 컴포넌트가 꺼지므로

        void Update()
        {
            HandleCursor();

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                transform.Rotate(0f, Input.GetAxis("Mouse X") * mouseSensitivity, 0f);
                _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * mouseSensitivity, -85f, 85f);
                if (CameraPivot != null)
                {
                    // CameraPivot이 폴백으로 transform 자신이면, 방금 위에서 적용한 요(yaw)를
                    // 그대로 두고 피치만 갱신해야 한다 — 통째로 덮어쓰면 매 프레임 yaw가 0으로
                    // 리셋되어 좌우 시점이 죽는다. 별도 자식 피벗(정상 배선)이면 기존처럼 yaw=0 유지.
                    float yaw = CameraPivot == transform ? CameraPivot.localEulerAngles.y : 0f;
                    CameraPivot.localEulerAngles = new Vector3(_pitch, yaw, 0f);
                }
            }

            var move = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
            var dir = transform.TransformDirection(Vector3.ClampMagnitude(move, 1f));
            if (Input.GetKey(KeyCode.Space)) dir.y += 1f;
            if (Input.GetKey(KeyCode.LeftControl)) dir.y -= 1f;
            transform.position += dir * (speed * Time.deltaTime);
        }

        void HandleCursor()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) SetCursorLocked(false);
            else if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es == null || !es.IsPointerOverGameObject())
                    SetCursorLocked(true);
            }
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
