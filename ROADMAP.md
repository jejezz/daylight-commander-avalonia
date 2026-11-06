# 이행 계획 — Daylight Commander → Avalonia (C#)

> 상태: **계획 초안 (2026-11-05)**. 사용자 승인 전에는 코드를 만들지 않는다. 아래 "정할 것"에 답이 필요하다.

## 왜 Avalonia인가 (시험 결과)

시험 프로젝트(이 폴더의 현재 코드)를 Windows에서 측정·확인한 결과.

| 항목 | Compose Desktop | Avalonia (NativeAOT) |
|---|---|---|
| 시작 시 흑백 프레임 | 있음 (AWT 창 한계, 해결 못 함) | **없음** |
| 최대화 복원 때 작은 창 → 큰 창 | 있음 (우회로 줄이기만 함) | **없음** |
| 프로세스 → 첫 프레임 | (JVM) | **0.41초** (R2R 1.2~1.4초, JIT 5.1초) |
| 배포 크기 | JVM 포함 | **34.9MB, 파일 4개** |
| 새 창 | - | 40~80ms |
| 10만 개 목록 | - | 부드러움 |

한계: 빈 화면 시험이다. 실제 목록·아이콘이 붙은 뒤의 속도, macOS·Linux 동작은 아직 확인하지 않았다.
Avalonia 본체는 MIT. 개발자 도구 등 유료 부가 제품은 쓰지 않는다 (템플릿의 `AvaloniaUI.DiagnosticsSupport`는 제거함).

## 원칙

- **Kotlin 판이 명세다.** Compose 판(1~5단계, 테스트 197개)과 Flutter 원본의 동작·키·문구를 그대로 옮긴다. 새로 설계하지 않는다.
- **Look and feel은 최초 Flutter 앱과 동일하게** 한다 (레이아웃, 색, 글자 크기 13/12/11, 앱 바 44, FocusTint, 아이콘 모양,
  단축키, 문구). 기준 문서는 원본 저장소의 `UI_UX.md`와 규약 `conventions/ui-ux.md`, `theming.md`. Fluent 기본 모양을 그대로 쓰지
  않고 원본에 맞춰 스타일을 덮어쓴다.
- **창 정책**: 독립 `Window`로 분리하는 편이 이로운 것만 대화상자 대신 창으로 만든다 (Compose 판과 같은 정책).
  설정·뷰어·편집기·터미널·작업 진행 목록은 독립 창 후보, 이름 입력·삭제 확인·충돌 같은 짧은 모달 질문은 대화상자로 둔다.
- 구조는 그대로: `Presentation → Application → Domain ← Data`. 프로젝트(어셈블리)로 나눠서 의존 방향을 컴파일러가 지키게 한다.
- **NativeAOT를 기본 배포 형태로 둔다.** 의존성을 추가할 때마다 AOT 게시가 되는지 확인한다 (리플렉션 의존 라이브러리 주의).
  JSON은 `System.Text.Json` 소스 생성기, P/Invoke는 `LibraryImport`.
- 개발 흐름은 지금과 같다: Claude가 계획·소스·단위 테스트, 각 단계 끝에 사용자가 직접 실행해 눈으로 확인. 한 번에 한 단계.
- 커밋은 사용자가 요청할 때만.

## 솔루션 구조 (제안)

```
daylight-commander-avalonia/
  DaylightCommander.slnx
  src/
    DaylightCommander.Domain/        FileEntry, FileError, AppSettings, PlacementMath ... (UI·IO 비종속)
    DaylightCommander.Services/      (= Kotlin 판의 application 계층. Avalonia의 Application 클래스와 이름이 겹치지 않게) ListLocalDirectory, FileOperations, OperationQueue, SettingsController ...
    DaylightCommander.Data/          JsonSettingsRepository ...
    DaylightCommander.App/           Avalonia (XAML + 뷰모델 + 창)  ← 지금 이 폴더의 시험 코드가 여기로 옮겨 간다
  tests/
    DaylightCommander.*.Tests/       xUnit
  gradle.properties 대응: Directory.Build.props 한 곳에 버전·정체성(app.*)
```

식별자는 Kotlin 판과 일부러 다르게 둔다(같은 PC에 나란히 설치·비교): `art.zoomon.daylightcommander.avalonia`,
설정 폴더 `DaylightCommanderAvalonia`. 표시 이름은 `Daylight Commander`.

## 개념 대응표 (Kotlin/Compose → C#/Avalonia)

| Kotlin / Compose | C# / Avalonia |
|---|---|
| `@Composable` | XAML 뷰 + 뷰모델 (코드 비하인드는 최소) |
| `StateFlow` 컨트롤러 (`PaneController`, `HomeState`) | `INotifyPropertyChanged` 뷰모델. 도메인 상태는 불변 `record`, 갱신은 `with` |
| `suspend` / `Flow` / `Job.cancel()` | `async` / `IAsyncEnumerable` / `CancellationToken` |
| `Dispatchers.IO` | `Task.Run` (파일 IO) |
| `sealed interface FileError` | `abstract record FileError` + 하위 `record` (패턴 매칭으로 문장 변환, 화면에서만) |
| `AppStrings` 인터페이스 (ko/en) | 같은 방식: `IAppStrings` + `KoreanStrings`/`EnglishStrings` (누락은 컴파일 오류). `resx`는 쓰지 않는다 |
| kotlinx-serialization | `System.Text.Json` 소스 생성기 (AOT 안전) |
| JNA (`DwmSetWindowAttribute`) | P/Invoke `LibraryImport` |
| `PlacementMath` (dp) | 그대로 이식 (테스트도 이식). Avalonia 좌표는 DIP |
| `FrameWindowScope.WindowChrome` | 필요 없을 가능성 큼 (시험에서 흰 프레임 없음). 다크 제목 표시줄만 확인 |
| Material Icons | 아이콘 세트(예: Lucide/Material, `PathIcon`) — 정할 것 |
| Skiko 렌더러 선택 | Avalonia 기본(Windows: Direct2D/Skia) |
| JUnit/kotlin.test | xUnit + FluentAssertions 없이 기본 `Assert` |

## 단계 (Kotlin 로드맵을 그대로 따른다)

각 단계는 실행 가능한 상태로 끝낸다. 위에서 아래 순서. 1~5단계는 Kotlin 코드가 있어서 **번역 + 테스트 이식**이 중심이다.

- [x] **A. 골격** (확인 완료: Domain·App·Tests 분리, 정체성·버전, 테마 6종×다크/라이트, SeoulNamsan, 앱 바 44, AOT 게시 스크립트) — 솔루션 분리(Domain/Application/Data/App/Tests), 정체성·버전(`Directory.Build.props`), 다크/라이트 테마,
      SeoulNamsan 글꼴, AOT 게시 스크립트, 시험 코드(`Trace`, 뷰어/새 창 버튼)를 정리해 옮김
- [x] **1. 도메인 + 로컬 목록** (확인 완료: `FileEntry`·정렬·`BytesFormat`·`ListLocalDirectory`, 가상화 목록, 폴더·파일 아이콘; 테스트 29개) — `FileEntry`, 디렉터리 읽기, 정렬, 한 패널 목록 (가상화 목록, 폴더·파일 아이콘)
- [x] **2. 듀얼 패널** (확인 완료: 좌우 패널, 활성 패널, 경로 표시줄, 뒤로/앞으로/상위/새로고침, 헤더 정렬, 숨김 토글, 더블클릭 이동; 테스트 51개) — 좌우 패널, 활성 패널, 경로 표시줄, 뒤로/앞으로/상위, 정렬, 숨김 토글
- [x] **3. 키보드 중심 조작** (확인 완료: 커서·선택·빠른 필터·단축키, 테스트 65개. 한글 IME 필터는 확인 필요) — 선택, 단축키 전체, 빠른 필터. **한글 IME 입력**을 처음부터 지원하는지 확인(Compose는 미지원이었음)
- [x] **4. 파일 작업** (확인 완료: 복사·이동·삭제(휴지통)·이름변경·새 폴더/파일, 작업 큐·진행률·취소, 충돌 질문, F키 바; 테스트 124개) — 복사/이동/삭제/이름변경, 작업 큐, 진행률·취소·충돌 처리, 휴지통(Windows: P/Invoke)
- [x] **5. 설정 + 영속화** (구현됨, 확인 대기: 설정 창(독립 윈도우, 외형·편집기), 모드·색 구성·글꼴·글자 크기·언어 ko/en, F4 외부 편집기, 즐겨찾기·최근 방문·드라이브 전환 메뉴, 창 위치·크기·최대화·마지막 경로 복원; 테스트 186개) — 모드·색 구성·글자 크기·언어(ko/en)·UI 글꼴·편집기(F4) 설정, 즐겨찾기, 최근 방문, 창 위치·크기·최대화·마지막 경로 복원
- [ ] **6. 창 확장 (원래 목적)** (보류: 7단계를 먼저 하기로 함. 패널 분리 방식은 사용자와 정해야 한다 — 후보: 패널을 새 창으로 옮기기 / 미러 창) — 패널을 독립 창으로 분리, 창 간 상태 공유 (시험에서 뷰어 창 공유는 이미 확인)
- [ ] **7. 뷰어/에디터/터미널** (7a 뷰어 확인 완료: F3 독립 창에서 텍스트·이미지·Hex. 7b 내장 텍스트 에디터 구현됨·확인 대기: F4 독립 창, UTF-8(BOM·줄바꿈 보존), TextMate 구문 강조, 저장·다른 이름으로 저장·닫기 확인, 찾기·바꾸기(대소문자·정규식), 줄 이동, 5MB 제한, 바이너리는 읽기 전용; 테스트 259개. 7b-2 구현됨·확인 대기: 마크다운 미리보기(편집기 오른쪽 반, Markdig, Ctrl+Shift+V, 표·목록·코드·링크·그림)과 메뉴 막대(파일·편집·찾기·보기, 항목 옆에 단축키 표시); 인코딩 선택은 원본에도 없어 하지 않음. 7c 확인 완료: PDF·비디오·오디오 뷰어(공식 Avalonia.Controls.WebView, MIT — 운영체제 웹뷰(Windows는 WebView2)에 파일을 그대로 열어 PDF 뷰어·동영상/음악 재생을 쓴다; 앱 +0.7MB, 안 되는 형식은 위쪽 "기본 앱으로 열기"). 7d 구현됨·확인 대기: 터미널 열기(앱 바 단추·Ctrl+`, 활성 패널 폴더에서 외부 터미널 — Windows Terminal→cmd, macOS Terminal, Linux gnome-terminal·konsole·xfce4-terminal·xterm). 7 단계 끝)
- [ ] **8. 원격 프로토콜** (8a 구현됨·확인 대기: 원격 경로 층(`sftp://user@host[:port]/경로`, Domain `Locations`) + SFTP(SSH.NET, NativeAOT 확인) — 연결 대화상자(비밀번호는 저장하지 않음, 서버 주소·포트·사용자만 저장 가능)·패널 경로 막대의 네트워크 메뉴(연결·저장한 서버·연결 해제), 호스트 키 기억·변경 시 거부, 목록·이동·새 폴더/파일·이름 변경·삭제, 로컬↔원격·서버 간 복사/이동(스트림, 충돌·진행률·취소), 끌어다 놓기, 원격 파일 보기(F3)·기본 앱으로 열기(임시 사본), 실제 SFTP 서버(asyncssh) 통합 테스트. 아직 없음: 원격 파일 편집(올려 두기), 속성·압축·터미널. 8b 구현됨·확인 대기: FTP(FluentFTP, NativeAOT 확인) — 연결 대화상자에 프로토콜 선택·"익명으로 연결"·FTP 기본 포트, 같은 파일 작업·전송, 읽기·쓰기는 임시 파일 경유(FTP는 연결 하나로 전송 하나만 가능), 실제 FTP 서버(pyftpdlib) 통합 테스트 12개. 남은 것: 8c WebDAV(HttpClient), 8d 다듬기(원격 편집 올려 두기, FTPS 등))
- [ ] **9. 제품 마무리** (9a 구현됨·확인 대기: 정보 창(앱 바 ⓘ)·오픈소스 라이선스 창, 속성(F-바·Alt+Enter, 독립 창, 폴더 크기 계산), 앱 아이콘(exe·창), 릴리스 워크플로(.github/workflows/release.yml, Windows: 테스트→NativeAOT→Inno Setup 설치 파일+zip→Release, 태그=AppVersion 검사), LICENSE; 테스트 270개. 9b 구현됨·확인 대기: 끌어다 놓기(패널 간·패널 안 폴더 행·탐색기에서 들어오는 파일, 기본=같은 드라이브 이동/다르면 복사, Ctrl=복사 Shift=이동)와 행 우클릭 메뉴·Ctrl+D 복제; 테스트 291개. 9c 구현됨·확인 대기: 압축(F-바·우클릭, zip, 이름 묻기)·압축풀기(커서의 zip → "zip 이름" 폴더, 경로 탈출 차단), 표준 System.IO.Compression이라 AOT 무문제; 테스트 303개. 9d 구현됨·확인 대기: 왼쪽 디렉터리 트리(앱 바 버튼·Ctrl+T, 드라이브 루트·지연 로딩, 활성 패널 따라가며 현재 경로 강조·조상 펼침, 폭 조절, 열림·폭 저장); 테스트 313개. 9e 구현됨·확인 대기: 아이콘을 Phosphor 투톤(MIT)으로 교체 — 벡터 DuoIcon 컨트롤, 팔레트·라이트/다크를 따르는 색, 확장자별 파일 아이콘, scripts/gen-phosphor-icons.py. 9f 구현됨·확인 대기: 원본(Flutter)의 트리 새 기능 — 새로고침(패널 목록이 바뀔 때·5초마다·버튼), 폴더 우클릭 메뉴(열기·새 폴더·이름 바꾸기·잘라내기·복사·붙여넣기·삭제·속성·즐겨찾기·파일 관리자·새로고침; 드라이브 루트는 이름 바꾸기·잘라내기·삭제 없음), 폴더 끌어다 놓기(트리 기본 이동·Ctrl 복사, 패널과 트리 사이 양방향, 외부 파일), 보던 폴더가 사라지면 가까운 상위 폴더로, 메뉴 행 높이 36. 9g 구현됨·확인 대기: 폴더 비교(앱 바 토글; 이름 기준, 한쪽에만 있음=강조색·내용이 다름=주황 행 배경) + 동기화 막대(차이 N개 →/←, 양방향 동기화: 한쪽에만 있는 것은 자동 복사, 다른 파일은 하나씩 묻기). 9h 구현됨·확인 대기: 좌우 패널 교환(앱 바 단추·Ctrl+U, 화면 위치만 바뀜·저장) + 두 패널 사이의 경로 맞추기 단추(→ ←, Ctrl+Shift+←/→), 동기화 막대 방향도 화면 위치 기준. 9i 구현됨·확인 대기: 1패널/2패널 전환(앱 바 단추·Ctrl+\, 활성 패널만 펼치고 다른 패널은 가장자리 막대로 접음 — 누르면 전환, 파일을 놓으면 복사/이동, 창 최소 폭 완화, 저장). 9j 구현됨·확인 대기: 패턴 선택(F-바 버튼; *·? 와일드카드, 이름 전체·대소문자 무시, 선택 추가/해제 — 원본과 같다). 남은 것: 창 도킹(화면 가장자리), 업데이트 확인, 폴더 비교, macOS·Linux 빌드 작업과 서명·공증)

## 위험과 확인할 것

| 위험 | 대응 |
|---|---|
| NativeAOT가 막는 라이브러리 (SSH, 압축 등) | 8단계 전에 후보 라이브러리로 AOT 게시 시험. 막히면 R2R(117MB)로 후퇴 |
| 한글 IME (빠른 필터) | 3단계 첫 작업으로 시험 |
| 실제 화면의 시작 속도 | 2단계 끝에 다시 측정 (목록·아이콘이 붙은 뒤) |
| macOS·Linux | 지금까지 Windows만 확인. 단계마다 Windows 먼저, 다른 OS는 별도 확인 지점을 둔다 |
| 휴지통·OS 연동·드래그아웃 | OS별 P/Invoke/네이티브 코드. Windows 먼저 |
| Avalonia 12 API 변화 | 12.1.5로 고정 |

## 정할 것 (사용자)

1. ~~프로젝트 위치·이름~~ → **결정: 현재 폴더 유지** (`src/`, `tests/` 추가). 2~5도 결정됨(CommunityToolkit.Mvvm, 규약 문서는 확인 뒤, 날짜는 원본 형식, A부터).
2. **뷰모델 스타일**: 직접 `INotifyPropertyChanged`(의존성 0, AOT 안전) vs `CommunityToolkit.Mvvm`(소스 생성기, AOT 호환).
   제안: CommunityToolkit.Mvvm — 반복 코드가 줄고 AOT에서도 동작.
3. **규약 문서**: Compose용 `compose/conventions/`와 같은 `avalonia/` 문서를 템플릿 저장소에 만들지 (Compose에서 한 것처럼, 구현·확인 뒤에 올림).
4. **날짜 형식**: 숫자 고정(`yyyy-MM-dd HH:mm`) vs 언어별 (이전부터 남은 질문).
5. 시작 단계: **A. 골격**부터 (제안).
