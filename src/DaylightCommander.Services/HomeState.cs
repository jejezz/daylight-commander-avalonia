using DaylightCommander.Domain;

namespace DaylightCommander.Services;

public enum PaneSide { Left, Right }

public enum NameMode { Rename, NewFolder, NewFile }

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
    readonly SettingsController? _settings;
    readonly ExternalEditorLauncher _editor;
    readonly Func<Func<Task>, Task> _onUi;
    HomeDialog? _dialog;

    /// <param name="runOnUi">
    /// 작업 스레드에서 끝난 작업이 패널을 새로 읽을 때, 패널 상태를 바꾸는 코드를 UI 스레드에서 돌리게 하는 함수.
    /// 패널 상태는 UI 스레드에서만 바꾼다는 약속을 지키려는 것이다. 기본은 그 자리에서 실행 (테스트).
    /// </param>
    public HomeState(string leftPath, string rightPath, PaneController.ListDirectory list,
        FileOperations? ops = null, Func<Func<Task>, Task>? runOnUi = null,
        SettingsController? settings = null, ExternalEditorLauncher? editor = null)
    {
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

    /// <summary>
    /// 편집(F4): 커서가 파일 위이고 설정의 외부 편집기가 정해져 있으면 그 프로그램으로 연다.
    /// 내장 에디터는 7단계에서 만들 예정이라, 내장 모드이거나 편집기를 안 골랐으면 아무것도 하지 않는다.
    /// 실행에 실패하면 활성 패널의 상태줄에 오류를 띄운다.
    /// </summary>
    public void RequestEdit()
    {
        var controller = ActiveController;
        if (controller.State.CursorEntry is not { IsDirectory: false } entry) return;
        if (_settings?.Value.ExternalEditor is not { } executable) return;
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

    public void Dispose() => Queue.Dispose();
}
