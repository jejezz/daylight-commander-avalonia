using System.Diagnostics;
using System.Text;
using DaylightCommander.Domain;

namespace DaylightCommander.Services;

/// <summary>텍스트 뷰어가 보여 줄 내용. <see cref="Truncated"/>면 앞부분만 읽은 것이다.</summary>
public sealed record TextPreview(string Text, bool Truncated, long Length);

/// <summary>Hex 뷰어가 보여 줄 바이트. <see cref="Truncated"/>면 앞부분만 읽은 것이다.</summary>
public sealed record HexPreview(byte[] Bytes, bool Truncated, long Length);

/// <summary>뷰어(F3)가 파일을 읽는 방법 (원본 <c>viewer_screen.dart</c>의 읽기 규칙).</summary>
public static class FileViewing
{
    /// <summary>텍스트 뷰어가 한 번에 읽는 상한. 큰 로그를 통째로 올리면 화면이 멈춘다 (원본에는 없는 안전장치).</summary>
    public const int MaxTextBytes = 4 * 1024 * 1024;

    /// <summary>Hex 뷰어 미리보기 상한 (원본과 같은 256KB). 큰 파일 전체를 메모리에 올리지 않는다.</summary>
    public const int MaxHexBytes = 256 * 1024;

    /// <summary>앞부분을 읽어 텍스트처럼 보이는지 살핀다 (확장자로 알 수 없는 파일). 읽지 못하면 false.</summary>
    public static async Task<bool> SniffTextAsync(string path, CancellationToken ct = default)
    {
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buffer = new byte[8192];
            var n = await stream.ReadAsync(buffer, ct);
            return FileKinds.LooksLikeText(buffer.AsSpan(0, n));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// 텍스트 파일을 읽는다. UTF-8로 읽고(BOM은 떼고), 안 되면 Latin-1로 폴백한다 — 인코딩 자동 감지는 아직 없다.
    /// 상한을 넘으면 앞부분만 읽고 <see cref="TextPreview.Truncated"/>를 켠다.
    /// </summary>
    public static async Task<TextPreview> ReadTextAsync(string path, int maxBytes = MaxTextBytes, CancellationToken ct = default)
    {
        var (bytes, length) = await ReadHeadAsync(path, maxBytes, ct);
        var truncated = length > bytes.Length;
        return new TextPreview(DecodeText(bytes, truncated), truncated, length);
    }

    internal static string DecodeText(byte[] bytes, bool truncated)
    {
        var strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        // 잘린 끝이 UTF-8 글자 한가운데일 수 있어서, 잘랐을 때는 끝의 몇 바이트를 덜어 가며 다시 시도한다.
        for (var cut = 0; cut <= (truncated ? 3 : 0); cut++)
        {
            try
            {
                var text = strict.GetString(bytes, 0, bytes.Length - cut);
                return text.Length > 0 && text[0] == '﻿' ? text[1..] : text;
            }
            catch (DecoderFallbackException)
            {
                // 다음 시도
            }
        }
        return Encoding.Latin1.GetString(bytes);
    }

    /// <summary>Hex 뷰어용으로 앞 <paramref name="maxBytes"/>바이트를 읽는다.</summary>
    public static async Task<HexPreview> ReadHexAsync(string path, int maxBytes = MaxHexBytes, CancellationToken ct = default)
    {
        var (bytes, length) = await ReadHeadAsync(path, maxBytes, ct);
        return new HexPreview(bytes, length > bytes.Length, length);
    }

    static async Task<(byte[] Bytes, long Length)> ReadHeadAsync(string path, int maxBytes, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var length = stream.Length;
        var buffer = new byte[(int)Math.Min(length, maxBytes)];
        var read = 0;
        while (read < buffer.Length)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(read), ct);
            if (n == 0) break;
            read += n;
        }
        return (read == buffer.Length ? buffer : buffer[..read], length);
    }

    /// <summary>
    /// Hex 한 줄: <c>오프셋(8자리 16진수)  16진수 16개(47자 폭)  ASCII</c>. 출력 가능한 ASCII가 아닌 바이트는 점(<c>.</c>)이다.
    /// </summary>
    public static string HexRow(byte[] bytes, int start)
    {
        var end = Math.Min(start + 16, bytes.Length);
        var hex = new StringBuilder(47);
        var ascii = new StringBuilder(16);
        for (var i = start; i < end; i++)
        {
            if (i > start) hex.Append(' ');
            hex.Append(bytes[i].ToString("x2"));
            ascii.Append(bytes[i] is >= 0x20 and < 0x7f ? (char)bytes[i] : '.');
        }
        return $"{start:x8}  {hex.ToString().PadRight(47)}  {ascii}";
    }

    public static int HexRowCount(byte[] bytes) => (bytes.Length + 15) / 16;
}

/// <summary>파일을 연다 (테스트에서 바꿀 수 있게 인터페이스로 둔다).</summary>
public interface IFileOpener
{
    void Open(string path);
}

/// <summary>파일을 OS가 확장자에 연결해 둔 기본 앱으로 연다 (탐색기/Finder에서 더블클릭한 것과 같다).</summary>
public sealed class OpenWithDefaultApp : IFileOpener
{
    public void Open(string path)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                using (Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })) { }
            else if (OperatingSystem.IsMacOS())
                using (Process.Start("open", [path])) { }
            else
                using (Process.Start("xdg-open", [path])) { }
        }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new FileOperationException(new FileError.Other(e.Message));
        }
    }
}
