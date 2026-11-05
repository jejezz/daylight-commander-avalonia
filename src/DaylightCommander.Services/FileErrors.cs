using DaylightCommander.Domain;

namespace DaylightCommander.Services;

/// <summary>사용자에게 보여줄 <see cref="FileError"/>를 가진 오류. 문장은 화면이 언어에 맞춰 만든다.</summary>
public sealed class FileOperationException(FileError error) : IOException(error.ToString())
{
    public FileError Error { get; } = error;
}

public static class FileErrors
{
    /// <summary>예외를 사용자에게 보여줄 <see cref="FileError"/>로 바꾼다. <paramref name="path"/>는 오류가 난 경로(알면).</summary>
    public static FileError From(Exception e, string? path = null)
    {
        string Name() => path is null ? "" : Path.GetFileName(path.TrimEnd('\\', '/')) is { Length: > 0 } n ? n : path;

        return e switch
        {
            FileOperationException f => f.Error,
            DirectoryNotFoundException or FileNotFoundException => new FileError.NotFound(Name()),
            UnauthorizedAccessException => new FileError.AccessDenied(Name()),
            _ => new FileError.Other(string.IsNullOrEmpty(e.Message) ? e.GetType().Name : e.Message),
        };
    }
}
