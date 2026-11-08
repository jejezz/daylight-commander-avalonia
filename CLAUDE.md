# Daylight Commander (Avalonia)

Flutter 원본 `C:\Users\jejez\MyWork\Projects\FLUTTER\daylight-commander-flutter`과 Kotlin/Compose 판
`C:\Users\jejez\MyWork\Projects\KOTLIN\daylight-commander-compose`(보류 중, 커밋 6291ab4)를 C#/Avalonia로 옮기는 프로젝트.
계획·결정·위험은 [ROADMAP.md](ROADMAP.md). **지금 위치: A·1~5단계 완료. 6단계(패널 분리)는 보류하고 7단계(뷰어·에디터)를 진행 중 — 7a 뷰어(F3) 완료, 7b 내장 에디터(F4) 구현됨·확인 대기. 제품 마무리 9a(정보·속성·릴리스 워크플로) 구현됨·확인 대기.**

- **Kotlin 판이 명세다.** 동작·키·문구·테스트를 그대로 옮긴다. 새로 설계하지 않는다.
- **Look and feel은 최초 Flutter 앱과 동일**하게 (UI_UX.md, 규약 `conventions/ui-ux.md`, `theming.md`). 색은 `{DynamicResource AppBg...}`로만 읽는다.
- **창 정책**: 독립 `Window`가 이로우면 대화상자 대신 창으로 (설정·뷰어·편집기·터미널·작업 목록). 짧은 모달 질문은 대화상자.
- 구조: `Presentation(App) → Services → Domain ← Data`. 프로젝트로 나눠 의존 방향을 컴파일러가 지킨다. `Services`는 Kotlin 판의
  application 계층(Avalonia의 `Application` 클래스와 이름 충돌을 피함). Domain/Services/Data/App/Tests 프로젝트가 있다.
- **NativeAOT가 기본 배포 형태.** 의존성을 추가할 때마다 `scripts\publish-aot.ps1`로 AOT 게시가 되는지 확인한다.
  JSON은 `System.Text.Json` 소스 생성기, P/Invoke는 `LibraryImport`. 뷰모델은 `CommunityToolkit.Mvvm`.
- 정체성·버전은 `Directory.Build.props` 하나 (`AppIdentity`/`AppVersion`이 어셈블리 메타데이터로 읽음).
  식별자 `art.zoomon.daylightcommander.avalonia`, 설정 폴더 `DaylightCommanderAvalonia`, 실행 파일 `DaylightCommander`.
- **개발 흐름 (코딩 → 테스팅 → 릴리징, `conventions/branching.md`)**: 계획 → 소스 → 단위 테스트와 가능한 자체 검증까지 Claude가 worktree 브랜치에서
  하고, 끝나면 **Claude가 커밋해 `staging`에 병합**한다 (없으면 `origin/main`에서 만든다). 사용자는 `staging`만 Taskbench 러너로 빌드·실행해
  눈으로 확인한다. OK가 나면 worktree를 지우고 `staging → main` PR을 올린다. PR 병합·버전 올림·릴리스 패키지는 사용자가 Taskbench로 한다.
  `main`은 직접 바꾸지 않는다. 앱 창은 요청받을 때만 띄운다. 한 번에 한 단계. 자체 검증으로 못 본 것(화면·Windows 설치 등)은 병합할 때 알린다.
- 테스트: `dotnet test DaylightCommander.slnx` (앱이 실행 중이면 실행 파일이 잠겨 App 빌드가 실패하므로 `dotnet test tests\<프로젝트>`로 개별 실행). 실행: `dotnet run -c Release --project src\DaylightCommander.App`.
- 설정은 `%APPDATA%\DaylightCommanderAvalonia\settings.json`. 화면 문구는 `IAppStrings`(ko/en, `Localization/`), XAML은 `{DynamicResource Str.*}`,
  코드는 `Strings.Current`로 읽는다 (새 문구는 인터페이스 → 한국어 → 영어 순으로 모두 추가, 빠뜨리면 컴파일 오류).
- **업데이트**: Flutter 앱들과 같은 방식(`jejezz/application-release-templates`의 `app_updater` + `update_service`)을 옮겼다. 서버(`introduce-public-repos`의
  `/api/update`)에 묻고 → 동의 → 내려받아 SHA-256 검증 → OS 설치기. 코드는 `Services/Updates/`(클라이언트·정책·설치기)와 `App/Updates/`(흐름·대화상자),
  문구는 `Update*`. 서버 주소는 `Directory.Build.props`의 `AppUpdateServer`(빌드 때 `-p:AppUpdateServer=`로 바꿈, 빈 값이면 끔). 서버가 앱을 아는 이름은
  저장소 이름(`AppIdentity.UpdateAppId`). 자동 확인은 시작 5초 뒤 하루 1회, 수동은 정보 창의 [업데이트 확인].
- 임시 시험 코드는 5단계에서 모두 지웠다 (`Trace`, 모드·색 순환 버튼, 뷰어 창). 뷰어는 7단계.


