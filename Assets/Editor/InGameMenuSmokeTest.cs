using System;
using System.Collections;
using Game.UI;
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

        menu.CloseForTests();
        if (!menu.IsOpen && Mathf.Approximately(Time.timeScale, originalTimeScale))
            Debug.Log("[InGameMenuSmoke] OK: 닫을 때 원래 시간 배율을 복구");
        else
            Debug.LogError("[InGameMenuSmoke] FAIL: 닫을 때 원래 시간 배율을 복구하지 못함");

        yield return null;
    }
}
