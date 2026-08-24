# macOS 기본 16:9 창 실행 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** macOS 빌드가 세로 네이티브 디스플레이에서도 기본 `1920×1080` 가로 창으로 실행되게 한다.

**Architecture:** Unity Standalone Player 설정만 `Windowed`로 바꿔 운영체제의 세로 네이티브 전체화면 해상도를 따르지 않게 한다. EditMode 회귀 테스트가 PlayerSettings의 해상도·창 모드·크기 고정 상태를 확인하며, 기존의 일회성 화면 진단 로그는 빌드 검증 후 제거한다.

**Tech Stack:** Unity 6000.4.0f1, Unity Test Framework (EditMode), macOS Standalone Player, CoplayDev unity-mcp.

## Global Constraints

- 브랜치는 `multi`여야 하며, 각 커밋 전 `git branch --show-current`로 확인한다.
- 변경 대상은 Standalone Player 표시 설정과 그 회귀 테스트뿐이다.
- `Screen.SetResolution`, 씬·프리팹·카메라·XR 패키지 설정은 변경하지 않는다.
- `Assets/02.Scripts/Net/GameBootstrap.cs`의 현재 미커밋 Screen 진단 로그는 검증용 임시 코드다. 진단 결과를 수집한 뒤 원래 상태로 복원하고 커밋에 포함하지 않는다.
- Unity 스크립트 검증은 Editor가 연결된 CoplayDev unity-mcp의 `run_tests`/Console 조회로 한다. 에디터 재시작, Library 삭제, 배치 CLI 실행은 하지 않는다.
- 최종 macOS 앱은 `Builds/Preternatural.app`에 생성한다.

## File structure

- `ProjectSettings/ProjectSettings.asset` — Standalone 실행 창 모드의 직렬화된 Unity Player 설정.
- `Assets/Editor/Tests/StandaloneDisplaySettingsTests.cs` — 의도한 macOS 기본 창 설정이 회귀하지 않았는지 확인하는 EditMode 테스트.
- `Assets/02.Scripts/Net/GameBootstrap.cs` — 기존 임시 실행 환경 진단 로그. Task 2에서 완전히 제거한다.

---

### Task 1: Standalone 16:9 창 설정과 회귀 테스트

**Files:**
- Create: `Assets/Editor/Tests/StandaloneDisplaySettingsTests.cs`
- Modify: `ProjectSettings/ProjectSettings.asset:110`

**Interfaces:**
- Consumes: `UnityEditor.PlayerSettings.fullScreenMode`, `defaultScreenWidth`, `defaultScreenHeight`, `resizableWindow`
- Produces: EditMode 테스트 `StandaloneDisplaySettingsTests.macOS_기본_창_설정은_고정_16대9이다()`; `Windowed`, `1920`, `1080`, `false`인 Standalone Player 설정

- [ ] **Step 1: 실패하는 EditMode 회귀 테스트를 작성한다.**

`Assets/Editor/Tests/StandaloneDisplaySettingsTests.cs`에 다음 내용을 만든다.

```csharp
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    public class StandaloneDisplaySettingsTests
    {
        [Test]
        public void macOS_기본_창_설정은_고정_16대9이다()
        {
            Assert.AreEqual(FullScreenMode.Windowed, PlayerSettings.fullScreenMode);
            Assert.AreEqual(1920, PlayerSettings.defaultScreenWidth);
            Assert.AreEqual(1080, PlayerSettings.defaultScreenHeight);
            Assert.IsFalse(PlayerSettings.resizableWindow);
        }
    }
}
```

- [ ] **Step 2: 테스트가 기존 `Fullscreen Window` 설정에서 실패하는지 확인한다.**

CoplayDev unity-mcp로 EditMode 테스트 `Game.Tests.StandaloneDisplaySettingsTests`를 실행한다.

Expected: `PlayerSettings.fullScreenMode` 값이 `FullScreenWindow`여서 첫 번째 assertion이 실패한다. 다른 세 assertion은 이미 `1920`, `1080`, `false`여야 한다.

- [ ] **Step 3: Player 설정을 한 값만 변경한다.**

`ProjectSettings/ProjectSettings.asset`에서 다음 직렬화된 값을 변경한다.

```yaml
# Before
fullscreenMode: 1

# After
fullscreenMode: 3
```

`3`은 `FullScreenMode.Windowed`다. `defaultScreenWidth: 1920`, `defaultScreenHeight: 1080`, `resizableWindow: 0`는 이미 요구값이므로 건드리지 않는다.

- [ ] **Step 4: 회귀 테스트와 컴파일을 확인한다.**

CoplayDev unity-mcp로 EditMode 테스트 `Game.Tests.StandaloneDisplaySettingsTests`를 다시 실행하고 Unity Console의 Error 항목을 조회한다.

Expected: 테스트 1개 통과, 새 컴파일 오류 0건.

- [ ] **Step 5: 설정과 테스트만 커밋한다.**

```bash
git branch --show-current
git add ProjectSettings/ProjectSettings.asset Assets/Editor/Tests/StandaloneDisplaySettingsTests.cs
git diff --cached --check
git commit -m "fix(mac): 기본 실행을 16대9 창으로 설정" \
  -m "Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```

Expected: 브랜치가 `multi`이고, 이 커밋에는 위 두 파일만 포함된다.

### Task 2: macOS 빌드에서 실제 창 비율을 검증하고 진단 코드를 정리한다

**Files:**
- Modify then restore: `Assets/02.Scripts/Net/GameBootstrap.cs`
- Generate only: `Builds/Preternatural.app`

**Interfaces:**
- Consumes: `NetBuildTool.BuildMac()`, `GameBootstrap.LogScreenDiagnostics()`의 `[Screen]` 시작 로그, macOS Player.log
- Produces: `Screen 1920x1080` 및 `mode=Windowed`가 기록된 실행 검증; 남는 `GameBootstrap.cs` 변경 없음

- [ ] **Step 1: 임시 진단 로그를 포함한 macOS 앱을 빌드한다.**

CoplayDev unity-mcp로 Unity 메뉴 `Game/빌드 (macOS)`를 실행한다. 완료 뒤 Unity Console에서 `[NetBuild] result=Succeeded`와 `errors=0`을 확인한다.

Expected: `Builds/Preternatural.app`이 새 빌드로 갱신되고 build summary에 오류가 없다.

- [ ] **Step 2: 빌드 앱을 한 번 실행해 표시 환경을 기록한다.**

`Builds/Preternatural.app`을 Finder에서 실행해 Homescreen이 가로 `16:9` 창으로 열리는지 확인한 뒤 앱을 종료한다. 다음 로그를 읽는다.

```bash
rg -n '\[Screen\]' '/Users/yoma/Library/Logs/DefaultCompany/HorrorGame project/Player.log'
```

Expected: 최신 `[Screen]` 줄에 `Screen 1920x1080`과 `mode=Windowed`가 포함되고, 캡처에서 보인 세로 콘텐츠 영역과 양옆 검은 여백이 없다.

- [ ] **Step 3: 임시 Screen 진단 코드를 원래 상태로 복원한다.**

`Assets/02.Scripts/Net/GameBootstrap.cs`에서 다음 호출을 삭제한다.

```csharp
LogScreenDiagnostics();
```

그리고 `LogScreenDiagnostics()` 메서드 전체를 삭제한다. 기존 `Awake()`의 남는 본문은 다음과 같아야 한다.

```csharp
async void Awake()
{
    if (s_instance != null) { Destroy(gameObject); return; }
    s_instance = this;
    DontDestroyOnLoad(gameObject);
    await InitializeAsync();
}
```

- [ ] **Step 4: 정리 후 컴파일과 전체 EditMode 테스트를 확인한다.**

CoplayDev unity-mcp로 Unity를 refresh한 뒤 모든 EditMode 테스트를 실행하고 Console Error 항목을 조회한다.

Expected: 모든 EditMode 테스트 통과, 새 Error 0건, `git diff -- Assets/02.Scripts/Net/GameBootstrap.cs` 출력 없음.

- [ ] **Step 5: 최종 변경 범위를 검토한다.**

```bash
git status --short
git diff HEAD --check
git diff HEAD -- ProjectSettings/ProjectSettings.asset Assets/Editor/Tests/StandaloneDisplaySettingsTests.cs
```

Expected: Task 1의 커밋 이후 추적되는 소스 변경이 없고, 빌드 산출물은 Git에 추가되지 않는다.
