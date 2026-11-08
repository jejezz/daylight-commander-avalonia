using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.App.Views;

/// <summary>
/// 뷰어 (F3). 파일 하나당 <b>독립 창</b> 하나다 (창 정책: 이 앱을 Avalonia로 옮기는 이유가 멀티 윈도우). 여러 파일을
/// 동시에 열어 두고 탐색기와 나란히 볼 수 있다. 텍스트·이미지·Hex를 보여 주고, 아직 지원하지 않는 형식(PDF·오디오·비디오)이나
/// 바이너리는 안내와 "기본 앱으로 열기" 버튼을 보여 준다. Esc로 닫는다.
/// <para>
/// 텍스트는 UTF-8(실패하면 Latin-1)로 읽고 4MB까지만 보인다. 이미지는 휠로 확대·축소(0.1~8배), 끌어서 이동, 더블클릭으로
/// 창 크기에 맞춘다. 맞춤 상태에서는 창 크기를 바꾸면 그림도 따라 맞춰진다. Hex는 앞 256KB만 보인다. 문구는 창을 열 때의 언어로 정해진다.
/// </para>
/// </summary>
public sealed class ViewerWindow : Window
{
    /// <summary>뷰어 글꼴 (fonts.md §4): 고정폭. 없는 글꼴은 다음 것으로 넘어간다.</summary>
    static readonly FontFamily Mono = new("Cascadia Mono, Consolas, Menlo, Monaco, DejaVu Sans Mono, monospace");

    public static void Open(string path, Window origin) =>
        WindowPlacementHelper.OpenNear(origin, new ViewerWindow(path));

    readonly string _path;
    readonly ViewerKind _kind;
    readonly Border _body = new();
    readonly ToggleButton _hexToggle = new() { Classes = { "tool" } };
    bool _hex;
    readonly StackPanel _zoomTools = new() { Orientation = Orientation.Horizontal, Spacing = 2, IsVisible = false };
    readonly TextBlock _zoomLabel = new() { VerticalAlignment = VerticalAlignment.Center, MinWidth = 48, TextAlignment = TextAlignment.Center };
    Action<double>? _zoomBy;
    Action? _zoomFit;
    int _generation; // 모드를 빠르게 바꿀 때 늦게 끝난 읽기가 화면을 덮지 않게 한다

    ViewerWindow(string path)
    {
        _path = path;
        var name = Path.GetFileName(path);
        _kind = FileKinds.Classify(name);
        Title = name;
        Width = 900;
        Height = 650;
        MinWidth = 420;
        MinHeight = 300;

        var bar = UiScale.Enable(new Border { Padding = new Thickness(16, 0, 8, 0), Height = 44, BorderThickness = new Thickness(0, 0, 0, 1) });
        bar.Bind(Border.BackgroundProperty, this.GetResourceObservable("AppSurface"));
        bar.Bind(Border.BorderBrushProperty, this.GetResourceObservable("AppStroke"));
        var title = new TextBlock
        {
            Text = name,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        title.Bind(TextBlock.FontSizeProperty, this.GetResourceObservable("FsTitle"));
        var icon = new Icons.DuoIcon { Icon = "memory" };
        _hexToggle.Content = icon;
        _hexToggle.Click += (_, _) => SetHex(_hexToggle.IsChecked == true);
        var openButton = new Button { Classes = { "tool" }, Content = new Icons.DuoIcon { Icon = "open-external" }, IsVisible = _kind is ViewerKind.Pdf or ViewerKind.Video or ViewerKind.Audio };
        ToolTip.SetTip(openButton, Strings.Current.ViewerOpenWithDefaultApp);
        openButton.Click += (_, _) =>
        {
            try { new OpenWithDefaultApp().Open(_path); }
            catch (FileOperationException e) { _body.Child = Note(Strings.Current.FileErrorText(e.Error)); }
        };
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { _zoomTools, openButton, _hexToggle } };
        var barGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(tools, 1);
        barGrid.Children.Add(title);
        barGrid.Children.Add(tools);
        bar.Child = barGrid;

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        Grid.SetRow(_body, 1);
        root.Children.Add(bar);
        root.Children.Add(_body);
        Content = root;

        BuildZoomTools();
        UpdateToggleText();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        };
        Opened += async (_, _) => await ShowAsync();
    }

    void BuildZoomTools()
    {
        var s = Strings.Current;
        var minus = new Button { Classes = { "tool" }, Content = "−" };
        var plus = new Button { Classes = { "tool" }, Content = "+" };
        var fit = new Button { Classes = { "tool" }, Content = _zoomLabel };
        ToolTip.SetTip(minus, s.ViewerZoomOut);
        ToolTip.SetTip(plus, s.ViewerZoomIn);
        ToolTip.SetTip(fit, s.ViewerZoomFit);
        minus.Click += (_, _) => _zoomBy?.Invoke(1 / 1.25);
        plus.Click += (_, _) => _zoomBy?.Invoke(1.25);
        fit.Click += (_, _) => _zoomFit?.Invoke();
        _zoomTools.Children.Add(minus);
        _zoomTools.Children.Add(fit);
        _zoomTools.Children.Add(plus);
    }

    void SetHex(bool hex)
    {
        _hex = hex;
        UpdateToggleText();
        _ = ShowAsync();
    }

    void UpdateToggleText()
    {
        var s = Strings.Current;
        ToolTip.SetTip(_hexToggle, _hex ? s.ViewerNormalView : s.ViewerHexView);
        _hexToggle.IsChecked = _hex;
    }

    // --- 본문 ---

    static TextBlock Note(string text)
    {
        var block = new TextBlock { Text = text, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        block.Bind(TextBlock.ForegroundProperty, Application.Current!.GetResourceObservable("AppTextMid"));
        return block;
    }

    async Task ShowAsync()
    {
        var generation = ++_generation;
        _zoomTools.IsVisible = false; // 이미지를 보여 줄 때만 BuildImage가 켠다
        _body.Child = Note(Strings.Current.PaneLoading);
        try
        {
            Control view;
            if (_hex) view = await BuildHexAsync();
            else
            {
                var kind = _kind;
                if (kind == ViewerKind.Unknown)
                    kind = await FileViewing.SniffTextAsync(_path) ? ViewerKind.Text : ViewerKind.Unknown;
                view = kind switch
                {
                    ViewerKind.Image => BuildImage(),
                    ViewerKind.Text => await BuildTextAsync(),
                    ViewerKind.Pdf or ViewerKind.Video or ViewerKind.Audio => BuildWeb(),
                    _ => BuildUnsupported(),
                };
            }
            if (generation == _generation) _body.Child = view;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            if (generation == _generation) _body.Child = Note(Strings.Current.ViewerLoadError(e.Message));
        }
    }

    async Task<Control> BuildTextAsync()
    {
        var preview = await FileViewing.ReadTextAsync(_path);
        var box = new TextBox
        {
            Text = preview.Text,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = Mono,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Padding = new Thickness(16),
        };
        box.Bind(TextBox.FontSizeProperty, this.GetResourceObservable("FsBody"));
        ScrollViewer.SetHorizontalScrollBarVisibility(box, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(box, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        return WithTruncationNote(box, preview.Truncated ? Strings.Current.ViewerTextTruncatedNote(FileViewing.MaxTextBytes / 1024) : null);
    }

    async Task<Control> BuildHexAsync()
    {
        var preview = await FileViewing.ReadHexAsync(_path);
        var rows = new List<string>(FileViewing.HexRowCount(preview.Bytes));
        for (var i = 0; i < preview.Bytes.Length; i += 16) rows.Add(FileViewing.HexRow(preview.Bytes, i));
        var list = new ListBox
        {
            ItemsSource = rows,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8),
            ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((row, _) =>
            {
                var text = new TextBlock { Text = row ?? "", FontFamily = Mono };
                text.Bind(TextBlock.FontSizeProperty, Application.Current!.GetResourceObservable("FsMid"));
                return text;
            }),
        };
        return WithTruncationNote(list, preview.Truncated ? Strings.Current.ViewerHexTruncatedNote(FileViewing.MaxHexBytes / 1024) : null);
    }

    static Control WithTruncationNote(Control content, string? note)
    {
        if (note is null) return content;
        var bar = new TextBlock { Text = note, Margin = new Thickness(16, 8) };
        bar.Bind(TextBlock.ForegroundProperty, Application.Current!.GetResourceObservable("AppTextMid"));
        bar.Bind(TextBlock.FontSizeProperty, Application.Current!.GetResourceObservable("FsSmall"));
        var panel = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        panel.Children.Add(bar);
        panel.Children.Add(content);
        return panel;
    }

    /// <summary>
    /// PDF·비디오·오디오는 운영체제의 웹뷰(Windows: WebView2)에 파일을 그대로 열어 보여 준다. 별도 재생 라이브러리를 싣지 않으므로
    /// 앱이 커지지 않고, 웹뷰가 아는 형식(PDF, mp4·webm·mkv, mp3·wav·flac·aac·m4a·ogg 등)만 재생된다. 안 되는 형식은 위쪽의
    /// "기본 앱으로 열기" 단추로 넘긴다. 창을 닫으면 웹뷰도 같이 치워져 소리가 멈춘다.
    /// </summary>
    Control BuildWeb()
    {
        var web = new NativeWebView();
        // WebView2는 사용자 데이터 폴더를 기본으로 실행 파일 옆에 만든다. 설치본(C:\Program Files)은 쓸 수 없어 앱이 죽으므로
        // 사용자별 쓰기 가능한 위치로 옮긴다. 초기화 전에(Source 설정 전에) 지정해야 한다.
        web.EnvironmentRequested += (_, e) =>
        {
            if (e is Avalonia.Platform.WindowsWebView2EnvironmentRequestedEventArgs w2)
                w2.UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DaylightCommander", "WebView2");
        };
        web.Source = new Uri(_path);
        Closed += (_, _) => _body.Child = null;
        return web;
    }

    Control BuildUnsupported()
    {
        var s = Strings.Current;
        var open = new Button { Classes = { "accent" }, Content = s.ViewerOpenWithDefaultApp, HorizontalAlignment = HorizontalAlignment.Center };
        open.Click += (_, _) =>
        {
            try { new OpenWithDefaultApp().Open(_path); }
            catch (FileOperationException e) { _body.Child = Note(s.FileErrorText(e.Error)); }
        };
        return new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { Note(s.ViewerUnsupported), open },
        };
    }

    // --- 이미지: 휠 확대·축소, 끌어서 이동, 더블클릭으로 창에 맞춤 ---

    Control BuildImage()
    {
        Bitmap bitmap;
        try
        {
            bitmap = new Bitmap(_path);
        }
        catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return Note(Strings.Current.ViewerImageLoadError(e.Message));
        }

        var image = new Image
        {
            Source = bitmap,
            Width = bitmap.Size.Width,
            Height = bitmap.Size.Height,
            Stretch = Stretch.Fill,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            RenderTransformOrigin = RelativePoint.TopLeft,
        };
        var surface = new Border { ClipToBounds = true, Background = Brushes.Transparent, Child = image };
        // 사용법 안내: 그림 위에 겹치지 않도록 그림 아래 별도 줄에 항상 둔다 (도구줄의 확대/축소 단추와 함께).
        var hint = new TextBlock
        {
            Text = Strings.Current.ViewerImageHint,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
        };
        hint.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("AppTextMid"));
        hint.Bind(TextBlock.FontSizeProperty, this.GetResourceObservable("FsSmall"));
        var hintBar = new Border { Padding = new Thickness(16, 6), BorderThickness = new Thickness(0, 1, 0, 0), Child = hint };
        hintBar.Bind(Border.BackgroundProperty, this.GetResourceObservable("AppSurface"));
        hintBar.Bind(Border.BorderBrushProperty, this.GetResourceObservable("AppStroke"));
        Grid.SetRow(hintBar, 1);
        var host = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Children = { surface, hintBar } };

        double scale = 1, offsetX = 0, offsetY = 0;
        const double MinScale = 0.1, MaxScale = 8;
        var fitMode = true;
        var lastSize = new Size();
        void Apply()
        {
            image.RenderTransform = new MatrixTransform(new Matrix(scale, 0, 0, scale, offsetX, offsetY));
            _zoomLabel.Text = $"{Math.Round(scale * 100)}%";
        }
        void Fit()
        {
            var viewport = surface.Bounds.Size;
            if (viewport.Width <= 0 || viewport.Height <= 0) return;
            // 창보다 큰 그림은 창에 맞추고, 작은 그림은 원래 크기로 가운데에 둔다.
            scale = Math.Clamp(Math.Min(1, Math.Min(viewport.Width / image.Width, viewport.Height / image.Height)), MinScale, MaxScale);
            offsetX = (viewport.Width - image.Width * scale) / 2;
            offsetY = (viewport.Height - image.Height * scale) / 2;
            fitMode = true;
            Apply();
        }
        // 마우스 아래(또는 지정한 점) 위치가 제자리에 있도록 이동량도 같이 바꾼다.
        void ZoomAt(Point p, double factor)
        {
            fitMode = false;
            var next = Math.Clamp(scale * factor, MinScale, MaxScale);
            offsetX = p.X - (p.X - offsetX) * next / scale;
            offsetY = p.Y - (p.Y - offsetY) * next / scale;
            scale = next;
            Apply();
        }
        _zoomBy = f => ZoomAt(new Point(surface.Bounds.Width / 2, surface.Bounds.Height / 2), f);
        _zoomFit = Fit;
        _zoomTools.IsVisible = true;

        // 맞춤 모드: 창 크기를 바꾸면 그림도 따라서 다시 맞춘다. 휠·끌기로 사용자가 배율·위치를 정하면 맞춤 모드가 풀리고,
        // 이후 창 크기가 바뀌어도 배율은 유지한 채 보던 가운데 지점이 가운데에 남도록 위치만 옮긴다. 더블클릭하면 다시 맞춤 모드.
        surface.SizeChanged += (_, e) =>
        {
            if (fitMode) Fit();
            else
            {
                offsetX += (e.NewSize.Width - lastSize.Width) / 2;
                offsetY += (e.NewSize.Height - lastSize.Height) / 2;
                Apply();
            }
            lastSize = e.NewSize;
        };
        // 마우스 휠·트랙패드 두 손가락 스크롤 (둘 다 휠 이벤트로 온다)
        surface.PointerWheelChanged += (_, e) =>
        {
            ZoomAt(e.GetPosition(surface), Math.Pow(1.1, e.Delta.Y));
            e.Handled = true;
        };
        // 트랙패드 핀치(macOS). 이벤트마다 이번 변화분의 배율이 온다 (터치스크린 핀치용 인식기는 달지 않았다).
        surface.AddHandler(InputElement.PinchEvent, (object? _, PinchEventArgs e) =>
        {
            ZoomAt(e.ScaleOrigin, e.Scale);
            e.Handled = true;
        });
        Point? drag = null;
        surface.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(surface).Properties.IsLeftButtonPressed) return;
            if (e.ClickCount == 2) { Fit(); return; }
            drag = e.GetPosition(surface);
            e.Pointer.Capture(surface);
        };
        surface.PointerMoved += (_, e) =>
        {
            if (drag is not { } start) return;
            fitMode = false;
            var now = e.GetPosition(surface);
            offsetX += now.X - start.X;
            offsetY += now.Y - start.Y;
            drag = now;
            Apply();
        };
        surface.PointerReleased += (_, e) =>
        {
            drag = null;
            e.Pointer.Capture(null);
        };
        return host;
    }
}
