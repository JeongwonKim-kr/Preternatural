# Esc Menu, Lobby Utilities, and Title Sequence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (\`- [ ]\`) syntax for tracking.

**Goal:** Add a safe Esc menu, persisted display-mode choice, lobby join-code copy and pre-game host handoff, plus a dark flickering Homescreen title sequence.

**Architecture:** Keep runtime presentation in code-generated uGUI components, matching \`Game.UI.MultiplayerMenu\`; do not hand-edit scene YAML. Keep deterministic decisions in small plain-C# helpers so EditMode tests prove them before Unity presentation is added. \`InGameEscapeMenu\` and \`HomeTitleSequence\` create their own temporary Canvases from scene-load callbacks, while \`SessionManager\` remains the sole owner of session lifecycle operations.

**Tech Stack:** Unity 6000.4.0f1, legacy Unity input API, uGUI + TextMeshPro, Unity Netcode for GameObjects 2.13.1, Multiplayer Services 2.2.4, NUnit EditMode tests, macOS standalone.

## Global Constraints

- Preserve the existing \`Homescreen\` single-player start path and \`GameScene\` gameplay behavior.
- Do not attempt gameplay host migration; the active NGO host ending a game still returns all peers to \`Homescreen\`.
- Only hand off \`ISession.Host\` while the active scene is \`Homescreen\`; choose the first non-host member in the session player order.
- During multiplayer, Esc must never set \`Time.timeScale\`; only locally owned movement and look behaviours are paused.
- Window mode is exactly \`1920×1080\` using \`FullScreenMode.Windowed\`; fullscreen is \`FullScreenMode.FullScreenWindow\`.
- Persist only the display-mode enum in \`PlayerPrefs\`; do not add resolution, quality, audio, or key-binding settings.
- All player-visible Korean labels use \`MultiplayerMenu.KoreanFont\`.
- Do not add packages or hand-edit \`.unity\`/\`.prefab\` YAML.

---

## File Structure

- \`Assets/02.Scripts/UI/DisplayModeSettings.cs\` — validates, persists, and applies the two allowed display modes.
- \`Assets/02.Scripts/UI/InGameMenuState.cs\` — plain decision helpers for scene eligibility and single/multiplayer pause policy.
- \`Assets/02.Scripts/UI/InGameEscapeMenu.cs\` — scene-aware, code-generated GameScene Esc menu; owns cursor, local input suspension, UI events, and return/quit actions.
- \`Assets/02.Scripts/UI/HomeTitleSequence.cs\` — deterministic, code-generated Homescreen title overlay and flicker timeline.
- \`Assets/02.Scripts/Net/LobbyHostElection.cs\` — pure successor selection for pre-game lobby ownership handoff.
- \`Assets/02.Scripts/Net/SessionManager.cs\` — invokes host election before the host leaves a Homescreen lobby; hooks host-changed notification.
- \`Assets/02.Scripts/Net/MultiplayerMenu.cs\` — adds a copy button beside the session join code and refreshes UI after host ownership changes.
- \`Assets/Editor/Tests/InGameMenuStateTests.cs\`, \`LobbyHostElectionTests.cs\`, \`HomeTitleSequenceTests.cs\` — focused EditMode tests.

## Task 1: Test and Implement Display/Pause Contracts

**Files:**
- Create: \`Assets/Editor/Tests/InGameMenuStateTests.cs\`
- Create: \`Assets/02.Scripts/UI/DisplayModeSettings.cs\`
- Create: \`Assets/02.Scripts/UI/InGameMenuState.cs\`

**Interfaces:**
- Produces: \`Game.UI.DisplayModePreference\` with \`Windowed = 0\` and \`Fullscreen = 1\`.
- Produces: \`DisplayModeSettings.LoadPreference()\`, \`SavePreference(DisplayModePreference)\`, \`Apply(DisplayModePreference)\`.
- Produces: \`InGameMenuState.IsGameScene(string)\` and \`ShouldPauseWorld(bool)\`.
- Consumed by: \`InGameEscapeMenu\`.

- [ ] **Step 1: Write the failing tests**

\`\`\`csharp
using NUnit.Framework;
using Game.UI;

public class InGameMenuStateTests
{
    [SetUp] public void SetUp() => DisplayModeSettings.DeleteSavedPreferenceForTests();
    [TearDown] public void TearDown() => DisplayModeSettings.DeleteSavedPreferenceForTests();

    [Test] public void LoadPreference_WithoutSavedValue_ReturnsWindowed()
        => Assert.That(DisplayModeSettings.LoadPreference(), Is.EqualTo(DisplayModePreference.Windowed));

    [Test] public void LoadPreference_InvalidSavedValue_FallsBackToWindowed()
    {
        DisplayModeSettings.SaveRawValueForTests(99);
        Assert.That(DisplayModeSettings.LoadPreference(), Is.EqualTo(DisplayModePreference.Windowed));
    }

    [Test] public void LoadPreference_SavedFullscreen_RoundTrips()
    {
        DisplayModeSettings.SavePreference(DisplayModePreference.Fullscreen);
        Assert.That(DisplayModeSettings.LoadPreference(), Is.EqualTo(DisplayModePreference.Fullscreen));
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public void ShouldPauseWorld_OnlyForOfflinePlay(bool listening, bool expected)
        => Assert.That(InGameMenuState.ShouldPauseWorld(listening), Is.EqualTo(expected));
}
\`\`\`

- [ ] **Step 2: Run the tests to verify the RED state**

Run Unity MCP \`run_tests(mode="EditMode", test_names=["InGameMenuStateTests"])\`.

Expected: discovery/compile fails because the state types do not exist.

- [ ] **Step 3: Write the minimal implementation**

\`\`\`csharp
namespace Game.UI
{
    public enum DisplayModePreference { Windowed = 0, Fullscreen = 1 }

    public static class InGameMenuState
    {
        public static bool IsGameScene(string sceneName) => sceneName == "GameScene";
        public static bool ShouldPauseWorld(bool multiplayerListening) => !multiplayerListening;
    }
}
\`\`\`

Implement \`DisplayModeSettings\` with private key \`"display.mode"\`. Normalize every stored value except 0 or 1 to \`Windowed\`; \`SavePreference\` calls \`PlayerPrefs.Save\`. \`Apply(Windowed)\` calls \`Screen.SetResolution(1920, 1080, FullScreenMode.Windowed)\`; \`Apply(Fullscreen)\` assigns \`Screen.fullScreenMode = FullScreenMode.FullScreenWindow\`. A \`RuntimeInitializeOnLoadMethod(BeforeSceneLoad)\` applies the saved setting.

- [ ] **Step 4: Run the tests to verify GREEN**

Run Unity MCP \`run_tests(mode="EditMode", test_names=["InGameMenuStateTests"], include_details=true)\`.

Expected: all four tests pass.

- [ ] **Step 5: Commit**

\`\`\`bash
git add Assets/02.Scripts/UI/DisplayModeSettings.cs Assets/02.Scripts/UI/InGameMenuState.cs Assets/Editor/Tests/InGameMenuStateTests.cs
git commit -m "feat: add in-game display and pause state"
\`\`\`

## Task 2: Test and Implement Pre-game Lobby Host Election

**Files:**
- Create: \`Assets/Editor/Tests/LobbyHostElectionTests.cs\`
- Create: \`Assets/02.Scripts/Net/LobbyHostElection.cs\`
- Modify: \`Assets/02.Scripts/Net/SessionManager.cs:120-181\`

**Interfaces:**
- Produces: \`LobbyHostElection.SelectSuccessor(string currentHostId, IReadOnlyList<string> playerIds)\`, returning the first non-host ID or \`null\`.
- Produces: host handoff in \`SessionManager.LeaveRoomAsync()\` only for the Homescreen lobby.

- [ ] **Step 1: Write the failing tests**

\`\`\`csharp
using NUnit.Framework;
using Game.Net;

public class LobbyHostElectionTests
{
    [Test] public void SelectSuccessor_ReturnsFirstNonHostMember()
        => Assert.That(LobbyHostElection.SelectSuccessor("host", new[] { "host", "guest-1", "guest-2" }), Is.EqualTo("guest-1"));

    [Test] public void SelectSuccessor_SkipsHostWhenHostIsNotFirst()
        => Assert.That(LobbyHostElection.SelectSuccessor("host", new[] { "guest-1", "host", "guest-2" }), Is.EqualTo("guest-1"));

    [Test] public void SelectSuccessor_WithoutAnotherMember_ReturnsNull()
        => Assert.That(LobbyHostElection.SelectSuccessor("host", new[] { "host" }), Is.Null);
}
\`\`\`

- [ ] **Step 2: Run the tests to verify the RED state**

Run Unity MCP \`run_tests(mode="EditMode", test_names=["LobbyHostElectionTests"])\`.

Expected: discovery/compile fails because \`LobbyHostElection\` does not exist.

- [ ] **Step 3: Implement election and wire the session lifecycle**

\`\`\`csharp
public static class LobbyHostElection
{
    public static string SelectSuccessor(string currentHostId, IReadOnlyList<string> playerIds)
    {
        foreach (var id in playerIds)
            if (!string.IsNullOrEmpty(id) && id != currentHostId) return id;
        return null;
    }
}
\`\`\`

Before \`EndSessionAsync\` in \`LeaveRoomAsync\`, call private \`TryTransferLobbyHostAsync\`. It returns unless the local player is host, the active scene is \`Homescreen\`, and there is another member. Build player IDs from \`ActiveSession.Players\`, select a successor, then:

\`\`\`csharp
var hostSession = ActiveSession.AsHost();
hostSession.Host = successor;
await hostSession.SavePropertiesAsync();
\`\`\`

On exception, log a warning and continue to the existing leave path. Subscribe/unsubscribe \`session.SessionHostChanged\` with the other session events; its handler invokes \`LobbyChanged\`.

- [ ] **Step 4: Run tests and inspect compilation**

Run Unity MCP \`run_tests(mode="EditMode", test_names=["LobbyHostElectionTests"], include_details=true)\`, then \`read_console(types=["error"], count="30")\`.

Expected: three tests pass and no new compilation error.

- [ ] **Step 5: Commit**

\`\`\`bash
git add Assets/02.Scripts/Net/LobbyHostElection.cs Assets/02.Scripts/Net/SessionManager.cs Assets/Editor/Tests/LobbyHostElectionTests.cs
git commit -m "feat: transfer lobby host before host leaves"
\`\`\`

## Task 3: Add Join-code Clipboard UI

**Files:**
- Modify: \`Assets/02.Scripts/Net/MultiplayerMenu.cs:48-57, 455-507, 210-245\`
- Modify: \`Assets/Editor/NetSmokeTest.cs\`

**Interfaces:**
- Produces: public \`CopyJoinCodeForTests()\`, also used as the Copy button handler.

- [ ] **Step 1: Write the failing smoke assertion**

After the existing smoke test creates a room and confirms a non-empty code, add:

\`\`\`csharp
menu.CopyJoinCodeForTests();
if (GUIUtility.systemCopyBuffer == SessionManager.Instance.JoinCode)
    Ok("방 코드 복사 확인");
else
    Fail("방 코드가 시스템 클립보드에 복사되지 않음");
\`\`\`

- [ ] **Step 2: Run the smoke test to verify the RED state**

Run Unity menu \`Game/Net/스모크 테스트\` and inspect \`방 코드 복사\` logs.

Expected: compile failure because \`CopyJoinCodeForTests\` does not exist. If UGS prevents room creation, record that environment limit; do not change runtime code to bypass UGS.

- [ ] **Step 3: Implement the copy button**

Create \`Button _copyCodeBtn\` immediately after \`RoomCodeText\` with label \`방 번호 복사\`. It is active only for a non-empty in-session code. Implement:

\`\`\`csharp
public void CopyJoinCodeForTests()
{
    var code = SessionManager.Instance?.JoinCode;
    if (string.IsNullOrEmpty(code)) return;
    GUIUtility.systemCopyBuffer = code;
    SetIdle("방 번호를 복사했습니다.");
}
\`\`\`

Wire it with \`_copyCodeBtn.onClick.AddListener(CopyJoinCodeForTests)\`; do not add a second EventSystem.

- [ ] **Step 4: Re-run the smoke/probe to verify GREEN**

Run the smoke test and confirm \`OK: 방 코드 복사 확인\`, then inspect Unity Console errors.

- [ ] **Step 5: Commit**

\`\`\`bash
git add Assets/02.Scripts/Net/MultiplayerMenu.cs Assets/Editor/NetSmokeTest.cs
git commit -m "feat: add lobby join-code copy"
\`\`\`

## Task 4: Add the Esc Menu Runtime UI

**Files:**
- Create: \`Assets/02.Scripts/UI/InGameEscapeMenu.cs\`
- Create: \`Assets/Editor/InGameMenuSmokeTest.cs\`

**Interfaces:**
- Consumes: \`InGameMenuState\`, \`DisplayModeSettings\`, \`SessionManager\`, \`NetworkManager\`, \`PlayerMovement\`, \`FirstPersonCamera\`.
- Produces: persistent \`InGameEscapeMenu.Instance\`, \`IsOpen\`, \`OpenForTests()\`, \`CloseForTests()\`.

- [ ] **Step 1: Write the failing PlayMode acceptance probe**

Create \`Game/UI/Esc 메뉴 스모크 테스트\`. Its coroutine loads \`GameScene\`, waits for \`InGameEscapeMenu.Instance\`, opens it, asserts \`IsOpen\` and \`Time.timeScale == 0f\` when no NetworkManager is listening, closes it, and asserts the original time scale:

\`\`\`csharp
if (menu.IsOpen && Mathf.Approximately(Time.timeScale, 0f))
    Debug.Log("[InGameMenuSmoke] OK: 싱글 Esc 메뉴가 시간을 멈춤");
else
    Debug.LogError("[InGameMenuSmoke] FAIL: 싱글 Esc 메뉴 상태");
\`\`\`

- [ ] **Step 2: Run the probe to verify the RED state**

Run \`Game/UI/Esc 메뉴 스모크 테스트\`.

Expected: compilation fails because \`InGameEscapeMenu\` does not exist.

- [ ] **Step 3: Implement the controller and Canvas**

Use \`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]\` to create exactly one \`DontDestroyOnLoad\` controller. Subscribe it to \`SceneManager.activeSceneChanged\` and create/destroy the screen-space Canvas only in \`GameScene\`. Use a 1920×1080 \`CanvasScaler\`, a dark full-screen backdrop, and a centered panel with \`계속하기\`, \`화면 설정\`, \`창 모드 (1920×1080)\`, \`전체 화면\`, \`처음 화면으로\`, and \`게임 종료\`.

\`Update\` only checks \`Input.GetKeyDown(KeyCode.Escape)\` in GameScene. On open, record current cursor, enabled local \`PlayerMovement\`/ \`FirstPersonCamera\`, and time scale. If NetworkManager is listening, disable only those local behaviours. Otherwise assign time scale 0. Close, scene changes, and \`OnDestroy\` restore exactly recorded values. Screen buttons save/apply \`DisplayModeSettings\`. Return uses \`SessionManager.Instance?.LeaveRoomAsync()\` for a session or loads Homescreen otherwise. Quit restores state then invokes \`Application.Quit\`; editor builds set \`UnityEditor.EditorApplication.isPlaying = false\`.

- [ ] **Step 4: Run the probe to verify GREEN**

Run \`Game/UI/Esc 메뉴 스모크 테스트\`, then read Console errors.

Expected: the single-player pause and close logs are OK; no new compile/runtime exception.

- [ ] **Step 5: Commit**

\`\`\`bash
git add Assets/02.Scripts/UI/InGameEscapeMenu.cs Assets/Editor/InGameMenuSmokeTest.cs
git commit -m "feat: add in-game esc menu"
\`\`\`

## Task 5: Test and Implement the Homescreen Title Sequence

**Files:**
- Create: \`Assets/Editor/Tests/HomeTitleSequenceTests.cs\`
- Create: \`Assets/02.Scripts/UI/HomeTitleSequence.cs\`

**Interfaces:**
- Produces: \`HomeTitleSequence.TitleAlphaAt(float)\` and \`HomeTitleSequence.DurationSeconds = 2.5f\`.

- [ ] **Step 1: Write the failing deterministic timeline tests**

\`\`\`csharp
using NUnit.Framework;
using Game.UI;

public class HomeTitleSequenceTests
{
    [Test] public void TitleAlphaAt_StartsVisible()
        => Assert.That(HomeTitleSequence.TitleAlphaAt(0f), Is.EqualTo(1f));

    [Test] public void TitleAlphaAt_UsesFlickerBeforeCompletion()
        => Assert.That(HomeTitleSequence.TitleAlphaAt(0.28f),
            Is.LessThan(HomeTitleSequence.TitleAlphaAt(0.16f)));

    [Test] public void TitleAlphaAt_AfterDuration_IsZero()
        => Assert.That(HomeTitleSequence.TitleAlphaAt(HomeTitleSequence.DurationSeconds + 0.01f), Is.EqualTo(0f));
}
\`\`\`

- [ ] **Step 2: Run the tests to verify the RED state**

Run Unity MCP \`run_tests(mode="EditMode", test_names=["HomeTitleSequenceTests"])\`.

Expected: discovery/compile fails because \`HomeTitleSequence\` does not exist.

- [ ] **Step 3: Implement the title overlay**

Create one persistent controller from \`RuntimeInitializeOnLoadMethod(AfterSceneLoad)\`; on each Homescreen entry it creates an overlay Canvas below \`MultiplayerMenu\` but above Homescreen art. Use a non-raycasting full-screen black Image (alpha 0.72), centered TMP \`Preternatural\` with \`MultiplayerMenu.KoreanFont\`, and a pale desaturated red-white color.

Implement \`TitleAlphaAt\` as a fixed piecewise curve: 1 at 0 seconds, a darker value at 0.28 seconds, intermittent flashes through 2 seconds, and linear fade to 0 by 2.5 seconds. Use \`Time.unscaledDeltaTime\`; key or mouse input jumps to the fade-out segment. Destroy the Canvas at completion. Set every overlay graphic \`raycastTarget = false\`.

- [ ] **Step 4: Run tests and inspect runtime**

Run the EditMode tests, then enter Play Mode from Homescreen. Confirm visible flicker, automatic end at 2.5 seconds, skip on input, and functioning multiplayer button.

Expected: all three tests pass and Console has no new errors.

- [ ] **Step 5: Commit**

\`\`\`bash
git add Assets/02.Scripts/UI/HomeTitleSequence.cs Assets/Editor/Tests/HomeTitleSequenceTests.cs
git commit -m "feat: add homescreen title sequence"
\`\`\`

## Task 6: Full Validation and macOS Build

**Files:**
- Modify only files from Tasks 1-5 when a verified test failure requires it.

- [ ] **Step 1: Record Console baseline and run focused EditMode tests**

Read Unity Console errors/warnings before final tests. Run:

\`\`\`text
run_tests(mode="EditMode", test_names=["InGameMenuStateTests", "LobbyHostElectionTests", "HomeTitleSequenceTests", "StandaloneDisplaySettingsTests"], include_details=true)
\`\`\`

Expected: all selected tests pass; report historical package-sample and obsolete API warnings separately.

- [ ] **Step 2: Validate game flows**

Run \`Game/UI/Esc 메뉴 스모크 테스트\`. When UGS is reachable, launch two clients: create room, copy code, join, have host leave in Homescreen, confirm the second client receives \`게임 시작\`; then start game and open Esc on one client while confirming the other continues. If UGS is unavailable, state only those skipped network assertions.

- [ ] **Step 3: Build and launch**

Run Unity menu \`Game/빌드 (macOS)\`; require Console \`[NetBuild] result=Succeeded\` and \`errors=0\`. Then run:

\`\`\`zsh
APP="/Users/yoma/projects/jamcoding/jungwon/Preternatural/Builds/Preternatural.app"
test -x "$APP/Contents/MacOS/HorrorGame project"
codesign --verify --deep --strict "$APP"
open -n "$APP"
\`\`\`

Validate title sequence, Esc open/close, both display selections, Homescreen return, and quit in the launched app.

- [ ] **Step 4: Final diff review and commit**

Run \`git diff --check\`, \`git status --short\`, and inspect every diff. Stage only Task 1-5 files; preserve the pre-existing untracked \`StandaloneDisplaySettingsTests.cs.meta\` and old macOS plan.

\`\`\`bash
git add Assets/02.Scripts/UI Assets/02.Scripts/Net/LobbyHostElection.cs Assets/02.Scripts/Net/SessionManager.cs Assets/02.Scripts/Net/MultiplayerMenu.cs Assets/Editor/Tests Assets/Editor/InGameMenuSmokeTest.cs Assets/Editor/NetSmokeTest.cs
git commit -m "feat: add esc menu and lobby utilities"
\`\`\`

## Plan Self-Review

- Spec coverage: Tasks 1 and 4 cover Esc, cursor/input behavior, display persistence, return, and quit; Tasks 2 and 3 cover limited lobby host transfer and join-code copy; Task 5 covers the dark deterministic title; Task 6 covers Unity, network, and macOS validation.
- Scope: gameplay host migration, dedicated servers, and unrelated settings remain excluded.
- Type consistency: all helpers are named and defined before their consumers.
- Placeholder scan: no prohibited placeholder or deferred implementation phrase remains.
