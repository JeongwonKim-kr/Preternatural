using UnityEngine;

namespace Game.Player
{
    /// 루트 이동 속도를 Animator의 Speed 파라미터로 전달한다.
    /// 위치 변화량 기반이라 소유자(CC 이동)·원격(NetworkTransform 보간) 모두
    /// 동일하게 동작 — 별도 네트워크 동기화가 필요 없다.
    public class PlayerAnimationDriver : MonoBehaviour
    {
        static readonly int SpeedParam = Animator.StringToHash("Speed");

        [SerializeField] Animator animator;
        [SerializeField] float damping = 0.12f; // 속도 스무딩 (블렌드 튐 방지)

        Vector3 _lastPosition;
        float _speed;

        void OnEnable() => _lastPosition = transform.position;

        void Update()
        {
            if (animator == null || !animator.isActiveAndEnabled || Time.deltaTime <= 0f) return;

            var delta = transform.position - _lastPosition;
            _lastPosition = transform.position;
            delta.y = 0f; // 낙하/점프는 로코모션 속도에서 제외

            var target = delta.magnitude / Time.deltaTime;
            _speed = Mathf.Lerp(_speed, target, 1f - Mathf.Exp(-Time.deltaTime / damping));
            animator.SetFloat(SpeedParam, _speed);
        }
    }
}
