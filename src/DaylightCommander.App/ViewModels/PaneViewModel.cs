using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DaylightCommander.Domain;
using DaylightCommander.Domain.Settings;
using DaylightCommander.Services;

namespace DaylightCommander.App.ViewModels;

/// <summary>
/// 한 패널의 화면용 상태. 실제 이동·정렬·히스토리·커서·선택·필터는 <see cref="PaneController"/>(UI 비종속)가 하고,
/// 이 클래스는 그 상태를 화면이 쓰는 속성과 명령으로 바꿔 준다.
/// </summary>
public partial class PaneViewModel : ObservableObject, IDisposable
{
    readonly PaneController _controller;
    IReadOnlyList<FileEntry>? _shownEntries;
    Dictionary<string, EntryItem> _byPath = [];
    IReadOnlySet<string> _shownSelection = new HashSet<string>();

    readonly SettingsController? _settings;

    public PaneViewModel(PaneController controller, SettingsController? settings = null)
    {
        _controller = controller;
        _settings = settings;
        _controller.StateChanged += Apply;
        Apply(_controller.State);
        if (_settings is not null) _settings.Changed += OnSettingsChanged;
        Strings.Changed += OnLanguageChanged;
    }

    public SettingsController? Settings => _settings;

    void OnSettingsChanged(AppSettings s) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(() => IsBookmarked = s.Bookmarks.Contains(CurrentPath));

    /// <summary>언어가 바뀌면 코드가 만들어 둔 문구(상태줄·머리글·안내)를 새 언어로 다시 만든다.</summary>
    void OnLanguageChanged() => Avalonia.Threading.Dispatcher.UIThread.Post(() => Apply(_controller.State));

    public void Dispose()
    {
        _controller.StateChanged -= Apply;
        Strings.Changed -= OnLanguageChanged;
        if (_settings is not null) _settings.Changed -= OnSettingsChanged;
    }

    public PaneController Controller => _controller;

    /// <summary>이 패널을 눌렀을 때 활성 패널로 만들어 달라는 요청 (HomeViewModel이 연결한다).</summary>
    public Action? ActivateRequested { get; set; }

    [ObservableProperty] public partial string CurrentPath { get; set; } = "";

    /// <summary>경로 표시줄에 쓰는 글. 사용자가 고치는 중일 수 있어 <see cref="CurrentPath"/>와 따로 둔다.</summary>
    [ObservableProperty] public partial string PathText { get; set; } = "";

    [ObservableProperty] public partial IReadOnlyList<EntryItem> Items { get; set; } = [];

    /// <summary>목록 대신 보여 줄 안내 문구(불러오는 중·비어 있음·일치 없음). 목록이 있으면 빈 문자열.</summary>
    [ObservableProperty] public partial string Message { get; set; } = "";

    [ObservableProperty] public partial string StatusText { get; set; } = "";

    /// <summary>상태 표시줄 오른쪽의 필터 표시. 필터가 없으면 빈 문자열.</summary>
    [ObservableProperty] public partial string FilterText { get; set; } = "";

    [ObservableProperty] public partial bool HasError { get; set; }
    [ObservableProperty] public partial bool CanGoBack { get; set; }
    [ObservableProperty] public partial bool CanGoForward { get; set; }
    [ObservableProperty] public partial bool CanGoUp { get; set; }
    [ObservableProperty] public partial bool ShowHidden { get; set; }
    [ObservableProperty] public partial bool IsActive { get; set; }

    /// <summary>지금 폴더가 즐겨찾기에 있는지 (하트 아이콘이 채워진다).</summary>
    [ObservableProperty] public partial bool IsBookmarked { get; set; }
    [ObservableProperty] public partial string NameHeader { get; set; } = "";
    [ObservableProperty] public partial string SizeHeader { get; set; } = "";
    [ObservableProperty] public partial string ModifiedHeader { get; set; } = "";

    /// <summary>커서 행. 컨트롤러가 정하고 화면은 따라 그린다 (화면에서 직접 고르지 않는다).</summary>
    [ObservableProperty] public partial EntryItem? Cursor { get; set; }

    /// <summary>컨트롤러 상태 → 화면 속성. 목록이 그대로면 항목을 다시 만들지 않는다 (커서·선택만 바뀌는 경우).</summary>
    void Apply(PaneState s)
    {
        CurrentPath = s.CurrentPath;
        PathText = s.CurrentPath;
        IsBookmarked = _settings?.Value.Bookmarks.Contains(s.CurrentPath) ?? false;
        CanGoBack = s.CanGoBack;
        CanGoForward = s.CanGoForward;
        CanGoUp = s.CanGoUp;
        ShowHidden = s.ShowHidden;
        HasError = s.Error is not null;

        var rebuilt = !ReferenceEquals(_shownEntries, s.VisibleEntries);
        if (rebuilt)
        {
            _shownEntries = s.VisibleEntries;
            var items = s.VisibleEntries.Select(e => new EntryItem(e)).ToList();
            _byPath = items.ToDictionary(i => i.Path);
            _shownSelection = new HashSet<string>();
            Items = items;
        }
        SyncSelection(s);

        Message = s.Loading ? Strings.Current.PaneLoading
            : Items.Count == 0 ? (s.QuickFilter.Length > 0 ? Strings.Current.PaneNoMatches : Strings.Current.PaneEmpty)
            : "";
        StatusText = s.Error is { } error
            ? Strings.Current.PaneOpenError(Strings.Current.FileErrorText(error))
            : s.Loading ? "" : Summary(s);
        FilterText = s.QuickFilter.Length > 0 ? Strings.Current.PaneFilter(s.QuickFilter) : "";

        NameHeader = Header(Strings.Current.ColumnName, SortField.Name, s);
        SizeHeader = Header(Strings.Current.ColumnSize, SortField.Size, s);
        ModifiedHeader = Header(Strings.Current.ColumnModified, SortField.Modified, s);

        // 항목을 다시 만들었으면 커서 항목도 새 객체로 다시 가리켜야 한다. 경로만 같고 옛 객체를 그대로 두면
        // 목록 컨트롤이 새 목록에서 선택을 잃은 채 남아 커서 강조가 사라진다 (복사 뒤 새로 읽은 뒤의 증상).
        var cursorPath = s.CursorEntry?.Path;
        var cursor = cursorPath is not null && _byPath.TryGetValue(cursorPath, out var item) ? item : null;
        if (!ReferenceEquals(Cursor, cursor)) Cursor = cursor;
    }

    /// <summary>바뀐 선택만 항목에 반영한다 (항목이 수만 개여도 선택이 바뀐 만큼만 일한다).</summary>
    void SyncSelection(PaneState s)
    {
        var visibleSelected = s.Selected.Where(_byPath.ContainsKey).ToHashSet();
        foreach (var path in _shownSelection.Except(visibleSelected))
            if (_byPath.TryGetValue(path, out var gone)) gone.IsSelected = false;
        foreach (var path in visibleSelected.Except(_shownSelection))
            _byPath[path].IsSelected = true;
        _shownSelection = visibleSelected;
    }

    static string Summary(PaneState s)
    {
        var text = Strings.Current.PaneItemCount(s.VisibleEntries.Count);
        var picked = s.VisibleEntries.Where(e => s.Selected.Contains(e.Path)).ToList();
        if (picked.Count == 0) return text;
        text += " · " + Strings.Current.PaneSelectedCount(picked.Count);
        var bytes = picked.Sum(e => e.SizeBytes ?? 0L);
        return bytes > 0 ? $"{text} ({BytesFormat.Format(bytes)})" : text;
    }

    static string Header(string title, SortField field, PaneState s) =>
        s.SortField != field ? title : $"{title} {(s.SortAscending ? "▲" : "▼")}";

    public Task StartAsync() => _controller.StartAsync();

    [RelayCommand] Task Back() => _controller.GoBackAsync();
    [RelayCommand] Task Forward() => _controller.GoForwardAsync();
    [RelayCommand] Task Up() => _controller.GoUpAsync();
    [RelayCommand] Task Refresh() => _controller.RefreshAsync();
    [RelayCommand] void ToggleHidden() => _controller.ToggleShowHidden();
    [RelayCommand] void Sort(string field) => _controller.SetSortField(Enum.Parse<SortField>(field));
    [RelayCommand] void ClearError() => _controller.ClearError();

    /// <summary>경로 표시줄에서 Enter. 이동에 실패하면 오류만 상태줄에 뜨고 입력한 글은 그대로 둔다.</summary>
    [RelayCommand] Task GoToPath() => _controller.NavigateToAsync(PathText);

    /// <summary>경로 표시줄에서 Esc: 고치던 글을 버리고 현재 경로로 되돌린다.</summary>
    [RelayCommand] void ResetPathText() => PathText = CurrentPath;

    /// <summary>더블클릭·Enter: 커서가 폴더면 들어간다. 파일 열기는 7단계(뷰어).</summary>
    [RelayCommand] Task OpenCursor() => _controller.OpenCursorAsync();

    /// <summary>마우스 클릭. Ctrl은 토글, Shift는 범위.</summary>
    public void Click(EntryItem item, bool ctrl, bool shift)
    {
        _controller.ClickEntry(item.Path, ctrl, shift);
    }

    public void RequestActivate() => ActivateRequested?.Invoke();
}
