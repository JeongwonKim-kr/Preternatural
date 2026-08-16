# 게임플레이 4:3 CRT 프레임 설계

## 목표

게임플레이를 중앙 4:3 CRT 화면으로 제한하고, 창의 나머지 좌우 영역을 끝까지 순수 검정으로 유지한다. 검정 바 안으로 게임 카메라 화면이 새지 않게 한다.

## 구성

`GameplayAspectFrame`은 GameScene의 로컬 플레이어 카메라에 적용되는 런타임 컴포넌트다.

- 활성 화면의 가로·세로 비율에서 중앙 4:3 viewport를 계산한다.
- 카메라의 `rect`를 계산된 중앙 viewport로 설정하고 `clearFlags=SolidColor`, background를 검정으로 설정한다.
- 창이 4:3보다 넓으면 좌우가 검정, 더 좁으면 상하가 검정으로 남는다.
- 해상도나 전체화면 모드가 바뀌면 viewport를 다시 계산한다.
- PSX 후처리, 기존 카메라 FOV, FirstPersonCamera, 게임 HUD와 Esc 메뉴는 바꾸지 않는다.
- GameScene을 벗어날 때 원래 카메라 rect·clearFlags·배경색을 복구한다.

## 검증

1. EditMode에서 16:9·4:3·세로 화면의 정규화 viewport 계산을 검사한다.
2. Play Mode에서 GameScene이 중앙 4:3 영역만 렌더링하고 양쪽 끝이 완전 검정인지 확인한다.
3. 창 모드와 전체화면 전환 후에도 4:3 프레임이 유지되는지 확인한다.
4. 네트워크 스모크와 macOS 빌드로 기존 씬 전환을 확인한다.
