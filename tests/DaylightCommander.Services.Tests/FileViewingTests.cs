using System.Text;
using DaylightCommander.Services;

namespace DaylightCommander.Services.Tests;

public sealed class FileViewingTests : IDisposable
{
    readonly string _dir = Directory.CreateTempSubdirectory("dc-view-test").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public async Task UTF8_텍스트를_읽고_BOM은_뗀다()
    {
        var path = Write("a.txt", [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("안녕 hello")]);
        var p = await FileViewing.ReadTextAsync(path);
        Assert.Equal("안녕 hello", p.Text);
        Assert.False(p.Truncated);
    }

    [Fact]
    public async Task UTF8이_아니면_Latin1로_읽는다()
    {
        var path = Write("b.txt", [0x63, 0x61, 0x66, 0xE9]); // café(Latin-1)
        var p = await FileViewing.ReadTextAsync(path);
        Assert.Equal("café", p.Text);
    }

    [Fact]
    public async Task 상한을_넘으면_앞부분만_읽고_잘렸다고_알린다()
    {
        var path = Write("big.txt", Encoding.ASCII.GetBytes(new string('x', 1000)));
        var p = await FileViewing.ReadTextAsync(path, maxBytes: 100);
        Assert.Equal(100, p.Text.Length);
        Assert.True(p.Truncated);
        Assert.Equal(1000, p.Length);
    }

    [Fact]
    public async Task UTF8_글자_한가운데서_잘려도_깨지지_않는다()
    {
        // '가'는 3바이트. 4바이트로 자르면 두 번째 글자가 중간에서 끊긴다.
        var path = Write("kr.txt", Encoding.UTF8.GetBytes("가나다라"));
        var p = await FileViewing.ReadTextAsync(path, maxBytes: 4);
        Assert.Equal("가", p.Text);
        Assert.True(p.Truncated);
    }

    [Fact]
    public async Task 빈_파일은_빈_글이다()
    {
        var p = await FileViewing.ReadTextAsync(Write("empty.txt", []));
        Assert.Equal("", p.Text);
        Assert.False(p.Truncated);
    }

    [Fact]
    public async Task 확장자를_모르는_파일은_앞부분을_보고_텍스트인지_가린다()
    {
        Assert.True(await FileViewing.SniffTextAsync(Write("x.unknown", "hello\nworld"u8.ToArray())));
        Assert.False(await FileViewing.SniffTextAsync(Write("y.unknown", [1, 2, 0, 3, 4])));
        Assert.False(await FileViewing.SniffTextAsync(Path.Combine(_dir, "missing.bin")));
    }

    [Fact]
    public async Task Hex_미리보기는_256KB에서_멈춘다()
    {
        var path = Write("big.bin", new byte[FileViewing.MaxHexBytes + 5000]);
        var p = await FileViewing.ReadHexAsync(path);
        Assert.Equal(FileViewing.MaxHexBytes, p.Bytes.Length);
        Assert.True(p.Truncated);
        Assert.Equal(FileViewing.MaxHexBytes + 5000, p.Length);
    }

    [Fact]
    public async Task 작은_파일은_Hex로_통째로_읽는다()
    {
        var p = await FileViewing.ReadHexAsync(Write("s.bin", [1, 2, 3]));
        Assert.Equal([1, 2, 3], p.Bytes);
        Assert.False(p.Truncated);
    }

    [Fact]
    public void Hex_한_줄_형식은_원본과_같다()
    {
        // 오프셋 8자리 + 두 칸 + 16진수(47자 폭으로 채움) + 두 칸 + ASCII(출력 불가는 점)
        var bytes = new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f, 0x2c, 0x20, 0x57, 0x6f, 0x72, 0x6c, 0x64, 0x21, 0x0a, 0xff };
        Assert.Equal(
            "00000000  48 65 6c 6c 6f 2c 20 57 6f 72 6c 64 21 0a ff" + "   " + "  " + "Hello, World!..",
            FileViewing.HexRow(bytes, 0));
    }

    [Fact]
    public void Hex_짧은_마지막_줄도_ASCII_칸의_위치가_같다()
    {
        var bytes = new byte[20];
        for (var i = 0; i < 20; i++) bytes[i] = (byte)(0x41 + i);
        Assert.Equal(2, FileViewing.HexRowCount(bytes));
        var first = FileViewing.HexRow(bytes, 0);
        var second = FileViewing.HexRow(bytes, 16);
        Assert.Equal("00000010  51 52 53 54", second[..21]);
        const int asciiStart = 8 + 2 + 47 + 2;
        Assert.Equal("ABCDEFGHIJKLMNOP", first[asciiStart..]);
        Assert.Equal("QRST", second[asciiStart..]);
    }

    [Fact]
    public void Hex_행_수는_16바이트_단위로_올림한다()
    {
        Assert.Equal(0, FileViewing.HexRowCount([]));
        Assert.Equal(1, FileViewing.HexRowCount(new byte[16]));
        Assert.Equal(2, FileViewing.HexRowCount(new byte[17]));
    }
}
