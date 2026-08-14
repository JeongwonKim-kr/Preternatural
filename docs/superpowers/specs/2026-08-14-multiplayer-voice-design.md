# Preternatural 멀티플레이어 + 근접 보이스 설계

날짜: 2026-08-14
브랜치: `multi` (모든 작업은 이 브랜치에서만 진행)
검증 도구: unityMCP (에디터 연결, `set_active_instance`로 Preternatural 지정)

## 목표

기존 싱글플레이 1인칭 공포 게임에 **최대 4인 풀 코옵 멀티플레이**와 **거리 감쇠 근접 보이스**를 추가한다. 기존 싱글플레이 경로는 별도로 유지한다.

## 결정 사항 (사용자 승인)

| 항목 | 결정 |
|---|---|
| 동기화 범위 | 풀 코옵 (상호작용 + 몬스터 + 사망→관전 + 게임오버) |
| 싱글 모드 | 별도 유지 (기존 시작 버튼 경로 무수정) |
| 플레이어 모델 | Sky Protective Suit + Free Sample Animation Set (jungwon Copyright Project에서 복사) |
| 스택 | NGO + Unity Sessions/Relay + Vivox + 익명 인증 (jungwon 검증 스택 이식) |

## 1. 패키지 + UGS 설정

추가 패키지:

- `com.unity.netcode.gameobjects` 2.12.x
- `com.unity.services.multiplayer` 2.2.x (Sessions API + Relay)
- `com.unity.services.vivox` 16.x
- `com.unity.services.authentication` 3.7.x

기존 URP 17.4 / PSX 셰이더 / ai.navigation 2.0.14와 충돌 없음.

**사용자 액션 필요**: Unity 대시보드에서 Preternatural용 UGS 프로젝트 생성, 에디터 Project Settings > Services에서 링크, Vivox 활성화(온보딩 완료 필수 — jungwon 때 Settings.json이 안 내려오던 원인). 이 작업 전까지 코드 작성은 가능하나 실동작 테스트는 불가.

## 2. 세션 / 로비 플로우

- Homescreen에 "멀티플레이" 버튼 추가. 기존 싱글 시작 버튼은 무수정.
- 멀티 패널: 닉네임 입력 + [방 만들기] / [코드 입장]. 방 생성 시 세션 코드 표시.
- jungwon에서 이식: `SessionManager`(CreateSessionAsync/JoinSessionByCodeAsync, `WithRelayNetwork()`, MaxPlayers=4, 에러 매핑, LastEndReason), `AuthProfile`(인스턴스별 프로필 분리 — 에디터 해시/`-authProfile` 인자/기본), `NicknameUtil`(UTF-8 경계 자르기), `NetworkManagerGuard`(중복 NetworkManager 제거).
- 호스트 [게임 시작] → NGO 씬 관리로 전원 GameScene 로드.
- Sessions SDK가 NetworkManager를 자동 시작하므로 StartHost/StartClient 직접 호출 금지 (jungwon 함정).
- 호스트 이탈 시: 클라이언트는 Homescreen 복귀 + 사유 안내. 메뉴 진입 시 커서 무조건 잠금 해제 (jungwon 함정 ⑧).

## 3. 플레이어 동기화

- `NetworkPlayer` 프리팹: CharacterController, Owner-authority NetworkTransform, 기존 PlayerMovement/FirstPersonCamera 로직은 오너만 활성.
- 원격 표시용 Sky Protective Suit 모델 + 1D 블렌드트리(대기/조깅/질주) + `PlayerAnimationDriver`(위치 델타로 Speed 구동, 네트워크 트래픽 없음). 오너 시점에서는 자기 모델 숨김(레이어 또는 렌더러 비활성).
- 모델은 `Assets/Models/Player/` 폴더 방식으로 배치해 추후 교체 용이하게.
- 손전등: `FlashlightOn` NetworkVariable(오너 쓰기), 기존 FlashlightController를 입력원으로 래핑. 원격 플레이어 모델에 손전등 라이트 부착.
- 스태미나·발소리: 로컬 전용 (동기화 안 함). 발소리는 원격 플레이어 위치에서도 재생(몰입 요소, PlayerAnimationDriver 속도 기반).
- 스폰: GameScene 기존 플레이어 시작 지점 주변 4자리 오프셋.
- 낙사 방어: kill-Z 리스폰 (jungwon 함정 — 씬 전환 중 바닥 없는 낙하).

## 4. 상호작용 동기화 (래퍼 패턴)

원칙: **기존 스크립트 무수정**. 어댑터 컴포넌트를 같은 GameObject에 추가. NetworkManager 미가동(싱글 경로)이면 어댑터는 아무 동작도 하지 않는다.

- `NetToggleSync` (범용 어댑터): 열림/닫힘/파괴 등 단일 상태를 NetworkVariable로 유지. 로컬 상호작용 발생 → ServerRpc → 상태 변경 → 전 클라이언트에서 기존 스크립트의 해당 메서드 호출(리플렉션 아닌 UnityEvent/직접 참조 바인딩). 대상: DoorInteractable, DrawerShelf, LeverInteractable/LeverDoorController, WoodBoard/BoardedDoor, MonitorDoorController, Door Teleport.
- `PickupItem`: 서버 권위 획득 — 먼저 요청한 1명만 획득, 전원에게 비활성화 전파.
- `CabinetHide`: 카메라 연출은 로컬, 위치는 NetworkTransform이 자연 동기화 — 별도 어댑터 불필요.
- GameScene의 in-scene NetworkObject는 씬 저장 후 OnValidate 재호출 + 재저장으로 GlobalObjectIdHash=0 방지 (jungwon 함정 ⑦, 에디터 스크립트로 자동화).

## 5. 몬스터 / 점프스케어 / 사망 / 게임오버

- `MonsterLookAI`: 호스트에서만 AI 로직 실행(NavMeshAgent). 위치는 NetworkTransform, 애니 상태는 NetworkVariable. 타겟 선정을 "가장 가까운 생존자"로 확장 — 기존 단일 `player` 참조를 생존자 목록 조회로 대체하는 어댑터.
- 시선 감지(LookAI 특성): 전 생존자 카메라 중 하나라도 조건 충족 시 반응하도록 호스트에서 판정.
- 점프스케어 카메라 연출: 트리거한 당사자 화면에만 재생. 몬스터 등장/이동 상태는 전원 동기화.
- 사망: 당사자 DeadScreen → 관전 모드(자유비행 SpectatorCamera 이식, 커서 잠금 유지). 사망자는 보이스 송신 음소거(듣기 가능), M키 뮤트 토글은 생존자만.
- 전원 사망 = 게임오버 → 전원 결과 표시 → Homescreen 복귀.
- 정적 상태는 도메인 리로드 비활성 대비 `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`로 리셋.

## 6. 근접 보이스 (Vivox)

- 포지셔널 채널: 세션 ID 기반 채널명, `Channel3DProperties(15, 2, 1.0f, InverseByDistance)` — 위치 인자만 사용 (SDK 파라미터명 오타 함정).
- jungwon `VoiceManager` 이식: 채널 정체성 검사(방 교차 누수 방지), 펜딩 채널 1슬롯 큐, 사망 뮤트 재적용(늦은 입장 레이스), 3D 위치 갱신(플레이어 머리 기준).
- M키 음소거 토글(생존자만), 마이크 권한 문구 ProjectSettings에 추가.

## 7. 검증 / 테스트

- **unityMCP**로 에디터 제어: `read_console`(컴파일 에러), `manage_scene`/`manage_gameobject`(씬 배선 확인), `execute_menu_item`, `run_tests`. 다중 인스턴스 연결 시 `set_active_instance`로 Preternatural 고정.
- EditMode 테스트: 순수 로직(닉네임, 상태 머신, 타겟 선정 등).
- 스모크 테스트(에디터 메뉴): 로그인 → 방 생성 → 게임 시작 → 상호작용 → 사망 → 관전 → 게임오버 → 복귀 자동 검증.
- 씬 배선은 에디터 스크립트(SceneWirer)로 재생성 가능하게 — 수동 배선 최소화.
- macOS 빌드 확인. 같은 Mac 2인 테스트: `open -n ... --args -authProfile p2`.
- 싱글 경로 회귀 확인: 멀티 코드 추가 후에도 기존 싱글 시작이 그대로 동작해야 함.

## 비용

전부 무료 티어: Relay 평균 50 CCU, Vivox 5,000 MAU, 익명 인증 무제한. 4인 테스트 범위에서 비용 0.

## 제외 (YAGNI)

- 재시작 투표, 라운드/점수 시스템 (게임오버 후 메뉴 복귀로 충분)
- 죽은 자 전용 보이스 채널
- Windows 빌드 (요청 시 별도)
- 몬스터 모델 교체, 사운드 추가 등 콘텐츠 작업
