# Lobby Corridor Preview Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show the existing GameScene corridor as a static PSX-processed preview behind every Homescreen menu and lobby state.

**Architecture:** A persistent `LobbyCorridorPreview` owns an additive `GameScene` load only while Homescreen is active. It disables GameScene runtime behaviour and HUD except the selected player camera's post-processing components, leaving the real corridor and PSX effect visible without player control or game progression. `MultiplayerMenu.OnStartClicked` awaits preview release before NGO starts its normal single-scene load.

**Tech Stack:** Unity 6.4, C#, UGUI/TextMeshPro, Unity SceneManager additive loading, Post Processing Stack v2, PSXShaderKit, NUnit EditMode tests, Unity MCP.

## Global Constraints

- Keep `Homescreen` as the active scene while `GameScene` is loaded additively for preview.
- Keep GameScene's player `Camera`, `PostProcessLayer`, and `PSXPostProcessEffect` enabled; disable its player input, HUD, game logic, and other cameras/listeners.
- Do not modify GameScene YAML, map assets, shaders, networking APIs, Relay/NGO setup, or SessionManager behavior.
- Before actual game start, unload the additive preview completely, then use the existing `LoadScene(GameScene, LoadSceneMode.Single)` path.
- Include Unity-generated `.meta` files for every new asset under `Assets/`.
- Do not stage existing unrelated LFS-filter changes or pre-existing untracked files.

---

## File Structure

- Create `Assets/02.Scripts/UI/LobbyCorridorPreview.cs`: owns preview-scene lifecycle, camera handoff, isolation, and cleanup.
- Create `Assets/Editor/Tests/LobbyCorridorPreviewTests.cs`: verifies deterministic scene-routing and behavior-whitelist rules.
- Modify `Assets/02.Scripts/Net/MultiplayerMenu.cs`: awaits preview release immediately before normal game-network startup.
- Modify `Assets/Editor/Tests/MultiplayerMenuViewTests.cs`: verifies the deterministic game-start release guard.

### Task 1: Deterministic preview lifecycle

**Files:**

- Create: `Assets/02.Scripts/UI/LobbyCorridorPreview.cs`
- Create: `Assets/Editor/Tests/LobbyCorridorPreviewTests.cs`

**Interfaces:**

- Produces: `LobbyCorridorPreview.IsHomescreen(string sceneName) : bool`
- Produces: `LobbyCorridorPreview.ShouldLoadPreview(string activeSceneName, bool previewLoaded, bool previewLoading) : bool`
- Produces: `LobbyCorridorPreview.ShouldReleasePreview(string activeSceneName, bool previewLoaded) : bool`
- Produces: `LobbyCorridorPreview.ReleaseForGameStartAsync() : Task`

- [ ] **Step 1: Write the failing test**

```csharp
using Game.UI;
using NUnit.Framework;

public class LobbyCorridorPreviewTests
{
    [TestCase("Homescreen", true)]
    [TestCase("GameScene", false)]
    [TestCase("", false)]
    public void IsHomescreen_OnlyMatchesTheMenuScene(string sceneName, bool expected)
        => Assert.That(LobbyCorridorPreview.IsHomescreen(sceneName), Is.EqualTo(expected));

    [Test]
    public void ShouldLoadPreview_OnlyLoadsOnceOnHomescreen()
    {
        Assert.That(LobbyCorridorPreview.ShouldLoadPreview("Homescreen", false, false), Is.True);
        Assert.That(LobbyCorridorPreview.ShouldLoadPreview("Homescreen", true, false), Is.False);
        Assert.That(LobbyCorridorPreview.ShouldLoadPreview("Homescreen", false, true), Is.False);
        Assert.That(LobbyCorridorPreview.ShouldLoadPreview("GameScene", false, false), Is.False);
    }

    [Test]
    public void ShouldReleasePreview_ReleasesWhenLeavingHomescreen()
    {
        Assert.That(LobbyCorridorPreview.ShouldReleasePreview("GameScene", true), Is.True);
        Assert.That(LobbyCorridorPreview.ShouldReleasePreview("Homescreen", true), Is.False);
        Assert.That(LobbyCorridorPreview.ShouldReleasePreview("GameScene", false), Is.False);
    }
}
```

- [ ] **Step 2: Run it to verify RED**

Run through Unity MCP:

```text
run_tests(mode="EditMode", test_names=["LobbyCorridorPreviewTests"])
```

Expected: compilation fails because `LobbyCorridorPreview` does not exist.

- [ ] **Step 3: Implement the minimal lifecycle**

Create a `Game.UI.LobbyCorridorPreview` MonoBehaviour with these exact static rules:

```csharp
const string HomescreenSceneName = "Homescreen";

public static bool IsHomescreen(string sceneName)
    => sceneName == HomescreenSceneName;

public static bool ShouldLoadPreview(string activeSceneName, bool previewLoaded, bool previewLoading)
    => IsHomescreen(activeSceneName) && !previewLoaded && !previewLoading;

public static bool ShouldReleasePreview(string activeSceneName, bool previewLoaded)
    => previewLoaded && !IsHomescreen(activeSceneName);
```

Use `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` to create a single `DontDestroyOnLoad` instance. Subscribe to `SceneManager.activeSceneChanged` in `Awake`, unsubscribe in `OnDestroy`, and call private `ReconcilePreview()` from the scene callback. Implement `ReleaseForGameStartAsync()` as a null-safe static wrapper around the instance unload task.

- [ ] **Step 4: Run the focused tests to verify GREEN**

```text
run_tests(mode="EditMode", test_names=["LobbyCorridorPreviewTests"])
```

Expected: all three test methods pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/02.Scripts/UI/LobbyCorridorPreview.cs \
  Assets/02.Scripts/UI/LobbyCorridorPreview.cs.meta \
  Assets/Editor/Tests/LobbyCorridorPreviewTests.cs \
  Assets/Editor/Tests/LobbyCorridorPreviewTests.cs.meta
git commit -m "feat: add lobby preview lifecycle"
```

### Task 2: Additive GameScene isolation and camera handoff

**Files:**

- Modify: `Assets/02.Scripts/UI/LobbyCorridorPreview.cs`
- Modify: `Assets/Editor/Tests/LobbyCorridorPreviewTests.cs`

**Interfaces:**

- Consumes: the three routing rules and `ReleaseForGameStartAsync()` from Task 1.
- Produces: exactly one enabled preview Camera and AudioListener while the static corridor is visible.

- [ ] **Step 1: Write the failing classification test**

Add this test:

```csharp
[TestCase("PSXShaderKit.PSXPostProcessEffect", true)]
[TestCase("UnityEngine.Rendering.PostProcessing.PostProcessLayer", true)]
[TestCase("FirstPersonCamera", false)]
[TestCase("PlayerMovement", false)]
[TestCase("Game.MonsterAI", false)]
public void KeepsPreviewBehaviour_OnlyKeepsCameraPostProcessing(string typeName, bool expected)
    => Assert.That(LobbyCorridorPreview.KeepsPreviewBehaviour(typeName), Is.EqualTo(expected));
```

- [ ] **Step 2: Run it to verify RED**

```text
run_tests(mode="EditMode", test_names=["LobbyCorridorPreviewTests.KeepsPreviewBehaviour_OnlyKeepsCameraPostProcessing"])
```

Expected: compilation fails because `KeepsPreviewBehaviour` does not exist.

- [ ] **Step 3: Implement the scene presentation**

Add this exact whitelist:

```csharp
public static bool KeepsPreviewBehaviour(string typeName)
    => typeName == "PSXShaderKit.PSXPostProcessEffect" ||
       typeName == "UnityEngine.Rendering.PostProcessing.PostProcessLayer";
```

When `ShouldLoadPreview` is true, call `SceneManager.LoadSceneAsync("GameScene", LoadSceneMode.Additive)`. In its completion path, first confirm that Homescreen is still active, then:

1. Find GameScene root `Player` and select its child Camera.
2. Cache the enabled states of the Homescreen `MainCamera` and its AudioListener; disable them only after selecting the preview camera.
3. Disable every `Behaviour` in the additive GameScene unless its fully-qualified type name satisfies `KeepsPreviewBehaviour`.
4. Deactivate the additive root `Canvas`, disable all other Cameras and AudioListeners, then enable the selected Camera and its AudioListener.
5. Do not enable `FirstPersonCamera`, `PlayerMovement`, `FlashlightController`, `ToolHolder`, `CrosshairInteract`, monster, jump-scare, death, or HUD code.
6. On Homescreen exit, cancellation, or `ReleaseForGameStartAsync()`, restore Homescreen camera/listener states, clear references, and await `SceneManager.UnloadSceneAsync(previewScene)`.
7. On missing root/camera or load failure, log one warning, restore Homescreen camera state, and leave the existing menu usable.

Do not set `Time.timeScale`: disabling GameScene behaviours freezes the preview without pausing UGS or UI tasks.

- [ ] **Step 4: Run focused tests and inspect compilation**

```text
run_tests(mode="EditMode", test_names=["LobbyCorridorPreviewTests"])
read_console(action="get", types=["error", "warning"], count="30")
```

Expected: all preview tests pass with no new compile error.

- [ ] **Step 5: Validate in Play Mode**

1. Open `Assets/01.Scenes/Homescreen.unity` and enter Play Mode.
2. Wait for preview load and capture the Game view.
3. Verify the real corridor has PSX pixelation/dither/interlace behind `Preternatural` and all three main buttons.
4. Send WASD, mouse, interaction, and flashlight input; verify camera pose and game UI do not change.
5. Click `방 만들기`, then `뒤로`; verify cards remain above the same corridor.
6. Stop Play Mode and verify the original Homescreen camera is restored.

- [ ] **Step 6: Commit**

```bash
git add Assets/02.Scripts/UI/LobbyCorridorPreview.cs \
  Assets/Editor/Tests/LobbyCorridorPreviewTests.cs
git commit -m "feat: show static corridor in lobby"
```

### Task 3: Release preview before normal game start

**Files:**

- Modify: `Assets/02.Scripts/Net/MultiplayerMenu.cs:331-370`
- Modify: `Assets/Editor/Tests/MultiplayerMenuViewTests.cs`

**Interfaces:**

- Consumes: `LobbyCorridorPreview.ReleaseForGameStartAsync() : Task`.
- Preserves: `SessionManager.StartGameNetworkAsync()` and `NetworkManager.SceneManager.LoadScene(GameSceneName, LoadSceneMode.Single)`.

- [ ] **Step 1: Write the failing start-order test**

```csharp
[Test]
public void StartGame_RequiresPreviewReleaseBeforeNetworkStart()
    => Assert.That(MultiplayerMenu.ShouldReleasePreviewBeforeStartingGame(true), Is.True);
```

- [ ] **Step 2: Run it to verify RED**

```text
run_tests(mode="EditMode", test_names=["MultiplayerMenuViewTests.StartGame_RequiresPreviewReleaseBeforeNetworkStart"])
```

Expected: compilation fails because the helper does not exist.

- [ ] **Step 3: Implement and await the release**

Add:

```csharp
public static bool ShouldReleasePreviewBeforeStartingGame(bool previewEnabled)
    => previewEnabled;
```

At the beginning of the successful `OnStartClicked` try block, before `StartGameNetworkAsync()`, add:

```csharp
if (ShouldReleasePreviewBeforeStartingGame(true))
    await LobbyCorridorPreview.ReleaseForGameStartAsync();
await SessionManager.Instance.StartGameNetworkAsync();
```

Keep existing host checks, busy behavior, failure messages, and the NGO single-scene load unchanged. A release failure must reach the existing catch and must not start networking.

- [ ] **Step 4: Run the selected regression suites**

```text
run_tests(mode="EditMode", test_names=[
  "MultiplayerMenuViewTests",
  "LobbyCorridorPreviewTests",
  "HomeMenuStateTests"
])
```

Expected: all selected tests pass.

- [ ] **Step 5: Run the network smoke test**

Use Unity menu item `Game/Net/스모크 테스트`.

Expected: no `FAIL:` logs; game start still reaches GameScene and normal return to Homescreen works.

- [ ] **Step 6: Commit**

```bash
git add Assets/02.Scripts/Net/MultiplayerMenu.cs Assets/Editor/Tests/MultiplayerMenuViewTests.cs
git commit -m "fix: release lobby preview before game start"
```

### Task 4: Full validation and macOS build

**Files:**

- Modify: no production source expected.

**Interfaces:**

- Consumes: Tasks 1–3.
- Produces: a verified macOS bundle with the corridor preview.

- [ ] **Step 1: Run all related EditMode suites**

```text
run_tests(mode="EditMode", test_names=[
  "LobbyCorridorPreviewTests",
  "HomeMenuStateTests",
  "HomeTitleSequenceTests",
  "MultiplayerMenuViewTests",
  "InGameMenuStateTests",
  "LobbyHostElectionTests"
])
```

Expected: every selected test passes.

- [ ] **Step 2: Run the visual regression pass**

1. Capture Homescreen's corridor-backed main menu.
2. Verify title glitches while the background remains fixed for ten seconds of keyboard/mouse input.
3. Verify `방 만들기`, `방 참여`, back navigation, and `게임 종료` view rules remain intact.
4. Verify the multiplayer lobby card renders over the same preview once a session is present.
5. Enter and leave GameScene through the normal path; verify no duplicate Camera or AudioListener warning occurs on return.

- [ ] **Step 3: Build and inspect the macOS app**

Use `Game/빌드 (macOS)`; Console must contain:

```text
[NetBuild] result=Succeeded
```

Then run:

```bash
codesign --verify --deep --strict "/Users/yoma/projects/jamcoding/jungwon/Preternatural/Builds/Preternatural.app"
file "/Users/yoma/projects/jamcoding/jungwon/Preternatural/Builds/Preternatural.app/Contents/MacOS/HorrorGame project"
open -n "/Users/yoma/projects/jamcoding/jungwon/Preternatural/Builds/Preternatural.app"
```

Expected: the signed universal app shows the static PSX corridor behind the Homescreen UI after the Unity splash.

- [ ] **Step 4: Audit the final worktree**

```bash
git diff --check HEAD
git status --short
git log --oneline -4
```

Expected: no whitespace errors; only known unrelated LFS-filter entries and old untracked files remain outside committed feature files.

