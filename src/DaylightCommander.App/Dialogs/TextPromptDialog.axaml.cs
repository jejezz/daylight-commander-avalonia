using Avalonia.Input;
using Avalonia.Threading;

namespace DaylightCommander.App.Dialogs;

/// <summary>
/// 한 줄 입력 대화상자: 이름 변경, 새 폴더, 새 파일, 복사·이동 대상 폴더. Enter로 확정하고 Esc로 닫는다.
/// 확정이 실패하면 열어 둔 채 <see cref="SetError"/>로 오류를 보여 준다 (입력한 글은 그대로).
/// </summary>
public partial class TextPromptDialog : DialogBase
{
    public TextPromptDialog(string title, string? prompt, string text, int selectStart, int selectEnd, string confirmLabel)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        if (prompt is not null)
        {
            PromptText.Text = prompt;
            PromptText.IsVisible = true;
        }
        Input.Text = text;
        CancelButton.Content = Strings.Current.CommonCancel;
        OkButton.Content = confirmLabel;

        CancelButton.Click += (_, _) => Close();
        OkButton.Click += (_, _) => Submit();
        Input.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter) { Submit(); e.Handled = true; }
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        };
        Opened += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            Input.Focus();
            Input.SelectionStart = selectStart;
            Input.SelectionEnd = selectEnd;
        });
    }

    /// <summary>확정 (Enter 또는 확인 버튼). 값은 입력한 글.</summary>
    public event Action<string>? Confirmed;

    void Submit() => Confirmed?.Invoke(Input.Text ?? "");

    public void SetError(string? message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = !string.IsNullOrEmpty(message);
        Input.Focus();
    }
}
