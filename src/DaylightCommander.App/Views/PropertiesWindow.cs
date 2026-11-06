using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.App.Views;

/// <summary>
/// 속성 창 (F-바의 속성). 요청마다 <b>독립 창</b> 하나다 — 여러 항목의 속성을 나란히 열어 비교할 수 있다.
/// 항목 하나면 이름·위치·종류·크기·날짜·특성을, 여러 개면 개수와 합계를 보여 준다. 폴더 크기는 안쪽까지 훑어야 해서
/// 창을 먼저 열고 "계산 중…"을 보이다가 값이 차는 대로 갱신한다 (창을 닫으면 계산도 멈춘다).
/// </summary>
public sealed class PropertiesWindow : Window
{
    public static void Open(IReadOnlyList<string> paths, Window origin)
    {
        PropertiesWindow window;
        try
        {
            window = new PropertiesWindow(paths);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return; // 방금 사라진 항목 등: 열 속성이 없다
        }
        WindowPlacementHelper.OpenNear(origin, window);
    }

    readonly IReadOnlyList<string> _paths;
    readonly TextBlock _size = new() { TextWrapping = TextWrapping.Wrap };
    readonly TextBlock _contains = new();
    readonly CancellationTokenSource _cts = new();
    readonly PropertiesInfo? _single;

    PropertiesWindow(IReadOnlyList<string> paths)
    {
        var s = Strings.Current;
        _paths = paths;
        _single = paths.Count == 1 ? FileProperties.Read(paths[0]) : null;
        var name = _single?.Name ?? s.PropMultiName(paths.Count);
        Title = s.PropTitle(name);
        SizeToContent = SizeToContent.Height;
        Width = 480;
        MinWidth = 380;
        CanResize = true;

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 16, RowSpacing = 8 };
        var row = 0;
        void Add(string label, Control value)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Top };
            l.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("AppTextMid"));
            Grid.SetRow(l, row);
            Grid.SetRow(value, row);
            Grid.SetColumn(value, 1);
            grid.Children.Add(l);
            grid.Children.Add(value);
            row++;
        }
        TextBlock Text(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };
        SelectableTextBlock Selectable(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };

        if (_single is { } p)
        {
            Add(s.PropName, Selectable(p.Name));
            Add(s.PropLocation, Selectable(p.Parent));
            Add(s.PropKind, Text(p.IsDirectory ? s.PropKindFolder : KindOfFile(p.Name)));
            if (p.LinkTarget is not null) Add(s.PropLinkTarget, Selectable(p.LinkTarget));
            Add(s.PropSize, _size);
            if (p.IsDirectory) Add(s.PropContains, _contains);
            Add(s.PropCreated, Text(Format(p.Created)));
            Add(s.PropModified, Text(Format(p.Modified)));
            Add(s.PropAccessed, Text(Format(p.Accessed)));
            Add(s.PropAttributes, Text(Attributes(p.Attributes)));
            if (!p.IsDirectory)
                _size.Text = SizeText(p.FileSize ?? 0);
            else
                _size.Text = s.PropCalculating;
        }
        else
        {
            Add(s.PropName, Text(name));
            Add(s.PropKind, Text(s.PropKindMixed));
            Add(s.PropSize, _size);
            Add(s.PropContains, _contains);
            _size.Text = s.PropCalculating;
        }

        var close = new Button { Content = s.AboutClose, HorizontalAlignment = HorizontalAlignment.Right, Classes = { "accent" }, Margin = new Thickness(0, 12, 0, 0) };
        close.Click += (_, _) => Close();
        Content = new StackPanel { Margin = new Thickness(24), Spacing = 8, Children = { grid, close } };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        };
        Closed += (_, _) =>
        {
            _cts.Cancel();
            _cts.Dispose();
        };
        Opened += async (_, _) => await MeasureAsync();
    }

    async Task MeasureAsync()
    {
        // 개별 파일뿐이라 이미 크기를 아는 경우에도 합계가 필요한 곳(폴더·여러 항목)만 훑는다.
        if (_single is { IsDirectory: false }) return;
        try
        {
            var progress = new Progress<PropertiesTotals>(Show);
            var totals = await FileProperties.MeasureAsync(_paths, progress, _cts.Token);
            Dispatcher.UIThread.Post(() => Show(totals));
        }
        catch (OperationCanceledException)
        {
            // 창이 닫혔다
        }
    }

    void Show(PropertiesTotals t)
    {
        var s = Strings.Current;
        _size.Text = SizeText(t.Bytes) + (t.Complete ? "" : "\n" + s.PropIncomplete);
        _contains.Text = s.PropContainsValue(t.Files, t.Folders);
    }

    static string SizeText(long bytes) => Strings.Current.PropSizeValue(BytesFormat.Format(bytes), bytes);

    static string Format(DateTime? time) =>
        time?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "--";

    static string KindOfFile(string fileName)
    {
        var ext = FileKinds.ExtensionOf(fileName);
        return ext.Length == 0 ? Strings.Current.PropKindFile : Strings.Current.PropKindFileExt(ext.ToUpperInvariant());
    }

    static string Attributes(FileAttributes a)
    {
        var s = Strings.Current;
        var parts = new List<string>();
        if (a.HasFlag(FileAttributes.ReadOnly)) parts.Add(s.PropReadOnly);
        if (a.HasFlag(FileAttributes.Hidden)) parts.Add(s.PropHidden);
        if (a.HasFlag(FileAttributes.System)) parts.Add(s.PropSystem);
        return parts.Count == 0 ? "--" : string.Join(", ", parts);
    }
}
