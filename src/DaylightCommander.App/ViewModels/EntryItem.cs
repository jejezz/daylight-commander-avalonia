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
    public double RowOpacity => Entry.IsHidden ? 0.5 : 1.0;
    public string SizeText { get; } = BytesFormat.Format(entry.SizeBytes);

    /// <summary>수정일은 로컬 시간의 <c>yyyy-MM-dd HH:mm</c> (원본 형식).</summary>
    public string ModifiedText { get; } =
        entry.ModifiedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "--";

    /// <summary>선택된 항목 (커서와는 별개). 행 글자가 주색이 되고 배경이 옅게 깔린다.</summary>
    [ObservableProperty] public partial bool IsSelected { get; set; }
}
