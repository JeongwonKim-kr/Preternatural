using Unity.Netcode;
using UnityEngine;

namespace Game.Net
{
    /// 메뉴 씬 재로드 시 중복 NetworkManager가 DontDestroyOnLoad에 누적되는 것을 방지.
    /// NGO는 중복 싱글턴 GO를 스스로 파괴하지 않는다 (OnEnable에서 무조건 DDOL 이동).
    public class NetworkManagerGuard : MonoBehaviour
    {
        void Awake()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.gameObject != gameObject)
            {
                // OnEnable 단계(싱글턴 등록/DDOL) 진입 전에 차단
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }
    }
}
