# 로비 대기실 설계

날짜: 2026-08-15
브랜치: `multi`

## 목표

방을 만들면 바로 게임에 들어가지 않고 **로비에서 대기**한다. 호스트는 방 코드를 공유해 최대 4명을 모으고, 준비되면 게임을 시작한다. 대기 중 음성으로 대화할 수 있다.

## 결정 사항 (사용자 승인)

| 항목 | 결정 |
|---|---|
| 게임 시작 조건 | 호스트 재량 — 혼자여도 시작 가능 |
| 로비 대화 | 음성 (텍스트 채팅 없음) |
| 표시 항목 | 방 코드, 현재 인원 n/4, 참가자 이름 목록, 게임 시작 버튼 |

## 1. 세션 흐름 변경

현재 `SessionManager.CreateRoomAsync`는 방 생성 직후 `LoadScene(GameScene)`을 호출한다 — 이 때문에 대기실이 존재하지 않는다. **이 자동 로드를 제거**한다.

- 방 생성 → 로비 대기 (씬은 Homescreen 유지)
- 호스트가 [게임 시작] → `NetworkManager.SceneManager.LoadScene("GameScene", Single)` (MultiplayerMenu의 기존 시작 경로 그대로)
- 참가자는 NGO 씬 동기화로 따라옴 (기존 동작, 변경 없음)

닉네임은 `SessionOptions.PlayerProperties["name"]` / `JoinSessionOptions.PlayerProperties["name"]`로 세션에 실어 보낸다. 이래야 다른 참가자의 이름을 읽을 수 있다.

## 2. SessionManager — 로비 상태 노출

UI가 Sessions API를 직접 다루지 않도록 SessionManager가 경계 역할을 한다.

```csharp
public bool IsHost { get; }                        // ActiveSession?.IsHost ?? false
public int PlayerCount { get; }                    // ActiveSession?.PlayerCount ?? 0
public IReadOnlyList<string> PlayerNames { get; }  // 이름 목록 (호스트가 첫 번째)
public event Action LobbyChanged;                  // 인원/이름 변동 통지
```

- `PlayerNames`: `ActiveSession.Players`를 순회해 `Properties["name"].Value`를 읽는다. 값이 없거나 비어 있으면 `"플레이어 N"`(N은 1부터의 순번)으로 대체한다. 호스트(`player.Id == ActiveSession.Host`)가 목록 맨 앞에 오도록 정렬한다.
- `LobbyChanged`: `session.PlayerJoined`, `session.PlayerHasLeft`, `session.PlayerPropertiesChanged`, `session.Changed`를 모두 이 이벤트 하나로 묶어 발행한다. `Hook`에서 구독하고 `EndSessionAsync`에서 해제한다.
- 기존 공개 API(`JoinCode`, `InSession`, `SessionStarted`, `SessionEnded`, `MaxPlayers`)는 변경하지 않는다.

## 3. MultiplayerMenu — 로비 패널

기존 코드 생성 UGUI에 로비 표시를 추가한다. 세션 중(`InSession == true`)일 때 보이는 내용:

- **방 코드** — 크게 표시 (참가자에게 불러줄 값)
- **인원** — `2/4` 형식
- **참가자 목록** — 이름 한 줄씩, 호스트는 `(호스트)` 표시
- **[게임 시작]** — 호스트에게만 보임. 인원과 무관하게 항상 활성
- **[나가기]** — 전원
- 참가자에게는 시작 버튼 대신 **"호스트가 시작하기를 기다리는 중"** 안내

갱신은 `SessionManager.LobbyChanged` 구독으로 하고, 이벤트 유실에 대비해 1초 주기 폴링을 안전망으로 둔다(값이 바뀐 경우에만 다시 그린다).

로비 진입 시 커서는 잠금 해제 상태를 유지한다(기존 패널 동작).

## 4. 로비 음성

`VoiceManager`는 이미 `SessionStarted`에서 포지셔널 채널(`"s-" + sessionId`)에 조인한다 — 로비 시점에 이미 음성이 연결된다. 문제는 로비에는 플레이어가 스폰되지 않아 3D 위치가 갱신되지 않는다는 점이다.

채널을 따로 만들지 않고 **로비 동안 위치를 전원 원점으로 고정**한다:

- `VoiceManager`가 `NetPlayer.Local == null`인 동안 주기적으로(0.5초) 자기 3D 위치를 원점/정면으로 설정한다. 모두 같은 지점에 있으므로 거리 감쇠 없이 서로 들린다.
- `NetPlayer.Local`이 생기면(게임 씬 진입) 갱신을 멈춘다. 이후는 기존 `VoicePositionUpdater`가 실제 카메라 위치로 인계한다.

채널 전환이 없으므로 게임 시작 시 음성이 끊기지 않는다. M키 뮤트, 사망 뮤트 등 기존 동작은 그대로다.

## 5. 검증

**스모크 테스트 수정 필수** — 현재 스모크는 "방 생성 → 자동 씬 전환"을 전제한다. 새 흐름으로 고친다:

1. 방 생성 후 **로비 상태 단언**: 씬이 Homescreen 유지, `PlayerCount == 1`, `PlayerNames`에 로컬 닉네임 포함, `IsHost == true`
2. 시작 경로 호출 → GameScene 전환 확인
3. 이후 단계(스폰·접지·오버레이·상호작용·몬스터·사망·게임오버)는 기존 그대로

추가 검증: 로비에서 `VoiceManager.VoiceReady == true`인지 확인(실패는 WARN — 에디터 마이크 권한 변수).

## 제외 (YAGNI)

- 텍스트 채팅
- 준비(Ready) 체크, 강퇴, 방 이름 지정
- 방 목록/공개 방 찾기 (코드 입장만 유지)
- 로비 전용 배경/연출
