using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using DaylightCommander.Domain.Settings;
using DaylightCommander.Services;

namespace DaylightCommander.App.Dialogs;

/// <summary>
/// 서버 연결 대화상자 (SFTP): 서버 주소·포트·사용자 이름·비밀번호. 비밀번호는 저장하지 않고, "이 서버를 저장"을 켜면 주소·포트·사용자 이름만
/// 저장해 다음에 한 번에 고를 수 있다. 연결하는 동안 입력을 잠그고, 실패하면 열어 둔 채 오류를 보여 준다 (입력은 그대로).
/// </summary>
public sealed class ConnectDialog : DialogBase
{
    readonly TextBox _host = new();
    readonly TextBox _port = new() { Width = 90 };
    readonly TextBox _user = new();
    readonly TextBox _password = new() { PasswordChar = '•' };
    readonly CheckBox _save = new();
    readonly TextBlock _error = new() { IsVisible = false, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    readonly Button _connect = new() { Classes = { "accent" } };
    readonly Button _cancel = new();

    public ConnectDialog(NetworkProfile? profile)
    {
        var s = Strings.Current;
        Title = s.ConnectTitle;
        MinWidth = 460;

        var heading = new TextBlock { Text = s.ConnectTitle, FontWeight = Avalonia.Media.FontWeight.Bold };
        heading.Bind(TextBlock.FontSizeProperty, this.GetResourceObservable("FsTitle"));
        _error.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("AppDanger"));
        _error.Bind(TextBlock.FontSizeProperty, this.GetResourceObservable("FsSmall"));

        _host.PlaceholderText = s.ConnectHostHint;
        _host.Text = profile?.Host ?? "";
        _port.Text = (profile is { Port: > 0 } ? profile.Port : 22).ToString();
        _user.Text = profile?.User ?? "";
        _save.Content = s.ConnectSave;
        _save.IsChecked = profile is not null;
        _cancel.Content = s.CommonCancel;
        _connect.Content = s.ConnectButton;

        Control Labeled(string label, Control field)
        {
            var text = new TextBlock { Text = label };
            text.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("AppTextMid"));
            text.Bind(TextBlock.FontSizeProperty, this.GetResourceObservable("FsSmall"));
            return new StackPanel { Spacing = 4, Children = { text, field } };
        }

        var hostRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        var hostField = Labeled(s.ConnectHost, _host);
        var portField = Labeled(s.ConnectPort, _port);
        Grid.SetColumn(portField, 1);
        hostRow.Children.Add(hostField);
        hostRow.Children.Add(portField);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 4, 0, 0), Children = { _cancel, _connect } };
        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 12,
            Children = { heading, hostRow, Labeled(s.ConnectUser, _user), Labeled(s.ConnectPassword, _password), _save, _error, buttons },
        };

        _cancel.Click += (_, _) => Close();
        _connect.Click += (_, _) => Submit();
        foreach (var box in new[] { _host, _port, _user, _password })
            box.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter) { Submit(); e.Handled = true; }
            };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && _connect.IsEnabled) { Close(); e.Handled = true; }
        };
        Opened += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            // 값이 채워진 곳은 건너뛰고 비어 있는 첫 칸에 초점을 둔다 (저장한 서버면 비밀번호부터).
            var first = string.IsNullOrEmpty(_host.Text) ? _host : string.IsNullOrEmpty(_user.Text) ? _user : _password;
            first.Focus();
        });
    }

    /// <summary>확정: 서버 주소, 포트, 사용자 이름, 비밀번호, 서버 저장 여부.</summary>
    public event Action<string, int, string, string, bool>? Confirmed;

    void Submit()
    {
        if (!_connect.IsEnabled) return;
        var port = int.TryParse(_port.Text, out var p) && p is > 0 and <= 65535 ? p : 22;
        Confirmed?.Invoke(_host.Text ?? "", port, _user.Text ?? "", _password.Text ?? "", _save.IsChecked == true);
    }

    /// <summary>홈 상태가 알려 준 연결 진행 상황을 화면에 반영한다.</summary>
    public void Update(HomeDialog.Connect state)
    {
        var s = Strings.Current;
        foreach (Control c in new Control[] { _host, _port, _user, _password, _save, _cancel }) c.IsEnabled = !state.Busy;
        _connect.IsEnabled = !state.Busy;
        _connect.Content = state.Busy ? s.ConnectBusy : s.ConnectButton;
        _error.IsVisible = state.Error is not null;
        _error.Text = state.Error is null ? "" : s.FileErrorText(state.Error);
        if (!state.Busy && state.Error is not null) _password.Focus(); // 실패하면 비밀번호를 다시 쓰게 한다
    }
}
