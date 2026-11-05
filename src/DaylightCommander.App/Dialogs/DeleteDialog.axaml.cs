using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace DaylightCommander.App.Dialogs;

/// <summary>
/// 삭제 확인. 확인 버튼에 초점이 있어 Enter로 바로 확정된다 (휴지통·영구 삭제 모두).
/// </summary>
public partial class DeleteDialog : DialogBase
{
    const int ShownNames = 5;

    public DeleteDialog(IReadOnlyList<string> paths, bool toTrash)
    {
        InitializeComponent();
        var title = toTrash ? Strings.Current.DeleteTrashTitle : Strings.Current.DeletePermanentTitle;
        Title = title;
        TitleText.Text = title;
        var what = Strings.Current.ItemsWhat(paths.Count, NameOf(paths.FirstOrDefault() ?? ""));
        MessageText.Text = toTrash ? Strings.Current.DeleteTrashPrompt(what) : Strings.Current.DeletePermanentPrompt(what);

        foreach (var path in paths.Take(ShownNames))
            Names.Children.Add(new TextBlock { Text = "· " + NameOf(path), Classes = { "small" }, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis });
        if (paths.Count > ShownNames)
            Names.Children.Add(new TextBlock { Text = Strings.Current.DeleteMore(paths.Count - ShownNames), Classes = { "small" } });

        CancelButton.Content = Strings.Current.CommonCancel;
        OkButton.Content = toTrash ? Strings.Current.DeleteTrashAction : Strings.Current.DeletePermanentAction;
        CancelButton.Click += (_, _) => Close();
        OkButton.Click += (_, _) => Confirmed?.Invoke();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        };
        // Enter로 바로 확정된다. 영구 삭제는 확인 버튼을 위험색으로 칠해 구분한다 (Shift+Delete를 눌러 연 것 자체가 의도).
        if (!toTrash) OkButton.Classes.Add("danger");
        Opened += (_, _) => Dispatcher.UIThread.Post(() => OkButton.Focus());
    }

    public event Action? Confirmed;

    static string NameOf(string path) =>
        Path.GetFileName(path.TrimEnd('\\', '/')) is { Length: > 0 } n ? n : path;
}
