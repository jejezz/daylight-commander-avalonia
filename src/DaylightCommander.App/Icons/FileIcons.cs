using DaylightCommander.Domain;

namespace DaylightCommander.App.Icons;

/// <summary>파일 목록의 아이콘: 확장자별로 모양과 색(앱 색 자원 이름)을 정한다.</summary>
public static class FileIcons
{
    public const string FolderColor = "AppIconFolder";
    const string Plain = "AppTextMid";

    static readonly Dictionary<string, (string Icon, string Color)> ByExtension = Build();

    static Dictionary<string, (string, string)> Build()
    {
        var map = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        void Add((string, string) icon, string extensions)
        {
            foreach (var ext in extensions.Split(' ')) map[ext] = icon;
        }

        Add(("file-archive", "AppIconArchive"), "zip 7z rar tar gz tgz bz2 xz zst cab iso jar");
        Add(("file-pdf", "AppIconPdf"), "pdf");
        Add(("file-image", "AppIconImage"), "jpg jpeg png gif webp bmp ico tif tiff svg heic");
        Add(("file-video", "AppIconVideo"), "mp4 mov mkv avi webm m4v wmv flv mpg mpeg");
        Add(("file-audio", "AppIconAudio"), "mp3 wav flac aac m4a ogg wma");
        Add(("file-doc", "AppIconDoc"), "doc docx odt rtf pages");
        Add(("file-sheet", "AppIconSheet"), "xls xlsx ods csv tsv numbers");
        Add(("file-slide", "AppIconSlide"), "ppt pptx odp key");
        Add(("file-app", "AppIconApp"), "exe msi app dmg apk deb rpm appimage dll so dylib");
        Add(("file-code", "AppIconCode"),
            "cs csproj sln slnx axaml xaml js jsx ts tsx mjs cjs vue svelte html htm css scss sass less json xml xsd xsl yaml yml toml " +
            "py java kt kts c h cpp cc cxx hpp go rs rb php pl lua swift dart sql sh bash zsh fish bat cmd ps1 psm1 gradle cmake proto");
        Add(("file-text", "AppIconText"), "txt md log ini cfg conf properties env rst tex srt gitignore");
        return map;
    }

    /// <summary>폴더는 노란 폴더, 파일은 확장자 표에 따르고 모르는 확장자는 무채색 파일 모양.</summary>
    public static (string Icon, string Color) For(string name, bool isDirectory)
    {
        if (isDirectory) return ("folder", FolderColor);
        return ByExtension.TryGetValue(FileKinds.ExtensionOf(name), out var found) ? found : ("file", Plain);
    }

    /// <summary>
    /// 격자 큰 아이콘의 배지 글자: 확장자(대문자, 4글자까지). 확장자가 없거나 길면 종류 이름, 모르는 형식은 배지 없음.
    /// </summary>
    public static string? BadgeFor(string name, bool isDirectory)
    {
        if (isDirectory) return null;
        var ext = FileKinds.ExtensionOf(name);
        if (!ByExtension.TryGetValue(ext, out var found)) return null;
        if (ext.Length is > 0 and <= 4) return ext.ToUpperInvariant();
        return found.Icon switch
        {
            "file-archive" => "ZIP", "file-image" => "IMG", "file-video" => "VID", "file-audio" => "AUD",
            "file-code" => "CODE", "file-text" => "TXT", "file-app" => "APP", _ => null,
        };
    }

    /// <summary>드라이브 종류별 아이콘: 내장은 하드 드라이브, 이동식은 USB, 네트워크는 네트워크 모양.</summary>
    public static string ForDrive(DriveKind kind) => kind switch
    {
        DriveKind.Removable => "drive-removable",
        DriveKind.Network => "drive-network",
        _ => "drive",
    };
}
