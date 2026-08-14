using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Game.Core
{
    /// 앱 시작 시 1회: UGS 초기화 → 익명 로그인.
    /// 에디터 다중 인스턴스/로컬 다중 빌드에서도 각자 다른 PlayerId를 갖도록
    /// AuthProfile로 프로필을 분리한다.
    public class GameBootstrap : MonoBehaviour
    {
        public static bool IsReady { get; private set; }
        public static string StatusMessage { get; private set; } = "서비스 초기화 중...";

        static GameBootstrap s_instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // 에디터 'Enter Play Mode without Domain Reload' 대응 — 이전 세션의 정적 상태 제거
            s_instance = null;
            IsReady = false;
            StatusMessage = "서비스 초기화 중...";
        }

        async void Awake()
        {
            if (s_instance != null) { Destroy(gameObject); return; }
            s_instance = this;
            DontDestroyOnLoad(gameObject);
            await InitializeAsync();
        }

        static async Task InitializeAsync()
        {
            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    var profile = AuthProfile.Resolve(
                        Environment.GetCommandLineArgs(), Application.dataPath, Application.isEditor);
                    var options = new InitializationOptions();
                    options.SetProfile(profile);
                    await UnityServices.InitializeAsync(options);
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                IsReady = true;
                StatusMessage = "준비 완료";
                Debug.Log($"[Bootstrap] 로그인 완료 PlayerId={AuthenticationService.Instance.PlayerId} " +
                          $"Profile={AuthenticationService.Instance.Profile}");
            }
            catch (Exception e)
            {
                StatusMessage = "서비스 연결 실패 — 인터넷/프로젝트 링크를 확인하세요.";
                Debug.LogException(e);
            }
        }
    }
}
