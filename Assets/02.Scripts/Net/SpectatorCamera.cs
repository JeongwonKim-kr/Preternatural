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

        void OnEnable() => SetCursorLocked(true);
        // OnDisable에서 해제하지 않는다 — 부활 시 FPC가 먼저 켜져 잠근 뒤 이 컴포넌트가 꺼지므로

        void Update()
        {
            HandleCursor();

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                transform.Rotate(0f, Input.GetAxis("Mouse X") * mouseSensitivity, 0f);
                _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * mouseSensitivity, -85f, 85f);
                if (CameraPivot != null)
                    CameraPivot.localEulerAngles = new Vector3(_pitch, 0f, 0f);
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
