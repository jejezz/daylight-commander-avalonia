using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.App.ViewModels;

/// <summary>트리의 한 줄 (드라이브 또는 폴더).</summary>
public sealed class TreeRow(string path, string label, int depth, bool expanded, bool leaf, bool selected, bool isDrive, DriveKind driveKind = DriveKind.Unknown, bool dimmed = false)
{
    public string Path { get; } = path;
    public string Label { get; } = label;
    public bool Expanded { get; } = expanded;
    public bool Leaf { get; } = leaf;
    public bool Selected { get; } = selected;
    public bool IsDrive { get; } = isDrive;
    public bool IsFolder => !IsDrive;
    /// <summary>숨김·시스템 폴더: 아이콘과 글자를 흐리게 그린다.</summary>
    public bool Dimmed { get; } = dimmed;
    public double RowOpacity => Dimmed ? 0.5 : 1.0;
    public string IconName => IsDrive ? Icons.FileIcons.ForDrive(driveKind) : Expanded ? "folder-open" : "folder";
    public string IconColor => IsDrive ? "AppPrimary" : "AppIconFolder";
    public bool CanExpand => !Leaf;
    public bool ShowCollapsed => !Leaf && !Expanded;
    public bool ShowExpanded => !Leaf && Expanded;
    public bool SameAs(TreeRow o) =>
        Path == o.Path && IconName == o.IconName && Label == o.Label && Dimmed == o.Dimmed && Expanded == o.Expanded && Leaf == o.Leaf && Selected == o.Selected && Indent == o.Indent;
    public Avalonia.Thickness Indent { get; } = new(UiScale.Scaled(4 + depth * 14), 0, 0, 0);
}

/// <summary>
/// 탐색기 모양의 디렉터리 트리 (원본 <c>DirectoryTree</c>). 루트는 드라이브 목록이고 하위 폴더는 펼칠 때 읽는다.
/// <b>활성 패널</b>을 따라간다: 폴더를 누르면 활성 패널이 그 폴더로 가고, 활성 패널의 현재 경로가 강조되며 조상이 펼쳐진다.
/// </summary>
public sealed partial class DirectoryTreeViewModel : ObservableObject
{
    readonly HomeState _state;
    readonly Action<string> _toggleBookmark;
    bool _reloading;
    IReadOnlyList<FileEntry>? _lastLeft, _lastRight;
    readonly HashSet<string> _expanded = new(PathComparer);
    readonly Dictionary<string, IReadOnlyList<DirectoryTree.SubFolder>> _children = new(PathComparer);
    readonly HashSet<string> _loading = new(PathComparer);
    IReadOnlyList<DriveEntry> _drives = [];
    readonly Dictionary<string, string?> _driveLabels = new(PathComparer);
    string _current = "";

    static StringComparer PathComparer => OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    public DirectoryTreeViewModel(HomeState state, Action<string> toggleBookmark)
    {
        _state = state;
        _toggleBookmark = toggleBookmark;
        state.ActiveChanged += _ => FollowActive();
        UiScale.Changed += () => Rebuild(); // 들여쓰기도 글자 크기를 따라간다
        state.Left.StateChanged += st =>
        {
            if (state.Active == PaneSide.Left) Follow(st.CurrentPath);
            if (!ReferenceEquals(st.Entries, _lastLeft)) { _lastLeft = st.Entries; _ = ReloadAsync(); }
        };
        state.Right.StateChanged += st =>
        {
            if (state.Active == PaneSide.Right) Follow(st.CurrentPath);
            if (!ReferenceEquals(st.Entries, _lastRight)) { _lastRight = st.Entries; _ = ReloadAsync(); }
        };
    }

    public ObservableCollection<TreeRow> Rows { get; } = [];

    /// <summary>트리를 처음 보여 줄 때(또는 열 때) 드라이브를 읽고 활성 패널 경로까지 펼친다.</summary>
    public void Refresh()
    {
        _drives = new ListDrives().Invoke();
        FollowActive();
        _ = LoadDriveLabelsAsync();
    }

    /// <summary>드라이브 이름(볼륨 레이블)을 뒤에서 읽어 채운다 — 끊긴 네트워크 드라이브가 화면을 멈추지 않게 드라이브마다 따로 읽는다.</summary>
    async Task LoadDriveLabelsAsync()
    {
        await Task.WhenAll(_drives.Select(async drive =>
        {
            var label = await Task.Run(() => ListDrives.VolumeLabel(drive));
            if (!_driveLabels.TryGetValue(drive.Path, out var old) || old != label)
            {
                _driveLabels[drive.Path] = label;
                Rebuild();
            }
        }));
    }

    void FollowActive() => Follow(_state.ActiveController.State.CurrentPath);

    void Follow(string path)
    {
        if (Locations.IsRemote(path)) return; // 트리는 로컬 폴더만 보여 준다 (서버 경로는 강조·펼침 없이 둔다)
        _current = path;
        foreach (var dir in DirectoryTree.Ancestors(path))
        {
            _expanded.Add(dir);
            _ = LoadChildrenAsync(dir);
        }
        Rebuild();
    }

    async Task LoadChildrenAsync(string path)
    {
        if (_children.ContainsKey(path) || !_loading.Add(path)) return;
        var names = await Task.Run(() => DirectoryTree.ListSubfolders(path));
        _loading.Remove(path);
        _children[path] = names;
        Rebuild();
    }

    public void Toggle(TreeRow row)
    {
        if (!_expanded.Remove(row.Path))
        {
            _expanded.Add(row.Path);
            _ = LoadChildrenAsync(row.Path);
        }
        Rebuild();
    }

    bool IsDrive(string path) => _drives.Any(d => DirectoryTree.SamePath(d.Path, path));

    /// <summary>
    /// 펼쳐진(= 보이는) 폴더의 자식 목록을 다시 읽는다. 달라진 곳만 갱신하고, 사라진 폴더는 펼침에서도 뺀다.
    /// 패널의 목록이 바뀔 때, 5초마다, 새로고침 버튼에서 부른다.
    /// </summary>
    public async Task ReloadAsync()
    {
        if (_reloading) return;
        _reloading = true;
        try
        {
            var changed = false;
            foreach (var path in _children.Keys.ToList())
            {
                if (!_expanded.Contains(path) && !IsDrive(path)) continue;
                var names = await Task.Run(() => DirectoryTree.ListSubfolders(path));
                if (!_children.TryGetValue(path, out var old) || !old.SequenceEqual(names))
                {
                    _children[path] = names;
                    changed = true;
                }
            }
            var gone = await Task.Run(() => _expanded.Where(e => !Directory.Exists(e)).ToList());
            if (gone.Count > 0)
            {
                foreach (var g in gone) { _expanded.Remove(g); _children.Remove(g); }
                changed = true;
            }
            if (changed) Rebuild();
        }
        finally
        {
            _reloading = false;
        }
    }

    /// <summary>새로고침 버튼: 드라이브 목록까지 다시 읽는다.</summary>
    public async Task ReloadAllAsync()
    {
        _drives = await Task.Run(() => new ListDrives().Invoke());
        Rebuild();
        await Task.WhenAll(ReloadAsync(), LoadDriveLabelsAsync());
    }

    // --- 폴더에 하는 동작 (우클릭 메뉴·끌어다 놓기). 모두 활성 패널을 기준으로 한다. ---

    public HomeState State => _state;

    public void Open(TreeRow row) => Select(row);
    public void NewFolder(TreeRow row) => _state.RequestNewFolderIn(row.Path);
    public void Rename(TreeRow row) => _state.RequestRenamePath(row.Path);
    public void Delete(TreeRow row) => _state.RequestDeletePaths([row.Path]);
    public void Properties(TreeRow row) => _state.RequestPropertiesOf([row.Path]);
    public void ToggleBookmark(TreeRow row) => _toggleBookmark(row.Path);
    public void Reveal(TreeRow row) => _state.RevealInFileManager(row.Path, _state.Active);

    /// <summary>끌어다 놓기: 트리에서는 기본이 이동, Ctrl이면 복사 (원본). 폴더를 자기 안에 놓는 오류는 활성 패널에 알린다.</summary>
    public void Drop(IReadOnlyList<string> paths, TreeRow target, DropMode mode) =>
        _state.Drop(paths, target.Path, mode, _state.Active);

    /// <summary>폴더를 눌렀다: 활성 패널이 그 폴더로 간다.</summary>
    public void Select(TreeRow row) => _ = _state.ActiveController.NavigateToAsync(row.Path);

    void Rebuild()
    {
        var rows = new List<TreeRow>();

        void Add(string path, string label, int depth, bool isDrive, DriveKind kind = DriveKind.Unknown, bool dimmed = false)
        {
            var expanded = _expanded.Contains(path);
            var children = _children.GetValueOrDefault(path);
            rows.Add(new TreeRow(path, label, depth, expanded, leaf: children is { Count: 0 },
                selected: DirectoryTree.SamePath(path, _current), isDrive, kind, dimmed));
            if (expanded && children is not null)
                foreach (var child in children)
                    Add(child.Path, Path.GetFileName(child.Path), depth + 1, isDrive: false, dimmed: child.Dimmed);
        }

        foreach (var drive in _drives)
            Add(drive.Path, ListDrives.DisplayName(drive, _driveLabels.GetValueOrDefault(drive.Path)), 0, isDrive: true, drive.Kind);

        // 바뀐 줄만 갈아 끼운다 (통째로 비우면 스크롤 위치가 맨 위로 돌아간다).
        for (var i = 0; i < rows.Count; i++)
        {
            if (i >= Rows.Count) Rows.Add(rows[i]);
            else if (!Rows[i].SameAs(rows[i])) Rows[i] = rows[i];
        }
        while (Rows.Count > rows.Count) Rows.RemoveAt(Rows.Count - 1);
    }
}
