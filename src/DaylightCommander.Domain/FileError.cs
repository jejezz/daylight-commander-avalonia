namespace DaylightCommander.Domain;

/// <summary>
/// 사용자에게 보여줄 파일 작업 오류. 문장이 아니라 <b>값</b>이다 — 어떤 언어로 보여줄지는 화면(<c>IAppStrings</c>)이 정한다
/// (conventions localization.md). 응용 계층이 한국어나 영어 문장을 만들면 언어를 바꿔도 이미 만들어진 문장이 남는다.
/// </summary>
public abstract record FileError
{
    private FileError() { }

    /// <summary>이름을 입력하지 않았다.</summary>
    public sealed record NameEmpty : FileError;

    /// <summary><c>.</c>, <c>..</c>처럼 이름으로 쓸 수 없는 값.</summary>
    public sealed record NameReserved(string Name) : FileError;

    /// <summary>이름에 쓸 수 없는 문자가 들어 있다.</summary>
    public sealed record NameInvalidChar(char Char) : FileError;

    /// <summary>폴더를 자기 자신 안으로 복사하거나 이동하려 했다.</summary>
    public sealed record IntoItself(string Name) : FileError;

    /// <summary>파일과 폴더는 서로 덮어쓸 수 없다.</summary>
    public sealed record DifferentTypes(string Name) : FileError;

    /// <summary>루트(<c>C:\</c>, <c>/</c>)는 복사하거나 이동할 수 없다.</summary>
    public sealed record RootNotAllowed : FileError;

    public sealed record AlreadyExists(string Name) : FileError;

    public sealed record NotFound(string Name) : FileError;

    public sealed record AccessDenied(string Name) : FileError;

    /// <summary>휴지통으로 옮기지 못했다. <paramref name="Detail"/>은 OS가 준 원문(있으면).</summary>
    public sealed record TrashFailed(string Name, string? Detail) : FileError;

    /// <summary>복사·이동의 대상 폴더가 없다.</summary>
    public sealed record TargetFolderMissing : FileError;

    /// <summary>편집기로 지정한 실행 파일이 없다.</summary>
    public sealed record EditorNotFound(string Path) : FileError;

    /// <summary>내장 에디터로 열기에는 파일이 너무 크다 (5MB 초과). F3 보기로 열 수 있다.</summary>
    public sealed record EditorTooLarge(string Name) : FileError;

    /// <summary>편집기를 실행하지 못했다. <paramref name="Detail"/>은 OS가 준 원문(있으면).</summary>
    public sealed record EditorLaunchFailed(string? Detail) : FileError;

    /// <summary>위에 해당하지 않는 오류. <paramref name="Detail"/>은 OS나 라이브러리가 준 원문이라 번역되지 않는다.</summary>
    public sealed record Other(string? Detail) : FileError;
}
