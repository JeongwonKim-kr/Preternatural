# Homescreen Main Menu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show a persistent glitching `Preternatural` title on Homescreen and let players enter the existing room creation or room join flow directly from two visible buttons.

**Architecture:** Keep title presentation in `HomeTitleSequence` and keep Session API calls in `SessionManager`. Add one small plain-C# screen-state helper so `MultiplayerMenu` can choose mutually exclusive main/create/join/lobby surfaces without mixing UI layout branches into networking code. Continue to create all uGUI at runtime; do not edit Homescreen YAML.

**Tech Stack:** Unity 6000.4.0f1, uGUI, TextMeshPro, Unity Multiplayer Services 2.2.4, NGO 2.13.1, NUnit EditMode tests, macOS standalone.

## Global Constraints

- Work only on branch `multi`; do not stage existing LFS status entries, `ProjectSettings/ProjectSettings.asset`, or pre-existing untracked files.
- Preserve `SessionManager.CreateRoomAsync`, `JoinRoomAsync`, `StartGameNetworkAsync`, the Relay/NGO start point, and the existing single-player start path.
- Keep UI code-generated and use `MultiplayerMenu.KoreanFont` for Korean labels.
- Homescreen presentation uses unscaled time and does not capture menu input.
- Do not add audio assets, shaders, Scene YAML wiring, room lists, ready systems, or gameplay host migration.

---

## File Structure

- Create: `Assets/02.Scripts/UI/HomeMenuState.cs` — deterministic selector for main, create-card, join-card, and lobby views.
- Create: `Assets/Editor/Tests/HomeMenuStateTests.cs` — focused EditMode coverage for the view selector.
- Modify: `Assets/02.Scripts/UI/HomeTitleSequence.cs` — persistent Homescreen title with deterministic glitch sampling.
- Modify: `Assets/Editor/Tests/HomeTitleSequenceTests.cs` — tests for cycle and valid glitch presentation samples.
- Modify: `Assets/02.Scripts/Net/MultiplayerMenu.cs` — two-button main menu, mutually exclusive input cards, and preserved lobby presentation.
- Create: `Assets/Editor/Tests/MultiplayerMenuViewTests.cs` — public view-selection seam for the runtime menu.
- Modify: `Assets/02.Scripts/UI/HomeMenuState.cs` — expose whether the main view should show a quit action.

### Task 1: Define Homescreen Menu State

**Files:**
- Create: `Assets/02.Scripts/UI/HomeMenuState.cs`
- Create: `Assets/Editor/Tests/HomeMenuStateTests.cs`

**Interfaces:**
- Produces: `Game.UI.HomeMenuView` enum with `Main`, `CreateRoom`, `JoinRoom`, and `Lobby`.
- Produces: `HomeMenuState.Select(bool inSession, HomeMenuView requested)`; an active session always returns `Lobby`.
- Consumed by: `Game.UI.MultiplayerMenu` in Task 3.

- [ ] **Step 1: Write the failing view-selection tests**

```csharp
using Game.UI;
using NUnit.Framework;

public class HomeMenuStateTests
{
    [TestCase(HomeMenuView.Main)]
    [TestCase(HomeMenuView.CreateRoom)]
    [TestCase(HomeMenuView.JoinRoom)]
    public void Select_WithoutSession_KeepsRequestedMainMenuView(HomeMenuView requested)
        => Assert.That(HomeMenuState.Select(false, requested), Is.EqualTo(requested));

    [Test]
    public void Select_WithSession_AlwaysShowsLobby()
        => Assert.That(HomeMenuState.Select(true, HomeMenuView.JoinRoom), Is.EqualTo(HomeMenuView.Lobby));
}
```

- [ ] **Step 2: Run the test and confirm it fails because the state API does not exist**

Run through Unity MCP: `run_tests(mode="EditMode", test_names=["HomeMenuStateTests"], include_details=true)`.

Expected: compilation failure naming missing `HomeMenuView` or `HomeMenuState`.

- [ ] **Step 3: Implement the minimal state selector**

```csharp
namespace Game.UI
{
    public enum HomeMenuView { Main, CreateRoom, JoinRoom, Lobby }

    public static class HomeMenuState
    {
        public static HomeMenuView Select(bool inSession, HomeMenuView requested)
            => inSession ? HomeMenuView.Lobby : requested == HomeMenuView.Lobby ? HomeMenuView.Main : requested;
    }
}
```

- [ ] **Step 4: Run the focused EditMode test and confirm it passes**

Run through Unity MCP: `run_tests(mode="EditMode", test_names=["HomeMenuStateTests"], include_details=true)`.

Expected: 2 tests pass with zero failures.

- [ ] **Step 5: Commit the test and state helper**

```bash
git add Assets/02.Scripts/UI/HomeMenuState.cs Assets/02.Scripts/UI/HomeMenuState.cs.meta Assets/Editor/Tests/HomeMenuStateTests.cs Assets/Editor/Tests/HomeMenuStateTests.cs.meta
git commit -m "feat: add homescreen menu state"
```

### Task 2: Make the Homescreen Title Persistent and Glitching

**Files:**
- Modify: `Assets/02.Scripts/UI/HomeTitleSequence.cs`
- Modify: `Assets/Editor/Tests/HomeTitleSequenceTests.cs`

**Interfaces:**
- Produces: `HomeTitleSequence.TitlePresentationAt(float seconds)`, returning alpha, color intensity, and horizontal offset for a repeating title cycle.
- Consumed by: `HomeTitleSequence.Update`; no network system consumes this visual-only API.

- [ ] **Step 1: Add failing deterministic title-presentation tests**

```csharp
[Test]
public void TitlePresentationAt_RepeatsAfterOneCycle()
{
    var atStart = HomeTitleSequence.TitlePresentationAt(0.18f);
    var afterCycle = HomeTitleSequence.TitlePresentationAt(2.18f);
    Assert.That(afterCycle.Alpha, Is.EqualTo(atStart.Alpha));
    Assert.That(afterCycle.HorizontalOffset, Is.EqualTo(atStart.HorizontalOffset));
}

[Test]
public void TitlePresentationAt_KeepsVisibleAlphaInRange()
{
    var sample = HomeTitleSequence.TitlePresentationAt(0.42f);
    Assert.That(sample.Alpha, Is.InRange(0.1f, 1f));
}
```

- [ ] **Step 2: Run the focused test and confirm it fails because `TitlePresentationAt` does not exist**

Run through Unity MCP: `run_tests(mode="EditMode", test_names=["HomeTitleSequenceTests"], include_details=true)`.

Expected: compilation failure naming missing `TitlePresentationAt`.

- [ ] **Step 3: Implement the title cycle and apply it to the persistent Canvas**

```csharp
public readonly struct TitlePresentation
{
    public readonly float Alpha;
    public readonly float Intensity;
    public readonly float HorizontalOffset;
    // constructor assigns all fields
}

public static TitlePresentation TitlePresentationAt(float seconds)
{
    float phase = Mathf.Repeat(seconds, 2f);
    // Use fixed phase ranges for alpha, intensity, and offset; do not use Random.
}
```

Replace the one-shot fade/destruction path with a Homescreen-only persistent Canvas. In `Update`, apply the sampled alpha and pale-red intensity to `_title.color`, and set its anchored X position from `HorizontalOffset`. Keep `_backdrop.raycastTarget` and `_title.raycastTarget` false. Destroy the overlay only when leaving Homescreen.

- [ ] **Step 4: Run title tests and inspect compilation**

Run through Unity MCP: `run_tests(mode="EditMode", test_names=["HomeTitleSequenceTests"], include_details=true)`, then read the latest Unity Console errors.

Expected: all title tests pass and no new compile errors exist.

- [ ] **Step 5: Commit the persistent title work**

```bash
git add Assets/02.Scripts/UI/HomeTitleSequence.cs Assets/Editor/Tests/HomeTitleSequenceTests.cs
git commit -m "feat: keep homescreen title glitching"
```

### Task 3: Replace the Toggle with Main Actions and Input Cards

**Files:**
- Modify: `Assets/02.Scripts/Net/MultiplayerMenu.cs`
- Create: `Assets/Editor/Tests/MultiplayerMenuViewTests.cs`

**Interfaces:**
- Consumes: `HomeMenuState.Select(bool, HomeMenuView)` from Task 1.
- Consumes: existing `SessionManager.CreateRoomAsync(string)`, `JoinRoomAsync(string, string)`, and lobby properties unchanged.
- Produces: visible Homescreen actions `방 만들기`, `방 참여`, and `뒤로`; no new public networking API.

- [ ] **Step 1: Add a failing state-driven UI test seam**

```csharp
using Game.UI;
using NUnit.Framework;

public class MultiplayerMenuViewTests
{
    [Test]
    public void VisibleViewForTests_SessionOverridesJoinCardWithLobby()
        => Assert.That(MultiplayerMenu.VisibleViewForTests(true, HomeMenuView.JoinRoom),
            Is.EqualTo(HomeMenuView.Lobby));
}
```

- [ ] **Step 2: Run the focused test and confirm the missing test seam fails**

Run through Unity MCP: `run_tests(mode="EditMode", test_names=["MultiplayerMenuViewTests"], include_details=true)`.

Expected: compilation failure naming missing `VisibleViewForTests`.

- [ ] **Step 3: Refactor `BuildUi` into the three visible non-lobby groups**

Implement these layout rules in the existing Canvas:

```csharp
// MainActions: "방 만들기" and "방 참여" buttons below the title.
// CreateCard: NickInput + create confirmation + "뒤로".
// JoinCard: NickInput + CodeInput + join confirmation + "뒤로".
// SessionGroup: retain room code, copy, roster, mic status, start/wait, leave.
```

Remove `_openBtn`, `_panelOpen`, `OnOpenToggleClicked`, and the hidden-by-default panel behavior. Add a private requested-view field initialized to `HomeMenuView.Main`; `Update` derives the active view with `HomeMenuState.Select(inSession, _requestedView)` and activates exactly one group. `뒤로` resets the requested view to `Main`. Reuse existing handlers and status text, and retain cursor unlock when Homescreen becomes active.

- [ ] **Step 4: Add the minimal test seam and make focused tests green**

```csharp
public static HomeMenuView VisibleViewForTests(bool inSession, HomeMenuView requested)
    => HomeMenuState.Select(inSession, requested);
```

Run through Unity MCP: `run_tests(mode="EditMode", test_names=["HomeMenuStateTests", "MultiplayerMenuViewTests"], include_details=true)`.

Expected: all focused tests pass.

- [ ] **Step 5: Validate the runtime Homescreen flow**

Enter Play Mode from Homescreen. Confirm the persistent title is visible behind the two buttons, each action opens only its corresponding card, `뒤로` restores the actions, and a successful room creation still reveals room code, copy, start, and leave controls. Exit Play Mode without saving scene changes.

- [ ] **Step 6: Commit the menu UI work**

```bash
git add Assets/02.Scripts/Net/MultiplayerMenu.cs Assets/Editor/Tests/MultiplayerMenuViewTests.cs Assets/Editor/Tests/MultiplayerMenuViewTests.cs.meta
git commit -m "feat: add homescreen room actions"
```

### Task 4: Full Regression Validation

**Files:**
- Modify only a failing Task 1-3 file if the verification below identifies a regression.

- [ ] **Step 1: Run the relevant EditMode suite**

Run through Unity MCP: `run_tests(mode="EditMode", test_names=["HomeMenuStateTests", "HomeTitleSequenceTests", "MultiplayerMenuViewTests", "InGameMenuStateTests", "LobbyHostElectionTests"], include_details=true)`.

Expected: all requested tests pass.

- [ ] **Step 2: Run Unity compilation and console review**

Refresh the Unity project, then inspect the Console for errors and warnings introduced by the feature.

Expected: no new compiler error or warning is attributed to the feature.

- [ ] **Step 3: Inspect the final diff and preserve unrelated state**

```bash
git diff --check HEAD
git status --short
git diff --name-only HEAD
```

Expected: only Task 1-3 implementation/test files are newly committed; existing LFS status entries, `ProjectSettings/ProjectSettings.asset`, and unrelated untracked files remain unstaged.

- [ ] **Step 4: Build the macOS app and launch it locally**

Use the existing Unity `Game/빌드 (macOS)` menu path. Verify the generated app starts on Homescreen and visibly shows the title plus two main actions. Do not add the `Builds/` output to Git.


### Task 5: Add Homescreen Quit Action

**Files:**
- Modify: `Assets/02.Scripts/UI/HomeMenuState.cs`
- Modify: `Assets/02.Scripts/Net/MultiplayerMenu.cs`
- Modify: `Assets/Editor/Tests/HomeMenuStateTests.cs`

**Interfaces:**
- Produces: `HomeMenuState.ShowsQuitAction(HomeMenuView view)`; only the unaffiliated `Main` view returns `true`.
- Consumed by: `MultiplayerMenu.Update` to show the `게임 종료` button only below the main room actions.

- [ ] **Step 1: Write the failing quit-visibility test**

```csharp
[TestCase(HomeMenuView.Main, true)]
[TestCase(HomeMenuView.CreateRoom, false)]
[TestCase(HomeMenuView.JoinRoom, false)]
[TestCase(HomeMenuView.Lobby, false)]
public void ShowsQuitAction_OnlyShowsOnMainView(HomeMenuView view, bool expected)
    => Assert.That(HomeMenuState.ShowsQuitAction(view), Is.EqualTo(expected));
```

- [ ] **Step 2: Run the test and confirm `ShowsQuitAction` is missing**

Run through Unity MCP: `run_tests(mode="EditMode", test_names=["HomeMenuStateTests"], include_details=true)`.

Expected: compilation failure naming missing `ShowsQuitAction`.

- [ ] **Step 3: Implement the state rule and wire the button**

```csharp
public static bool ShowsQuitAction(HomeMenuView view) => view == HomeMenuView.Main;
```

Create the existing-style red `게임 종료` button beneath `방 참여`. In `Update`, activate it from `ShowsQuitAction(view)`. Its click handler must set `UnityEditor.EditorApplication.isPlaying = false` under `UNITY_EDITOR`; otherwise call `Application.Quit()`. Do not alter the existing GameScene Esc-menu exit handler.

- [ ] **Step 4: Run EditMode tests and Play Mode smoke**

Run `HomeMenuStateTests` plus `MultiplayerMenuViewTests`, then enter Homescreen Play Mode and confirm the quit action is visible only on the first main screen. In the Editor, do not click the quit action during smoke validation because it intentionally ends Play Mode.

- [ ] **Step 5: Commit**

```bash
git add Assets/02.Scripts/UI/HomeMenuState.cs Assets/02.Scripts/Net/MultiplayerMenu.cs Assets/Editor/Tests/HomeMenuStateTests.cs
git commit -m "feat: add homescreen quit action"
```
