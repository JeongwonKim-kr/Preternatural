using UnityEngine;

namespace Game.Net
{
    /// 원격 손전등 모델은 손에 남겨 두되, 빛의 방향은 이미 네트워크 동기화되는
    /// 머리 카메라 회전을 따른다. 별도 회전 RPC를 보내지 않아도 실제 조준점이 일치한다.
    [DisallowMultipleComponent]
    public sealed class RemoteFlashlightAim : MonoBehaviour
    {
        [SerializeField] Transform aimSource;

        public void Configure(Transform source)
        {
            aimSource = source;
        }

        void LateUpdate()
        {
            if (aimSource != null)
                transform.rotation = aimSource.rotation;
        }
    }
}
