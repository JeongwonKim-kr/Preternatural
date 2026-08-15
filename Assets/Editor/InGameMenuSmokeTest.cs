using System;
using System.Collections;
using Game.UI;
using Game.Gameplay;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class InGameMenuSmokeTest
{
    const string RunningFlag = "InGameMenuSmokeTest.Running";
    const string GameScenePath = "Assets/01.Scenes/GameScene.unity";

    static IEnumerator s_probe;

    [MenuItem("Game/UI/Esc 메뉴 스모크 테스트")]
    public static void Run()
    {
        SessionState.SetBool(RunningFlag, true);
        EditorSceneManager.OpenScene(GameScenePath);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode ||
            !SessionState.GetBool(RunningFlag, false)) return;

        SessionState.SetBool(RunningFlag, false);
        s_probe = RunProbe();
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        try
        {
            if (s_probe != null && s_probe.MoveNext()) return;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        s_probe = null;
        EditorApplication.update -= Tick;
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
    }

    static IEnumerator RunProbe()
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while (InGameEscapeMenu.Instance == null && Time.realtimeSinceStartup < deadline)
            yield return null;

        var menu = InGameEscapeMenu.Instance;
        if (menu == null)
        {
            Debug.LogError("[InGameMenuSmoke] FAIL: InGameEscapeMenu.Instance를 찾지 못함");
            yield break;
        }

        var networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening)
        {
            Debug.LogError("[InGameMenuSmoke] FAIL: 싱글 검증 중 NetworkManager가 listening 상태임");
            yield break;
        }

        float originalTimeScale = Time.timeScale;
        menu.OpenForTests();

        if (menu.IsOpen && Mathf.Approximately(Time.timeScale, 0f))
            Debug.Log("[InGameMenuSmoke] OK: 싱글 Esc 메뉴가 시간을 멈춤");
        else
            Debug.LogError("[InGameMenuSmoke] FAIL: 싱글 Esc 메뉴 상태");

        var spectatorObject = new GameObject("InGameMenuSpectatorProbe");
        var spectator = spectatorObject.AddComponent<SpectatorCamera>();
        spectator.enabled = true;
        yield return null;
        if (Cursor.lockState == CursorLockMode.None && Cursor.visible)
            Debug.Log("[InGameMenuSmoke] OK: 메뉴 중 관전 전환이 커서를 다시 잠그지 않음");
        else
            Debug.LogError("[InGameMenuSmoke] FAIL: 메뉴 중 관전 전환이 커서를 다시 잠금");
        UnityEngine.Object.Destroy(spectatorObject);

        menu.CloseForTests();
        if (!menu.IsOpen && Mathf.Approximately(Time.timeScale, originalTimeScale))
            Debug.Log("[InGameMenuSmoke] OK: 닫을 때 원래 시간 배율을 복구");
        else
            Debug.LogError("[InGameMenuSmoke] FAIL: 닫을 때 원래 시간 배율을 복구하지 못함");

        var lifecycleProbe = RunMultiplayerControlLifecycleProbe();
        while (lifecycleProbe.MoveNext())
            yield return lifecycleProbe.Current;
        yield return null;
    }

    static IEnumerator RunMultiplayerControlLifecycleProbe()
    {
        var playerObject = new GameObject("InGameMenuLifecycleProbe", typeof(CharacterController));
        var movement = playerObject.AddComponent<PlayerMovement>();
        var cameraObject = new GameObject("Camera");
        cameraObject.transform.SetParent(playerObject.transform, false);
        var firstPersonCamera = cameraObject.AddComponent<FirstPersonCamera>();
        firstPersonCamera.playerMovement = movement;

        movement.SetMenuInputSuppressed(true);
        firstPersonCamera.SetMenuInputSuppressed(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 실제 MonoBehaviour.Start가 실행되어도, 이미 열린 메뉴를 뒤늦게 잠그면 안 된다.
        yield return null;
        if (Cursor.lockState == CursorLockMode.None && Cursor.visible)
            Debug.Log("[InGameMenuSmoke] OK: 늦게 시작한 로컬 컨트롤이 메뉴 커서를 유지");
        else
            Debug.LogError("[InGameMenuSmoke] FAIL: 늦게 시작한 로컬 컨트롤이 메뉴 커서를 다시 잠금");

        // 깨어남/은신 해제처럼 메뉴 도중 권위 코드가 enable한 상태는 메뉴 해제 뒤에도 유지해야 한다.
        movement.enabled = false;
        firstPersonCamera.enabled = false;
        movement.SetMenuInputSuppressed(true);
        firstPersonCamera.SetMenuInputSuppressed(true);
        movement.enabled = true;
        firstPersonCamera.enabled = true;
        movement.SetMenuInputSuppressed(false);
        firstPersonCamera.SetMenuInputSuppressed(false);
        bool newerEnabledStatePreserved = movement.enabled && firstPersonCamera.enabled;

        // 사망/은신 진입처럼 메뉴 도중 권위 코드가 disable한 상태도 stale snapshot으로 되살리면 안 된다.
        movement.SetMenuInputSuppressed(true);
        firstPersonCamera.SetMenuInputSuppressed(true);
        movement.enabled = false;
        firstPersonCamera.enabled = false;
        movement.SetMenuInputSuppressed(false);
        firstPersonCamera.SetMenuInputSuppressed(false);
        bool newerDisabledStatePreserved = !movement.enabled && !firstPersonCamera.enabled;

        if (newerEnabledStatePreserved && newerDisabledStatePreserved)
            Debug.Log("[InGameMenuSmoke] OK: 메뉴 해제가 최신 권위 enabled 상태를 보존");
        else
            Debug.LogError("[InGameMenuSmoke] FAIL: 메뉴 해제가 stale enabled 상태를 복원");

        UnityEngine.Object.Destroy(playerObject);
    }
}
