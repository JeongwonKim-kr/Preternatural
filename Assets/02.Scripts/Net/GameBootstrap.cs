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
        public static string StatusMessage { get; private set; } = "CONNECTING TO SERVICES...";

        static GameBootstrap s_instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // 에디터 'Enter Play Mode without Domain Reload' 대응 — 이전 세션의 정적 상태 제거
            s_instance = null;
            IsReady = false;
            StatusMessage = "CONNECTING TO SERVICES...";
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
                        Environment.GetCommandLineArgs(), Application.dataPath, Application.persistentDataPath, Application.isEditor);
                    var options = new InitializationOptions();
                    options.SetProfile(profile);
                    await UnityServices.InitializeAsync(options);
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                IsReady = true;
                StatusMessage = "READY";
                Debug.Log($"[Bootstrap] 로그인 완료 PlayerId={AuthenticationService.Instance.PlayerId} " +
                          $"Profile={AuthenticationService.Instance.Profile}");
            }
            catch (Exception e)
            {
                StatusMessage = "SERVICE CONNECTION FAILED. CHECK INTERNET AND PROJECT LINK.";
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// 같은 로컬 익명 계정이 이미 Lobby 멤버로 남아 게스트 참가가 거부된 경우에만 호출한다.
        /// 현재 프로필을 보존한 채 다른 프로세스 전용 프로필로 전환하고 새 익명 계정으로 재로그인한다.
        /// </summary>
        public static async Task SwitchToConflictFreeGuestProfileAsync()
        {
            var auth = AuthenticationService.Instance;
            var process = System.Diagnostics.Process.GetCurrentProcess();
            long startTicks;
            try { startTicks = process.StartTime.Ticks; }
            catch { startTicks = DateTime.UtcNow.Ticks; }

            string previousProfile = auth.Profile;
            string guestProfile = AuthProfile.CreateConflictProfile(previousProfile, process.Id, startTicks);

            IsReady = false;
            StatusMessage = "SWITCHING GUEST IDENTITY...";

            if (auth.IsSignedIn) auth.SignOut();
            auth.SwitchProfile(guestProfile);
            await auth.SignInAnonymouslyAsync();

            IsReady = true;
            StatusMessage = "READY";
            Debug.Log($"[Bootstrap] 게스트 계정 충돌 복구 PlayerId={auth.PlayerId} " +
                      $"Profile={auth.Profile} PreviousProfile={previousProfile}");
        }
    }
}
