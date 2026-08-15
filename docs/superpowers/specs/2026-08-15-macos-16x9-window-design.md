# macOS 기본 16:9 창 실행 설계

## 목표

macOS 실행 파일이 세로 네이티브 디스플레이를 감지해 숏폼 비율로 보이는 문제를 없앤다. 기본 실행 시 게임은 고정 크기 `1920×1080` 가로 창으로 표시한다.

## 근거

2026-08-15 Player.log에서 빌드가 `Screen 1200x1920 native=1200x1920 mode=FullScreenWindow`으로 실행된 것을 확인했다. 현재 `Fullscreen Window`는 macOS의 네이티브 디스플레이 해상도를 사용하고, 비율을 보존하기 위해 검은 여백을 추가한다. 씬 카메라는 전체 뷰포트와 대상 Render Texture 없음으로 설정돼 있어 원인이 아니다.

## 변경 범위

`ProjectSettings/ProjectSettings.asset`의 Standalone 기본 표시 모드만 변경한다.

- Fullscreen Mode: `Windowed`
- Default Screen Width: `1920`
- Default Screen Height: `1080`
- Resizable Window: 기존처럼 비활성화

`Screen.SetResolution` 호출, 씬·프리팹·카메라 변경, XR 패키지 변경은 포함하지 않는다.

## 동작 및 예외 처리

- 앱은 macOS 디스플레이의 네이티브 방향과 무관하게 16:9 크기로 열린다.
- 창 크기는 사용자가 임의로 조절할 수 없다.
- 사용자는 macOS 창 제어로 필요할 때 전체 화면으로 전환할 수 있으며, 그 동작은 운영체제에 맡긴다.

## 검증

1. 기존 Screen 진단 로그는 다음 빌드에 포함해 실행 환경을 기록한다.
2. macOS 빌드를 새로 만든 뒤 앱을 실행한다.
3. `~/Library/Logs/DefaultCompany/HorrorGame project/Player.log`에서 `Screen 1920x1080`과 `mode=Windowed`를 확인한다.
4. 홈 화면과 게임 씬 모두 가로 비율로 보이는지 확인한다.
5. Unity Console에 새 컴파일 오류가 없는지 확인한다.

## 비목표

- 모든 디스플레이에 맞춘 동적 해상도 선택 UI
- 런타임 해상도 전환 메뉴
- Windows 또는 모바일 표시 설정 변경
