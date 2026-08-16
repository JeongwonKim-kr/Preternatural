# Gameplay 4:3 Frame Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render GameScene only inside a centered 4:3 viewport and leave the remaining display pure black.

**Architecture:** A persistent runtime controller finds the local gameplay camera only in GameScene, caches its presentation settings, and applies a normalized centered 4:3 camera rect. It recalculates on screen-size change and restores camera settings outside GameScene.

**Tech Stack:** Unity 6.4, C#, Camera viewport rect, NUnit EditMode, Unity MCP.

## Global Constraints

- Target ratio is exactly 4:3.
- Do not modify GameScene YAML, PSX effects, FirstPersonCamera, networking, ProjectSettings, or UI scenes.
- Preserve camera FOV/post-processing and restore original rect/clear settings outside GameScene.
- Include generated metas and preserve unrelated dirty files.

---

### Task 1: 4:3 viewport calculation and runtime application

**Files:**

- Create: `Assets/02.Scripts/UI/GameplayAspectFrame.cs`
- Create: `Assets/Editor/Tests/GameplayAspectFrameTests.cs`

**Interfaces:**

- Produces: `GameplayAspectFrame.CentralViewportFor(float screenWidth, float screenHeight) : Rect`

- [ ] **Step 1: Write failing tests**

```csharp
[TestCase(1920f, 1080f, 0.125f, 0f, 0.75f, 1f)]
[TestCase(1440f, 1080f, 0f, 0f, 1f, 1f)]
[TestCase(1080f, 1920f, 0f, 0.2890625f, 1f, 0.421875f)]
public void CentralViewportFor_UsesCenteredFourByThree(float width, float height,
    float x, float y, float viewportWidth, float viewportHeight)
{
    var actual = GameplayAspectFrame.CentralViewportFor(width, height);
    Assert.That(actual.x, Is.EqualTo(x).Within(0.0001f));
    Assert.That(actual.y, Is.EqualTo(y).Within(0.0001f));
    Assert.That(actual.width, Is.EqualTo(viewportWidth).Within(0.0001f));
    Assert.That(actual.height, Is.EqualTo(viewportHeight).Within(0.0001f));
}
```

- [ ] **Step 2: Run RED**

```text
run_tests(mode="EditMode", test_names=["GameplayAspectFrameTests"])
```

Expected: missing `GameplayAspectFrame` compiler error.

- [ ] **Step 3: Implement**

Create a DontDestroyOnLoad controller that subscribes to active scene changes. In GameScene find the local NetPlayer HeadCamera, falling back only to the non-network scene Player child Camera. Cache `rect`, `clearFlags`, and `backgroundColor`; set clear flags SolidColor/background black and camera rect to `CentralViewportFor(Screen.width, Screen.height)`. Update only when Screen width/height changes. Restore cached settings on GameScene exit.

```csharp
public static Rect CentralViewportFor(float width, float height)
{
    const float target = 4f / 3f;
    float aspect = width / height;
    if (aspect >= target)
    {
        float viewportWidth = target / aspect;
        return new Rect((1f - viewportWidth) * .5f, 0f, viewportWidth, 1f);
    }
    float viewportHeight = aspect / target;
    return new Rect(0f, (1f - viewportHeight) * .5f, 1f, viewportHeight);
}
```

- [ ] **Step 4: GREEN, Play Mode, commit**

Run focused tests, then Play GameScene at 16:9 and verify centered 4:3 with solid black sides, resolution change recalculation, and camera restoration on Homescreen return.

```bash
git add Assets/02.Scripts/UI/GameplayAspectFrame.cs Assets/02.Scripts/UI/GameplayAspectFrame.cs.meta Assets/Editor/Tests/GameplayAspectFrameTests.cs Assets/Editor/Tests/GameplayAspectFrameTests.cs.meta
git commit -m "feat: frame gameplay in 4x3"
```

### Task 2: Regression and macOS validation

- [ ] Run GameplayAspectFrameTests, existing menu/preview tests, and Game/Net smoke.
- [ ] Build macOS via Game/빌드 (macOS), require [NetBuild] result=Succeeded.
- [ ] Launch build and capture the GameScene: center is 4:3, all side area is black with no gameplay leakage.
- [ ] Run codesign verification, git diff --check, and worktree audit.

