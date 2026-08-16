# Game Start Blitz Transition Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hide every lobby-to-GameScene loading frame behind a black, brief blitz transition that hands off to the existing wake-up intro.

**Architecture:** A persistent `GameStartTransition` creates a highest-order nonserialized UGUI overlay and owns the transition lifecycle. `MultiplayerMenu` starts it before releasing the corridor preview; the transition watches active-scene changes, fades only after GameScene is active, and cancels on failed/returned Homescreen paths.

**Tech Stack:** Unity 6.4, C#, UGUI/TextMeshPro, SceneManager, NUnit EditMode, Unity MCP.

## Global Constraints

- The overlay must cover the display before preview release and remain until GameScene activation.
- Use unscaled time and deterministic black/white pulse values; no new scene YAML, textures, shaders, or sound assets.
- Preserve GameScene, FirstPersonCamera, IntroFadeIn, Relay/NGO/SessionManager, and ProjectSettings unchanged.
- On network start failure or Homescreen return, remove the overlay and restore normal menu input.
- Include generated `.meta` files and do not stage unrelated LFS/ProjectSettings/untracked changes.

---

### Task 1: Deterministic blitz presentation

**Files:**

- Create: `Assets/02.Scripts/UI/GameStartTransition.cs`
- Create: `Assets/Editor/Tests/GameStartTransitionTests.cs`

**Interfaces:**

- Produces: `GameStartTransition.BlitzAlphaAt(float seconds) : float`
- Produces: `GameStartTransition.IsOpaqueBeforeSceneHandoff(float seconds) : bool`
- Produces: `GameStartTransition.Begin() : void`, `Cancel() : void`

- [ ] **Step 1: Write the failing EditMode tests**

```csharp
[TestCase(0f, 1f)]
[TestCase(0.04f, 1f)]
[TestCase(0.09f, 0.25f)]
[TestCase(0.18f, 1f)]
[TestCase(0.35f, 1f)]
public void BlitzAlphaAt_UsesBlackBaseWithBriefPulses(float seconds, float expected)
    => Assert.That(GameStartTransition.BlitzAlphaAt(seconds), Is.EqualTo(expected));

[TestCase(0f, true)]
[TestCase(0.35f, true)]
[TestCase(0.36f, false)]
public void IsOpaqueBeforeSceneHandoff_EndsAfterBlitzWindow(float seconds, bool expected)
    => Assert.That(GameStartTransition.IsOpaqueBeforeSceneHandoff(seconds), Is.EqualTo(expected));
```

- [ ] **Step 2: Run focused tests and capture RED**

```text
run_tests(mode="EditMode", test_names=["GameStartTransitionTests"])
```

Expected: compiler error because `GameStartTransition` is missing.

- [ ] **Step 3: Implement the overlay and state machine**

Create a persistent singleton with a ScreenSpaceOverlay Canvas at sorting order 1000, raycast-blocking black Image, and a small full-screen white Image for pulses. Implement exact deterministic values:

```csharp
public static float BlitzAlphaAt(float seconds)
{
    if (seconds < 0.06f) return 1f;
    if (seconds < 0.12f) return 0.25f;
    if (seconds < 0.18f) return 0.92f;
    if (seconds < 0.25f) return 0.15f;
    return 1f;
}
public static bool IsOpaqueBeforeSceneHandoff(float seconds) => seconds <= 0.35f;
```

`Begin()` activates black immediately and starts unscaled pulse time. `Update()` keeps black alpha 1, applies `BlitzAlphaAt` to the white image during the first 0.35 seconds, then returns white alpha to zero while retaining black. `Cancel()` deactivates the Canvas. Subscribe/unsubscribe `SceneManager.activeSceneChanged`; after GameScene becomes active, fade black alpha to zero over 0.2 seconds unscaled and destroy/deactivate the overlay. When Homescreen becomes active while transition is pending, call `Cancel()`.

- [ ] **Step 4: Run GREEN and compile checks**

```text
run_tests(mode="EditMode", test_names=["GameStartTransitionTests"])
read_console(action="get", types=["error","warning"], count="30")
```

Expected: all tests pass; no new compile errors.

- [ ] **Step 5: Commit**

```bash
git add Assets/02.Scripts/UI/GameStartTransition.cs Assets/02.Scripts/UI/GameStartTransition.cs.meta Assets/Editor/Tests/GameStartTransitionTests.cs Assets/Editor/Tests/GameStartTransitionTests.cs.meta
git commit -m "feat: add game start blitz transition"
```

### Task 2: Start-path integration and runtime proof

**Files:**

- Modify: `Assets/02.Scripts/Net/MultiplayerMenu.cs:331-370`
- Modify: `Assets/Editor/NetSmokeTest.cs:164-169`

**Interfaces:**

- Consumes: `GameStartTransition.Begin()`, `Cancel()`.
- Preserves: `LobbyCorridorPreview.ReleaseForGameStartAsync()` then `SessionManager.StartGameNetworkAsync()` then NGO `LoadScene(GameSceneName, LoadSceneMode.Single)`.

- [ ] **Step 1: Add a failing smoke assertion**

Immediately before `menu.OnStartClicked();`, add:

```csharp
if (!GameStartTransition.IsActiveForTests)
{
    Fail("게임 시작 블리츠 오버레이가 시작 전에 표시되지 않음");
    return;
}
```

- [ ] **Step 2: Run Net smoke and capture RED**

Use `Game/Net/스모크 테스트`.

Expected: Console contains the new FAIL because MultiplayerMenu has not begun the transition.

- [ ] **Step 3: Integrate before preview release**

At the start of the successful `OnStartClicked` try block add:

```csharp
GameStartTransition.Begin();
await LobbyCorridorPreview.ReleaseForGameStartAsync();
await SessionManager.Instance.StartGameNetworkAsync();
```

In the existing catch and either early post-network failure branch, add `GameStartTransition.Cancel();` before restoring the menu idle state. Expose a read-only `IsActiveForTests` property from the transition component.

- [ ] **Step 4: Run focused and end-to-end validation**

1. Run `GameStartTransitionTests`, `LobbyCorridorPreviewTests`, and `HomeMenuStateTests`.
2. Run `Game/Net/스모크 테스트`; require `SMOKE DONE` and no `FAIL:`.
3. In Play Mode capture frames from start click until GameScene intro: every frame must be black or black-plus-white-blitz until the existing GameScene intro takes over.
4. Force a start failure or invoke `Cancel()` in a probe; verify menu becomes visible and interactive again.

- [ ] **Step 5: Commit**

```bash
git add Assets/02.Scripts/Net/MultiplayerMenu.cs Assets/Editor/NetSmokeTest.cs
git commit -m "feat: cover game load with blitz"
```

### Task 3: macOS validation

- [ ] **Step 1:** Run all Task 2 test suites and inspect Console.
- [ ] **Step 2:** Build with `Game/빌드 (macOS)`; require `[NetBuild] result=Succeeded`.
- [ ] **Step 3:** Launch `Builds/Preternatural.app`, trigger game start, and capture the black/blitz handoff without Unity default background.
- [ ] **Step 4:** Run `codesign --verify --deep --strict`, `git diff --check HEAD`, and `git status --short`.

