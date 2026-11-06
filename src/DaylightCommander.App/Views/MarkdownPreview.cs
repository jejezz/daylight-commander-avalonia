using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Markdig;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DaylightCommander.App.Views;

/// <summary>
/// 마크다운 미리보기 (원본 <c>EditorMarkdownPreview</c>). Markdig로 문서를 읽어 Avalonia 컨트롤로 그린다 — 제목·문단·강조·코드·목록·
/// 인용·표·링크·그림·할 일 목록. 글이 바뀌면 잠깐(250ms) 기다렸다가 다시 그려서 긴 문서에서도 입력이 끊기지 않는다.
/// 상대 경로 그림은 <see cref="BaseDirectory"/>(편집 중인 파일이 있는 폴더)에서 찾고, http(s) 그림은 내려받아 보여 준다.
/// </summary>
public sealed class MarkdownPreview : UserControl
{
    static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables().UseTaskLists().UseAutoLinks().UseEmphasisExtras().Build();
    static readonly FontFamily Mono = new("Cascadia Mono, Consolas, Menlo, Monaco, DejaVu Sans Mono, monospace");
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    readonly StackPanel _page = new() { Spacing = 10, Margin = new Thickness(20, 16) };
    readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(250) };
    string _text = "";
    int _generation;

    public MarkdownPreview()
    {
        Content = new ScrollViewer { Content = _page, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            Render();
        };
    }

    /// <summary>상대 경로 그림을 찾을 폴더.</summary>
    public string BaseDirectory { get; set; } = "";

    /// <summary>보여 줄 마크다운. 값이 바뀌면 잠시 뒤 다시 그린다 (<paramref name="immediately"/>면 곧바로).</summary>
    public void SetText(string text, bool immediately = false)
    {
        if (text == _text && _page.Children.Count > 0) return;
        _text = text;
        _debounce.Stop();
        if (immediately) Render(); else _debounce.Start();
    }

    void Render()
    {
        _generation++;
        _page.Children.Clear();
        foreach (var block in Markdown.Parse(_text, Pipeline))
            if (Block(block) is { } control) _page.Children.Add(control);
    }

    // --- 블록 ---

    static double BodySize() =>
        Application.Current is { } app && app.TryFindResource("FsBody", app.ActualThemeVariant, out var v) && v is double d ? d : 13;

    Control? Block(Markdig.Syntax.Block block) => block switch
    {
        HeadingBlock h => Heading(h),
        ParagraphBlock p => Text(p.Inline, BodySize()),
        ListBlock l => List(l),
        QuoteBlock q => Quote(q),
        FencedCodeBlock c => Code(c),
        CodeBlock c => Code(c),
        ThematicBreakBlock => Rule(),
        Markdig.Extensions.Tables.Table t => Table(t),
        HtmlBlock h => Code(h),
        _ => null,
    };

    Control Heading(HeadingBlock h)
    {
        double[] scale = [2.0, 1.6, 1.35, 1.15, 1.0, 0.9];
        var size = BodySize() * scale[Math.Clamp(h.Level, 1, 6) - 1];
        var text = Text(h.Inline, size, bold: true);
        if (h.Level > 2) return text;
        var line = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, 4), Child = text };
        line.Bind(Border.BorderBrushProperty, Application.Current!.GetResourceObservable("AppStroke"));
        return line;
    }

    Control List(ListBlock list)
    {
        var rows = new StackPanel { Spacing = 4 };
        var number = int.TryParse(list.OrderedStart, out var start) ? start : 1;
        foreach (var item in list.OfType<ListItemBlock>())
        {
            var marker = list.IsOrdered ? $"{number++}." : "•";
            var body = new StackPanel { Spacing = 4 };
            foreach (var child in item) if (Block(child) is { } c) body.Children.Add(c);
            var markerText = new TextBlock { Text = marker, MinWidth = 22, TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 0, 8, 0) };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            Grid.SetColumn(body, 1);
            row.Children.Add(markerText);
            row.Children.Add(body);
            rows.Children.Add(row);
        }
        rows.Margin = new Thickness(8, 0, 0, 0);
        return rows;
    }

    Control Quote(QuoteBlock quote)
    {
        var body = new StackPanel { Spacing = 6 };
        foreach (var child in quote) if (Block(child) is { } c) body.Children.Add(c);
        var border = new Border { BorderThickness = new Thickness(3, 0, 0, 0), Padding = new Thickness(12, 2, 0, 2), Child = body, Opacity = 0.85 };
        border.Bind(Border.BorderBrushProperty, Application.Current!.GetResourceObservable("AppStrokeStrong"));
        return border;
    }

    Control Code(LeafBlock code)
    {
        var text = new SelectableTextBlock
        {
            Text = string.Join("\n", code.Lines.Lines.Take(code.Lines.Count).Select(l => l.ToString())),
            FontFamily = Mono,
            FontSize = BodySize() * 0.92,
        };
        var box = new Border { CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 8), Child = new ScrollViewer
        {
            Content = text,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        } };
        box.Bind(Border.BackgroundProperty, Application.Current!.GetResourceObservable("AppSurface"));
        return box;
    }

    Control Rule()
    {
        var line = new Border { Height = 1, Margin = new Thickness(0, 6) };
        line.Bind(Border.BackgroundProperty, Application.Current!.GetResourceObservable("AppStrokeStrong"));
        return line;
    }

    Control Table(Markdig.Extensions.Tables.Table table)
    {
        var grid = new Grid();
        var rows = table.OfType<Markdig.Extensions.Tables.TableRow>().ToList();
        var columns = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
        for (var c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (var r = 0; r < rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var cells = rows[r].OfType<Markdig.Extensions.Tables.TableCell>().ToList();
            for (var c = 0; c < cells.Count; c++)
            {
                var paragraph = cells[c].OfType<ParagraphBlock>().FirstOrDefault();
                var text = Text(paragraph?.Inline, BodySize(), bold: rows[r].IsHeader);
                var align = c < table.ColumnDefinitions.Count ? table.ColumnDefinitions[c].Alignment : null;
                if (text is SelectableTextBlock tb)
                    tb.TextAlignment = align switch
                    {
                        Markdig.Extensions.Tables.TableColumnAlign.Center => TextAlignment.Center,
                        Markdig.Extensions.Tables.TableColumnAlign.Right => TextAlignment.Right,
                        _ => TextAlignment.Left,
                    };
                var cell = new Border { BorderThickness = new Thickness(0.5), Padding = new Thickness(10, 6), Child = text };
                cell.Bind(Border.BorderBrushProperty, Application.Current!.GetResourceObservable("AppStrokeStrong"));
                if (rows[r].IsHeader) cell.Bind(Border.BackgroundProperty, Application.Current!.GetResourceObservable("AppSurface"));
                Grid.SetRow(cell, r);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }
        return new ScrollViewer
        {
            Content = grid,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
    }

    // --- 줄 안 ---

    Control Text(ContainerInline? inline, double size, bool bold = false)
    {
        var block = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap, FontSize = size };
        if (bold) block.FontWeight = FontWeight.Bold;
        if (inline is not null) AddInlines(block.Inlines!, inline);
        return block;
    }

    void AddInlines(InlineCollection target, ContainerInline container)
    {
        foreach (var inline in container) AddInline(target, inline);
    }

    void AddInline(InlineCollection target, Markdig.Syntax.Inlines.Inline inline)
    {
        switch (inline)
        {
            case LiteralInline lit:
                target.Add(new Run(lit.Content.ToString()));
                break;
            case LineBreakInline br:
                target.Add(br.IsHard ? new LineBreak() : new Run(" "));
                break;
            case CodeInline code:
                var run = new Run(code.Content) { FontFamily = Mono, FontSize = BodySize() * 0.92 };
                run.Bind(TextElement.BackgroundProperty, Application.Current!.GetResourceObservable("AppSurface"));
                target.Add(run);
                break;
            case EmphasisInline emphasis:
                var span = new Span();
                if (emphasis.DelimiterChar == '~') span.TextDecorations = TextDecorations.Strikethrough;
                else if (emphasis.DelimiterCount >= 2) span.FontWeight = FontWeight.Bold;
                else span.FontStyle = FontStyle.Italic;
                AddInlines(span.Inlines, emphasis);
                target.Add(span);
                break;
            case LinkInline { IsImage: true } image:
                target.Add(new InlineUIContainer(Picture(image.Url, AltText(image))));
                break;
            case LinkInline link:
                target.Add(new InlineUIContainer(LinkButton(link)));
                break;
            case AutolinkInline auto:
                target.Add(new InlineUIContainer(LinkButton(auto.Url, auto.Url)));
                break;
            case TaskList task:
                target.Add(new Run(task.Checked ? "☑ " : "☐ "));
                break;
            case HtmlInline html:
                target.Add(new Run(html.Tag));
                break;
            case ContainerInline other:
                AddInlines(target, other);
                break;
        }
    }

    static string AltText(LinkInline link) =>
        string.Concat(link.Descendants<LiteralInline>().Select(l => l.Content.ToString()));

    Control LinkButton(LinkInline link) =>
        LinkButton(link.Url, string.Concat(link.Descendants<LiteralInline>().Select(l => l.Content.ToString())) is { Length: > 0 } t ? t : link.Url);

    Control LinkButton(string? url, string? label)
    {
        var text = new TextBlock { Text = label ?? url, TextDecorations = TextDecorations.Underline, TextWrapping = TextWrapping.Wrap };
        text.Bind(TextBlock.ForegroundProperty, Application.Current!.GetResourceObservable("AppPrimary"));
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        ToolTip.SetTip(button, url);
        button.Click += (_, _) => OpenLink(url);
        return button;
    }

    /// <summary>http(s)·mailto만 연다 (문서가 임의 프로그램을 실행시키지 못하게).</summary>
    static void OpenLink(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https" or "mailto")) return;
        try { using var _ = Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }

    Control Picture(string? url, string alt)
    {
        var image = new Image { MaxWidth = 560, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
        ToolTip.SetTip(image, alt);
        var generation = _generation;
        _ = LoadPictureAsync(image, url, alt, generation);
        return image;
    }

    async Task LoadPictureAsync(Image image, string? url, string alt, int generation)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            Bitmap? bitmap = null;
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            {
                var bytes = await Http.GetByteArrayAsync(uri);
                bitmap = new Bitmap(new MemoryStream(bytes));
            }
            else
            {
                var path = Uri.TryCreate(url, UriKind.Absolute, out var file) && file.IsFile
                    ? file.LocalPath
                    : Path.Combine(BaseDirectory, Uri.UnescapeDataString(url).Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(path)) bitmap = await Task.Run(() => new Bitmap(path));
            }
            if (bitmap is not null && generation == _generation) image.Source = bitmap;
        }
        catch (Exception e) when (e is IOException or HttpRequestException or TaskCanceledException or ArgumentException or NotSupportedException)
        {
            // 그림을 못 읽으면 비워 둔다 (설명 글은 툴팁으로 남는다).
        }
    }
}
