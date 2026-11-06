namespace DaylightCommander.Domain;

/// <summary>뷰어(F3)가 어떻게 열지 정하는 파일 종류.</summary>
public enum ViewerKind { Text, Image, Pdf, Video, Audio, Unknown }

/// <summary>
/// 파일 이름(확장자)으로 뷰어 종류를 정한다 (원본 <c>viewer_screen.dart</c>의 표). 모르는 형식(<see cref="ViewerKind.Unknown"/>)은
/// 호출한 쪽이 앞부분을 읽어 <see cref="LooksLikeText"/>로 한 번 더 살핀다.
/// </summary>
public static class FileKinds
{
    static readonly HashSet<string> TextExtensions = new(StringComparer.Ordinal)
    {
        "txt", "md", "json", "xml", "html", "htm", "css", "java", "dart", "py", "js",
        "ts", "yaml", "yml", "sql", "sh", "log", "ini", "csv", "c", "cpp", "h", "kt",
        "swift", "gradle",
        // 빌드·설정·스크립트·소스 등 순수 텍스트가 확실한 확장자.
        "mk", "mak", "bp", "cmake", "bzl", "bazel", "ninja", "toml", "cfg", "conf",
        "properties", "env", "rc", "cc", "cxx", "hpp", "hh", "hxx", "m", "mm", "go",
        "rs", "rb", "php", "pl", "pm", "lua", "r", "scala", "groovy", "cs", "vb",
        "bat", "cmd", "ps1", "psm1", "bash", "zsh", "fish", "jsx", "tsx", "mjs",
        "cjs", "vue", "svelte", "scss", "sass", "less", "svg", "xsd", "xsl", "xslt",
        "plist", "rst", "adoc", "tex", "bib", "tsv", "diff", "patch", "aidl", "hal",
        "proto", "thrift", "graphql", "gql", "tf", "tfvars", "gitignore",
        "gitattributes", "editorconfig", "dockerignore", "lock", "sln", "csproj",
        "vcxproj", "pro", "pri", "qml", "asm", "s", "ld", "dts", "dtsi", "rules",
        "service", "desktop", "srt", "vtt", "ass", "arb", "pubspec",
        "axaml", "slnx", "props", "targets",
    };

    /// <summary>확장자가 없거나 흔한 이름의 순수 텍스트 파일 (소문자 비교).</summary>
    static readonly HashSet<string> TextFileNames = new(StringComparer.Ordinal)
    {
        "makefile", "gnumakefile", "dockerfile", "readme", "license", "licence",
        "changelog", "authors", "contributing", "notice", "copying", "todo",
        "cmakelists.txt", "gemfile", "rakefile", "procfile", "vagrantfile",
        "jenkinsfile", "build", "workspace", "owners", "codeowners",
    };

    static readonly HashSet<string> ImageExtensions = new(StringComparer.Ordinal) { "jpg", "jpeg", "png", "gif", "webp", "bmp" };
    static readonly HashSet<string> VideoExtensions = new(StringComparer.Ordinal) { "mp4", "mov", "mkv", "avi", "webm", "m4v" };
    static readonly HashSet<string> AudioExtensions = new(StringComparer.Ordinal) { "mp3", "wav", "flac", "aac", "m4a", "ogg", "wma" };

    /// <summary>마지막 점 뒤의 소문자 확장자. 점이 없거나 맨 앞/맨 뒤에만 있으면 빈 문자열.</summary>
    public static string ExtensionOf(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        return dot <= 0 || dot == fileName.Length - 1 ? "" : fileName[(dot + 1)..].ToLowerInvariant();
    }

    public static ViewerKind Classify(string fileName)
    {
        var ext = ExtensionOf(fileName);
        if (ImageExtensions.Contains(ext)) return ViewerKind.Image;
        if (TextExtensions.Contains(ext) || TextFileNames.Contains(fileName.ToLowerInvariant())) return ViewerKind.Text;
        if (ext == "pdf") return ViewerKind.Pdf;
        if (VideoExtensions.Contains(ext)) return ViewerKind.Video;
        if (AudioExtensions.Contains(ext)) return ViewerKind.Audio;
        return ViewerKind.Unknown;
    }

    /// <summary>
    /// 앞부분 <paramref name="head"/>만 보고 텍스트인지 추정한다. NUL 바이트가 있거나 제어 문자가 많으면 바이너리,
    /// 아니면 텍스트(UTF-8이 아니어도 포함)로 본다.
    /// </summary>
    public static bool LooksLikeText(ReadOnlySpan<byte> head)
    {
        if (head.IsEmpty) return true;
        var control = 0;
        foreach (var b in head)
        {
            if (b == 0) return false;
            // 탭·개행·FF·ESC는 텍스트에 흔하다.
            if (b < 0x20 && b is not (9 or 10 or 13 or 12 or 27)) control++;
        }
        return control * 100 < head.Length * 5;
    }
}
