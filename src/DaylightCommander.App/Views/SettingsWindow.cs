using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DaylightCommander.Domain;
using DaylightCommander.Domain.Settings;
using DaylightCommander.Domain.Theme;
using SettingsFontSize = DaylightCommander.Domain.Settings.FontSize;

namespace DaylightCommander.App.Views;

/// <summary>
/// 설정 창. 독립 윈도우다 (창 정책: 이 앱을 Avalonia로 옮기는 이유가 멀티 윈도우이고, 설정은 다른 창과 같이 띄워 두고
/// 바로 결과를 보는 편이 낫다). 앱에 하나만 열린다 — 이미 열려 있으면 앞으로 가져온다.
/// <para>
/// 구성은 규약(settings-screen.md)을 따른다: 왼쪽 구역 목록(좁으면 위쪽 탭) + 오른쪽 내용.
/// "외형" 구역은 모드 → 색 구성 → 글꼴 → 글자 크기 → 언어, "편집기" 구역은 F4로 여는 텍스트 편집기.
/// 바꾸는 즉시 적용되고(저장은 모아서) 열려 있는 모든 창이 같이 바뀐다. 문구가 언어마다 달라서 내용은 코드로 만들고,
/// 언어가 바뀌면 처음부터 다시 만든다.
/// </para>
/// </summary>
public sealed class SettingsWindow : Window
{
    const double WideBreakpoint = 700; // 이 폭 이상이면 왼쪽 구역 목록, 미만이면 위쪽 탭
    const double ControlHeight = 36;

    static SettingsWindow? _instance;

    /// <summary>설정 창을 연다. 이미 열려 있으면 앞으로 가져온다. <paramref name="origin"/>이 있는 모니터 가운데에 뜬다.</summary>
    public static void Open(Window origin)
    {
        if (_instance is { } open)
        {
            if (open.WindowState == WindowState.Minimized) open.WindowState = WindowState.Normal;
            open.Activate();
            return;
        }
        var window = new SettingsWindow();
        var screen = origin.Screens.ScreenFromWindow(origin) ?? origin.Screens.Primary;
        if (screen is not null)
        {
            var work = screen.WorkingArea;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Position = new PixelPoint(
                work.X + (int)((work.Width - window.Width * screen.Scaling) / 2),
                work.Y + (int)((work.Height - window.Height * screen.Scaling) / 2));
        }
        _instance = window;
        window.Show();
    }

    enum Section { Appearance, Editor }

    readonly Grid _root = new();
    readonly StackPanel _nav = new() { Spacing = 4 };
    readonly ScrollViewer _scroll = new() { VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
    readonly Border _divider = new() { Width = 1 };
    readonly List<Action> _refreshers = [];
    Section _section = Section.Appearance;
    bool _wide = true;

    SettingsWindow()
    {
        Width = 860;
        Height = 620;
        MinWidth = 420;
        MinHeight = 380;
        Title = Strings.Current.SettingsWindowTitle(AppIdentity.DisplayName);

        _divider.Bind(Border.BackgroundProperty, this.GetResourceObservable("AppStroke"));
        _root.Children.Add(_nav);
        _root.Children.Add(_divider);
        _root.Children.Add(_scroll);
        Content = _root;

        Rebuild();
        ApplyLayout(Width);

        // 창 크기에 따라 구역 목록 위치를 바꾼다.
        PropertyChanged += (_, e) =>
        {
            if (e.Property == ClientSizeProperty) ApplyLayout(ClientSize.Width);
        };
        Strings.Changed += OnLanguageChanged;
        App.Settings.Changed += OnSettingsChanged;
        Closed += (_, _) =>
        {
            Strings.Changed -= OnLanguageChanged;
            App.Settings.Changed -= OnSettingsChanged;
            _instance = null;
        };
    }

    void OnLanguageChanged() => Dispatcher.UIThread.Post(() =>
    {
        Title = Strings.Current.SettingsWindowTitle(AppIdentity.DisplayName);
        Rebuild();
    });

    void OnSettingsChanged(AppSettings _) => Dispatcher.UIThread.Post(() =>
    {
        foreach (var refresh in _refreshers) refresh();
    });

    // --- 배치 ---

    void ApplyLayout(double width)
    {
        _wide = width >= WideBreakpoint;
        _root.ColumnDefinitions.Clear();
        _root.RowDefinitions.Clear();
        if (_wide)
        {
            _root.ColumnDefinitions.Add(new ColumnDefinition(200, GridUnitType.Pixel));
            _root.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Pixel));
            _root.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            Grid.SetColumn(_nav, 0); Grid.SetRow(_nav, 0); Grid.SetColumnSpan(_nav, 1);
            Grid.SetColumn(_divider, 1); Grid.SetRow(_divider, 0); Grid.SetColumnSpan(_divider, 1);
            Grid.SetColumn(_scroll, 2); Grid.SetRow(_scroll, 0); Grid.SetColumnSpan(_scroll, 1);
            _divider.Width = 1; _divider.Height = double.NaN;
            _nav.Orientation = Orientation.Vertical;
            _nav.Margin = new Thickness(16, 24, 16, 16);
        }
        else
        {
            _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _root.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Pixel));
            _root.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            Grid.SetColumn(_nav, 0); Grid.SetRow(_nav, 0); Grid.SetColumnSpan(_nav, 1);
            Grid.SetColumn(_divider, 0); Grid.SetRow(_divider, 1); Grid.SetColumnSpan(_divider, 1);
            Grid.SetColumn(_scroll, 0); Grid.SetRow(_scroll, 2); Grid.SetColumnSpan(_scroll, 1);
            _divider.Width = double.NaN; _divider.Height = 1;
            _nav.Orientation = Orientation.Horizontal;
            _nav.Margin = new Thickness(16, 12, 16, 8);
        }
        _scroll.Padding = _wide ? new Thickness(40, 32, 40, 32) : new Thickness(20, 16, 20, 24);
    }

    // --- 내용 ---

    void Rebuild()
    {
        _refreshers.Clear();
        _nav.Children.Clear();
        var s = Strings.Current;
        AddNavItem(Section.Appearance, s.SettingsSectionAppearance, "IconPalette");
        AddNavItem(Section.Editor, s.SettingsSectionEditor, "IconEdit");

        var page = new StackPanel { Spacing = 8, MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left };
        if (_section == Section.Appearance) BuildAppearance(page);
        else BuildEditor(page);
        _scroll.Content = page;
        OnSettingsChanged(App.Settings.Value);
    }

    void AddNavItem(Section section, string label, string iconKey)
    {
        var button = new ToggleButton
        {
            Classes = { "nav" },
            IsChecked = _section == section,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };
        var icon = new PathIcon { Width = 20, Height = 20 };
        icon.Bind(PathIcon.DataProperty, this.GetResourceObservable(iconKey));
        button.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children = { icon, new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center } },
        };
        button.Click += (_, _) =>
        {
            _section = section;
            Rebuild();
        };
        _nav.Children.Add(button);
    }

    static TextBlock Heading(string text, bool first = false)
    {
        var block = new TextBlock { Text = text, FontWeight = FontWeight.Bold, Margin = new Thickness(0, first ? 0 : 20, 0, 4) };
        block.Bind(TextBlock.FontSizeProperty, Application.Current!.GetResourceObservable("FsMid"));
        return block;
    }

    void BuildAppearance(StackPanel page)
    {
        var s = Strings.Current;

        page.Children.Add(Heading(s.SettingsMode, first: true));
        page.Children.Add(Segmented(
            [(s.SettingsModeSystem, ThemeMode.System), (s.ThemeLight, ThemeMode.Light), (s.ThemeDark, ThemeMode.Dark)],
            () => App.Settings.Value.Theme,
            v => App.Settings.Update(x => x with { Theme = v })));

        page.Children.Add(Heading(s.SettingsPalette));
        page.Children.Add(PaletteChips());

        page.Children.Add(Heading(s.SettingsFont));
        page.Children.Add(FontPicker());

        page.Children.Add(Heading(s.SettingsFontSize));
        page.Children.Add(Segmented(
            [(s.FontSizeSmall, SettingsFontSize.Small), (s.FontSizeNormal, SettingsFontSize.Normal), (s.FontSizeLarge, SettingsFontSize.Large)],
            () => App.Settings.Value.FontSize,
            v => App.Settings.Update(x => x.WithFontSize(v))));

        page.Children.Add(Heading(s.SettingsLanguage));
        page.Children.Add(Segmented<AppLocale?>(
            [(s.LanguageSystemShort, null), ("한국어", AppLocale.Korean), ("English", AppLocale.English)],
            () => App.Settings.Value.Locale,
            v => App.Settings.Update(x => x with { Locale = v })));
    }

    /// <summary>
    /// 여러 선택지 중 하나를 고르는 가로 버튼 묶음 (Flutter의 SegmentedButton): 바깥이 알약 모양으로 둥글고, 칸 사이에는 가는 구분선,
    /// 고른 칸은 주색으로 칠하고 체크 표시가 붙는다.
    /// </summary>
    Control Segmented<T>((string Label, T Value)[] options, Func<T> current, Action<T> pick)
    {
        var buttons = new List<(ToggleButton Button, PathIcon Check, T Value)>();
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        for (var i = 0; i < options.Length; i++)
        {
            var (label, value) = options[i];
            if (i > 0)
            {
                var divider = new Border { Width = 1 };
                divider.Bind(Border.BackgroundProperty, this.GetResourceObservable("AppStrokeStrong"));
                row.Children.Add(divider);
            }
            var check = new PathIcon { Width = 16, Height = 16, IsVisible = false };
            check.Bind(PathIcon.DataProperty, this.GetResourceObservable("IconCheck"));
            var button = new ToggleButton
            {
                Classes = { "seg" },
                Height = ControlHeight - 2,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children = { check, new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center } },
                },
            };
            button.Click += (_, _) => pick(value);
            buttons.Add((button, check, value));
            row.Children.Add(button);
        }
        void Refresh()
        {
            var now = current();
            foreach (var (button, check, value) in buttons)
            {
                var on = EqualityComparer<T>.Default.Equals(value, now);
                button.IsChecked = on;
                check.IsVisible = on;
            }
        }
        _refreshers.Add(Refresh);
        var frame = new Border { Classes = { "segframe" }, Child = row, HorizontalAlignment = HorizontalAlignment.Left };
        return frame;
    }

    Control PaletteChips()
    {
        var s = Strings.Current;
        var names = new Dictionary<ColorPalette, string>
        {
            [ColorPalette.Saturn] = s.PaletteSaturn, [ColorPalette.Teal] = s.PaletteTeal, [ColorPalette.Amber] = s.PaletteAmber,
            [ColorPalette.Rose] = s.PaletteRose, [ColorPalette.Slate] = s.PaletteSlate, [ColorPalette.HighContrast] = s.PaletteHighContrast,
        };
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        var chips = new List<(ToggleButton Chip, ColorPalette Value)>();
        foreach (var palette in Enum.GetValues<ColorPalette>())
        {
            var dark = ThemeService.IsDark(Application.Current!);
            var dot = new Avalonia.Controls.Shapes.Ellipse
            {
                Width = 14, Height = 14,
                Fill = new SolidColorBrush(Color.FromUInt32(dark ? palette.DarkPrimary() : palette.LightPrimary())),
            };
            var chip = new ToggleButton
            {
                Classes = { "chip" },
                Height = ControlHeight,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { dot, new TextBlock { Text = names[palette], VerticalAlignment = VerticalAlignment.Center } },
                },
            };
            chip.Click += (_, _) => App.Settings.Update(x => x with { Palette = palette });
            chips.Add((chip, palette));
            wrap.Children.Add(chip);
        }
        _refreshers.Add(() =>
        {
            foreach (var (chip, value) in chips) chip.IsChecked = value == App.Settings.Value.Palette;
        });
        return wrap;
    }

    /// <summary>
    /// 글꼴 고르기. 첫 항목은 기본 글꼴(SeoulNamsan)이고 그 아래에 시스템에 설치된 글꼴이 이름 순으로 나온다.
    /// 글꼴이 수백 개일 수 있어 위쪽에 검색 칸을 두고, 각 이름은 <b>자기 글꼴로</b> 그려서 어떤 모양인지 바로 보인다.
    /// </summary>
    Control FontPicker()
    {
        var s = Strings.Current;
        var button = new Button { Classes = { "picker" }, Height = ControlHeight, MinWidth = 260, HorizontalAlignment = HorizontalAlignment.Left };
        var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        var arrow = new PathIcon { Width = 14, Height = 14 };
        arrow.Bind(PathIcon.DataProperty, this.GetResourceObservable("IconArrowDown"));
        button.Content = new DockPanel { LastChildFill = true, Children = { arrow, label } };
        DockPanel.SetDock(arrow, Dock.Right);
        arrow.Margin = new Thickness(8, 0, 0, 0);

        var systemFonts = FontManager.Current.SystemFonts.Select(f => f.Name).Distinct()
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();
        string DefaultLabel() => s.FontDefaultOption(ThemeService.DefaultFontName);

        var search = new TextBox { PlaceholderText = s.FontSearchHint, Margin = new Thickness(0, 0, 0, 6) };
        var list = new ListBox { Height = 320, Width = 340, Classes = { "fonts" } };
        list.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((name, _) =>
        {
            // 목록이 스크롤되며 행을 재활용할 때 데이터가 비어 있는 채로 템플릿이 불린다. 그때는 빈 글자만 돌려준다.
            if (name is null) return new TextBlock();
            var isDefault = name == ThemeService.DefaultFontName;
            var family = isDefault ? Application.Current!.FindResource("AppFont") as FontFamily : new FontFamily(name);
            return new TextBlock { Text = isDefault ? DefaultLabel() : name, FontFamily = family ?? FontFamily.Default };
        });
        void Fill()
        {
            var query = search.Text?.Trim() ?? "";
            var items = new List<string>();
            if (query.Length == 0) items.Add(ThemeService.DefaultFontName); // 첫 항목: 기본 글꼴
            items.AddRange(systemFonts.Where(n => n.Contains(query, StringComparison.CurrentCultureIgnoreCase)));
            list.ItemsSource = items;
        }
        Fill();
        search.TextChanged += (_, _) => Fill();

        var flyout = new Flyout { Content = new StackPanel { Children = { search, list } }, Placement = PlacementMode.BottomEdgeAlignedLeft };
        button.Flyout = flyout;
        list.SelectionChanged += (_, e) =>
        {
            if (e.AddedItems.Count == 0 || e.AddedItems[0] is not string picked) return;
            App.Settings.Update(x => x with { UiFontFamily = picked == ThemeService.DefaultFontName ? null : picked });
            list.SelectedItem = null;
            flyout.Hide();
        };
        flyout.Closed += (_, _) => { search.Text = ""; Fill(); };
        flyout.Opened += (_, _) => search.Focus();

        _refreshers.Add(() =>
        {
            var name = App.Settings.Value.UiFontFamily;
            var isDefault = string.IsNullOrWhiteSpace(name) || name == ThemeService.DefaultFontName;
            // 고른 글꼴 이름도 그 글꼴로 보여 준다.
            label.Text = isDefault ? DefaultLabel() : name;
            label.FontFamily = isDefault ? (Application.Current!.FindResource("AppFont") as FontFamily ?? FontFamily.Default) : new FontFamily(name!);
        });
        return button;
    }

    void BuildEditor(StackPanel page)
    {
        var s = Strings.Current;
        page.Children.Add(Heading(s.EditorTitle, first: true));

        var builtin = new RadioButton { Content = s.EditorBuiltin, GroupName = "editor" };
        var external = new RadioButton { Content = s.EditorExternal, GroupName = "editor" };
        builtin.Click += (_, _) => App.Settings.Update(x => x with { EditorMode = EditorMode.Builtin });
        external.Click += (_, _) => App.Settings.Update(x => x with { EditorMode = EditorMode.External });
        page.Children.Add(builtin);
        page.Children.Add(external);

        var note = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(28, 0, 0, 0) };
        note.Bind(TextBlock.FontSizeProperty, Application.Current!.GetResourceObservable("FsSmall"));
        note.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("AppTextMid"));
        page.Children.Add(note);

        var path = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        var choose = new Button { Content = s.EditorChoose, Height = ControlHeight };
        var clear = new Button { Content = s.EditorClear, Height = ControlHeight };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 8, Margin = new Thickness(28, 8, 0, 0) };
        var pathBox = new Border { Classes = { "pathbox" }, Child = path, Height = ControlHeight };
        Grid.SetColumn(pathBox, 0); Grid.SetColumn(choose, 1); Grid.SetColumn(clear, 2);
        row.Children.Add(pathBox); row.Children.Add(choose); row.Children.Add(clear);
        page.Children.Add(row);

        choose.Click += async (_, _) =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = s.EditorChooseTitle,
                AllowMultiple = false,
            });
            if (files.Count == 0 || files[0].TryGetLocalPath() is not { } picked) return;
            // 프로그램을 고르면 자동으로 "외부 프로그램" 모드가 된다.
            App.Settings.Update(x => x with { EditorExecutable = picked, EditorMode = EditorMode.External });
        };
        clear.Click += (_, _) => App.Settings.Update(x => x with { EditorExecutable = null });

        _refreshers.Add(() =>
        {
            var cur = App.Settings.Value;
            var isExternal = cur.EditorMode == EditorMode.External;
            builtin.IsChecked = !isExternal;
            external.IsChecked = isExternal;
            row.IsVisible = isExternal;
            note.Text = isExternal ? s.EditorExternalHint : s.EditorNotReady;
            path.Text = string.IsNullOrWhiteSpace(cur.EditorExecutable) ? s.EditorExecutableNone : cur.EditorExecutable;
            clear.IsEnabled = !string.IsNullOrWhiteSpace(cur.EditorExecutable);
        });
    }
}
