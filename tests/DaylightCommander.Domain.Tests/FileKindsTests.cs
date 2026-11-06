using DaylightCommander.Domain;

namespace DaylightCommander.Domain.Tests;

public class FileKindsTests
{
    [Theory]
    [InlineData("photo.JPG", ViewerKind.Image)]
    [InlineData("a.png", ViewerKind.Image)]
    [InlineData("notes.txt", ViewerKind.Text)]
    [InlineData("Program.cs", ViewerKind.Text)]
    [InlineData("Main.axaml", ViewerKind.Text)]
    [InlineData("doc.PDF", ViewerKind.Pdf)]
    [InlineData("clip.mp4", ViewerKind.Video)]
    [InlineData("song.flac", ViewerKind.Audio)]
    [InlineData("data.bin", ViewerKind.Unknown)]
    [InlineData("archive.zip", ViewerKind.Unknown)]
    public void 확장자로_뷰어_종류를_정한다(string name, ViewerKind expected) =>
        Assert.Equal(expected, FileKinds.Classify(name));

    [Theory]
    [InlineData("Makefile")]
    [InlineData("DOCKERFILE")]
    [InlineData("README")]
    [InlineData("CMakeLists.txt")]
    public void 확장자가_없어도_흔한_텍스트_이름은_텍스트다(string name) =>
        Assert.Equal(ViewerKind.Text, FileKinds.Classify(name));

    [Fact]
    public void 점으로_시작하는_이름은_확장자가_없다()
    {
        Assert.Equal("", FileKinds.ExtensionOf(".gitignore"));
        Assert.Equal("", FileKinds.ExtensionOf("trailing."));
        Assert.Equal("gz", FileKinds.ExtensionOf("a.tar.GZ"));
        Assert.Equal(ViewerKind.Unknown, FileKinds.Classify(".gitignore")); // 이름 표에는 없다 (확장자 표의 gitignore는 "x.gitignore"용)
    }

    [Fact]
    public void NUL이_있으면_바이너리다()
    {
        Assert.False(FileKinds.LooksLikeText([0x41, 0x00, 0x42]));
    }

    [Fact]
    public void 제어_문자가_5퍼센트_미만이면_텍스트로_본다()
    {
        var text = new byte[200];
        Array.Fill(text, (byte)'a');
        text[0] = 0x01; // 0.5%
        Assert.True(FileKinds.LooksLikeText(text));
        for (var i = 0; i < 20; i++) text[i] = 0x01; // 10%
        Assert.False(FileKinds.LooksLikeText(text));
    }

    [Fact]
    public void 탭_개행_ESC는_텍스트에_흔해서_제어_문자로_세지_않는다()
    {
        Assert.True(FileKinds.LooksLikeText("a\tb\r\nc\f\u001b"u8));
        Assert.True(FileKinds.LooksLikeText([]));
    }

    [Fact]
    public void UTF8이_아니어도_텍스트다()
    {
        Assert.True(FileKinds.LooksLikeText([0xB0, 0xA1, 0xB3, 0xAA, 0x20, 0x61])); // CP949 "가나 a"
    }
}
