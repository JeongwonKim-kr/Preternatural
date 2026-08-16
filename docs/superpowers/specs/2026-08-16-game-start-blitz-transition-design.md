# 게임 시작 블리츠 전환 설계

## 목표

로비에서 게임을 시작할 때 프리뷰 씬이 해제되는 중 Unity의 기본 배경이 보이지 않게 한다. 검정 전체 화면 위에 짧은 흰색 노이즈 블리츠를 넣고, GameScene의 기존 기상 인트로 페이드로 자연스럽게 연결한다.

## 흐름

1. 호스트가 게임 시작을 누르면 즉시 입력을 막는 최상위 검정 전환 오버레이를 표시한다.
2. 프리뷰 GameScene을 해제하고 기존 Relay/NGO 게임 시작 흐름을 수행한다.
3. 오버레이는 검정을 기본으로 유지하며, 약 0.35초 동안 2~3회의 짧은 백색·노이즈 블리츠를 표시한다.
4. NGO가 GameScene을 로드하는 동안에도 오버레이는 유지돼 Unity 기본 배경이나 씬 언로드 중간 화면이 드러나지 않는다.
5. GameScene이 활성화되면 기존 `IntroFadeIn`/`FirstPersonCamera` 기상 인트로가 화면을 인계받는다. 전환 오버레이는 검정 상태에서 빠르게 사라져 두 페이드가 겹치지 않는다.
6. 네트워크 시작 또는 씬 로드에 실패하면 오버레이를 제거하고 기존 로비 오류 문구·입력 가능 상태를 복구한다.

## 구성

`GameStartTransition`은 앱 수명 동안 하나만 존재하는 코드 생성 Screen Space Overlay Canvas를 소유한다.

- `Begin()`은 검정 배경과 입력 차단을 즉시 켠다.
- `Update()`는 `Time.unscaledDeltaTime`으로 결정적인 블리츠 알파·밝기 패턴을 갱신한다. 무작위 시드나 새 이미지 에셋은 사용하지 않는다.
- `CompleteAfterGameScene()`은 GameScene 활성화 후 짧게 검정을 유지한 뒤 오버레이를 제거한다.
- `Cancel()`은 실패/복귀 경로에서 즉시 오버레이와 입력 차단을 제거한다.
- `MultiplayerMenu.OnStartClicked`는 프리뷰 해제보다 먼저 `Begin()`을 호출하고, 기존 네트워크 시작·NGO 씬 로드 순서는 보존한다.

## 제외 범위

- GameScene, `FirstPersonCamera`, 기존 인트로 Canvas, Relay/NGO/SessionManager와 Project Settings를 수정하지 않는다.
- 사운드, 새 셰이더, 새 텍스처, 씬 YAML을 추가하지 않는다.

## 검증

1. EditMode에서 블리츠 패턴이 검정 기본·밝은 펄스·안전한 종료 상태를 갖는지 테스트한다.
2. Play Mode에서 시작 버튼 직후부터 GameScene 인트로까지 기본 배경이 보이지 않는지 캡처한다.
3. 네트워크 스모크가 기존 GameScene 진입·Homescreen 복귀를 보존하는지 확인한다.
4. macOS 빌드 실행에서 같은 전환을 확인한다.
