# Preternatural 멀티플레이어 + 근접 보이스 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 기존 싱글플레이 1인칭 공포 게임에 최대 4인 풀 코옵(상호작용·몬스터·사망→관전·게임오버 동기화)과 거리 감쇠 근접 보이스를 추가한다. 싱글 경로는 그대로 보존한다.

**Architecture:** NGO 2.12 Owner-authority 플레이어 + 호스트 권위 몬스터/월드 상태. Unity Sessions API(방 코드+Relay)로 접속, Vivox 포지셔널 채널로 보이스. 기존 상호작용 스크립트는 `NetToggleSync`/`NetOneShotSync` 어댑터를 경유하도록 입력 지점만 최소 수정 — 오프라인이면 어댑터가 즉시 로컬 실행하므로 싱글 동작 동일.

**Tech Stack:** Unity 6000.4.0f1, com.unity.netcode.gameobjects 2.12.0, com.unity.services.multiplayer 2.2.4, com.unity.services.vivox 16.11.0, com.unity.services.authentication 3.7.3, com.unity.multiplayer.playmode 2.0.2, unityMCP(검증)

## Global Constraints

- **브랜치: `multi` 고정.** 모든 커밋 전 `git branch --show-current` 확인. 다른 브랜치면 작업 중단 후 BLOCKED 보고.
- 저장소: `/Users/yoma/projects/jamcoding/jungwon/Preternatural` (git — Unity VC 아님).
- **이식 소스(RESTORE):** `/private/tmp/claude-501/-Users-yoma-projects-jamcoding-jungwon/642d51fd-cb07-4c11-9054-4aaeea801d94/scratchpad/copyright-restore` — jungwon Copyright Project cs:5 복원본. 이 경로가 없으면 BLOCKED 보고(재복원 필요).
- **검증은 unityMCP.** 에디터가 Preternatural 프로젝트로 열려 있어야 함. 각 태스크 시작 시 `mcpforunity://instances` 확인, 다중 연결이면 `set_active_instance`로 Preternatural 지정. 연결 불가면 BLOCKED 보고 — **에디터 재시작 금지, Library 삭제 금지, 배치 CLI 실행 금지.**
- 컴파일 확인 = unityMCP `refresh_unity` 후 `read_console`(Error 필터)에서 에러 0건.
- 새 코드 네임스페이스: 이식 파일은 원본 `Game.*` 유지, 신규 파일은 `Game.Net`/`Game.Player` 등 같은 체계. asmdef 만들지 않음(전부 Assembly-CSharp).
- 씬 이름 상수: 메뉴=`"Homescreen"`, 게임=`"GameScene"`.
- 최대 인원 4 (`SessionManager.MaxPlayers`).
- Sessions SDK가 NetworkManager를 자동 시작 — `StartHost`/`StartClient` 직접 호출 금지.
- `Channel3DProperties(15, 2, 1.0f, "InverseByDistance")`는 반드시 위치 인자로만 생성.
- EditMode 테스트 위치: `Assets/Editor/Tests/` (Assembly-CSharp-Editor, asmdef 불필요). 실행: unityMCP `run_tests`(EditMode).
- 기존 스크립트 수정은 이 계획에 명시된 지점만. 연출 코루틴·로직 본문 무수정.
- 커밋 메시지 끝: `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`

---

### Task 1: 패키지 추가 + 컴파일 확인

**Files:**
- Modify: `Packages/manifest.json`

**Interfaces:**
- Produces: NGO/Sessions/Vivox/Auth/MPPM 패키지 사용 가능 상태

- [ ] **Step 1: manifest.json에 의존성 5개 추가**

`"com.coplaydev.unity-mcp"` 줄 아래에 삽입:

```json
    "com.unity.multiplayer.playmode": "2.0.2",
    "com.unity.netcode.gameobjects": "2.12.0",
    "com.unity.services.authentication": "3.7.3",
    "com.unity.services.multiplayer": "2.2.4",
    "com.unity.services.vivox": "16.11.0",
```

(알파벳 순서 유지 — `com.unity.modules.*` 앞, 기존 항목 사이 올바른 위치에.)

- [ ] **Step 2: 패키지 해석 + 컴파일 확인**

unityMCP `manage_packages`(resolve) 또는 `refresh_unity` 실행 → `read_console`(Error)에서 에러 0건 확인. Vivox/NGO 어셈블리 로드 확인: `unity_reflect`로 `Unity.Netcode.NetworkManager` 타입 존재 확인.

- [ ] **Step 3: 커밋**

```bash
git add Packages/manifest.json Packages/packages-lock.json
git commit -m "feat: NGO+Sessions+Vivox+Auth 패키지 추가"
```

---

### Task 2: 에셋 + 공용 코드 이식

**Files:**
- Create: `Assets/Models/Player/` (RESTORE에서 복사), `Assets/Sky_Protective_suit/`, `Assets/VanillaLoopStudio/`
- Create: `Assets/02.Scripts/Net/{AuthProfile.cs, NicknameUtil.cs, SessionErrorMessages.cs, GameBootstrap.cs, SessionManager.cs, NetworkManagerGuard.cs, VoiceManager.cs, VoicePositionUpdater.cs, PlayerAnimationDriver.cs, SpectatorCamera.cs}`
- Test: `Assets/Editor/Tests/NicknameUtilTests.cs`

**Interfaces:**
- Produces:
  - `Game.Core.AuthProfile.Resolve()` — 인스턴스별 인증 프로필 문자열
  - `Game.Core.NicknameUtil.Normalize(string)/ToFixed(string)` — UTF-8 경계 자르기
  - `Game.Core.GameBootstrap` — UGS 초기화+익명 로그인, `static bool IsReady`, `static string StatusMessage`
  - `Game.Net.SessionManager` — `Instance`, `CreateRoomAsync(nick)`, `JoinRoomAsync(code,nick)`, `LeaveRoomAsync()`, `ActiveSession`, `JoinCode`, `InSession`, `MaxPlayers=4`, `SessionStarted/SessionEnded` 이벤트, `LastEndReason`
  - `Game.Voice.VoiceManager` — `Instance`, `VoiceReady`, `ToggleMute()`, `SetMuted(bool)`, 세션 참여 시 포지셔널 채널 자동 조인
  - `Game.Player.PlayerAnimationDriver` — Animator `Speed` 파라미터 구동
  - `Game.Gameplay.SpectatorCamera` — 자유비행 관전(기본 비활성)

- [ ] **Step 1: 에셋 폴더 3개 복사 (.meta 포함 — GUID 보존으로 프리팹 링크 유지)**

```bash
RESTORE="/private/tmp/claude-501/-Users-yoma-projects-jamcoding-jungwon/642d51fd-cb07-4c11-9054-4aaeea801d94/scratchpad/copyright-restore"
DEST="/Users/yoma/projects/jamcoding/jungwon/Preternatural/Assets"
cp -R "$RESTORE/Assets/Sky_Protective_suit" "$RESTORE/Assets/Sky_Protective_suit.meta" \
      "$RESTORE/Assets/VanillaLoopStudio" "$RESTORE/Assets/VanillaLoopStudio.meta" \
      "$RESTORE/Assets/Models" "$RESTORE/Assets/Models.meta" "$DEST/"
```

- [ ] **Step 2: 스크립트 10개 복사**

`Assets/02.Scripts/Net/` 폴더 생성 후 RESTORE의 `Assets/Scripts/`에서 복사 (`.meta`는 복사하지 않음 — 새 GUID 생성):
Core/AuthProfile.cs, Core/NicknameUtil.cs, Core/SessionErrorMessages.cs, Core/GameBootstrap.cs, Net/SessionManager.cs, Net/NetworkManagerGuard.cs, Voice/VoiceManager.cs, Voice/VoicePositionUpdater.cs, Player/PlayerAnimationDriver.cs, Gameplay/SpectatorCamera.cs

- [ ] **Step 3: SessionManager 씬 상수 수정**

`SessionManager.cs` 29-30행:

```csharp
        const string GameScene = "GameScene";
        const string MenuScene = "Homescreen";
```

- [ ] **Step 4: 테스트 이식**

RESTORE의 EditMode 테스트 중 NicknameUtil 관련 파일을 찾아(`grep -rl NicknameUtil "$RESTORE"` 로 테스트 파일 확인) `Assets/Editor/Tests/NicknameUtilTests.cs`로 복사. asmdef 참조 없는 순수 NUnit이어야 함 — `using NUnit.Framework;`와 `using Game.Core;`만 필요.

- [ ] **Step 5: 컴파일 + 테스트 확인**

unityMCP `refresh_unity` → `read_console` 에러 0건 → `run_tests`(EditMode) 전체 통과.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Models* Assets/Sky_Protective_suit* Assets/VanillaLoopStudio* "Assets/02.Scripts/Net" Assets/Editor
git commit -m "feat: jungwon 공용 코드+플레이어 모델 에셋 이식"
```

---

### Task 3: 어댑터 코어 — NetToggleSync / NetOneShotSync + 타겟 선정 로직

**Files:**
- Create: `Assets/02.Scripts/Net/NetToggleSync.cs`
- Create: `Assets/02.Scripts/Net/NetOneShotSync.cs`
- Create: `Assets/02.Scripts/Net/NetTargeting.cs`
- Test: `Assets/Editor/Tests/NetTargetingTests.cs`

**Interfaces:**
- Produces:
  - `Game.Net.NetToggleSync : NetworkBehaviour` — `event Action<bool> OnStateChanged`, `bool State`, `void RequestSet(bool)`, `static bool Online`
  - `Game.Net.NetOneShotSync : NetworkBehaviour` — `event Action OnFired`, `bool Fired`, `void RequestFire()`
  - `Game.Net.NetTargeting.Nearest(Vector3, IReadOnlyList<NetTargeting.Candidate>) : int` — 후보 없으면 -1

- [ ] **Step 1: 타겟 선정 실패 테스트 작성**

```csharp
using NUnit.Framework;
using UnityEngine;
using Game.Net;

public class NetTargetingTests
{
    static NetTargeting.Candidate C(float x, bool alive = true, bool hidden = false)
        => new NetTargeting.Candidate { Position = new Vector3(x, 0, 0), Alive = alive, Hidden = hidden };

    [Test] public void Nearest_PicksClosestAliveVisible()
    {
        var list = new[] { C(10f), C(3f), C(1f, alive: false), C(2f, hidden: true) };
        Assert.AreEqual(1, NetTargeting.Nearest(Vector3.zero, list));
    }

    [Test] public void Nearest_ReturnsMinusOneWhenNoneEligible()
    {
        var list = new[] { C(1f, alive: false), C(2f, hidden: true) };
        Assert.AreEqual(-1, NetTargeting.Nearest(Vector3.zero, list));
    }

    [Test] public void Nearest_EmptyList_ReturnsMinusOne()
        => Assert.AreEqual(-1, NetTargeting.Nearest(Vector3.zero, new NetTargeting.Candidate[0]));
}
```

- [ ] **Step 2: 테스트 실패 확인** — unityMCP `run_tests`(EditMode): `NetTargeting` 미정의 컴파일 에러 또는 실패 확인.

- [ ] **Step 3: 구현**

`NetTargeting.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Game.Net
{
    /// 몬스터 호스트 권위 타겟 선정 — 순수 로직 (테스트 대상).
    public static class NetTargeting
    {
        public struct Candidate { public Vector3 Position; public bool Alive; public bool Hidden; }

        public static int Nearest(Vector3 from, IReadOnlyList<Candidate> candidates)
        {
            int best = -1; float bestSqr = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (!candidates[i].Alive || candidates[i].Hidden) continue;
                float d = (candidates[i].Position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = i; }
            }
            return best;
        }
    }
}
```

`NetToggleSync.cs`:

```csharp
using System;
using Unity.Netcode;

namespace Game.Net
{
    /// 상호작용 상태 1개(열림/제거됨 등) 동기화 어댑터.
    /// 오프라인(싱글)에서는 RequestSet이 즉시 OnStateChanged를 로컬 호출 — 기존 동작 보존.
    public class NetToggleSync : NetworkBehaviour
    {
        readonly NetworkVariable<bool> _state = new(); // 서버 쓰기 기본
        bool _localState;

        public event Action<bool> OnStateChanged;
        public static bool Online =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        public bool State => Online && IsSpawned ? _state.Value : _localState;

        public override void OnNetworkSpawn()
        {
            _state.OnValueChanged += HandleChanged;
            if (_state.Value) OnStateChanged?.Invoke(true); // 늦은 입장 동기화
        }

        public override void OnNetworkDespawn() => _state.OnValueChanged -= HandleChanged;
        void HandleChanged(bool _, bool now) => OnStateChanged?.Invoke(now);

        public void RequestSet(bool value)
        {
            if (!Online || !IsSpawned)
            {
                if (_localState == value) return;
                _localState = value;
                OnStateChanged?.Invoke(value);
                return;
            }
            SetRpc(value);
        }

        [Rpc(SendTo.Server)]
        void SetRpc(bool value) { _state.Value = value; }
    }
}
```

`NetOneShotSync.cs`:

```csharp
using System;
using Unity.Netcode;

namespace Game.Net
{
    /// 1회성 월드 이벤트(추격 트리거 등) 동기화. 최초 1회만 전파.
    public class NetOneShotSync : NetworkBehaviour
    {
        readonly NetworkVariable<bool> _fired = new();
        bool _localFired;

        public event Action OnFired;
        public bool Fired => NetToggleSync.Online && IsSpawned ? _fired.Value : _localFired;

        public override void OnNetworkSpawn()
        {
            _fired.OnValueChanged += HandleChanged;
            // 늦은 입장자에게는 재생하지 않음 — 이미 지나간 연출.
        }

        public override void OnNetworkDespawn() => _fired.OnValueChanged -= HandleChanged;
        void HandleChanged(bool was, bool now) { if (!was && now) OnFired?.Invoke(); }

        public void RequestFire()
        {
            if (!NetToggleSync.Online || !IsSpawned)
            {
                if (_localFired) return;
                _localFired = true;
                OnFired?.Invoke();
                return;
            }
            FireRpc();
        }

        [Rpc(SendTo.Server)]
        void FireRpc() { if (!_fired.Value) _fired.Value = true; }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인** — `run_tests`(EditMode) 3건 통과, `read_console` 에러 0건.

- [ ] **Step 5: 커밋**

```bash
git add "Assets/02.Scripts/Net/NetToggleSync.cs" "Assets/02.Scripts/Net/NetOneShotSync.cs" "Assets/02.Scripts/Net/NetTargeting.cs" Assets/Editor/Tests
git commit -m "feat: 동기화 어댑터 코어(NetToggleSync/NetOneShotSync) + 타겟 선정 로직"
```

---

### Task 4: NetPlayer — 네트워크 플레이어 컴포넌트

**Files:**
- Create: `Assets/02.Scripts/Net/NetPlayer.cs`

**Interfaces:**
- Consumes: `PlayerMovement`(public isHiding), `FirstPersonCamera`, `FlashlightController`, `ToolHolder`, `CrosshairInteract`, `PlayerAnimationDriver`, `NicknameUtil.ToFixed`
- Produces: `Game.Net.NetPlayer : NetworkBehaviour` —
  - `static readonly List<NetPlayer> All`
  - `NetworkVariable<bool> FlashlightOn, IsHiding, IsAlive`(오너 쓰기 Flashlight/Hiding, 서버 쓰기 Alive), `NetworkVariable<FixedString64Bytes> Nickname`
  - `Camera HeadCamera { get; }` (원격은 disabled Camera — 몬스터 시선 판정용 transform)
  - `void ServerKill()` (서버 전용 — 사망 처리 시작)
  - `event Action<NetPlayer> LocalPlayerDied` (static)

- [ ] **Step 1: 구현**

```csharp
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Game.Core;
using Game.Player;

namespace Game.Net
{
    /// 스폰된 플레이어 루트. 오너: 기존 싱글 컴포넌트 활성 + 상태 발행.
    /// 원격: 컨트롤 비활성 + 모델 표시 + 손전등 라이트 미러.
    public class NetPlayer : NetworkBehaviour
    {
        public static readonly List<NetPlayer> All = new();
        public static event Action<NetPlayer> LocalPlayerDied;

        [Header("빌드 툴이 배선")]
        [SerializeField] Behaviour[] ownerOnly;        // PlayerMovement, FirstPersonCamera, FlashlightController, ToolHolder, CrosshairInteract, AudioListener
        [SerializeField] GameObject ownerCameraObject; // 카메라 GO (원격은 Camera 비활성, transform은 회전 동기 대상)
        [SerializeField] GameObject remoteModelRoot;   // Sky Protective Suit (오너에겐 숨김)
        [SerializeField] Light remoteFlashlight;       // 원격 표시용 손전등 라이트
        [SerializeField] Light ownerFlashlight;        // 기존 FlashlightController의 Light
        [SerializeField] PlayerMovement movement;
        const float KillZ = -30f;

        public NetworkVariable<bool> FlashlightOn = new(writePerm: NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> IsHiding = new(writePerm: NetworkVariableWritePermission.Owner);
        public NetworkVariable<bool> IsAlive = new(true);
        public NetworkVariable<FixedString64Bytes> Nickname = new(writePerm: NetworkVariableWritePermission.Owner);

        public Camera HeadCamera { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); LocalPlayerDied = null; }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            HeadCamera = ownerCameraObject.GetComponent<Camera>();
            bool owner = IsOwner;

            foreach (var b in ownerOnly) if (b) b.enabled = owner;
            HeadCamera.enabled = owner;
            var listener = ownerCameraObject.GetComponent<AudioListener>();
            if (listener) listener.enabled = owner;
            remoteModelRoot.SetActive(!owner);
            remoteFlashlight.enabled = false;

            if (owner)
            {
                Nickname.Value = NicknameUtil.ToFixed(SessionManager.LocalNickname);
                var scenePlayer = GameObject.Find("Player");
                if (scenePlayer && scenePlayer != gameObject)
                {
                    // 스폰 위치 = 씬 Player 자리 + 클라이언트별 오프셋 (겹침 방지)
                    var cc = GetComponent<CharacterController>();
                    if (cc) cc.enabled = false;
                    transform.SetPositionAndRotation(
                        scenePlayer.transform.position + scenePlayer.transform.right * ((OwnerClientId % 4) * 1.4f),
                        scenePlayer.transform.rotation);
                    if (cc) cc.enabled = true;
                    scenePlayer.SetActive(false);
                }
            }

            FlashlightOn.OnValueChanged += (_, on) => { if (!IsOwner) remoteFlashlight.enabled = on; };
            IsAlive.OnValueChanged += HandleAliveChanged;
        }

        public override void OnNetworkDespawn() => All.Remove(this);

        void Update()
        {
            if (!IsOwner || !IsSpawned) return;
            FlashlightOn.Value = ownerFlashlight && ownerFlashlight.enabled;
            IsHiding.Value = movement && movement.isHiding;
            if (transform.position.y < KillZ)
            {
                var cc = GetComponent<CharacterController>();
                if (cc) cc.enabled = false;
                transform.position = new Vector3(0f, 2f, 0f);
                if (cc) cc.enabled = true;
            }
        }

        public void ServerKill()
        {
            if (!IsServer || !IsAlive.Value) return;
            IsAlive.Value = false;
        }

        void HandleAliveChanged(bool _, bool alive)
        {
            if (alive) return;
            if (!IsOwner) { remoteModelRoot.SetActive(false); remoteFlashlight.enabled = false; return; }
            foreach (var b in ownerOnly) if (b) b.enabled = false;
            LocalPlayerDied?.Invoke(this);
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인** — `refresh_unity` → `read_console` 에러 0건.

- [ ] **Step 3: 커밋**

```bash
git add "Assets/02.Scripts/Net/NetPlayer.cs"
git commit -m "feat: NetPlayer — 오너 게이팅, 손전등/은신/생존 동기화, kill-Z"
```

---

### Task 5: 에디터 빌드 툴 — NetworkPlayer 프리팹 + 씬 배선

**Files:**
- Create: `Assets/Editor/NetSetupTool.cs`

**Interfaces:**
- Consumes: `NetPlayer`, `NetToggleSync`, `NetOneShotSync`, `PlayerAnimationDriver`, RESTORE 이식 모델 `Assets/Models/Player/PlayerModel.prefab`
- Produces:
  - 메뉴 `Game/Net/1. 플레이어 프리팹 생성` → `Assets/Prefabs/NetPlayer.prefab`
  - 메뉴 `Game/Net/2. 씬 네트워크 배선` → Homescreen에 부트스트랩 GO, GameScene에 어댑터 부착 + 해시 이중저장
  - `NetSetupTool.FixSceneNetworkObjectHashes(Scene, string)` — GlobalObjectIdHash=0 방지

- [ ] **Step 1: 구현**

핵심 로직 (전체 파일은 이 구조대로 작성):

```csharp
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Net;
using Game.Player;

public static class NetSetupTool
{
    const string PlayerPrefabPath = "Assets/Prefabs/NetPlayer.prefab";
    const string ModelPrefabPath = "Assets/Models/Player/PlayerModel.prefab";
    const string GameScenePath = "Assets/01.Scenes/GameScene.unity";
    const string HomeScenePath = "Assets/01.Scenes/Homescreen.unity";

    [MenuItem("Game/Net/1. 플레이어 프리팹 생성")]
    public static void BuildPlayerPrefab()
    {
        var scene = EditorSceneManager.OpenScene(GameScenePath);
        var scenePlayer = GameObject.Find("Player");
        if (!scenePlayer) { Debug.LogError("[NetSetup] GameScene에 Player 없음"); return; }

        var root = Object.Instantiate(scenePlayer);
        root.name = "NetPlayer";
        // 1) NetworkObject + Owner NetworkTransform
        var no = root.AddComponent<NetworkObject>();
        var nt = root.AddComponent<NetworkTransform>();
        nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
        nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
        // 2) 카메라 GO 찾기 (자식에서 Camera 검색; 없으면 씬의 FirstPersonCamera 보유 GO를 자식으로 복제)
        var cam = root.GetComponentInChildren<Camera>(true);
        // ... 카메라가 씬 별도 GO면: 그 GO를 복제해 root 자식으로 붙이고 FirstPersonCamera 참조 재배선
        // 카메라 GO에도 Owner NetworkTransform 추가(회전 동기 — 몬스터 시선/보이스 위치용)
        // 3) 원격 모델: PlayerModel.prefab 인스턴스 → root 자식, 발 y 오프셋은 CharacterController 기준
        //    Animator.runtimeAnimatorController = Assets/Models/Player/PlayerLocomotion.controller
        //    applyRootMotion=false, PlayerAnimationDriver 부착
        // 4) 원격 손전등 Light: 모델 머리 근처 SpotLight 생성, disabled
        // 5) NetPlayer 부착 + SerializedObject로 ownerOnly/모델/라이트 필드 배선
        //    ownerOnly = { PlayerMovement, FirstPersonCamera, FlashlightController, ToolHolder, CrosshairInteract }
        //    (각각 GetComponentInChildren로 수집, null 허용)
        // 6) PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath) 후 씬 인스턴스 삭제
    }

    [MenuItem("Game/Net/2. 씬 네트워크 배선")]
    public static void WireScenes()
    {
        WireHomescreen();
        WireGameScene();
    }

    static void WireHomescreen()
    {
        var scene = EditorSceneManager.OpenScene(HomeScenePath);
        // "NetBootstrap" GO 없으면 생성:
        //   NetworkManager (UnityTransport 자동 추가, PlayerPrefab = NetPlayer.prefab,
        //                   NetworkConfig.EnableSceneManagement = true)
        //   GameBootstrap, SessionManager, VoiceManager, NetworkManagerGuard, MultiplayerMenu(Task 6)
        // 기존 GO 무수정.
        EditorSceneManager.SaveScene(scene);
    }

    static void WireGameScene()
    {
        var scene = EditorSceneManager.OpenScene(GameScenePath);
        // 어댑터 부착 대상 (이름→컴포넌트 검색, 각 GO에 NetworkObject+어댑터 없으면 추가):
        //   DrawerShelf 전부 → NetToggleSync
        //   LeverInteractable 전부 → NetToggleSync
        //   DoorInteractable 전부 → NetToggleSync
        //   BoardedDoor 전부 → NetToggleSync (문 열림), WoodBoard 전부 → NetToggleSync (판자 제거)
        //   MonitorDoorController 전부 → NetToggleSync
        //   PickupItem 전부 → NetworkObject + NetPickupSync(Task 7)
        //   MonsterChaseTrigger/JumpscareTriggerPart2/MonsterJumpscareTrigger 전부 → NetOneShotSync
        //   MonsterLookAI GO → NetworkObject + NetworkTransform(서버 권위 기본) + MonsterNetAdapter(Task 8)
        EditorSceneManager.SaveScene(scene);
        FixSceneNetworkObjectHashes(scene, GameScenePath);
    }

    /// jungwon 함정 ⑦: in-scene NetworkObject GlobalObjectIdHash는
    /// 씬 저장 → OnValidate 리플렉션 재호출 → 재저장해야 0이 아님.
    public static void FixSceneNetworkObjectHashes(Scene scene, string path)
    {
        EditorSceneManager.SaveScene(scene, path);
        var method = typeof(NetworkObject).GetMethod("OnValidate",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null) throw new MissingMethodException("NetworkObject.OnValidate 리플렉션 실패 — NGO 버전 확인");
        foreach (var no in Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        { method.Invoke(no, null); EditorUtility.SetDirty(no); }
        EditorSceneManager.SaveScene(scene, path);
        foreach (var no in Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(no);
            if (so.FindProperty("GlobalObjectIdHash").uintValue == 0)
                Debug.LogError($"[NetSetup] 해시 0: {no.name}");
        }
    }
}
```

주석으로 표시한 `...` 부분은 실제 코드로 완성할 것 — 카메라 탐색 분기, 모델 인스턴스화, SerializedObject 배선 모두 구현. RESTORE의 `Assets/Editor/SceneBuilder.cs`에서 `BuildModelVisual`/`BuildPlayerAnimatorController`/`EnsureClipLoops` 참고(동일 로직 재사용 가능 — 필요 함수만 NetSetupTool로 복사).

- [ ] **Step 2: 실행 + 검증**

unityMCP `execute_menu_item`으로 메뉴 1→2 순서 실행 → `read_console` 에러 0건, "해시 0" 로그 없음 확인. `manage_scene`(get_hierarchy)으로 Homescreen에 NetBootstrap 존재, GameScene 어댑터 부착 확인. `Assets/Prefabs/NetPlayer.prefab` 존재 확인.

- [ ] **Step 3: 커밋**

```bash
git add Assets/Editor/NetSetupTool.cs Assets/Prefabs Assets/01.Scenes
git commit -m "feat: NetSetupTool — NetPlayer 프리팹 생성 + 씬 네트워크 배선 + 해시 이중저장"
```

---

### Task 6: Homescreen 멀티플레이 UI

**Files:**
- Create: `Assets/02.Scripts/Net/MultiplayerMenu.cs`

**Interfaces:**
- Consumes: `SessionManager`, `GameBootstrap.IsReady/StatusMessage`, `NicknameUtil.Normalize`
- Produces: `Game.UI.MultiplayerMenu : MonoBehaviour` — 런타임 코드 생성 UGUI 패널(M 버튼 아님, Homescreen에 "멀티플레이" 버튼 추가). 기존 싱글 시작 버튼 무수정.

- [ ] **Step 1: 구현**

런타임 self-build UI (씬 직렬화 회피 — jungwon 함정 ③ AddListener 비직렬화):

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Game.Core;
using Game.Net;

namespace Game.UI
{
    /// Homescreen 멀티 패널. Awake에서 UGUI 전부 코드 생성+와이어링.
    /// 기존 Homescreen UI는 건드리지 않고 별도 Canvas 위에 그림.
    public class MultiplayerMenu : MonoBehaviour
    {
        Canvas _canvas; GameObject _panel;
        TMP_InputField _nickInput, _codeInput;
        TMP_Text _status;
        Button _openBtn, _createBtn, _joinBtn, _startBtn, _leaveBtn;
        bool _busy;

        void Awake()
        {
            if (SceneManager.GetActiveScene().name != "Homescreen") return;
            BuildUi(); // Canvas(sortingOrder=100) + "멀티플레이" 토글 버튼(우하단) + 패널
        }
        // BuildUi: 닉네임 입력, [방 만들기], 코드 입력, [코드 입장], 상태 라벨,
        //          세션 중: 방 코드 표시 + [게임 시작](호스트만) + [나가기]
        // OnCreate: _busy 가드 → await SessionManager.Instance.CreateRoomAsync(nick)
        //           예외 시 _status.text = SessionErrorMessages.MessageFor(e)
        // OnJoin:   NicknameUtil.Normalize + 코드 대문자 트림 → JoinRoomAsync
        // OnStart:  호스트만 — NetworkManager.SceneManager.LoadScene("GameScene", LoadSceneMode.Single)
        // OnLeave:  LeaveRoomAsync
        // Update:   GameBootstrap.IsReady 전 버튼 비활성 + StatusMessage 표시,
        //           SessionManager.LastEndReason 있으면 표시 후 소거
        // 커서: 패널 열릴 때 Cursor.lockState=None, visible=true (jungwon 함정 ⑧)
    }
}
```

전체 구현 작성(대략 250-350행). RESTORE `Assets/Scripts/UI/MainMenuUI.cs` 참고 — 동일 패턴(입력 검증, busy 가드, 에러 매핑) 재사용하되 Preternatural은 UI를 코드 생성.

- [ ] **Step 2: NetSetupTool 배선 확인** — Task 5의 NetBootstrap GO에 MultiplayerMenu 포함됨. `refresh_unity` → 에러 0건. 에디터 Play(Homescreen)로 패널 열림/닫힘, "UGS 미링크" 상태 문구 표시 확인(`manage_editor` play + `read_console`).

- [ ] **Step 3: 커밋**

```bash
git add "Assets/02.Scripts/Net/MultiplayerMenu.cs"
git commit -m "feat: Homescreen 멀티플레이 패널 (코드 생성 UI, 싱글 경로 무수정)"
```

---

### Task 7: 상호작용 동기화 — 최소 수정 + Pickup

**Files:**
- Modify: `Assets/02.Scripts/DrawerShelf.cs`, `LeverInteractable.cs`, `DoorInteractable.cs`, `LeverDoorController.cs`, `BoardedDoor.cs`, `WoodBoard.cs`, `MonitorDoorController.cs`, `MonsterChaseTrigger.cs`, `JumpscareTriggerPart2.cs`, `MonsterJumpscareTriggerPart3.cs`
- Create: `Assets/02.Scripts/Net/NetPickupSync.cs`
- Modify: `Assets/02.Scripts/PickupItem.cs`

**Interfaces:**
- Consumes: `NetToggleSync`, `NetOneShotSync`
- Produces: 모든 공유 월드 상태가 4클라이언트에서 일치. 오프라인 동작 불변.

**수정 패턴 (전 스크립트 공통):** 입력 감지 지점에서 직접 실행하던 호출을 어댑터 경유로 치환. 어댑터 없으면(컴포넌트 미부착) 기존 코드 그대로 실행 — 이중 안전.

- [ ] **Step 1: DrawerShelf 수정 (대표 예 — 나머지 동일 패턴)**

클래스에 추가:

```csharp
Game.Net.NetToggleSync _sync;

void Awake()
{
    _sync = GetComponent<Game.Net.NetToggleSync>();
    if (_sync != null) _sync.OnStateChanged += open => StartCoroutine(MoveDrawer(open));
}
```

Update()의 E키 처리에서 `StartCoroutine(MoveDrawer(!isOpen))` (실제 기존 호출부 확인 후) 치환:

```csharp
if (_sync != null) _sync.RequestSet(!_sync.State);
else StartCoroutine(MoveDrawer(!isOpen)); // 어댑터 없으면 기존 경로
```

- [ ] **Step 2: 나머지 토글류 동일 적용**
  - `LeverInteractable.OnInteract()` → `_sync.RequestSet(true)`; `OnStateChanged += _ => controller.PullLever(leverNumber)` (중복 호출 방지: 이미 당긴 레버면 무시 — `_sync.State` 확인)
  - `DoorInteractable.OnInteract()` → 잠김 상태(레버 3개 미완)면 기존 `controller.InteractWithDoor()` 그대로(로컬 흔들림 연출), 열림 가능 상태면 `_sync.RequestSet(true)`; `OnStateChanged += _ => controller.InteractWithDoor()`
  - `WoodBoard`: 크로우바 제거 성공 지점 → `_sync.RequestSet(true)`; `OnStateChanged += _ => StartCoroutine(RemoveBoard())` — RemoveBoard 내 도구 소모/파괴 로직이 전 클라이언트에서 실행되도록 코루틴 시작만 치환, 도구 확인은 요청자만
  - `BoardedDoor.OpenDoor` 진입점 → 토글 패턴
  - `MonitorDoorController.OnInteract()` → 원샷처럼 사용: `_sync.RequestSet(true)`; `OnStateChanged += _ => StartCoroutine(KeyboardSequence())`
  - 트리거 3종(`MonsterChaseTrigger`, `JumpscareTriggerPart2`, `MonsterJumpscareTrigger`): `OnTriggerEnter`에서 로컬 플레이어 콜라이더인지 확인(`other.GetComponentInParent<NetPlayer>()?.IsOwner != false`) 후 `_oneShot.RequestFire()`; `OnFired += ` 기존 코루틴 시작. 오프라인이면 기존 즉시 실행.
  - `JumpscareTriggerpart1`(사운드만): 수정하지 않음 — 로컬 연출 유지.
  - `DoorTeleport`: 수정하지 않음 — 개인 이동+연출, NetworkTransform이 위치 자동 동기화.
  - `CabinetHide`: 수정하지 않음 — NetPlayer.IsHiding이 PlayerMovement.isHiding으로 이미 동기화.

- [ ] **Step 3: NetPickupSync 구현**

```csharp
using Unity.Netcode;
using UnityEngine;

namespace Game.Net
{
    /// 아이템 획득 서버 권위 중재. 먼저 요청한 1명만 획득.
    /// 획득 시 전 클라이언트에서 월드 오브젝트 숨김, 드롭 시 지정 위치에 재표시.
    public class NetPickupSync : NetworkBehaviour
    {
        readonly NetworkVariable<bool> _taken = new();
        readonly NetworkVariable<ulong> _holder = new();
        public bool Taken => NetToggleSync.Online && IsSpawned ? _taken.Value : _localTaken;
        bool _localTaken;
        public event System.Action<bool, ulong> OnTakenChanged; // (taken, holderClientId)

        public override void OnNetworkSpawn()
        {
            _taken.OnValueChanged += (_, t) => OnTakenChanged?.Invoke(t, _holder.Value);
            if (_taken.Value) OnTakenChanged?.Invoke(true, _holder.Value);
        }

        public void RequestPickup()
        {
            if (!NetToggleSync.Online || !IsSpawned)
            { _localTaken = true; OnTakenChanged?.Invoke(true, 0); return; }
            PickupRpc();
        }

        public void RequestDrop(Vector3 position)
        {
            if (!NetToggleSync.Online || !IsSpawned)
            { _localTaken = false; OnTakenChanged?.Invoke(false, 0); return; }
            DropRpc(position);
        }

        [Rpc(SendTo.Server)]
        void PickupRpc(RpcParams p = default)
        {
            if (_taken.Value) return; // 이미 다른 사람이 가짐
            _holder.Value = p.Receive.SenderClientId;
            _taken.Value = true;
        }

        [Rpc(SendTo.Server)]
        void DropRpc(Vector3 position, RpcParams p = default)
        {
            if (!_taken.Value || _holder.Value != p.Receive.SenderClientId) return;
            SetDropPositionRpc(position);
            _taken.Value = false;
        }

        [Rpc(SendTo.Everyone)]
        void SetDropPositionRpc(Vector3 position) => transform.position = position;
    }
}
```

- [ ] **Step 4: PickupItem/ToolHolder 연결**

`PickupItem.PickUp()` 진입부: `_sync != null` 이면 `_sync.RequestPickup()`만 호출하고 실제 장착은 `OnTakenChanged` 콜백에서 — `holderClientId == NetworkManager.Singleton.LocalClientId`면 기존 장착 로직 실행, 아니면 `gameObject.SetActive(false)`(원격 획득 = 월드에서 숨김). `ToolHolder.DropTool()`의 던지기 직후 `_sync.RequestDrop(droppedPos)` 호출 추가(툴 오브젝트에서 NetPickupSync 검색).

- [ ] **Step 5: 검증**

`refresh_unity` → 에러 0건. NetSetupTool 메뉴 2 재실행(어댑터 부착 갱신) → 해시 로그 확인. 에디터 Play(GameScene 직접, 오프라인): 서랍/레버/픽업 기존 동작 그대로인지 `read_console` 에러 확인 — **싱글 회귀 게이트.**

- [ ] **Step 6: 커밋**

```bash
git add Assets/02.Scripts
git commit -m "feat: 상호작용 동기화 — 토글/원샷/픽업 어댑터 경유, 오프라인 경로 보존"
```

---

### Task 8: 몬스터 호스트 권위 + 사망 처리

**Files:**
- Create: `Assets/02.Scripts/Net/MonsterNetAdapter.cs`
- Modify: `Assets/02.Scripts/MonsterAI.cs` (Jumpscare 분기 1곳)

**Interfaces:**
- Consumes: `MonsterLookAI`(public player/playerCamera/agent 필드, Jumpscare()), `NetPlayer.All`, `NetTargeting.Nearest`, `NetPlayer.ServerKill()`
- Produces: `Game.Net.MonsterNetAdapter : NetworkBehaviour` — 호스트만 AI 실행, 타겟 재배선, 원격 애니 상태 동기화, 희생자에게 점프스케어 RPC

- [ ] **Step 1: MonsterNetAdapter 구현**

```csharp
using Unity.Netcode;
using UnityEngine;
using Game.Player;

namespace Game.Net
{
    /// 몬스터 호스트 권위. 클라이언트: AI/NavMesh 비활성(NetworkTransform 수신만).
    /// 호스트: 매 프레임 가장 가까운 생존·비은신 플레이어를 MonsterLookAI.player에 주입.
    public class MonsterNetAdapter : NetworkBehaviour
    {
        [SerializeField] MonsterLookAI ai;
        [SerializeField] UnityEngine.AI.NavMeshAgent agent;
        readonly NetworkVariable<int> _animState = new(); // 0=Idle 1=Run 2=Jumpscare

        public override void OnNetworkSpawn()
        {
            if (ai == null) ai = GetComponent<MonsterLookAI>();
            if (agent == null) agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (!IsServer) { ai.enabled = false; agent.enabled = false; }
            _animState.OnValueChanged += (_, s) => { if (!IsServer) PlayAnim(s); };
        }

        void PlayAnim(int state)
        {
            if (ai.monsterAnimation == null) return;
            var clip = state == 2 ? ai.jumpscareAnimation : state == 1 ? ai.runAnimation : ai.idleAnimation;
            if (clip != null && !ai.monsterAnimation.IsPlaying(clip.name))
                ai.monsterAnimation.CrossFade(clip.name, 0.2f);
        }

        void LateUpdate()
        {
            if (!IsServer || !IsSpawned || NetPlayer.All.Count == 0) return;
            var candidates = new NetTargeting.Candidate[NetPlayer.All.Count];
            for (int i = 0; i < NetPlayer.All.Count; i++)
                candidates[i] = new NetTargeting.Candidate {
                    Position = NetPlayer.All[i].transform.position,
                    Alive = NetPlayer.All[i].IsAlive.Value,
                    Hidden = NetPlayer.All[i].IsHiding.Value };
            int idx = NetTargeting.Nearest(transform.position, candidates);
            if (idx < 0) { ai.player = null; return; }
            var target = NetPlayer.All[idx];
            ai.player = target.transform;
            ai.playerCamera = target.HeadCamera;
            // 단순화(스펙 대비): 시선 감지(CheckLooking)는 최근접 타겟 카메라 기준으로만 판정.
            // 전 생존자 시선 판정은 CheckLooking 로직 복제가 필요해 제외 — 체감 차이 미미.
            _animState.Value = CurrentAnimState(); // ai 상태 → int 매핑 (Animation.IsPlaying 검사)
        }

        int CurrentAnimState()
        {
            if (ai.monsterAnimation == null) return 0;
            if (ai.jumpscareAnimation != null && ai.monsterAnimation.IsPlaying(ai.jumpscareAnimation.name)) return 2;
            if (ai.runAnimation != null && ai.monsterAnimation.IsPlaying(ai.runAnimation.name)) return 1;
            return 0;
        }

        /// 호스트가 공격 판정 시 MonsterAI 수정부에서 호출.
        public void ServerAttack(NetPlayer victim)
        {
            if (!IsServer) return;
            victim.ServerKill();
            JumpscareRpc(RpcTarget.Single(victim.OwnerClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void JumpscareRpc(RpcParams _)
        {
            // 희생자 클라이언트: 로컬 몬스터로 기존 Jumpscare() 연출 실행
            ai.Jumpscare();
        }
    }
}
```

`CurrentAnimState`/`PlayAnim`은 실제 MonsterLookAI 애니 필드명(idleAnimation/runAnimation/jumpscareAnimation)으로 완성. `ai.Jumpscare()`가 private이면 public으로 변경(수정 허용 지점).

- [ ] **Step 2: MonsterAI.cs 공격 분기 수정 (1곳)**

`distanceToPlayer <= attackDistance` → `Jumpscare()` 호출부(216행 부근):

```csharp
var adapter = GetComponent<Game.Net.MonsterNetAdapter>();
if (adapter != null && Game.Net.NetToggleSync.Online)
{
    var victim = ai타겟에서 NetPlayer 획득; // player.GetComponentInParent<NetPlayer>()
    if (victim != null) { adapter.ServerAttack(victim); return; }
}
Jumpscare(); // 오프라인 기존 경로
```

(실제 변수명은 파일 확인 후 맞춤. `Jumpscare()` 접근 제한자 public 변경 포함.)

- [ ] **Step 3: 사망 → 관전 연결**

`NetPlayer.LocalPlayerDied` 구독자를 `MultiplayerMenu`가 아닌 전용 컴포넌트로 — `MonsterNetAdapter` 아래에 두지 말고 `NetPlayer.HandleAliveChanged`의 오너 분기에서 직접:

```csharp
// NetPlayer.HandleAliveChanged 오너 사망 분기 끝에 추가
var spectator = ownerCameraObject.GetComponent<Game.Gameplay.SpectatorCamera>()
             ?? ownerCameraObject.AddComponent<Game.Gameplay.SpectatorCamera>();
spectator.enabled = true; // OnEnable에서 커서 잠금 (이식된 SpectatorCamera 동작)
```

점프스케어 연출(ai.Jumpscare)이 카메라를 비활성화하므로, JumpscareRpc 수신부에서 연출 후 5초 뒤 SpectatorCamera 활성 코루틴으로 감쌈. DeadScreenController는 멀티에서 사용하지 않음(씬 리로드 금지) — 호출 경로가 몬스터 연출에 묶여 있지 않은지 확인하고, 묶여 있으면 `NetToggleSync.Online`이면 건너뛰기.

- [ ] **Step 4: 게임오버 — 전원 사망 시**

`MonsterNetAdapter.ServerAttack` 끝에:

```csharp
bool anyAlive = false;
foreach (var p in NetPlayer.All) if (p.IsAlive.Value) { anyAlive = true; break; }
if (!anyAlive) GameOverRpc();
```

```csharp
[Rpc(SendTo.Everyone)]
void GameOverRpc()
{
    Game.Net.SessionManager.LastEndReason = "전원 사망 — 게임 오버";
    _ = Game.Net.SessionManager.Instance.LeaveRoomAsync(); // 세션 종료 → Homescreen 복귀(SessionEnded 핸들러)
}
```

(SessionManager의 SessionEnded 처리에 Homescreen 로드가 이미 있는지 이식 코드 확인 — 없으면 MultiplayerMenu가 SessionEnded 구독해 `SceneManager.LoadScene("Homescreen")`.)

- [ ] **Step 5: 검증** — `refresh_unity` 에러 0건, `run_tests` 통과 유지, NetSetupTool 메뉴 2 재실행(몬스터 어댑터 부착).

- [ ] **Step 6: 커밋**

```bash
git add Assets/02.Scripts
git commit -m "feat: 몬스터 호스트 권위 + 타겟 선정 + 점프스케어/사망/관전/게임오버"
```

---

### Task 9: 근접 보이스 배선

**Files:**
- Modify: `Assets/02.Scripts/Net/NetPlayer.cs` (VoicePositionUpdater 부착)
- Modify: `ProjectSettings/ProjectSettings.asset` (마이크 권한 문구)

**Interfaces:**
- Consumes: `VoiceManager`(Task 2 이식 — 세션 참여 시 자동 채널 조인), `VoicePositionUpdater`
- Produces: 4인 근접 보이스 — 2m 원음량, 15m 무음, 사망자 송신 뮤트

- [ ] **Step 1: 오너 카메라에 VoicePositionUpdater 부착**

`NetPlayer.OnNetworkSpawn` 오너 분기:

```csharp
if (owner) ownerCameraObject.AddComponent<Game.Voice.VoicePositionUpdater>();
```

- [ ] **Step 2: M키 뮤트 토글 + 사망 뮤트**

`NetPlayer.Update()` 오너 분기 앞부분에 추가:

```csharp
if (Input.GetKeyDown(KeyCode.M) && IsAlive.Value && Game.Voice.VoiceManager.Instance != null)
    Game.Voice.VoiceManager.Instance.ToggleMute();
```

이식된 VoiceManager의 `LocalPlayerAlive` 연동 확인. jungwon에서는 PlayerLifeState가 갱신 — Preternatural에서는 `NetPlayer.HandleAliveChanged` 오너 분기에서 `VoiceManager.Instance.SetMuted(true)` 호출 + VoiceManager 내 LocalPlayerAlive 참조가 남아 있으면 컴파일 에러이므로 해당 참조를 public bool 프로퍼티로 유지하고 NetPlayer가 세팅하도록 조정.

- [ ] **Step 3: 마이크 권한 문구**

unityMCP `manage_editor` 또는 직접 편집으로 `ProjectSettings.asset`의 `microphoneUsageDescription:` 에 `근접 음성 대화에 마이크를 사용합니다` 설정.

- [ ] **Step 4: 검증** — `refresh_unity` 에러 0건. (실채널 테스트는 UGS 링크 후 Task 11 스모크에서.)

- [ ] **Step 5: 커밋**

```bash
git add "Assets/02.Scripts/Net" ProjectSettings/ProjectSettings.asset
git commit -m "feat: 근접 보이스 배선 — 위치 갱신, 사망 뮤트, 마이크 권한 문구"
```

---

### Task 10: UGS 링크 게이트 (사용자 액션)

**Files:** 없음 (설정 확인만)

- [ ] **Step 1: 링크 상태 확인** — `ProjectSettings/ProjectSettings.asset`의 `cloudProjectId:` 값 비어 있지 않은지 확인.
- [ ] **Step 2: 비어 있으면 BLOCKED 보고** — 사용자에게 안내: Unity 대시보드에서 프로젝트 생성 → 에디터 Project Settings > Services 링크 → Vivox 활성화(온보딩 완료 — Settings.json이 `ProjectSettings/Packages/com.unity.services.vivox/`에 생겨야 함). 완료 전 Task 11 진행 불가.

---

### Task 11: 스모크 테스트 + 통합 검증

**Files:**
- Create: `Assets/Editor/NetSmokeTest.cs`

**Interfaces:**
- Consumes: 전체 시스템
- Produces: 메뉴 `Game/Net/스모크 테스트` — 로그인→방 생성→GameScene 로드→스폰·접지→서랍 토글→몬스터 타겟 확인→강제 사망→관전 활성→게임오버→Homescreen 복귀, 각 단계 `OK:`/`FAIL:` 로그

- [ ] **Step 1: 구현** — jungwon RESTORE `Assets/Editor/SmokeTest.cs` 패턴 이식(async 단계 러너, 타임아웃, OK/FAIL 로그). Preternatural 단계로 교체:
  1. `GameBootstrap.IsReady` 대기 (30s 타임아웃)
  2. `CreateRoomAsync("스모크")` → JoinCode 6자리 확인
  3. `NetworkManager.SceneManager.LoadScene("GameScene")` → 씬 로드 대기
  4. NetPlayer 스폰 대기 → 접지 확인(0.5s 동안 y 변화 < 0.05, y > -5)
  5. 씬의 NetToggleSync 하나 `RequestSet(true)` → State==true 확인
  6. 몬스터 어댑터 `ai.player != null` 확인
  7. 로컬 NetPlayer `ServerKill()` (호스트=서버) → `IsAlive==false` + SpectatorCamera enabled 확인
  8. 게임오버 경로 → 세션 종료 + Homescreen 복귀 확인
  9. `NetworkManager.Singleton` 정리 상태 확인

- [ ] **Step 2: 실행** — unityMCP `execute_menu_item`("Game/Net/스모크 테스트") → `read_console`에서 FAIL 0건 확인. 실패 시 해당 단계 수정 후 재실행.

- [ ] **Step 3: 싱글 회귀 최종 확인** — 에디터 Play를 Homescreen에서 시작 → 기존 싱글 시작 경로로 GameScene 진입 → `read_console` 에러/예외 0건 (unityMCP `manage_editor` play/stop + 콘솔).

- [ ] **Step 4: EditMode 전체 테스트** — `run_tests` 전체 통과.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Editor/NetSmokeTest.cs
git commit -m "test: 멀티 전체 루프 스모크 테스트"
```

---

### Task 12: macOS 빌드 + 마무리

**Files:**
- Create: `Assets/Editor/NetBuildTool.cs`

- [ ] **Step 1: 빌드 툴** — RESTORE `Assets/Editor/BuildTool.cs` 이식, 출력 `Builds/Preternatural.app`, 씬 목록 `{Homescreen, GameScene}`, target=StandaloneOSX.
- [ ] **Step 2: 빌드 실행** — `execute_menu_item` → 결과 로그에서 `result=Succeeded errors=0` 확인.
- [ ] **Step 3: 커밋 + 보고**

```bash
git add Assets/Editor/NetBuildTool.cs
git commit -m "build: macOS 빌드 툴 + 멀티플레이 마일스톤 완료"
```

보고 내용: 스모크 결과, 남은 수동 확인(2대 실기기 접속·보이스 감쇠·몬스터 동기화 육안 확인), `open -n ... --args -authProfile p2` 2인 로컬 테스트 방법.

---

## 태스크 의존 순서

1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → (10 사용자 게이트) → 11 → 12. 3·4는 2 이후 병렬 가능. 6은 5 이후(NetBootstrap 배선 대상). 11은 10 완료 필수.
