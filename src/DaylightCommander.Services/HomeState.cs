using DaylightCommander.Domain;

namespace DaylightCommander.Services;

public enum PaneSide { Left, Right }

public enum NameMode { Rename, NewFolder, NewFile, Compress }

/// <summary>디렉터리 트리 안에서 복사·잘라내기한 폴더.</summary>
public sealed record TreeClipboard(string Path, bool Cut);

/// <summary>홈 화면 위에 뜨는 대화상자. 이름 충돌 대화상자는 <see cref="OperationQueue.Conflict"/>가 따로 가진다.</summary>
public abstract record HomeDialog
{
    private HomeDialog() { }

    /// <summary>복사/이동 확인. 대상 폴더는 직접 고칠 수 있다.</summary>
    public sealed record Transfer(FileOperationKind Kind, IReadOnlyList<string> Sources, string Target, FileError? Error = null) : HomeDialog;

    public sealed record Delete(IReadOnlyList<string> Paths, bool ToTrash) : HomeDialog;

    /// <summary>이름변경(원본 경로 있음) / 새 폴더 / 새 파일.</summary>
    public sealed record NameInput(NameMode Mode, string Parent, string? OriginalPath = null, string InitialText = "", FileError? Error = null) : HomeDialog;
}

/// <summary>
/// 홈 화면(좌우 패널)의 앱 수준 상태. 활성 패널(원본 <c>activePaneProvider</c>), 파일 작업 큐, 열린 대화상자를 가진다.
/// 파일 작업의 대상은 선택한 항목, 선택이 없으면 커서 항목이고, 복사·이동의 기본 대상은 반대편 패널의 폴더다.
/// UI 스레드에서만 부른다고 가정한다 (큐의 이벤트는 작업 스레드에서 올 수 있다).
/// </summary>
public sealed class HomeState : IDisposable
{
    readonly FileOperations _ops;
    readonly ArchiveService _archive = new();
    readonly SettingsController? _settings;
    readonly ExternalEditorLauncher _editor;
    readonly IFileOpener _opener;
    readonly IFileRevealer _revealer;
    readonly Func<Func<Task>, Task> _onUi;
    HomeDialog? _dialog;

    /// <param name="runOnUi">
    /// 작업 스레드에서 끝난 작업이 패널을 새로 읽을 때, 패널 상태를 바꾸는 코드를 UI 스레드에서 돌리게 하는 함수.
    /// 패널 상태는 UI 스레드에서만 바꾼다는 약속을 지키려는 것이다. 기본은 그 자리에서 실행 (테스트).
    /// </param>
    public HomeState(string leftPath, string rightPath, PaneController.ListDirectory list,
        FileOperations? ops = null, Func<Func<Task>, Task>? runOnUi = null,
        SettingsController? settings = null, ExternalEditorLauncher? editor = null, IFileOpener? opener = null, IFileRevealer? revealer = null)
    {
        _opener = opener ?? new OpenWithDefaultApp();
        _revealer = revealer ?? new FileReveal();
        _ops = ops ?? new FileOperations();
        _settings = settings;
        _editor = editor ?? new ExternalEditorLauncher();
        var onUi = _onUi = runOnUi ?? (work => work());
        // 패널은 이동할 때마다 최근 경로에 기록한다 (시작 경로는 기록하지 않는다).
        void Visit(string path) => settings?.RecordVisit(path);
        Left = new PaneController(leftPath, list, onVisit: Visit);
        Right = new PaneController(rightPath, list, onVisit: Visit);
        // 작업이 끝나면 두 패널을 새로 읽는다.
        Queue = new OperationQueue(_ops, () => onUi(async () =>
        {
            await Left.RefreshAsync();
            await Right.RefreshAsync();
        }));
        Queue.ConflictChanged += () => ModalChanged?.Invoke();
    }

    public PaneController Left { get; }
    public PaneController Right { get; }
    public OperationQueue Queue { get; }

    public PaneSide Active { get; private set; } = PaneSide.Left;
    public event Action<PaneSide>? ActiveChanged;

    public HomeDialog? Dialog => _dialog;

    /// <summary>대화상자가 열리거나 닫힐 때.</summary>
    public event Action<HomeDialog?>? DialogChanged;

    /// <summary>대화상자나 충돌 질문이 떠 있으면 true. 이때 목록 단축키는 꺼진다.</summary>
    public bool ModalOpen => _dialog is not null || Queue.Conflict is not null;

    public event Action? ModalChanged;

    public PaneController Controller(PaneSide side) => side == PaneSide.Left ? Left : Right;
    public PaneController ActiveController => Controller(Active);
    PaneController OtherController => Controller(Active == PaneSide.Left ? PaneSide.Right : PaneSide.Left);

    public void Activate(PaneSide side)
    {
        if (Active == side) return;
        Active = side;
        ActiveChanged?.Invoke(side);
    }

    public void SwitchActive() => Activate(Active == PaneSide.Left ? PaneSide.Right : PaneSide.Left);

    void SetDialog(HomeDialog? dialog)
    {
        _dialog = dialog;
        DialogChanged?.Invoke(dialog);
        ModalChanged?.Invoke();
    }

    public void CloseDialog() => SetDialog(null);

    // --- 대화상자 열기 ---

    /// <summary>복사(F5) / 이동(F6).</summary>
    public void RequestTransfer(FileOperationKind kind)
    {
        if (kind == FileOperationKind.Delete) throw new ArgumentException("전송 종류가 아니다", nameof(kind));
        var sources = ActiveController.TargetEntries();
        if (sources.Count == 0) return;
        SetDialog(new HomeDialog.Transfer(kind, sources.Select(e => e.Path).ToList(), OtherController.State.CurrentPath));
    }

    /// <summary>삭제(F8/Delete). <paramref name="permanent"/>면 휴지통을 거치지 않는다 (Shift+Delete).</summary>
    public void RequestDelete(bool permanent = false)
    {
        var targets = ActiveController.TargetEntries();
        if (targets.Count == 0) return;
        SetDialog(new HomeDialog.Delete(targets.Select(e => e.Path).ToList(), ToTrash: !permanent && _ops.CanTrash));
    }

    /// <summary>이름변경(F2, 커서 항목) / 새 폴더(F7) / 새 파일.</summary>
    public void RequestName(NameMode mode)
    {
        var parent = ActiveController.State.CurrentPath;
        if (mode == NameMode.Rename)
        {
            if (ActiveController.State.CursorEntry is not { } entry) return;
            SetDialog(new HomeDialog.NameInput(mode, parent, entry.Path, entry.Name));
        }
        else
        {
            SetDialog(new HomeDialog.NameInput(mode, parent));
        }
    }

    /// <summary>압축: 선택(없으면 커서) 항목을 현재 폴더에 zip으로 묶는다. 이름을 먼저 묻는다.</summary>
    public void RequestCompress()
    {
        var targets = ActiveController.TargetEntries();
        if (targets.Count == 0) return;
        var parent = ActiveController.State.CurrentPath;
        SetDialog(new HomeDialog.NameInput(NameMode.Compress, parent, InitialText: ArchiveService.DefaultZipName(targets.Select(e => e.Path).ToList())));
    }

    /// <summary>압축 풀기: 커서의 zip을 현재 폴더 아래 "zip 이름" 폴더로 푼다.</summary>
    public async Task ExtractAsync()
    {
        var controller = ActiveController;
        if (controller.State.CursorEntry is not { IsDirectory: false } entry || !ArchiveService.IsZip(entry.Name)) return;
        FileError? error = null;
        try
        {
            var dest = Path.Combine(controller.State.CurrentPath, ArchiveService.ExtractFolderName(entry.Name));
            await _archive.ExtractAsync(entry.Path, dest);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            error = e is InvalidDataException ? new FileError.Other(e.Message) : FileErrors.From(e, entry.Path);
        }
        await OtherController.RefreshAsync();
        await controller.RefreshAsync();
        if (error is not null) controller.ReportError(error); // 새로고침이 오류를 지우므로 그 뒤에 알린다
    }

    /// <summary>보기(F3) 요청. 화면(창)이 이 이벤트를 받아 뷰어 창을 연다. 인자는 파일 경로.</summary>
    public event Action<string>? ViewRequested;

    /// <summary>F3: 커서가 파일 위면 뷰어를 열어 달라고 알린다. 폴더 위에서는 아무것도 하지 않는다.</summary>
    public void RequestView()
    {
        if (ActiveController.State.CursorEntry is { IsDirectory: false } entry) ViewRequested?.Invoke(entry.Path);
    }

    /// <summary>
    /// 파일을 OS 기본 앱으로 연다 (Enter·더블클릭, 원본과 같다). 실패하면 해당 패널의 상태줄에 오류를 띄운다.
    /// </summary>
    public void OpenFile(string path, PaneSide side)
    {
        try
        {
            _opener.Open(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Controller(side).ReportError(FileErrors.From(e, path));
        }
    }

    // --- 디렉터리 트리 (폴더 경로를 대상으로 하는 동작) ---

    /// <summary>트리에서 복사/잘라내기한 폴더. 붙여넣기 메뉴가 쓴다 (패널의 선택과는 별개).</summary>
    public TreeClipboard? TreeClipboard { get; private set; }

    public void CopyToTreeClipboard(string path, bool cut) => TreeClipboard = new TreeClipboard(path, cut);

    /// <summary>트리 클립보드의 폴더를 <paramref name="targetDir"/> 안에 붙인다 (잘라내기는 이동, 복사는 복사). 잘라낸 것은 한 번만 붙는다.</summary>
    public void PasteTreeClipboard(string targetDir)
    {
        if (TreeClipboard is not { } clip) return;
        Drop([clip.Path], targetDir, clip.Cut ? DropMode.ForceMove : DropMode.ForceCopy);
        if (clip.Cut) TreeClipboard = null;
    }

    /// <summary>트리의 "새 폴더": <paramref name="parent"/> 안에 만든다.</summary>
    public void RequestNewFolderIn(string parent) => SetDialog(new HomeDialog.NameInput(NameMode.NewFolder, parent));

    /// <summary>트리의 "이름 바꾸기": 패널의 커서가 아니라 <paramref name="path"/> 항목을 바꾼다.</summary>
    public void RequestRenamePath(string path) =>
        SetDialog(new HomeDialog.NameInput(NameMode.Rename, Path.GetDirectoryName(path) ?? "", path, Path.GetFileName(path)));

    /// <summary>트리의 "삭제": 패널의 선택이 아니라 <paramref name="paths"/>를 지울지 묻는다.</summary>
    public void RequestDeletePaths(IReadOnlyList<string> paths)
    {
        if (paths.Count > 0) SetDialog(new HomeDialog.Delete(paths, ToTrash: _ops.CanTrash));
    }

    /// <summary>트리의 "속성": 해당 경로의 속성 창을 열어 달라고 알린다.</summary>
    public void RequestPropertiesOf(IReadOnlyList<string> paths)
    {
        if (paths.Count > 0) PropertiesRequested?.Invoke(paths);
    }

    // --- 끌어다 놓기·복제·파일 관리자에서 보기 ---

    /// <summary>
    /// 끌어다 놓기: <paramref name="sources"/>를 <paramref name="destinationDir"/>에 놓는다. 기본은 같은 드라이브면 이동, 다르면 복사이고
    /// Ctrl(복사)·Shift(이동)로 바꿀 수 있다 (<see cref="DropPlanner"/>). 확인 대화상자 없이 바로 작업 큐에 넣는다.
    /// 폴더를 자기 자신 안에 놓는 것 같은 오류는 <paramref name="errorSide"/> 패널의 상태줄에 띄운다.
    /// </summary>
    public void Drop(IReadOnlyList<string> sources, string destinationDir, DropMode mode = DropMode.Auto, PaneSide? errorSide = null)
    {
        var plan = DropPlanner.Plan(sources, destinationDir, new ListDrives().Invoke(), mode);
        switch (plan.Action)
        {
            case DropAction.Error:
                Controller(errorSide ?? Active).ReportError(plan.Error!);
                return;
            case DropAction.Copy:
                Queue.EnqueueCopy(sources, destinationDir);
                break;
            case DropAction.Move:
                Queue.EnqueueMove(sources, destinationDir);
                break;
            default:
                return;
        }
        // 끌어 온 항목이 선택돼 있었다면 선택을 푼다.
        foreach (var controller in new[] { Left, Right })
            if (controller.State.Selected.Overlaps(sources)) controller.ClearSelection();
    }

    /// <summary>여기에 복제(Ctrl+D): 대상 항목을 같은 폴더에 "이름 (1)"로 복사한다 (충돌 질문 없이).</summary>
    public void DuplicateInPlace()
    {
        var targets = ActiveController.TargetEntries();
        if (targets.Count == 0) return;
        Queue.EnqueueCopy(targets.Select(e => e.Path).ToList(), ActiveController.State.CurrentPath);
        ActiveController.ClearSelection();
    }

    /// <summary>파일 관리자(탐색기 등)에서 항목을 보여 준다. 실패하면 해당 패널의 상태줄에 오류를 띄운다.</summary>
    public void RevealInFileManager(string path, PaneSide side)
    {
        try
        {
            _revealer.Reveal(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Controller(side).ReportError(FileErrors.From(e, path));
        }
    }

    /// <summary>속성 창을 열어 달라는 요청. 인자는 대상 경로들(선택한 항목, 없으면 커서 항목).</summary>
    public event Action<IReadOnlyList<string>>? PropertiesRequested;

    /// <summary>속성(F-바): 활성 패널의 대상 항목들의 속성 창을 열어 달라고 알린다. 대상이 없으면 아무 일도 없다.</summary>
    public void RequestProperties()
    {
        var targets = ActiveController.TargetEntries();
        if (targets.Count > 0) PropertiesRequested?.Invoke(targets.Select(e => e.Path).ToList());
    }

    /// <summary>내장 에디터로 열어 달라는 요청. 화면(창)이 이 이벤트를 받아 에디터 창을 연다. 인자는 파일 경로.</summary>
    public event Action<string>? EditRequested;

    /// <summary>
    /// 편집(F4): 커서가 파일 위일 때만 동작한다. 설정에서 외부 편집기를 골랐으면 그 프로그램으로 열고(실패하면 활성 패널의
    /// 상태줄에 오류), 아니면 내장 에디터를 열어 달라고 알린다(<see cref="EditRequested"/>). 5MB를 넘는 파일은
    /// 내장 에디터로 열지 않고 이유를 상태줄에 띄운다 — F3 보기로 열 수 있다.
    /// </summary>
    public void RequestEdit()
    {
        var controller = ActiveController;
        if (controller.State.CursorEntry is not { IsDirectory: false } entry) return;

        if (_settings?.Value.ExternalEditor is { } executable)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    _editor.Open(executable, entry.Path);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    await _onUi(() =>
                    {
                        controller.ReportError(FileErrors.From(e));
                        return Task.CompletedTask;
                    });
                }
            });
            return;
        }

        if (entry.SizeBytes > TextFiles.EditorMaxBytes)
        {
            controller.ReportError(new FileError.EditorTooLarge(entry.Name));
            return;
        }
        EditRequested?.Invoke(entry.Path);
    }

    // --- 대화상자 확정 ---

    /// <summary>복사/이동을 큐에 넣는다. 대상이 폴더가 아니면 대화상자에 오류를 띄우고 남겨 둔다.</summary>
    public void ConfirmTransfer(string targetText)
    {
        if (_dialog is not HomeDialog.Transfer d) return;
        string target;
        try
        {
            target = PaneController.Normalize(targetText);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            SetDialog(d with { Target = targetText, Error = FileErrors.From(e, targetText) });
            return;
        }
        if (!Directory.Exists(target))
        {
            SetDialog(d with { Target = targetText, Error = new FileError.TargetFolderMissing() });
            return;
        }
        if (d.Kind == FileOperationKind.Copy) Queue.EnqueueCopy(d.Sources, target);
        else Queue.EnqueueMove(d.Sources, target);
        SetDialog(null);
        ActiveController.ClearSelection();
    }

    public void ConfirmDelete()
    {
        if (_dialog is not HomeDialog.Delete d) return;
        Queue.EnqueueDelete(d.Paths, d.ToTrash);
        SetDialog(null);
        ActiveController.ClearSelection();
    }

    /// <summary>이름변경/새 폴더/새 파일을 실행한다. 실패하면 대화상자에 오류를 띄우고 남겨 둔다.</summary>
    public async Task ConfirmNameAsync(string text)
    {
        if (_dialog is not HomeDialog.NameInput d) return;
        try
        {
            var created = d.Mode switch
            {
                NameMode.Compress => await CompressAsync(d.Parent, text),
                NameMode.Rename => await _ops.RenameAsync(d.OriginalPath!, text),
                NameMode.NewFolder => await _ops.CreateDirectoryAsync(d.Parent, text),
                _ => await _ops.CreateFileAsync(d.Parent, text),
            };
            SetDialog(null);
            await OtherController.RefreshAsync();
            await ActiveController.RefreshAsync(focus: created);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            if (_dialog is HomeDialog.NameInput current)
                SetDialog(current with { Error = FileErrors.From(e, d.Parent) });
        }
    }

    async Task<string> CompressAsync(string parent, string text)
    {
        var name = ArchiveService.IsZip(text) ? text : text + ".zip";
        var zipPath = Path.Combine(parent, name);
        var sources = ActiveController.TargetEntries().Select(e => e.Path).ToList();
        await _archive.CompressAsync(sources, zipPath);
        ActiveController.ClearSelection();
        return zipPath;
    }

    public void Dispose() => Queue.Dispose();
}
