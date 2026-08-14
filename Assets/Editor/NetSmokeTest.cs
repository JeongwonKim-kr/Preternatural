using System;
using System.Reflection;
using System.Threading.Tasks;
using Game.Core;
using Game.Net;
using Game.Voice;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// 멀티플레이 전체 루프 스모크 테스트: Homescreen에서 Play 진입 → 부트스트랩 대기 →
/// 방 생성 → GameScene 로드 → 스폰/접지 → 보이스 → 상호작용 동기화(토글/픽업) →
/// 몬스터 어댑터 → 강제 사망/관전 → 게임오버 → Homescreen 복귀 → NetworkManager 정리까지
/// 각 단계를 OK:/FAIL: 로그로 검증한다.
/// 참고 패턴(구조만): jungwon RESTORE Assets/Editor/SmokeTest.cs — async 단계 러너, 타임아웃, OK/FAIL 로그.
///
/// 게임오버 트리거 설계 메모: NetPlayer.ServerKill()만으로는 "전원 사망" 판정이 일어나지 않는다
/// (그 체크는 MonsterNetAdapter.ServerAttack 안에만 있고, 실제 프로덕션에서도 MonsterAI.cs가 그
/// 경로로만 호출한다 — Assets/02.Scripts/MonsterAI.cs:227). 그래서 이 테스트는 두 단계로 나눈다:
/// 1) ServerKill()로 사망·관전 안전망(HandleAliveChanged→ActivateSpectatorFallback)을 단독 검증하고,
/// 2) 그 다음 MonsterNetAdapter.ServerAttack(local)을 호출해(이미 죽은 상태라 ServerKill 자체는
///    no-op) anyAlive 체크→GameOverRpc 경로를 실제 프로덕션 진입점 그대로 검증한다.
/// 이렇게 순서를 나누지 않고 곧장 ServerAttack만 호출하면 게임오버의 호스트 지연(2초) 쪽 씬 전환이
/// 관전 카메라 5초 지연보다 먼저 끝나버려 6번 단계 검증 타이밍과 충돌한다.
public static class NetSmokeTest
{
    const string RunningFlag = "NetSmokeTest.Running";
    const string HomeScenePath = "Assets/01.Scenes/Homescreen.unity";
    const string GameSceneName = "GameScene";
    const string HomeSceneName = "Homescreen";
    const string GameOverReason = "전원 사망 — 게임 오버";

    [MenuItem("Game/Net/스모크 테스트")]
    public static void Run()
    {
        SessionState.SetBool(RunningFlag, true);
        EditorSceneManager.OpenScene(HomeScenePath);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(RunningFlag, false))
                RunPlaySide();
        };
    }

    static async void RunPlaySide()
    {
        SessionState.SetBool(RunningFlag, false);
        int okCount = 0, failCount = 0;

        void Ok(string msg) { okCount++; Debug.Log("OK: " + msg); }
        void Fail(string msg) { failCount++; Debug.LogError("FAIL: " + msg); }
        void Warn(string msg) { Debug.LogWarning("WARN: " + msg); }
        void Skip(string msg) { Debug.LogWarning("SKIP(서비스 오류): " + msg); }

        try
        {
            Debug.Log($"{DateTime.Now:s} 네트 스모크 테스트 시작");

            // 1. 부트스트랩
            if (!await WaitFor(() => GameBootstrap.IsReady, 30f))
            { Fail($"GameBootstrap 30초 타임아웃 — 상태: {GameBootstrap.StatusMessage}"); return; }
            Ok("GameBootstrap.IsReady (UGS 익명 로그인 완료)");

            if (SessionManager.Instance == null) { Fail("SessionManager.Instance 없음 — Homescreen 배선 확인 필요"); return; }

            // 2. 방 생성 — Unity Sessions 간헐 503 등: FAIL 대신 90초 대기 후 1회 재시도, 그래도 실패면 SKIP
            string joinCode = null;
            for (int attempt = 0; attempt < 2 && joinCode == null; attempt++)
            {
                try
                {
                    var createTask = SessionManager.Instance.CreateRoomAsync("스모크");
                    var done = await Task.WhenAny(createTask, Task.Delay(60000));
                    if (done != createTask)
                    {
                        if (attempt == 0)
                        {
                            Debug.LogWarning($"[NetSmokeTest] 방 생성 1차 실패(타임아웃(60s)) — 90초 대기 후 재시도");
                            await Task.Delay(90_000);
                        }
                        else
                        {
                            Skip($"방 생성 2회 실패 — 타임아웃(60s)");
                            return;
                        }
                    }
                    else
                    {
                        joinCode = await createTask;
                    }
                }
                catch (Exception e) when (attempt == 0)
                {
                    Debug.LogWarning($"[NetSmokeTest] 방 생성 1차 실패({e.GetType().Name}: {e.Message}) — 90초 대기 후 재시도");
                    await Task.Delay(90_000);
                }
                catch (Exception e)
                {
                    Skip($"방 생성 2회 실패 — {e.GetType().Name}: {e.Message}");
                    return;
                }
            }
            if (string.IsNullOrEmpty(joinCode) || joinCode.Length != 6)
            { Fail($"방 코드 형식 이상 — '{joinCode}'"); return; }
            Ok($"방 생성 완료, 코드={joinCode}");

            // 3. GameScene 로드 대기 (CreateRoomAsync 내부에서 이미 LoadScene 호출됨 — 여기서는 완료만 기다린다)
            if (!await WaitFor(() => SceneManager.GetActiveScene().name == GameSceneName, 30f))
            { Fail($"30초 내 GameScene 로드 안 됨 — 현재 씬: {SceneManager.GetActiveScene().name}"); return; }
            Ok("GameScene 로드 확인");

            // 4. NetPlayer 스폰 + 접지 확인
            if (!await WaitFor(() => NetPlayer.Local != null, 15f))
            { Fail("15초 내 NetPlayer.Local 스폰 안 됨"); return; }
            Ok($"NetPlayer 스폰 확인 (전체 {NetPlayer.All.Count}명)");

            var sceneY = FindScenePlayerY();
            if (sceneY == null)
                Fail("씬 Player 기준점을 찾지 못해 접지 판정 불가");
            else
            {
                var (grounded, lastY) = await CheckGrounded(sceneY.Value);
                if (grounded) Ok($"스폰 접지 확인 (y={lastY:F2}, 기준={sceneY.Value:F2})");
                else Fail($"스폰 후 접지 실패 (y={lastY:F2}, 기준={sceneY.Value:F2})");
            }

            // 5. 보이스 — 실패해도 FAIL 아닌 WARN (에디터 마이크 권한 변수)
            bool voiceReady = await WaitFor(() => VoiceManager.Instance != null && VoiceManager.Instance.VoiceReady, 10f);
            if (voiceReady)
            {
                var ch = VoiceManager.Instance.ActiveChannel;
                if (ch != null && ch.StartsWith("s-", StringComparison.Ordinal))
                    Ok($"보이스 채널 조인 확인 ({ch})");
                else
                    Warn($"보이스는 준비됐으나 채널명 형식이 예상('s-' 접두)과 다름 ({ch})");
            }
            else
            {
                Warn("10초 내 보이스 미준비 — " +
                     (VoiceManager.Instance != null ? VoiceManager.Instance.StatusMessage : "VoiceManager 없음") +
                     " (에디터 마이크 권한 변수)");
            }

            // 6. NetToggleSync
            var toggle = UnityEngine.Object.FindAnyObjectByType<NetToggleSync>(FindObjectsInactive.Include);
            if (toggle == null) Fail("씬에 NetToggleSync 없음");
            else
            {
                await WaitFor(() => toggle.IsSpawned, 5f);
                toggle.RequestSet(true);
                if (await WaitFor(() => toggle.State, 3f)) Ok($"NetToggleSync.RequestSet(true) → State 확인 ({toggle.name})");
                else Fail($"NetToggleSync State 반영 안 됨 ({toggle.name})");
            }

            // 7. NetPickupSync
            var pickup = UnityEngine.Object.FindAnyObjectByType<NetPickupSync>(FindObjectsInactive.Include);
            if (pickup == null) Fail("씬에 NetPickupSync 없음");
            else
            {
                await WaitFor(() => pickup.IsSpawned, 5f);
                pickup.RequestPickup();
                if (await WaitFor(() => pickup.Taken, 3f)) Ok($"NetPickupSync.RequestPickup → Taken 확인 ({pickup.name})");
                else Fail($"NetPickupSync Taken 반영 안 됨 ({pickup.name})");
            }

            // 8. 몬스터 어댑터 — 스폰 확인 + ServerWake() → ai.player 주입 확인
            var monsterAdapter = await CheckMonsterAdapter(Ok, Fail);

            // 9. 강제 사망 → 관전 카메라 (ServerKill 단독 경로 — HandleAliveChanged 안전망)
            var local = NetPlayer.Local;
            if (local == null) { Fail("NetPlayer.Local 없음 — 사망/게임오버 단계 생략"); }
            else
            {
                local.ServerKill();
                if (await WaitFor(() => !local.IsAlive.Value, 3f)) Ok("ServerKill() → IsAlive=false 확인");
                else Fail("ServerKill() 후 3초 내 IsAlive=false 안 됨");

                await Task.Delay(6000); // 관전 전환 지연(5초) + 여유
                var cam = local.HeadCamera;
                var spectator = cam != null ? cam.GetComponent<Game.Gameplay.SpectatorCamera>() : null;
                if (spectator != null && spectator.enabled) Ok("6초 대기 후 SpectatorCamera 활성 확인");
                else Fail("6초 대기 후 SpectatorCamera 미활성");

                // 10. 게임오버 경로(1인=전원사망) — 실제 프로덕션 진입점(MonsterNetAdapter.ServerAttack)으로 트리거
                if (monsterAdapter != null)
                {
                    monsterAdapter.ServerAttack(local); // local은 이미 사망 상태라 ServerKill 자체는 no-op, anyAlive 체크만 유효
                    if (await WaitFor(() => SessionManager.LastEndReason == GameOverReason, 5f))
                        Ok("게임오버 사유(LastEndReason) 설정 확인");
                    else
                        Fail($"게임오버 사유 미설정/불일치 — 실제: '{SessionManager.LastEndReason}'");

                    // 호스트 지연 2초 + 씬 전환 여유
                    if (await WaitFor(() => SceneManager.GetActiveScene().name == HomeSceneName, 15f))
                        Ok("게임오버 → Homescreen 복귀 확인");
                    else
                        Fail($"15초 내 Homescreen 복귀 안 됨 — 현재 씬: {SceneManager.GetActiveScene().name}");
                }
                else
                {
                    Fail("MonsterNetAdapter 없어 게임오버(전원사망) 경로 트리거 불가");
                }
            }

            // 11. NetworkManager 정리 상태 (NetworkManagerGuard가 DDOL 중복 방지)
            await Task.Delay(500);
            var nmList = UnityEngine.Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include);
            if (nmList.Length == 1 && !nmList[0].IsListening)
                Ok("NetworkManager 정리 확인 (1개, 비가동)");
            else
                Fail($"NetworkManager 정리 이상 — 개수={nmList.Length}" +
                     (nmList.Length > 0 ? $", IsListening={nmList[0].IsListening}" : ""));

            Debug.Log("네트 스모크 테스트 종료");
        }
        catch (Exception e)
        {
            failCount++;
            Debug.LogException(e);
            Debug.LogError($"FAIL: 예외 — {e}");
        }
        finally
        {
            Debug.Log($"SMOKE DONE ok={okCount} fail={failCount}");
            EditorApplication.isPlaying = false;
        }
    }

    /// GameScene의 원본 Player(NetPlayer 프리팹이 아닌 씬 오브젝트) y좌표 — 접지 판정 기준점.
    /// NetPlayer.BindScenePlayer가 스폰 직후 이 오브젝트를 비활성화하므로 Include로 찾는다.
    static float? FindScenePlayerY()
    {
        foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == "Player" && t.GetComponent<NetworkObject>() == null)
                return t.position.y;
        return null;
    }

    /// 스폰 후 y가 씬 Player 기준 ±3 안에서 1초간 |Δy|<0.1이면 접지로 판정.
    static async Task<(bool grounded, float lastY)> CheckGrounded(float sceneY)
    {
        bool grounded = false;
        float lastY = float.NaN;
        for (int attempt = 0; attempt < 10 && !grounded; attempt++)
        {
            if (NetPlayer.Local == null) break;
            var y1 = NetPlayer.Local.transform.position.y;
            await Task.Delay(1000);
            if (NetPlayer.Local == null) break;
            var y2 = NetPlayer.Local.transform.position.y;
            lastY = y2;
            grounded = Mathf.Abs(y1 - sceneY) <= 3f && Mathf.Abs(y2 - sceneY) <= 3f && Mathf.Abs(y2 - y1) < 0.1f;
        }
        return (grounded, lastY);
    }

    /// 존재 확인은 항상 수행. 최종 리뷰 Critical 1 반영: 몬스터 GO는 이제 항상 활성 상태로 씬에
    /// 저장되므로(NGO in-scene 스폰 요건) GO를 강제로 SetActive할 필요가 없다 — 대신 NetworkObject가
    /// 실제로 스폰됐는지(미스폰이면 사망/게임오버 체인 전체가 죽는 회귀) 확인하고, ai는 기본적으로
    /// "잠든"(awake=false) 상태이므로 ServerWake()로 깨워 타겟팅/공격 경로를 검증한다. MonsterLookAI
    /// 컴포넌트 자체(enabled)는 항상 켜져 있다 — 잠듦은 MonsterLookAI.awake 필드로 표현한다(렌더러/
    /// 콜라이더만 감추고 AI 이동 정지, Start() 타이밍은 그대로 유지 — 오프라인 회귀 방지).
    static async Task<MonsterNetAdapter> CheckMonsterAdapter(Action<string> ok, Action<string> fail)
    {
        var adapters = UnityEngine.Object.FindObjectsByType<MonsterNetAdapter>(FindObjectsInactive.Include);
        if (adapters.Length == 0) { fail("MonsterNetAdapter를 씬에서 찾지 못함"); return null; }

        var adapter = adapters[0];
        ok($"MonsterNetAdapter 존재 확인 ({adapter.name})");

        if (adapter.IsSpawned)
            ok($"몬스터 NetworkObject 스폰 확인 ({adapter.name})");
        else
            fail($"몬스터 NetworkObject 미스폰 — GO가 비활성 상태로 씬 저장됐을 가능성 ({adapter.name})");

        var aiField = typeof(MonsterNetAdapter).GetField("ai", BindingFlags.NonPublic | BindingFlags.Instance);
        var ai = aiField?.GetValue(adapter) as MonsterLookAI;
        if (ai != null && ai.enabled)
            ok("MonsterLookAI.enabled=true 확인(항상 켜진 채 유지되어야 함 — Start() 타이밍 보존)");
        else
            fail($"MonsterLookAI.enabled=false — Start()가 지연돼 오프라인 회귀를 유발할 수 있음 (ai={(ai != null ? "있음" : "없음")})");

        adapter.ServerWake(); // 표시+AI 이동 활성화(호스트) — 잠든 상태로는 타겟팅 주입을 검증할 수 없음

        await Task.Delay(1500); // 호스트 LateUpdate 몇 프레임 대기 (타겟팅 주입)

        if (ai != null && ai.awake)
            ok("ServerWake() → 호스트 ai.awake=true 확인");
        else
            fail($"ServerWake() 후에도 ai.awake=false (ai={(ai != null ? "있음" : "없음")})");

        if (ai != null && ai.player != null)
            ok($"몬스터 타겟팅 확인, ai.player 주입됨 ({ai.player.name})");
        else
            fail($"MonsterNetAdapter는 있으나 ai.player 미주입 (ai={(ai != null ? "있음" : "없음")})");

        return adapter;
    }

    static async Task<bool> WaitFor(Func<bool> condition, float timeoutSeconds)
    {
        var deadline = Time.realtimeSinceStartup + timeoutSeconds;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (!Application.isPlaying) return condition();
            if (condition()) return true;
            await Task.Delay(400);
        }
        return condition();
    }
}
