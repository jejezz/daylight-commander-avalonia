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
- [ ] **5. 설정 + 영속화** (구현됨, 확인 대기: 설정 창(독립 윈도우, 외형·편집기), 모드·색 구성·글꼴·글자 크기·언어 ko/en, F4 외부 편집기, 즐겨찾기·최근 방문·드라이브 전환 메뉴, 창 위치·크기·최대화·마지막 경로 복원; 테스트 186개) — 모드·색 구성·글자 크기·언어(ko/en)·UI 글꼴·편집기(F4) 설정, 즐겨찾기, 최근 방문, 창 위치·크기·최대화·마지막 경로 복원
- [ ] **6. 창 확장 (원래 목적)** — 패널을 독립 창으로 분리, 창 간 상태 공유 (시험에서 뷰어 창 공유는 이미 확인)
- [ ] **7. 뷰어/에디터/터미널** — 독립 창. 텍스트·이미지 먼저
- [ ] **8. 원격 프로토콜** — SFTP → FTP → WebDAV (AOT 호환 라이브러리 확인 필요: SSH.NET 등)
- [ ] **9. 압축, 속성, 폴더 비교, 업데이트, 정보 창, 패키징**

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
