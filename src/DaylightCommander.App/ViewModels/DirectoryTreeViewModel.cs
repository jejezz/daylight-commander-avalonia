using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.App.ViewModels;

/// <summary>트리의 한 줄 (드라이브 또는 폴더).</summary>
public sealed class TreeRow(string path, string label, int depth, bool expanded, bool leaf, bool selected, bool isDrive, DriveKind driveKind = DriveKind.Unknown)
{
    public string Path { get; } = path;
    public string Label { get; } = label;
    public bool Expanded { get; } = expanded;
    public bool Leaf { get; } = leaf;
    public bool Selected { get; } = selected;
    public bool IsDrive { get; } = isDrive;
    public bool IsFolder => !IsDrive;
    public string IconName => IsDrive ? Icons.FileIcons.ForDrive(driveKind) : Expanded ? "folder-open" : "folder";
    public string IconColor => IsDrive ? "AppPrimary" : "AppIconFolder";
    public bool CanExpand => !Leaf;
    public bool ShowCollapsed => !Leaf && !Expanded;
    public bool ShowExpanded => !Leaf && Expanded;
    public bool SameAs(TreeRow o) =>
        Path == o.Path && IconName == o.IconName && Label == o.Label && Expanded == o.Expanded && Leaf == o.Leaf && Selected == o.Selected && Indent == o.Indent;
    public Avalonia.Thickness Indent { get; } = new(4 + depth * 14, 0, 0, 0);
}

/// <summary>
/// 탐색기 모양의 디렉터리 트리 (원본 <c>DirectoryTree</c>). 루트는 드라이브 목록이고 하위 폴더는 펼칠 때 읽는다.
/// <b>활성 패널</b>을 따라간다: 폴더를 누르면 활성 패널이 그 폴더로 가고, 활성 패널의 현재 경로가 강조되며 조상이 펼쳐진다.
/// </summary>
public sealed partial class DirectoryTreeViewModel : ObservableObject
{
    readonly HomeState _state;
    readonly HashSet<string> _expanded = new(PathComparer);
    readonly Dictionary<string, IReadOnlyList<string>> _children = new(PathComparer);
    readonly HashSet<string> _loading = new(PathComparer);
    IReadOnlyList<DriveEntry> _drives = [];
    string _current = "";

    static StringComparer PathComparer => OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    public DirectoryTreeViewModel(HomeState state)
    {
        _state = state;
        state.ActiveChanged += _ => FollowActive();
        state.Left.StateChanged += st => { if (state.Active == PaneSide.Left) Follow(st.CurrentPath); };
        state.Right.StateChanged += st => { if (state.Active == PaneSide.Right) Follow(st.CurrentPath); };
    }

    public ObservableCollection<TreeRow> Rows { get; } = [];

    /// <summary>트리를 처음 보여 줄 때(또는 열 때) 드라이브를 읽고 활성 패널 경로까지 펼친다.</summary>
    public void Refresh()
    {
        _drives = new ListDrives().Invoke();
        FollowActive();
    }

    void FollowActive() => Follow(_state.ActiveController.State.CurrentPath);

    void Follow(string path)
    {
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

    /// <summary>폴더를 눌렀다: 활성 패널이 그 폴더로 간다.</summary>
    public void Select(TreeRow row) => _ = _state.ActiveController.NavigateToAsync(row.Path);

    void Rebuild()
    {
        var rows = new List<TreeRow>();

        void Add(string path, string label, int depth, bool isDrive, DriveKind kind = DriveKind.Unknown)
        {
            var expanded = _expanded.Contains(path);
            var children = _children.GetValueOrDefault(path);
            rows.Add(new TreeRow(path, label, depth, expanded, leaf: children is { Count: 0 },
                selected: DirectoryTree.SamePath(path, _current), isDrive, kind));
            if (expanded && children is not null)
                foreach (var child in children)
                    Add(child, Path.GetFileName(child), depth + 1, isDrive: false);
        }

        foreach (var drive in _drives) Add(drive.Path, drive.Name, 0, isDrive: true, drive.Kind);

        // 바뀐 줄만 갈아 끼운다 (통째로 비우면 스크롤 위치가 맨 위로 돌아간다).
        for (var i = 0; i < rows.Count; i++)
        {
            if (i >= Rows.Count) Rows.Add(rows[i]);
            else if (!Rows[i].SameAs(rows[i])) Rows[i] = rows[i];
        }
        while (Rows.Count > rows.Count) Rows.RemoveAt(Rows.Count - 1);
    }
}
