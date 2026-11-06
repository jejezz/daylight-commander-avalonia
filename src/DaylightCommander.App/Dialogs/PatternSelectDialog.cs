using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;

namespace DaylightCommander.App.Dialogs;

/// <summary>
/// 패턴으로 선택 (원본 <c>showPatternSelectDialog</c>): <c>*</c>·<c>?</c> 패턴을 쓰고 "선택 추가" 또는 "선택 해제"를 고른다.
/// Enter는 선택 추가, Esc는 닫기. 처음 글은 <c>*.</c>이고 끝에 커서가 있다.
/// </summary>
public sealed class PatternSelectDialog : DialogBase
{
    public PatternSelectDialog()
    {
        var s = Strings.Current;
        Title = s.PatternSelectTitle;
        MinWidth = 440;

        var heading = new TextBlock { Text = s.PatternSelectTitle, FontWeight = Avalonia.Media.FontWeight.Bold };
        heading.Bind(TextBlock.FontSizeProperty, this.GetResourceObservable("FsTitle"));
        var input = new TextBox { Text = "*.", PlaceholderText = s.PatternHint };

        var cancel = new Button { Content = s.CommonCancel };
        var deselect = new Button { Content = s.PatternDeselect };
        var add = new Button { Content = s.PatternAdd, Classes = { "accent" } };
        cancel.Click += (_, _) => Close();
        deselect.Click += (_, _) => Confirmed?.Invoke(input.Text ?? "", false);
        add.Click += (_, _) => Confirmed?.Invoke(input.Text ?? "", true);
        input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { Confirmed?.Invoke(input.Text ?? "", true); e.Handled = true; }
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 4, 0, 0), Children = { cancel, deselect, add } };
        Content = new StackPanel { Margin = new Thickness(24), Spacing = 12, Children = { heading, input, buttons } };
        Opened += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            input.Focus();
            input.SelectionStart = input.SelectionEnd = (input.Text ?? "").Length;
        });
    }

    /// <summary>패턴과 함께, 참이면 선택 추가·거짓이면 선택 해제.</summary>
    public event Action<string, bool>? Confirmed;
}
