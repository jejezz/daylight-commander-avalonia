using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using DaylightCommander.Domain;

namespace DaylightCommander.App.ViewModels;

/// <summary>목록 한 줄. 도메인 <see cref="FileEntry"/>에 화면용 문자열과 선택 표시를 더한 것.</summary>
public sealed partial class EntryItem(FileEntry entry) : ObservableObject
{
    public FileEntry Entry { get; } = entry;
    public string Path => Entry.Path;
    public string Name => Entry.Name;
    public bool IsDirectory => Entry.IsDirectory;
    public bool IsFile => !Entry.IsDirectory;

    /// <summary>아이콘 모양과 색(앱 색 자원 이름): 폴더·확장자별 파일 아이콘.</summary>
    public string IconName { get; } = Icons.FileIcons.For(entry.Name, entry.IsDirectory).Icon;
    public string IconColor { get; } = Icons.FileIcons.For(entry.Name, entry.IsDirectory).Color;
    /// <summary>격자 큰 아이콘의 확장자 배지 글자 (없으면 null).</summary>
    public string? BadgeText { get; } = Icons.FileIcons.BadgeFor(entry.Name, entry.IsDirectory);
    public double RowOpacity => Entry.IsHidden ? 0.5 : 1.0;
    public string SizeText { get; } = BytesFormat.Format(entry.SizeBytes);

    /// <summary>수정일은 로컬 시간의 <c>yyyy-MM-dd HH:mm</c> (원본 형식).</summary>
    public string ModifiedText { get; } =
        entry.ModifiedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "--";

    /// <summary>선택된 항목 (커서와는 별개). 행 글자가 주색이 되고 배경이 옅게 깔린다.</summary>
    [ObservableProperty] public partial bool IsSelected { get; set; }

    /// <summary>격자 보기의 커서 타일 (목록 보기는 ListBox의 선택으로 그린다).</summary>
    [ObservableProperty] public partial bool IsCursor { get; set; }

    /// <summary>격자 타일 크기. 보기 방식이 바뀌면 <see cref="PaneViewModel"/>이 맞춘다.</summary>
    [ObservableProperty] public partial double IconSize { get; set; }
    [ObservableProperty] public partial double TileWidth { get; set; }
    [ObservableProperty] public partial double TileHeight { get; set; }

    /// <summary>내용 썸네일. 만들기 전이나 대상이 아니면 null (이때 형식 아이콘을 보여 준다).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThumbnail), nameof(NoThumbnail))]
    public partial Avalonia.Media.Imaging.Bitmap? Thumbnail { get; set; }

    public bool HasThumbnail => Thumbnail is not null;
    public bool NoThumbnail => Thumbnail is null;

    CancellationTokenSource? _thumbCts;
    int _thumbSize;

    /// <summary>타일이 화면에 나타났을 때: 아직 없으면 백그라운드로 썸네일을 만든다. 같은 크기로 다시 불러도 한 번만 한다.</summary>
    public void RequestThumbnail(int tileSize)
    {
        if (!Services.ThumbnailPolicy.ShouldGenerate(Entry) || (Thumbnail is not null && _thumbSize == tileSize) || _thumbCts is not null) return;
        var cts = _thumbCts = new CancellationTokenSource();
        _ = LoadThumbnailAsync(tileSize, cts); // 기다리지 않는다: UI는 형식 아이콘을 먼저 보이고, 끝나면 썸네일로 바뀐다
    }

    async Task LoadThumbnailAsync(int tileSize, CancellationTokenSource cts)
    {
        try
        {
            var bitmap = await ThumbnailLoader.LoadAsync(Entry, tileSize, cts.Token); // 이어지는 코드는 UI 스레드
            if (!cts.IsCancellationRequested && bitmap is not null) { Thumbnail = bitmap; _thumbSize = tileSize; }
        }
        catch (OperationCanceledException) { }
        finally { if (ReferenceEquals(_thumbCts, cts)) _thumbCts = null; }
    }

    /// <summary>타일이 화면에서 사라졌다: 대기 중인 작업을 취소한다 (이미 만든 썸네일은 그대로 둔다).</summary>
    public void CancelThumbnail()
    {
        _thumbCts?.Cancel();
        _thumbCts = null;
    }

    /// <summary>폴더 비교 모드에서의 비교 결과 (같거나 비교 중이 아니면 null). 행 배경색이 된다.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOnlyHere), nameof(IsDiffers))]
    public partial Services.FileDiffStatus? DiffStatus { get; set; }

    public bool IsOnlyHere => DiffStatus == Services.FileDiffStatus.OnlyHere;
    public bool IsDiffers => DiffStatus == Services.FileDiffStatus.Differs;
}
